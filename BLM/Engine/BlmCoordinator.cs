using LosPr.BLM.Core;

namespace LosPr.BLM.Engine;

public sealed class BlmCoordinator
{
    public const long PostAckReconcileTimeoutMs = 1500;

    private const long ConfirmedHandoffTimeoutMs = 1000;
    private readonly object _gate = new();
    private readonly IBlmClock _clock;
    private readonly IBlmActionIdNormalizer _normalizer;
    private BlmIntent _intent = BlmIntent.Empty;
    private long _nextSerial;

    public BlmCoordinator(
        IBlmClock? clock = null,
        IBlmActionIdNormalizer? normalizer = null)
    {
        _clock = clock ?? SystemBlmClock.Instance;
        _normalizer = normalizer ?? IdentityBlmActionIdNormalizer.Instance;
    }

    public BlmIntent Peek()
    {
        lock (_gate)
        {
            return _intent;
        }
    }

    public bool TryBegin(
        long stateGeneration,
        TransitionKind kind,
        TransitionStep step,
        TransitionDeliveryChannel deliveryChannel,
        uint expectedActionId,
        TransitionExpectation expectation,
        RotationMode modeAtRequest,
        long expireMs,
        string reason,
        IceToFireRoute iceToFireRoute = IceToFireRoute.None,
        long totalExpireMs = 15000,
        uint targetEntityId = 0)
    {
        if (stateGeneration <= 0
            || kind == TransitionKind.None
            || step == TransitionStep.None
            || deliveryChannel == TransitionDeliveryChannel.None
            || modeAtRequest == RotationMode.None
            || expectedActionId == 0
            || expireMs <= 0
            || totalExpireMs <= 0
            || !IsValidInitialStep(kind, iceToFireRoute, modeAtRequest, step)
            || !IsDeliveryAllowedForStep(kind, modeAtRequest, step, deliveryChannel)
            || !IsExpectedActionForStep(step, expectedActionId)
            || !IsExpectationAllowed(
                kind,
                step,
                iceToFireRoute,
                expectedActionId,
                expectation))
        {
            return false;
        }

        lock (_gate)
        {
            if (_intent.IsActive)
            {
                return false;
            }

            var now = _clock.NowMs;
            var transitionExpireAtMs = now + totalExpireMs;
            _intent = new BlmIntent
            {
                StateGeneration = stateGeneration,
                Kind = kind,
                Step = step,
                Stage = TransitionStage.Requested,
                DeliveryChannel = deliveryChannel,
                ModeAtRequest = modeAtRequest,
                IceToFireRoute = iceToFireRoute,
                TargetEntityIdAtRequest = targetEntityId,
                Serial = ++_nextSerial,
                StepIndex = 0,
                ExpectedActionId = expectedActionId,
                ExpectedAdjustedActionId = NormalizeOrOriginal(expectedActionId),
                Expectation = expectation,
                SetAtMs = now,
                ExpireAtMs = Math.Min(now + expireMs, transitionExpireAtMs),
                TransitionExpireAtMs = transitionExpireAtMs,
                Reason = reason,
            };
            return true;
        }
    }

    public bool TryMarkQueued(
        long stateGeneration,
        long serial,
        int stepIndex,
        uint actionId,
        BlmQueueChannel queueChannel,
        float gcdRemainSeconds,
        uint globalSequenceBaseline,
        long expireMs,
        string reason)
    {
        if (stateGeneration <= 0
            || actionId == 0
            || queueChannel == BlmQueueChannel.None
            || expireMs <= 0
            || !float.IsFinite(gcdRemainSeconds)
            || gcdRemainSeconds < 0f)
        {
            return false;
        }

        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.Serial != serial
                || _intent.StepIndex != stepIndex
                || _intent.Stage != TransitionStage.Requested
                || !MatchesFrozenAction(_intent, actionId)
                || !CanQueueVia(_intent.DeliveryChannel, queueChannel, gcdRemainSeconds))
            {
                return false;
            }

