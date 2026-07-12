namespace LosPr.BLM.Engine;

public sealed class BlmFollowUpCoordinator
{
    public const long PostAckReconcileTimeoutMs = 1500;
    public const long TriggerAckTimeoutMs = 5000;

    private readonly object _gate = new();
    private readonly IBlmClock _clock;
    private BlmFollowUpIntent _intent = BlmFollowUpIntent.Empty;
    private long _nextSerial;

    public BlmFollowUpCoordinator(IBlmClock? clock = null)
    {
        _clock = clock ?? SystemBlmClock.Instance;
    }

    public BlmFollowUpIntent Peek()
    {
        lock (_gate)
        {
            return _intent;
        }
    }

    public bool TryBegin(BlmFollowUpRequest request, BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (request.Kind != BlmFollowUpKind.FlareStarAfterMovementDespair
            || request.StateGeneration <= 0
            || request.CombatSerial <= 0
            || request.FirePhaseSerial <= 0
            || request.TargetEntityId == 0
            || request.TriggerActionId != BLMSkill.绝望
            || request.RequiredActionId != BLMSkill.耀星
            || request.ExpireAfterMs <= 0
            || request.StateGeneration != context.Tracker.StateGeneration
            || request.CombatSerial != context.Tracker.CombatSerial
            || request.FirePhaseSerial != context.Tracker.FirePhaseSerial
            || request.TargetEntityId != context.TargetEntityId
            || !context.InFire
            || context.AfStacks != 3
            || !context.AstralSoulFull
            || context.Mp < BlmFireBudget.DespairMinimumMp)
        {
            return false;
        }

        lock (_gate)
        {
            if (_intent.IsPending)
            {
                return false;
            }

            var now = _clock.NowMs;
            var totalExpireAtMs = now + request.ExpireAfterMs;
            _intent = new BlmFollowUpIntent
            {
                Kind = request.Kind,
                Stage = BlmFollowUpStage.AwaitingTriggerAck,
                StateGeneration = request.StateGeneration,
                CombatSerial = request.CombatSerial,
                FirePhaseSerial = request.FirePhaseSerial,
                Serial = ++_nextSerial,
                TargetEntityId = request.TargetEntityId,
                TriggerActionId = request.TriggerActionId,
                RequiredActionId = request.RequiredActionId,
                RequestedAfterGlobalSequence = request.RequestedAfterGlobalSequence,
                RequestedAtMs = now,
                StageDeadlineAtMs = Math.Min(now + TriggerAckTimeoutMs, totalExpireAtMs),
                TotalExpireAtMs = totalExpireAtMs,
                Reason = request.Reason,
            };
            return true;
        }
    }

    public BlmFollowUpAckToken CaptureAckToken(
        long stateGeneration,
        long combatSerial)
    {
        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.CombatSerial != combatSerial)
            {
                return default;
            }

