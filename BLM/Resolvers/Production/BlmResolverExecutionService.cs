using System.Linq;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.Resolvers.Level100;

namespace LosPr.BLM.Resolvers.Production;

internal sealed record BlmResolverProductionFrame
{
    public long FrameSequence { get; init; }
    public long StateGeneration { get; init; }
    public long CapturedAtMs { get; init; }
    public uint PlayerEntityId { get; init; }
    public bool IsAoeMode { get; init; }
    public int EnemyCount { get; init; }
    public uint TargetEntityId { get; init; }
    public uint AoeTargetId { get; init; }
    public bool AoeTargetCanUseAttack { get; init; }
    public int AoeTargetHitCount { get; init; }
    public bool AoeTargetIsCurrentTarget { get; init; }
    public bool HasTarget { get; init; }
    public bool HasValidTarget { get; init; }
    public BlmResolverInput Input { get; init; } = new();
    public BlmDecisionFrame Decision { get; init; } = new();
}

internal sealed class BlmResolverExecutionService
{
    public const int HeartbeatIntervalMs = 5000;
    private const long OgcdAckTimeoutMs = 2000;
    private const long InstantGcdAckTimeoutMs = 2000;
    private const long CastGcdAckTimeoutMs = 5000;
    private const long Blizzard3AckTimeoutMs = 6000;

    private readonly object _gate = new();
    private readonly BlmStateTracker _tracker;
    private readonly IBlmDebugSink _debug;
    private readonly HashSet<BlmResolverChannel> _deliveredChannels = [];

    private BlmResolverProductionFrame? _latest;
    private long _nextFrameSequence;
    private string _lastFrameFingerprint = string.Empty;
    private long _lastFramePublishedAtMs;