            var now = _clock.NowMs;
            if (IsExpiredNoLock(now))
            {
                _intent = Cancelled(_intent, "Transition timeout before queue");
                return false;
            }

            _intent = _intent with
            {
                Stage = TransitionStage.Queued,
                QueuedAfterGlobalSequence = globalSequenceBaseline,
                QueuedAtMs = now,
                QueuedDeadlineAtMs = Math.Min(now + expireMs, _intent.TransitionExpireAtMs),
                AcknowledgedActionId = 0,
                AcknowledgedGlobalSequence = 0,
                AcknowledgedAtMs = 0,
                SetAtMs = now,
                ExpireAtMs = Math.Min(now + expireMs, _intent.TransitionExpireAtMs),
                Reason = reason,
            };
            return true;
        }
    }

    public BlmTransitionAckToken CaptureAckToken(long stateGeneration)
    {
        lock (_gate)
        {
            return _intent.StateGeneration == stateGeneration
                ? _intent.AckToken
                : default;
        }
    }

    public bool TryAcknowledge(
        BlmTransitionAckToken token,
        uint actionId,
        uint globalSequence,
        long receivedAtMs)
    {
        if (!token.IsValid || actionId == 0 || receivedAtMs < 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (_intent.Stage != TransitionStage.Queued
                || _intent.AckToken != token
                || !MatchesFrozenAction(_intent, actionId))
            {
                return false;
            }

            if (_intent.AcknowledgedActionId != 0)
            {
                return _intent.AcknowledgedActionId == actionId
                    && (_intent.AcknowledgedGlobalSequence == globalSequence
                        || globalSequence == 0);
            }

            var now = _clock.NowMs;
            if (receivedAtMs < token.QueuedAtMs
                || receivedAtMs > token.DeadlineMs
                || now > _intent.TransitionExpireAtMs)
            {
                _intent = Cancelled(_intent, "Transition timeout before Ack");
                return false;
            }

            if (globalSequence != 0
                && token.QueuedAfterGlobalSequence != 0
                && !IsSequenceNewer(globalSequence, token.QueuedAfterGlobalSequence))
            {
                return false;
            }

            _intent = _intent with
            {
                AcknowledgedActionId = actionId,
                AcknowledgedGlobalSequence = globalSequence,
                AcknowledgedAtMs = receivedAtMs,
                ExpireAtMs = Math.Min(
                    now + PostAckReconcileTimeoutMs,
                    _intent.TransitionExpireAtMs),
                Reason = $"Ack action {actionId}: {_intent.Reason}",
            };
            return true;
        }
    }

    public void Reconcile(long stateGeneration, BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (!_intent.IsActive || _intent.StateGeneration != stateGeneration)
            {
                return;
            }

            var now = _clock.NowMs;
            if (IsExpiredNoLock(now))
            {
                _intent = Cancelled(_intent, "Transition timeout");
                return;
            }

            if (_intent.Stage != TransitionStage.Queued
                || !_intent.HasExpectedAck
                || !ExpectationSatisfied(_intent.Expectation, context))
            {
                return;
            }

            _intent = _intent with
            {
                Stage = TransitionStage.Confirmed,
                SetAtMs = now,
                ExpireAtMs = Math.Min(
                    now + ConfirmedHandoffTimeoutMs,
                    _intent.TransitionExpireAtMs),
                Reason = $"Confirmed after Gauge reconcile: {_intent.Reason}",
            };
        }
    }

    public bool TryAdvance(
        long stateGeneration,
        long serial,
        int stepIndex,
        TransitionStep nextStep,
        TransitionDeliveryChannel nextDeliveryChannel,
        uint nextExpectedActionId,
        TransitionExpectation nextExpectation,
        long expireMs,
        string reason)
    {
        if (stateGeneration <= 0
            || nextStep == TransitionStep.None
            || nextDeliveryChannel == TransitionDeliveryChannel.None
            || nextExpectedActionId == 0
            || expireMs <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.Serial != serial
                || _intent.StepIndex != stepIndex
                || _intent.Stage != TransitionStage.Confirmed
                || !IsValidAdvance(
                    _intent.Kind,
                    _intent.IceToFireRoute,
                    _intent.ModeAtRequest,
                    _intent.Step,
                    nextStep)
                || !IsDeliveryAllowedForStep(
                    _intent.Kind,
                    _intent.ModeAtRequest,
                    nextStep,
                    nextDeliveryChannel)
                || !IsExpectedActionForStep(nextStep, nextExpectedActionId)
                || !IsExpectationAllowed(
                    _intent.Kind,
                    nextStep,
                    _intent.IceToFireRoute,
                    nextExpectedActionId,
                    nextExpectation))
            {
                return false;
            }

            var now = _clock.NowMs;
            if (IsExpiredNoLock(now))
            {
                _intent = Cancelled(_intent, "Transition timeout before advance");
                return false;
            }

            _intent = _intent with
            {
                Step = nextStep,
                Stage = TransitionStage.Requested,
                DeliveryChannel = nextDeliveryChannel,
                StepIndex = _intent.StepIndex + 1,
                ExpectedActionId = nextExpectedActionId,
                ExpectedAdjustedActionId = NormalizeOrOriginal(nextExpectedActionId),
                QueuedAfterGlobalSequence = 0,
                QueuedAtMs = 0,
                QueuedDeadlineAtMs = 0,
                Expectation = nextExpectation,
                AcknowledgedActionId = 0,
                AcknowledgedGlobalSequence = 0,
                AcknowledgedAtMs = 0,
                SetAtMs = now,
                ExpireAtMs = Math.Min(now + expireMs, _intent.TransitionExpireAtMs),
                Reason = reason,
            };
            return true;
        }
    }

    public bool TryComplete(
        long stateGeneration,
        long serial,
        int stepIndex,
        string reason)
    {
        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.Serial != serial
                || _intent.StepIndex != stepIndex
                || _intent.Stage != TransitionStage.Confirmed
                || !IsValidTerminalStep(
                    _intent.Kind,
                    _intent.IceToFireRoute,
                    _intent.ModeAtRequest,
                    _intent.Step))
            {
                return false;
            }

            var now = _clock.NowMs;
            if (IsExpiredNoLock(now))
            {
                _intent = Cancelled(_intent, "Transition timeout before complete");
                return false;
            }

            _intent = _intent with
            {
                Stage = TransitionStage.Completed,
                ExpectedActionId = 0,
                ExpectedAdjustedActionId = 0,
                QueuedAfterGlobalSequence = 0,
                QueuedAtMs = 0,
                QueuedDeadlineAtMs = 0,
                AcknowledgedActionId = 0,
                AcknowledgedGlobalSequence = 0,
                AcknowledgedAtMs = 0,
                ExpireAtMs = 0,
                TransitionExpireAtMs = 0,
                Reason = reason,
            };
            return true;
        }
    }

    public bool TryCancel(
        long stateGeneration,
        long serial,
        int stepIndex,
        string reason)
    {
        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.Serial != serial
                || _intent.StepIndex != stepIndex
                || !_intent.IsActive)
            {
                return false;
            }

            _intent = Cancelled(_intent, reason);
            return true;
        }
    }

    public void CancelActive(string reason)
    {
        lock (_gate)
        {
            if (_intent.IsActive)
            {
                _intent = Cancelled(_intent, reason);
            }
        }
    }

    public void ClearTerminal()
    {
        lock (_gate)
        {
            if (!_intent.IsActive)
            {
                _intent = BlmIntent.Empty;
            }
        }
    }

    private bool IsExpiredNoLock(long now)
        => now > _intent.ExpireAtMs || now > _intent.TransitionExpireAtMs;

    private uint NormalizeOrOriginal(uint actionId)
    {
        var normalized = _normalizer.Normalize(actionId);
        return normalized == 0 ? actionId : normalized;
    }

    private bool MatchesFrozenAction(BlmIntent intent, uint actionId)
        => actionId == intent.ExpectedActionId
            || actionId == intent.ExpectedAdjustedActionId;

    private static bool IsSequenceNewer(uint value, uint baseline)
        => unchecked((int)(value - baseline)) > 0;

    private static BlmIntent Cancelled(BlmIntent current, string reason) => current with
    {
        Stage = TransitionStage.Cancelled,
        ExpectedActionId = 0,
        ExpectedAdjustedActionId = 0,
        QueuedAfterGlobalSequence = 0,
        QueuedAtMs = 0,
        QueuedDeadlineAtMs = 0,
        AcknowledgedActionId = 0,
        AcknowledgedGlobalSequence = 0,
        AcknowledgedAtMs = 0,
        ExpireAtMs = 0,
        TransitionExpireAtMs = 0,
        Reason = reason,
    };

    private static bool IsValidInitialStep(
        TransitionKind kind,
        IceToFireRoute route,
        RotationMode mode,
        TransitionStep step) => (kind, route) switch
        {
            (TransitionKind.IceToFire, IceToFireRoute.ExistingFirestarter) =>
                mode == RotationMode.SingleTarget && step == TransitionStep.CommitIceGcd,
            (TransitionKind.IceToFire, IceToFireRoute.Af1ParadoxRecovery) =>
                mode == RotationMode.SingleTarget
                    && step is TransitionStep.CommitIceGcd or TransitionStep.UseAfParadox,
            (TransitionKind.IceToFire, IceToFireRoute.B4TransposeDespair) =>
                mode == RotationMode.SingleTarget && step == TransitionStep.CommitIceGcd,
            (TransitionKind.IceToFire, IceToFireRoute.AoeFireEntry) =>
                mode is RotationMode.TwoTargetAoe or RotationMode.ThreePlusAoe
                    && step == TransitionStep.CommitIceGcd,
            (TransitionKind.FireToIce, IceToFireRoute.None) =>
                step == TransitionStep.CommitFireFinisher,
            (TransitionKind.ManafontExtension, IceToFireRoute.None) =>
                step == TransitionStep.CommitFireFinisher,
            _ => false,
        };

    private static bool IsValidAdvance(
        TransitionKind kind,
        IceToFireRoute route,
        RotationMode mode,
        TransitionStep current,
        TransitionStep next) => (kind, route, mode) switch
        {
            (TransitionKind.IceToFire, IceToFireRoute.ExistingFirestarter,
                RotationMode.SingleTarget) => (current, next) is
                (TransitionStep.CommitIceGcd, TransitionStep.UseTranspose)
                or (TransitionStep.UseTranspose, TransitionStep.UseFirestarterF3),
            (TransitionKind.IceToFire, IceToFireRoute.Af1ParadoxRecovery,
                RotationMode.SingleTarget) => (current, next) is
                (TransitionStep.CommitIceGcd, TransitionStep.UseTranspose)
                or (TransitionStep.UseTranspose, TransitionStep.UseAfParadox)
                or (TransitionStep.UseAfParadox, TransitionStep.UseFirestarterF3),
            (TransitionKind.IceToFire, IceToFireRoute.B4TransposeDespair,
                RotationMode.SingleTarget) => (current, next) is
                (TransitionStep.CommitIceGcd, TransitionStep.UseTranspose)
                or (TransitionStep.UseTranspose, TransitionStep.UseTransposeDespair),
            (TransitionKind.IceToFire, IceToFireRoute.AoeFireEntry,
                RotationMode.TwoTargetAoe or RotationMode.ThreePlusAoe) =>
                (current, next) is (TransitionStep.CommitIceGcd, TransitionStep.UseTranspose),
            (TransitionKind.FireToIce, IceToFireRoute.None,
                RotationMode.SingleTarget) => (current, next) is
                (TransitionStep.CommitFireFinisher, TransitionStep.UseTranspose)
                or (TransitionStep.UseTranspose, TransitionStep.UseInstantBuff)
                or (TransitionStep.UseTranspose, TransitionStep.UseIceParadoxWait)
                or (TransitionStep.UseTranspose, TransitionStep.UseBlizzard3)
                or (TransitionStep.UseIceParadoxWait, TransitionStep.UseInstantBuff)
                or (TransitionStep.UseIceParadoxWait, TransitionStep.UseBlizzard3)
                or (TransitionStep.UseInstantBuff, TransitionStep.UseBlizzard3),
            (TransitionKind.FireToIce, IceToFireRoute.None,
                RotationMode.TwoTargetAoe or RotationMode.ThreePlusAoe) =>
                (current, next) is (TransitionStep.CommitFireFinisher, TransitionStep.UseTranspose),
            (TransitionKind.ManafontExtension, IceToFireRoute.None, _) =>
                (current, next) is (TransitionStep.CommitFireFinisher, TransitionStep.UseManafont),
            _ => false,
        };

    private static bool IsValidTerminalStep(
        TransitionKind kind,
        IceToFireRoute route,
        RotationMode mode,
        TransitionStep step) => (kind, route, mode) switch
        {
            (TransitionKind.IceToFire,
                IceToFireRoute.ExistingFirestarter or IceToFireRoute.Af1ParadoxRecovery,
                RotationMode.SingleTarget) => step == TransitionStep.UseFirestarterF3,
            (TransitionKind.IceToFire, IceToFireRoute.B4TransposeDespair,
                RotationMode.SingleTarget) => step == TransitionStep.UseTransposeDespair,
            (TransitionKind.IceToFire, IceToFireRoute.AoeFireEntry,
                RotationMode.TwoTargetAoe or RotationMode.ThreePlusAoe) =>
                step == TransitionStep.UseTranspose,
            (TransitionKind.FireToIce, IceToFireRoute.None, RotationMode.SingleTarget) =>
                step == TransitionStep.UseBlizzard3,
            (TransitionKind.FireToIce, IceToFireRoute.None,
                RotationMode.TwoTargetAoe or RotationMode.ThreePlusAoe) =>
                step == TransitionStep.UseTranspose,
            (TransitionKind.ManafontExtension, IceToFireRoute.None, _) =>
                step == TransitionStep.UseManafont,
            _ => false,
        };

    private static bool IsDeliveryAllowedForStep(
        TransitionKind kind,
        RotationMode mode,
        TransitionStep step,
        TransitionDeliveryChannel channel) => step switch
        {
            TransitionStep.CommitIceGcd
                or TransitionStep.UseAfParadox
                or TransitionStep.UseFirestarterF3
                or TransitionStep.CommitFireFinisher
                or TransitionStep.UseIceParadoxWait
                or TransitionStep.UseTransposeDespair
                or TransitionStep.UseBlizzard3 => channel == TransitionDeliveryChannel.Gcd,
            TransitionStep.UseTranspose when kind == TransitionKind.FireToIce
                && mode == RotationMode.SingleTarget => channel == TransitionDeliveryChannel.OffGcd,
            TransitionStep.UseTranspose or TransitionStep.UseManafont =>
                channel is TransitionDeliveryChannel.OffGcd
                    or TransitionDeliveryChannel.OffGcdOrAlways,
            TransitionStep.UseInstantBuff => channel == TransitionDeliveryChannel.OffGcd,
            _ => false,
        };

    private static bool CanQueueVia(
        TransitionDeliveryChannel channel,
        BlmQueueChannel queueChannel,
        float gcdRemainSeconds) => channel switch
        {
            TransitionDeliveryChannel.Gcd => queueChannel == BlmQueueChannel.Gcd,
            TransitionDeliveryChannel.OffGcd =>
                queueChannel == BlmQueueChannel.OffGcd && gcdRemainSeconds > 0.6f,
            TransitionDeliveryChannel.OffGcdOrAlways =>
                (queueChannel == BlmQueueChannel.OffGcd && gcdRemainSeconds > 0.6f)
                || (queueChannel == BlmQueueChannel.Always && gcdRemainSeconds <= 0.6f),
            _ => false,
        };

    private bool IsExpectedActionForStep(TransitionStep step, uint actionId) => step switch
    {
        TransitionStep.CommitIceGcd => BlmSkillBook.IsIcePhaseCommitGcd(actionId, _normalizer),
        TransitionStep.CommitFireFinisher =>
            BlmSkillBook.IsFireFinisherCommitGcd(actionId, _normalizer),
        TransitionStep.UseTranspose => Matches(BLMSkill.星灵移位, actionId),
        TransitionStep.UseAfParadox => Matches(BLMSkill.悖论, actionId),
        TransitionStep.UseFirestarterF3 => Matches(BLMSkill.爆炎, actionId),
        TransitionStep.UseInstantBuff =>
            Matches(MageUniversalSkill.即刻咏唱, actionId)
            || Matches(BLMSkill.三连咏唱, actionId),
        TransitionStep.UseIceParadoxWait => Matches(BLMSkill.悖论, actionId),
        TransitionStep.UseTransposeDespair => Matches(BLMSkill.绝望, actionId),
        TransitionStep.UseBlizzard3 => Matches(BLMSkill.冰封, actionId),
        TransitionStep.UseManafont => Matches(BLMSkill.魔泉, actionId),
        _ => false,
    };

    private bool IsExpectationAllowed(
        TransitionKind kind,
        TransitionStep step,
        IceToFireRoute route,
        uint actionId,
        TransitionExpectation expectation) => (kind, step, route) switch
        {
            (TransitionKind.IceToFire, TransitionStep.CommitIceGcd,
                IceToFireRoute.ExistingFirestarter) =>
                expectation == TransitionExpectation.IceReadyWithFirestarter,
            (TransitionKind.IceToFire, TransitionStep.CommitIceGcd,
                IceToFireRoute.Af1ParadoxRecovery) =>
                expectation == TransitionExpectation.IceReadyForAf1Paradox,
            (TransitionKind.IceToFire, TransitionStep.CommitIceGcd,
                IceToFireRoute.B4TransposeDespair) =>
                expectation == TransitionExpectation.IceReadyForTransposeDespair,
            (TransitionKind.IceToFire, TransitionStep.CommitIceGcd,
                IceToFireRoute.AoeFireEntry) =>
                expectation == TransitionExpectation.AoeIceResourcesReady,
            (TransitionKind.IceToFire, TransitionStep.UseTranspose,
                IceToFireRoute.ExistingFirestarter) =>
                expectation == TransitionExpectation.AstralFireOne,
            (TransitionKind.IceToFire, TransitionStep.UseTranspose,
                IceToFireRoute.Af1ParadoxRecovery) =>
                expectation == TransitionExpectation.AstralFireOneWithParadox,
            (TransitionKind.IceToFire, TransitionStep.UseTranspose,
                IceToFireRoute.B4TransposeDespair) =>
                expectation == TransitionExpectation.AstralFireOne,
            (TransitionKind.IceToFire, TransitionStep.UseTranspose,
                IceToFireRoute.AoeFireEntry) =>
                expectation == TransitionExpectation.AstralFireOne,
            (TransitionKind.IceToFire, TransitionStep.UseAfParadox,
                IceToFireRoute.Af1ParadoxRecovery) =>
                expectation == TransitionExpectation.FirestarterPresent,
            (TransitionKind.IceToFire, TransitionStep.UseFirestarterF3, _) =>
                expectation == TransitionExpectation.AstralFireThree,
            (TransitionKind.IceToFire, TransitionStep.UseTransposeDespair,
                IceToFireRoute.B4TransposeDespair) =>
                expectation == TransitionExpectation.AstralFireThree,
            (TransitionKind.FireToIce, TransitionStep.CommitFireFinisher, _) =>
                expectation == TransitionExpectation.FireFinisherReady,
            (TransitionKind.FireToIce, TransitionStep.UseTranspose, _) =>
                expectation == TransitionExpectation.UmbralIceOne,
            (TransitionKind.FireToIce, TransitionStep.UseInstantBuff, _)
                when Matches(MageUniversalSkill.即刻咏唱, actionId) =>
                expectation == TransitionExpectation.SwiftcastPresent,
            (TransitionKind.FireToIce, TransitionStep.UseInstantBuff, _)
                when Matches(BLMSkill.三连咏唱, actionId) =>
                expectation == TransitionExpectation.TriplecastPresent,
            (TransitionKind.FireToIce, TransitionStep.UseIceParadoxWait, _) =>
                expectation == TransitionExpectation.RemainInIce,
            (TransitionKind.FireToIce, TransitionStep.UseBlizzard3, _) =>
                expectation == TransitionExpectation.UmbralIceThree,
            (TransitionKind.ManafontExtension, TransitionStep.CommitFireFinisher, _) =>
                expectation == TransitionExpectation.FireFinisherReady,
            (TransitionKind.ManafontExtension, TransitionStep.UseManafont, _) =>
                expectation == TransitionExpectation.ManafontResourcesRestored,
            _ => false,
        };

    private bool Matches(uint expectedActionId, uint actualActionId)
        => BlmSkillBook.ActionIdsMatch(expectedActionId, actualActionId, _normalizer);

    private static bool ExpectationSatisfied(
        TransitionExpectation expectation,
        BlmContext context) => expectation switch
        {
            TransitionExpectation.AckOnly => true,
            TransitionExpectation.RemainInIce => context.InIce,
            TransitionExpectation.RemainInFire => context.InFire,
            TransitionExpectation.AstralFireOne => context.InFire && context.AfStacks == 1,
            TransitionExpectation.AstralFireOneWithParadox =>
                context.InFire && context.AfStacks == 1 && context.HasParadox,
            TransitionExpectation.AstralFireThree => context.InFire && context.AfStacks == 3,
            TransitionExpectation.FireFinisherReady =>
                context.InFire && (context.Level < 35 || context.AfStacks == 3),
            TransitionExpectation.UmbralIceOne => context.InIce && context.IceStacks == 1,
            TransitionExpectation.UmbralIceThree => context.InIce && context.IceStacks == 3,
            TransitionExpectation.FirestarterPresent => context.InFire && context.HasFirestarter,
            TransitionExpectation.IceReadyWithFirestarter =>
                context.InIce && context.IceStacks == 3 && context.HasFirestarter,
            TransitionExpectation.IceReadyForAf1Paradox =>
                context.Level >= 90
                && context.InIce
                && context.IceStacks == 3
                && context.UmbralHearts == 3,
            TransitionExpectation.IceReadyForTransposeDespair =>
                context.Level >= 100
                && context.InIce
                && context.IceStacks == 3
                && context.UmbralHearts == 3
                && context.IsMpFull,
            TransitionExpectation.AoeIceResourcesReady =>
                context.InIce
                && context.IsMpFull
                && (context.Level < 35 || context.IceStacks == 3)
                && (context.Level < 58 || context.UmbralHearts == 3),
            TransitionExpectation.SwiftcastPresent => context.HasUsableSwiftcast,
            TransitionExpectation.TriplecastPresent => context.HasUsableTriplecast,
            TransitionExpectation.ManafontResourcesRestored =>
                context.HasManafontResourcesRestored,
            _ => false,
        };
}
