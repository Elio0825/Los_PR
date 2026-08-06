using LosPr.BLM.Resolvers;

namespace LosPr.BLM.BossFlight;

internal enum BlmBossFlightState
{
    Inactive,
    Armed,
    Preparing,
    Completed,
}

internal sealed class BlmBossFlightService
{
    private const long MinimumCombatTimeMs = 10_000;
    private const long AlwaysAckTimeoutMs = 2_000;
    private const long GcdAckTimeoutMs = 2_000;
    private const int MaximumSoulCasts = 3;

    private readonly BlmStateTracker _tracker;
    private readonly IBlmClock _clock;
    private readonly IBlmActionIdNormalizer _normalizer;
    private long _combatStartedAtMs = -1;
    private uint _lastAttackableTargetId;
    private uint _pendingActionId;
    private long _pendingActionDeadlineMs;
    private int _soulCastCount;

    public BlmBossFlightService(
        BlmStateTracker tracker,
        IBlmClock? clock = null,
        IBlmActionIdNormalizer? normalizer = null)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _clock = clock ?? SystemBlmClock.Instance;
        _normalizer = normalizer ?? IdentityBlmActionIdNormalizer.Instance;
    }

    public BlmBossFlightState State { get; private set; }

    public int SoulCastCount => _soulCastCount;

    public void OnBattleStarted(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Reset();
        _combatStartedAtMs = Now(context);
        ObserveContext(context);
    }

    public void ObserveContext(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.IsAvailable)
        {
            return;
        }

        if (!context.InCombat || !context.IsAlive)
        {
            Reset();
            return;
        }

        var nowMs = Now(context);
        if (_combatStartedAtMs < 0)
        {
            _combatStartedAtMs = nowMs;
        }

        if (!context.BossFlightEnabled)
        {
            if (_pendingActionId != 0)
            {
                _tracker.CancelIssuedAction();
                _pendingActionId = 0;
                _pendingActionDeadlineMs = 0;
            }

            State = BlmBossFlightState.Inactive;
            _soulCastCount = 0;
        }

        if (context.HasValidTarget && context.TargetEntityId != 0)
        {
            _lastAttackableTargetId = context.TargetEntityId;
            if (State is BlmBossFlightState.Preparing or BlmBossFlightState.Completed)
            {
                State = context.BossFlightEnabled
                    ? BlmBossFlightState.Armed
                    : BlmBossFlightState.Inactive;
                _soulCastCount = 0;
            }
            else if (context.BossFlightEnabled && State == BlmBossFlightState.Inactive)
            {
                State = BlmBossFlightState.Armed;
            }

            return;
        }

        if (!context.BossFlightEnabled
            || _lastAttackableTargetId == 0
            || nowMs - _combatStartedAtMs < MinimumCombatTimeMs)
        {
            return;
        }

        if (State is BlmBossFlightState.Armed or BlmBossFlightState.Inactive)
        {
            State = BlmBossFlightState.Preparing;
        }
    }

    public void MarkNoTarget(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.IsAvailable || !context.InCombat || !context.IsAlive)
        {
            return;
        }

        var nowMs = Now(context);
        if (_combatStartedAtMs < 0)
        {
            _combatStartedAtMs = nowMs;
        }

        if (context.BossFlightEnabled
            && _lastAttackableTargetId != 0
            && nowMs - _combatStartedAtMs >= MinimumCombatTimeMs)
        {
            State = BlmBossFlightState.Preparing;
        }
    }

    public PAction? Resolve(
        BlmResolverChannel channel,
        BlmContext context,
        bool highPriorityQueueActive = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        ObserveContext(context);

        if (State != BlmBossFlightState.Preparing
            || !context.BossFlightEnabled
            || context.HasValidTarget
            || highPriorityQueueActive
            || _tracker.GetTrackerSnapshot().HasPendingIssuedAction
            || !context.CanAct
            || !context.IsAlive
            || !context.InCombat
            || context.IsCasting
            || channel == BlmResolverChannel.Always && context.AnimationLockSeconds > 0f
            || context.GcdRemainSeconds > 0.6f)
        {
            return null;
        }

        ExpirePending(_clock.NowMs);
        if (_pendingActionId != 0)
        {
            return null;
        }

        if (channel == BlmResolverChannel.Always
            && context.InFire
            && context.IceStacks < 3
            && context.Transpose.IsReady)
        {
            return Issue(
                context,
                BLMSkill.星灵移位,
                context.Transpose.ActionId,
                ActionType.Always,
                isGcd: false,
                ackTimeoutMs: AlwaysAckTimeoutMs);
        }

        if (channel != BlmResolverChannel.Gcd || !context.InIce)
        {
            return null;
        }

        var hasFullHearts = context.Level < 58 || context.UmbralHearts >= 3;
        if ((context.IceStacks >= 3 && hasFullHearts && context.IsMpFull)
            || _soulCastCount >= MaximumSoulCasts)
        {
            State = BlmBossFlightState.Completed;
            return null;
        }

        if (!context.UmbralSoul.IsReady)
        {
            return null;
        }

        return Issue(
            context,
            BLMSkill.灵极魂,
            context.UmbralSoul.ActionId,
            ActionType.Gcd,
            isGcd: true,
            ackTimeoutMs: GcdAckTimeoutMs);
    }

    public void OnActionEffect(BlmActionEffectAck ack, bool accepted)
    {
        if (_pendingActionId == 0
            || !BlmSkillBook.ActionIdsMatch(
                _pendingActionId,
                ack.ActionId,
                _normalizer))
        {
            return;
        }

        _pendingActionId = 0;
        _pendingActionDeadlineMs = 0;
        if (accepted && BlmSkillBook.ActionIdsMatch(BLMSkill.灵极魂, ack.ActionId, _normalizer))
        {
            _soulCastCount++;
        }
    }

    public void Reset()
    {
        State = BlmBossFlightState.Inactive;
        _combatStartedAtMs = -1;
        _lastAttackableTargetId = 0;
        _pendingActionId = 0;
        _pendingActionDeadlineMs = 0;
        _soulCastCount = 0;
    }

    private PAction? Issue(
        BlmContext context,
        uint requestedActionId,
        uint adjustedActionId,
        ActionType actionType,
        bool isGcd,
        long ackTimeoutMs)
    {
        if (adjustedActionId == 0)
        {
            return null;
        }

        var issuedAtMs = _clock.NowMs;
        var trackerSnapshot = _tracker.GetTrackerSnapshot();
        var metadata = new BlmIssuedActionMetadata(
            trackerSnapshot.StateGeneration,
            requestedActionId,
            adjustedActionId,
            issuedAtMs,
            trackerSnapshot.LastAckGlobalSequence,
            issuedAtMs + ackTimeoutMs,
            WasInstant: true,
            IsGcd: isGcd);
        if (!_tracker.TryRegisterIssuedAction(metadata))
        {
            return null;
        }

        _pendingActionId = adjustedActionId;
        _pendingActionDeadlineMs = issuedAtMs + ackTimeoutMs;
        return new PAction(adjustedActionId, actionType, ActionTargetType.Self);
    }

    private void ExpirePending(long nowMs)
    {
        if (_pendingActionId != 0 && nowMs > _pendingActionDeadlineMs)
        {
            _pendingActionId = 0;
            _pendingActionDeadlineMs = 0;
        }
    }

    private long Now(BlmContext context)
        => context.CapturedAtMs > 0 ? context.CapturedAtMs : _clock.NowMs;
}