            return _intent.Stage switch
            {
                BlmFollowUpStage.AwaitingTriggerAck => BuildAckToken(
                    _intent.TriggerActionId,
                    _intent.RequestedAfterGlobalSequence,
                    _intent.RequestedAtMs),
                BlmFollowUpStage.Active => BuildAckToken(
                    _intent.RequiredActionId,
                    _intent.TriggerAckGlobalSequence,
                    _intent.TriggerAckAtMs),
                BlmFollowUpStage.RequiredQueued => BuildAckToken(
                    _intent.RequiredActionId,
                    _intent.QueuedAfterGlobalSequence,
                    _intent.QueuedAtMs),
                _ => default,
            };
        }
    }

    public bool TryAcknowledge(
        BlmFollowUpAckToken token,
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
            if (_intent.StateGeneration != token.StateGeneration
                || _intent.CombatSerial != token.CombatSerial
                || _intent.Serial != token.Serial
                || actionId != token.ExpectedActionId
                || receivedAtMs < token.RegisteredAtMs
                || receivedAtMs > token.DeadlineAtMs
                || receivedAtMs > _intent.StageDeadlineAtMs
                || _clock.NowMs > _intent.TotalExpireAtMs
                || (globalSequence != 0
                    && token.SequenceBaseline != 0
                    && !IsSequenceNewer(globalSequence, token.SequenceBaseline)))
            {
                return false;
            }

            var isTrigger = token.StageAtCapture == BlmFollowUpStage.AwaitingTriggerAck
                && _intent.Stage == BlmFollowUpStage.AwaitingTriggerAck
                && actionId == _intent.TriggerActionId;
            var isRequired = (token.StageAtCapture is BlmFollowUpStage.Active
                    or BlmFollowUpStage.RequiredQueued)
                && (_intent.Stage is BlmFollowUpStage.Active
                    or BlmFollowUpStage.RequiredQueued)
                && actionId == _intent.RequiredActionId;
            if (!isTrigger && !isRequired)
            {
                return false;
            }

            var now = _clock.NowMs;
            if (isTrigger)
            {
                _intent = _intent with
                {
                    Stage = BlmFollowUpStage.TriggerAcknowledged,
                    TriggerAckGlobalSequence = globalSequence,
                    TriggerAckAtMs = receivedAtMs,
                    StageDeadlineAtMs = Math.Min(
                        now + PostAckReconcileTimeoutMs,
                        _intent.TotalExpireAtMs),
                    Reason = $"绝望 Ack 已确认：{_intent.Reason}",
                };
            }
            else
            {
                _intent = _intent with
                {
                    Stage = BlmFollowUpStage.RequiredAcknowledged,
                    RequiredAckGlobalSequence = globalSequence,
                    RequiredAckAtMs = receivedAtMs,
                    StageDeadlineAtMs = Math.Min(
                        now + PostAckReconcileTimeoutMs,
                        _intent.TotalExpireAtMs),
                    Reason = $"耀星 Ack 已确认：{_intent.Reason}",
                };
            }

            return true;
        }
    }

    public void Reconcile(
        long stateGeneration,
        long combatSerial,
        long firePhaseSerial,
        BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (!_intent.IsPending)
            {
                return;
            }

            var now = _clock.NowMs;
            if (now > _intent.StageDeadlineAtMs
                || now > _intent.TotalExpireAtMs)
            {
                _intent = Cancelled(_intent, "Follow-up timeout");
                return;
            }

            if (_intent.StateGeneration != stateGeneration
                || _intent.CombatSerial != combatSerial
                || _intent.FirePhaseSerial != firePhaseSerial
                || _intent.TargetEntityId != context.TargetEntityId
                || !context.HasValidTarget
                || !context.InRange
                || !context.InFire
                || context.AfStacks != 3)
            {
                _intent = Cancelled(_intent, "Follow-up facts changed");
                return;
            }

            switch (_intent.Stage)
            {
                case BlmFollowUpStage.AwaitingTriggerAck:
                    if (!context.AstralSoulFull)
                    {
                        _intent = Cancelled(_intent, "Soul changed before Despair Ack");
                    }

                    break;
                case BlmFollowUpStage.TriggerAcknowledged:
                    if (!context.AstralSoulFull)
                    {
                        _intent = Cancelled(
                            _intent,
                            "Soul changed after Despair Ack");
                    }
                    else if (context.Mp < BlmFireBudget.DespairMinimumMp)
                    {
                        _intent = _intent with
                        {
                            Stage = BlmFollowUpStage.Active,
                            StageDeadlineAtMs = _intent.TotalExpireAtMs,
                            Reason = "绝望后 Gauge 已确认，强制后续耀星。",
                        };
                    }

                    break;
                case BlmFollowUpStage.Active:
                case BlmFollowUpStage.RequiredQueued:
                    if (!context.AstralSoulFull)
                    {
                        _intent = Cancelled(_intent, "Soul consumed without matching Flare Star Ack");
                    }

                    break;
                case BlmFollowUpStage.RequiredAcknowledged:
                    if (!context.AstralSoulFull)
                    {
                        _intent = _intent with
                        {
                            Stage = BlmFollowUpStage.Completed,
                            StageDeadlineAtMs = 0,
                            TotalExpireAtMs = 0,
                            Reason = "耀星 Ack 与 Gauge 消耗均已确认。",
                        };
                    }

                    break;
            }
        }
    }

    public bool TryMarkRequiredQueued(
        long stateGeneration,
        long serial,
        uint actionId,
        uint globalSequenceBaseline,
        long expireMs,
        string reason)
    {
        if (stateGeneration <= 0 || serial <= 0 || actionId == 0 || expireMs <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.Serial != serial
                || _intent.Stage != BlmFollowUpStage.Active
                || actionId != _intent.RequiredActionId)
            {
                return false;
            }

            var now = _clock.NowMs;
            if (now > _intent.TotalExpireAtMs)
            {
                _intent = Cancelled(_intent, "Follow-up timeout before queue");
                return false;
            }

            _intent = _intent with
            {
                Stage = BlmFollowUpStage.RequiredQueued,
                QueuedAfterGlobalSequence = globalSequenceBaseline,
                QueuedAtMs = now,
                StageDeadlineAtMs = Math.Min(now + expireMs, _intent.TotalExpireAtMs),
                Reason = reason,
            };
            return true;
        }
    }

    public bool TryCancel(
        long stateGeneration,
        long serial,
        string reason)
    {
        lock (_gate)
        {
            if (_intent.StateGeneration != stateGeneration
                || _intent.Serial != serial
                || !_intent.IsPending)
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
            if (_intent.IsPending)
            {
                _intent = Cancelled(_intent, reason);
            }
        }
    }

    public void ClearTerminal()
    {
        lock (_gate)
        {
            if (!_intent.IsPending)
            {
                _intent = BlmFollowUpIntent.Empty;
            }
        }
    }

    private BlmFollowUpAckToken BuildAckToken(
        uint expectedActionId,
        uint sequenceBaseline,
        long registeredAtMs)
        => new(
            _intent.StateGeneration,
            _intent.CombatSerial,
            _intent.Serial,
            _intent.Stage,
            expectedActionId,
            sequenceBaseline,
            registeredAtMs,
            Math.Min(_intent.StageDeadlineAtMs, _intent.TotalExpireAtMs));

    private static bool IsSequenceNewer(uint value, uint baseline)
        => unchecked((int)(value - baseline)) > 0;

    private static BlmFollowUpIntent Cancelled(
        BlmFollowUpIntent current,
        string reason) => current with
        {
            Stage = BlmFollowUpStage.Cancelled,
            StageDeadlineAtMs = 0,
            TotalExpireAtMs = 0,
            Reason = reason,
        };
}