    public BlmResolverExecutionService(
        BlmStateTracker tracker,
        IBlmDebugSink? debugSink = null)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _debug = debugSink ?? NullBlmDebugSink.Instance;
    }

    public BlmResolverProductionFrame? GetSnapshot()
    {
        lock (_gate)
        {
            return _latest;
        }
    }

    public void InvalidateFrame()
    {
        lock (_gate)
        {
            _latest = null;
            _deliveredChannels.Clear();
            ResetPublicationStateNoLock();
        }
    }

    public bool BeginFrame(BlmContext context, BlmResolverInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(input);

        List<BlmDebugEventDraft>? drafts = null;
        lock (_gate)
        {
            var primaryTargetChanged = _latest is { } previous
                && (previous.TargetEntityId != context.TargetEntityId
                    || previous.HasTarget != context.HasTarget
                    || previous.HasValidTarget != context.HasValidTarget);
            var aoeDeliveryTargetChanged = _latest is { } aoePrevious
                && (aoePrevious.IsAoeMode != context.IsAoeMode
                    || aoePrevious.AoeTargetId != context.AoeTargetId
                    || aoePrevious.AoeTargetCanUseAttack
                        && !context.AoeTargetCanUseAttack);
            if (!IsSameFrameFacts(context, input))
            {
                _latest = null;
                _deliveredChannels.Clear();
                ResetPublicationStateNoLock();
                return false;
            }

            if (primaryTargetChanged && HasPendingTargetDependentAction())
            {
                _tracker.CancelIssuedAction();
            }
            else if (aoeDeliveryTargetChanged && HasPendingAoeTargetedGcd())
            {
                _tracker.CancelIssuedAction();
            }

            var frame = new BlmResolverProductionFrame
            {
                FrameSequence = ++_nextFrameSequence,
                StateGeneration = context.Tracker.StateGeneration,
                CapturedAtMs = context.CapturedAtMs,
                PlayerEntityId = context.PlayerEntityId,
                IsAoeMode = context.IsAoeMode,
                EnemyCount = context.EnemyCount,
                TargetEntityId = context.TargetEntityId,
                AoeTargetId = context.AoeTargetId,
                AoeTargetCanUseAttack = context.AoeTargetCanUseAttack,
                AoeTargetHitCount = context.AoeTargetHitCount,
                AoeTargetIsCurrentTarget = context.AoeTargetIsCurrentTarget,
                HasTarget = context.HasTarget,
                HasValidTarget = context.HasValidTarget,
                Input = input,
                Decision = Level100ResolverEngine.Evaluate(input),
            };
            _latest = frame;
            _deliveredChannels.Clear();

            var fingerprint = FrameFingerprint(frame);
            var nowMs = frame.CapturedAtMs;
            var shouldPublish = !string.Equals(
                    fingerprint,
                    _lastFrameFingerprint,
                    StringComparison.Ordinal)
                || nowMs < _lastFramePublishedAtMs
                || nowMs - _lastFramePublishedAtMs >= HeartbeatIntervalMs;
            if (shouldPublish)
            {
                var reason = _lastFrameFingerprint.Length == 0
                    ? "Started"
                    : string.Equals(
                        fingerprint,
                        _lastFrameFingerprint,
                        StringComparison.Ordinal)
                        ? "Heartbeat"
                        : "Changed";
                _lastFrameFingerprint = fingerprint;
                _lastFramePublishedAtMs = nowMs;
                drafts = BuildFrameDrafts(frame, context, reason);
            }
        }

        Publish(drafts);
        return true;
    }

    public PAction? Resolve(
        BlmResolverChannel channel,
        BlmContext context,
        bool highPriorityQueueActive = false)
    {
        ArgumentNullException.ThrowIfNull(context);

        BlmResolverCandidate? deliveredCandidate = null;
        PAction? result = null;
        lock (_gate)
        {
            var frame = _latest;
            if (frame is null
                || !MatchesFrame(frame, context)
                || _deliveredChannels.Contains(channel)
                || highPriorityQueueActive
                || frame.Input.HighPriorityQueueActive
                || frame.Decision.DeliveryBlocked)
            {
                return null;
            }

            if (channel == BlmResolverChannel.Gcd
                && (frame.Decision.GcdBlockedByTransposeHold
                    || frame.Decision.GcdBlockedByAlwaysBridge))
            {
                return null;
            }

            if (channel == BlmResolverChannel.OffGcd
                && (frame.Decision.HoldGcdForTranspose
                    || frame.Decision.AlwaysCandidate is not null
                    || frame.Decision.AlwaysBridgeCandidate is not null
                    || frame.Decision.RemainingWeaves <= 0
                    || !CanDeliverOffGcd(context)))
            {
                return null;
            }

            if (channel == BlmResolverChannel.Always
                && !CanDeliverAlways(frame, context))
            {
                return null;
            }

            var candidate = Candidate(frame.Decision, channel);
            if (candidate is null || candidate.ActionId == 0)
            {
                return null;
            }

            result = CreatePAction(candidate, channel, context);
            if (result is null)
            {
                return null;
            }

            var isGcd = channel == BlmResolverChannel.Gcd;
            var wasInstant = isGcd && PredictInstant(context, candidate.ActionId);
            var issuedAtMs = context.CapturedAtMs;
            var timeoutMs = AckTimeoutFor(candidate.ActionId, isGcd, wasInstant);
            var metadata = new BlmIssuedActionMetadata(
                frame.StateGeneration,
                candidate.ActionId,
                candidate.ActionId,
                issuedAtMs,
                context.Tracker.LastAckGlobalSequence,
                issuedAtMs + timeoutMs,
                wasInstant,
                isGcd);
            if (!_tracker.TryRegisterIssuedAction(metadata))
            {
                return null;
            }

            _deliveredChannels.Add(channel);
            deliveredCandidate = candidate;
        }

        PublishDispatch(context, channel, deliveredCandidate!, result!);
        return result;
    }

    private static bool CanDeliverAlways(
        BlmResolverProductionFrame frame,
        BlmContext context)
        => !context.IsCasting
            && context.AnimationLockSeconds <= 0f
            && (frame.Decision.RemainingWeaves > 0
                || context.GcdRemainSeconds <= 0.6f);

    private bool HasPendingAoeTargetedGcd()
    {
        var snapshot = _tracker.GetTrackerSnapshot();
        return snapshot.HasPendingIssuedAction
            && BlmSkillBook.IsAoeTargetedGcdId(snapshot.PendingIssuedActionId);
    }

    private bool HasPendingTargetDependentAction()
    {
        var snapshot = _tracker.GetTrackerSnapshot();
        return snapshot.HasPendingIssuedAction
            && !BlmSkillBook.IsKnownSelfAbilityId(snapshot.PendingIssuedActionId);
    }

    private static bool CanDeliverOffGcd(BlmContext context)
        => BlmDecisionPrimitives.CanWeaveNow(
            context.IsCasting,
            context.GcdRemainSeconds,
            context.AnimationLockSeconds);

    private static long AckTimeoutFor(uint actionId, bool isGcd, bool wasInstant)
    {
        if (!isGcd)
        {
            return OgcdAckTimeoutMs;
        }

        if (actionId == BLMSkill.冰封)
        {
            return Blizzard3AckTimeoutMs;
        }

        return wasInstant ? InstantGcdAckTimeoutMs : CastGcdAckTimeoutMs;
    }

    private static bool PredictInstant(BlmContext context, uint actionId)
        => actionId is BLMSkill.闪雷 or BLMSkill.暴雷 or BLMSkill.高闪雷
            or BLMSkill.震雷 or BLMSkill.霹雷 or BLMSkill.高震雷
            or BLMSkill.悖论 or BLMSkill.异言 or BLMSkill.秽浊
            || actionId == BLMSkill.绝望 && context.Level >= 100
            || actionId == BLMSkill.爆炎 && context.HasFirestarter
            || context.HasSwiftcast
            || context.TriplecastStacks > 0;

    private static PAction? CreatePAction(
        BlmResolverCandidate candidate,
        BlmResolverChannel channel,
        BlmContext context)
    {
        var actionType = channel switch
        {
            BlmResolverChannel.Gcd => ActionType.Gcd,
            BlmResolverChannel.Always => ActionType.Always,
            BlmResolverChannel.OffGcd => ActionType.OffGcd,
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
        };
        var action = new PAction(candidate.ActionId, actionType, ActionTargetType.Self);
        switch (candidate.TargetKind)
        {
            case BlmResolverTargetKind.Self:
                return action;
            case BlmResolverTargetKind.CurrentTarget:
                if (!context.HasValidTarget || context.TargetEntityId == 0)
                {
                    return null;
                }

                action.NetworkTid = context.TargetEntityId;
                return action;
            case BlmResolverTargetKind.SpecifiedTarget:
                if (candidate.TargetId == 0)
                {
                    return null;
                }

                action.NetworkTid = candidate.TargetId;
                return action;
            case BlmResolverTargetKind.Potion:
            default:
                return null;
        }
    }

    private static BlmResolverCandidate? Candidate(
        BlmDecisionFrame decision,
        BlmResolverChannel channel)
        => channel switch
        {
            BlmResolverChannel.Gcd => decision.GcdCandidate,
            BlmResolverChannel.Always => decision.AlwaysCandidate
                ?? decision.AlwaysBridgeCandidate,
            BlmResolverChannel.OffGcd => decision.OffGcdCandidate,
            _ => null,
        };

    private static List<BlmDebugEventDraft> BuildFrameDrafts(
        BlmResolverProductionFrame frame,
        BlmContext context,
        string reason)
    {
        var drafts = new List<BlmDebugEventDraft>(3);
        foreach (var channel in Enum.GetValues<BlmResolverChannel>())
        {
            var candidate = Candidate(frame.Decision, channel);
            var blocked = IsDeliveryBlocked(frame, channel, context);
            var candidateActionId = candidate?.ActionId ?? 0;
            var targetId = ResolveTargetId(candidate, context);
            drafts.Add(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.ResolverFrame,
                Context = context,
                MonotonicMs = frame.CapturedAtMs,
                EntryPoint = $"Resolver.Frame.{channel}",
                ActionId = candidateActionId,
                NormalizedActionId = blocked ? 0 : candidateActionId,
                PActionType = channel.ToString(),
                RuleId = candidate?.ResolverId ?? string.Empty,
                Reason = reason,
                Detail = frame.Input.FactCoverage.UnsupportedSummary,
                TargetEntityId = targetId,
                Resolver = new BlmDebugResolverDraft
                {
                    FrameSequence = frame.FrameSequence,
                    FrameCapturedAtMs = frame.CapturedAtMs,
                    FrameStateGeneration = frame.StateGeneration,
                    Channel = channel,
                    CandidateActionId = candidateActionId,
                    DeliverableActionId = blocked ? 0 : candidateActionId,
                    TargetEntityId = targetId,
                    TargetKind = candidate?.TargetKind ?? default,
                    ResolverId = candidate?.ResolverId ?? string.Empty,
                    CheckCode = candidate?.CheckCode ?? 0,
                    HoldGcdForTranspose = frame.Decision.HoldGcdForTranspose,
                    GcdBlockedByTransposeHold = frame.Decision.GcdBlockedByTransposeHold,
                    GcdBlockedByAlwaysBridge = frame.Decision.GcdBlockedByAlwaysBridge,
                    DeliveryBlocked = blocked,
                    BlockReason = frame.Decision.BlockReason,
                    HighPriorityQueueActive = frame.Input.HighPriorityQueueActive,
                    RemainingWeaves = frame.Decision.RemainingWeaves,
                    FactCoverage = frame.Input.FactCoverage.UnsupportedSummary,
                },
            });
        }

        return drafts;
    }

    private static bool IsDeliveryBlocked(
        BlmResolverProductionFrame frame,
        BlmResolverChannel channel,
        BlmContext context)
        => frame.Decision.DeliveryBlocked
            || channel == BlmResolverChannel.Gcd
                && (frame.Decision.GcdBlockedByTransposeHold
                    || frame.Decision.GcdBlockedByAlwaysBridge)
            || channel == BlmResolverChannel.OffGcd
                && (frame.Decision.HoldGcdForTranspose
                    || frame.Decision.AlwaysCandidate is not null
                    || frame.Decision.AlwaysBridgeCandidate is not null
                    || frame.Decision.RemainingWeaves <= 0
                    || !CanDeliverOffGcd(context))
            || channel == BlmResolverChannel.Always
                && !CanDeliverAlways(frame, context);

    private static uint ResolveTargetId(
        BlmResolverCandidate? candidate,
        BlmContext context)
        => candidate?.TargetKind switch
        {
            BlmResolverTargetKind.CurrentTarget => context.TargetEntityId,
            BlmResolverTargetKind.SpecifiedTarget => candidate.TargetId,
            _ => 0,
        };

    private static string FrameFingerprint(BlmResolverProductionFrame frame)
        => string.Join(
            '|',
            frame.StateGeneration,
            frame.PlayerEntityId,
            frame.IsAoeMode,
            frame.EnemyCount,
            frame.TargetEntityId,
            frame.AoeTargetId,
            frame.AoeTargetCanUseAttack,
            frame.AoeTargetHitCount,
            frame.AoeTargetIsCurrentTarget,
            frame.HasTarget,
            frame.HasValidTarget,
            CandidateFingerprint(frame.Decision.GcdCandidate),
            CandidateFingerprint(frame.Decision.AlwaysCandidate),
            CandidateFingerprint(frame.Decision.OffGcdCandidate),
            CandidateFingerprint(frame.Decision.AlwaysBridgeCandidate),
            frame.Decision.HoldGcdForTranspose,
            frame.Decision.GcdBlockedByTransposeHold,
            frame.Decision.GcdBlockedByAlwaysBridge,
            frame.Decision.RemainingWeaves,
            frame.Decision.DeliveryBlocked,
            frame.Decision.BlockReason,
            frame.Input.HighPriorityQueueActive,
            frame.Input.FactCoverage.UnsupportedSummary);

    private void ResetPublicationStateNoLock()
    {
        _lastFrameFingerprint = string.Empty;
        _lastFramePublishedAtMs = 0;
    }

    private static string CandidateFingerprint(BlmResolverCandidate? candidate)
        => candidate is null
            ? "-"
            : string.Join(
                ':',
                candidate.ActionId,
                candidate.TargetId,
                candidate.TargetKind,
                candidate.ResolverId,
                candidate.CheckCode);

    private static bool MatchesFrame(
        BlmResolverProductionFrame frame,
        BlmContext context)
        => frame.StateGeneration > 0
            && frame.StateGeneration == context.Tracker.StateGeneration
            && frame.CapturedAtMs == context.CapturedAtMs
            && frame.PlayerEntityId == context.PlayerEntityId
            && frame.IsAoeMode == context.IsAoeMode
            && frame.EnemyCount == context.EnemyCount
            && frame.TargetEntityId == context.TargetEntityId
            && frame.AoeTargetId == context.AoeTargetId
            && frame.AoeTargetCanUseAttack == context.AoeTargetCanUseAttack
            && frame.AoeTargetHitCount == context.AoeTargetHitCount
            && frame.AoeTargetIsCurrentTarget == context.AoeTargetIsCurrentTarget
            && frame.HasTarget == context.HasTarget
            && frame.HasValidTarget == context.HasValidTarget;

    private static bool IsSameFrameFacts(
        BlmContext context,
        BlmResolverInput input)
    {
        var generation = context.Tracker.StateGeneration;
        return generation > 0
            && input.StateGeneration == generation
            && input.FactCoverage.GenerationConsistent
            && input.Context.CapturedAtMs == context.CapturedAtMs
            && input.Context.PlayerEntityId == context.PlayerEntityId
            && input.Context.IsSingleTargetMode == !context.IsAoeMode
            && input.Context.EnemyCount == context.EnemyCount
            && input.Context.CurrentTargetId == context.TargetEntityId
            && input.Context.AoeTargetId == context.AoeTargetId
            && input.Context.AoeTargetCanUseAttack == context.AoeTargetCanUseAttack
            && input.Context.AoeTargetHitCount == context.AoeTargetHitCount
            && input.Context.AoeTargetIsCurrentTarget
                == context.AoeTargetIsCurrentTarget
            && input.Context.HasTarget == context.HasTarget
            && input.Context.CanUseAttackActionOnTarget == context.HasValidTarget
            && HasMatchingTargetFact(context, input)
            && input.RecentHistory.All(action => action.StateGeneration == generation)
            && (input.PreviousGcd is null
                || input.PreviousGcd.Value.StateGeneration == generation);
    }

    private static bool HasMatchingTargetFact(
        BlmContext context,
        BlmResolverInput input)
    {
        if (context.HasTarget != (context.TargetEntityId != 0)
            || context.HasValidTarget && !context.HasTarget)
        {
            return false;
        }

        if (!context.HasTarget && context.TargetEntityId == 0)
        {
            return input.Context.CurrentTargetId == 0;
        }

        return input.DotTargets.Any(target =>
            target.EntityId == context.TargetEntityId
            && target.IsValid == context.HasValidTarget
            && target.CanUseAttackActionOn == context.HasValidTarget);
    }

    private void PublishDispatch(
        BlmContext context,
        BlmResolverChannel channel,
        BlmResolverCandidate candidate,
        PAction action)
    {
        try
        {
            _debug.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.DispatchReturned,
                Context = context,
                MonotonicMs = context.CapturedAtMs,
                EntryPoint = channel.ToString(),
                ActionId = action.ActionId,
                NormalizedActionId = candidate.ActionId,
                PActionType = action.Type.ToString(),
                RuleId = candidate.ResolverId,
                Reason = $"Resolver#{candidate.ManifestOrder}/Check={candidate.CheckCode}",
                Detail = "已向 PromeRotation 返回 Resolver 候选；等待服务器 ActionEffect Ack。",
                TargetEntityId = action.NetworkTid,
            });
        }
        catch
        {
            // Diagnostics must never alter action delivery.
        }
    }

    private void Publish(List<BlmDebugEventDraft>? drafts)
    {
        if (drafts is null)
        {
            return;
        }

        foreach (var draft in drafts)
        {
            try
            {
                _debug.Publish(draft);
            }
            catch
            {
                // Diagnostics must never alter frame creation.
            }
        }
    }
}
