using System.Collections.Immutable;

namespace LosPr.BLM.Core;

internal sealed class BlmStateTracker
{
    private const int SeenSequenceCapacity = 4096;
    private const long ZeroSequenceDedupeWindowMs = 150;
    private const long HardcastStartTimeoutMs = 750;
    private const long HardcastCompletionAckGraceMs = 250;
    private const float HardcastCompletionRemainThresholdSeconds = 0.15f;
    public const long ManafontReconcileTimeoutMs = 1500;
    public const int AcknowledgedActionHistoryCapacity = 256;
    public const int ZeroSequenceDedupeCapacity = 256;

    private readonly object _gate = new();
    private readonly IBlmClock _clock;
    private readonly IBlmActionIdNormalizer _normalizer;
    private readonly HashSet<SequenceEventKey> _seenSequences = new();
    private readonly Queue<SequenceEventKey> _seenSequenceOrder = new();
    private readonly Dictionary<uint, ZeroSequenceEvent> _lastZeroSequenceByAction = new();
    private readonly Queue<ZeroSequenceEvent> _zeroSequenceOrder = new();
    private readonly BlmActionSuccess[] _acknowledgedActionHistory =
        new BlmActionSuccess[AcknowledgedActionHistoryCapacity];
    private readonly Dictionary<uint, BlmActionSuccess> _lastActionSuccessByAlias = new();

    private BlmContext _latestContext;
    private BlmPhase _lastObservedPhase;
    private AcrState _lastAcrState;
    private uint _playerEntityId;
    private uint _jobId;
    private bool _combatActive;
    private bool _dead;
    private bool _disposed;
    private ushort _lastTerritoryId;
    private BlmActionEffectAck? _lastAckAwaitingGauge;
    private PendingManafontSync? _pendingManafont;
    private BlmIssuedActionMetadata? _pendingIssuedAction;
    private bool _pendingHardcastObserved;
    private float _pendingHardcastLastRemainSeconds;
    private long _pendingHardcastEndedAtMs;

    private long _combatSerial;
    private long _stateGeneration;
    private bool _historyReliable;
    private long _firePhaseSerial;
    private long _icePhaseSerial;
    private long _paradoxUsedFireSerial;
    private long _paradoxUsedIceSerial;
    private int _fire4Count;
    private int _fire4CountSinceManafont;
    private bool _manafontActiveThisFire;
    private long _manafontUseSerial;
    private uint _lastGcdId;
    private long _lastGcdAtMs;
    private long _lastGcdStartedAtMs;
    private uint _lastOgcdId;
    private long _lastOgcdAtMs;
    private uint _lastAckActionId;
    private uint _lastAckGlobalSequence;
    private long _lastAckAtMs;
    private BlmPhase _lastAckPhaseBefore;
    private long _lastAckGeneration;
    private uint _lastGaugeReconciledActionId;
    private long _lastGaugeReconciledAtMs;
    private string _lastResetReason = "初始化";
    private int _acknowledgedActionHistoryStart;
    private int _acknowledgedActionHistoryCount;
    private long _actionSuccessSerial;
    private long _zeroSequenceSerial;

    public BlmStateTracker(
        BlmContext initialContext,
        IBlmClock? clock = null,
        IBlmActionIdNormalizer? normalizer = null)
    {
        _clock = clock ?? SystemBlmClock.Instance;
        _normalizer = normalizer ?? IdentityBlmActionIdNormalizer.Instance;
        _latestContext = initialContext ?? throw new ArgumentNullException(nameof(initialContext));
        _stateGeneration = 1;
        _combatActive = initialContext.InCombat;
        _combatSerial = _combatActive ? 1 : 0;
        _dead = initialContext.IsAvailable && !initialContext.IsAlive;
        _lastObservedPhase = initialContext.Phase;
        _lastAcrState = initialContext.AcrState;
        _playerEntityId = initialContext.PlayerEntityId;
        _jobId = initialContext.JobId;
        InitializePhaseSerialsNoLock(initialContext.Phase);
        if (initialContext.IsAvailable)
        {
            UpdateGcdStartedAtNoLock(initialContext);
        }
        _historyReliable = initialContext.IsAvailable
            && !_dead
            && !_combatActive
            && initialContext.Phase == BlmPhase.Neutral;
        RefreshLatestContextNoLock(initialContext);
    }

    public long CombatSerial
    {
        get
        {
            lock (_gate)
            {
                return _combatSerial;
            }
        }
    }

    public long StateGeneration
    {
        get
        {
            lock (_gate)
            {
                return _stateGeneration;
            }
        }
    }

    public BlmContext GetContextSnapshot()
    {
        lock (_gate)
        {
            return _latestContext;
        }
    }

    public BlmTrackerSnapshot GetTrackerSnapshot()
    {
        lock (_gate)
        {
            return BuildSnapshotNoLock();
        }
    }

    public BlmTrackerDecisionSnapshot CaptureDecisionSnapshot()
    {
        lock (_gate)
        {
            var history = ImmutableArray.CreateBuilder<BlmActionSuccess>(
                _acknowledgedActionHistoryCount);
            BlmActionSuccess? previousGcd = null;
            for (var offset = 0; offset < _acknowledgedActionHistoryCount; offset++)
            {
                var index = (_acknowledgedActionHistoryStart + offset)
                    % AcknowledgedActionHistoryCapacity;
                var success = _acknowledgedActionHistory[index];
                if (success.StateGeneration != _stateGeneration)
                {
                    continue;
                }

                history.Add(success);
                if (success.IsGcd
                    && (previousGcd is null
                        || success.Serial > previousGcd.Value.Serial))
                {
                    previousGcd = success;
                }
            }

            return new BlmTrackerDecisionSnapshot
            {
                Snapshot = BuildSnapshotNoLock(),
                RecentHistory = history.MoveToImmutable(),
                PreviousGcd = previousGcd,
            };
        }
    }

    public bool TryRegisterIssuedAction(BlmIssuedActionMetadata metadata)
    {
        lock (_gate)
        {
            if (_disposed
                || metadata.StateGeneration != _stateGeneration
                || metadata.RequestedId == 0
                || metadata.AdjustedAtIssue == 0
                || metadata.DeadlineAtMs < metadata.IssuedAtMs
                || metadata.DeadlineAtMs < _clock.NowMs)
            {
                return false;
            }

            ExpireIssuedActionNoLock(_clock.NowMs);
            if (_pendingIssuedAction is { } pending)
            {
                return pending == metadata;
            }

            _pendingIssuedAction = metadata;
            ResetPendingHardcastObservationNoLock();
            return true;
        }
    }

    public void CancelIssuedAction()
    {
        lock (_gate)
        {
            ClearPendingIssuedActionNoLock();
        }
    }

    public void CancelTargetDependentIssuedAction()
    {
        lock (_gate)
        {
            if (_pendingIssuedAction is { } pending
                && !BlmSkillBook.IsKnownSelfTargetActionId(pending.RequestedId)
                && !BlmSkillBook.IsKnownSelfTargetActionId(pending.AdjustedAtIssue))
            {
                ClearPendingIssuedActionNoLock();
            }
        }
    }

    public BlmActionEffectAck CreateAckEnvelope(
        uint sourceId,
        uint actionId,
        uint globalSequence,
        BlmPhase phaseBefore,
        long receivedAtMs)
        => CreateAckEnvelope(
            sourceId,
            actionId,
            globalSequence,
            phaseBefore,
            receivedAtMs,
            0,
            0f,
            false);

    public BlmActionEffectAck CreateAckEnvelope(
        uint sourceId,
        uint actionId,
        uint globalSequence,
        BlmPhase phaseBefore,
        long receivedAtMs,
        long observedGcdStartedAtMs,
        float observedGcdRemainMs,
        bool hasHasteAtAck)
    {
        lock (_gate)
        {
            var phaseSerial = phaseBefore switch
            {
                BlmPhase.Fire => _firePhaseSerial,
                BlmPhase.Ice => _icePhaseSerial,
                _ => 0,
            };

            return new BlmActionEffectAck(
                _combatSerial,
                _stateGeneration,
                sourceId,
                actionId,
                globalSequence,
                receivedAtMs,
                phaseBefore,
                phaseSerial,
                observedGcdStartedAtMs,
                observedGcdRemainMs,
                hasHasteAtAck);
        }
    }

    public bool ApplyActionEffect(BlmActionEffectAck ack)
    {
        lock (_gate)
        {
            if (_disposed
                || ack.ActionId == 0
                || ack.StateGeneration != _stateGeneration
                || ack.CombatSerial != _combatSerial
                || _playerEntityId == 0
                || ack.SourceId != _playerEntityId
                || IsDuplicateNoLock(ack))
            {
                return false;
            }

            _lastAckActionId = ack.ActionId;
            _lastAckGlobalSequence = ack.GlobalSequence;
            _lastAckAtMs = ack.ReceivedAtMs;
            _lastAckPhaseBefore = ack.PhaseBefore;
            _lastAckGeneration = ack.StateGeneration;
            _lastAckAwaitingGauge = ack;

            var isKnownGcd = BlmSkillBook.IsKnownGcdAction(ack.ActionId, _normalizer);
            var isTrackedOgcd = !isKnownGcd && IsTrackedOgcd(ack.ActionId);
            var issuedMetadata = TakeMatchingIssuedActionNoLock(
                ack,
                isKnownGcd,
                isTrackedOgcd);
            RecordActionSuccessNoLock(ack, isKnownGcd, issuedMetadata);

            if (isKnownGcd)
            {
                _lastGcdId = ack.ActionId;
                _lastGcdAtMs = ack.ReceivedAtMs;
                ApplyGcdFactsNoLock(ack);
            }
            else if (IsTrackedOgcd(ack.ActionId))
            {
                _lastOgcdId = ack.ActionId;
                _lastOgcdAtMs = ack.ReceivedAtMs;
                ApplyOgcdFactsNoLock(ack);
            }

            RefreshLatestContextNoLock(_latestContext);
            return true;
        }
    }

    public void Reconcile(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var resetApplied = ApplyLifecycleEdgesNoLock(context);
            if (!resetApplied && context.IsAvailable)
            {
                ApplyPhaseEdgeNoLock(context.Phase);
                UpdateGcdStartedAtNoLock(context);
            }

            var nowMs = _clock.NowMs;
            ReconcilePendingHardcastNoLock(context, nowMs);
            ExpireIssuedActionNoLock(nowMs);

            if (_lastAckAwaitingGauge is { } genericAck
                && genericAck.StateGeneration == _stateGeneration)
            {
                _lastGaugeReconciledActionId = genericAck.ActionId;
                _lastGaugeReconciledAtMs = _clock.NowMs;
                _lastAckAwaitingGauge = null;
            }

            ReconcileManafontNoLock(context);

            if (context.IsAvailable)
            {
                _lastObservedPhase = context.Phase;
                _lastAcrState = context.AcrState;
                _playerEntityId = context.PlayerEntityId != 0
                    ? context.PlayerEntityId
                    : _playerEntityId;
                _jobId = context.JobId != 0 ? context.JobId : _jobId;
            }
            RefreshLatestContextNoLock(context);
        }
    }

    public void BeginCombat()
    {
        lock (_gate)
        {
            if (_disposed || _combatActive)
            {
                return;
            }

            BeginCombatNoLock(_latestContext, "进入新战斗");
        }
    }

    public void EndCombat()
    {
        lock (_gate)
        {
            if (_disposed || !_combatActive)
            {
                return;
            }

            EndCombatNoLock(_latestContext, "战斗结束");
        }
    }

    public void OnPlayerDied()
    {
        lock (_gate)
        {
            if (_disposed || _dead)
            {
                return;
            }

            ApplyDeathNoLock(_latestContext, "玩家死亡");
        }
    }

    public void OnPlayerRevived()
    {
        lock (_gate)
        {
            if (_disposed || !_dead)
            {
                return;
            }

            ApplyReviveNoLock(_latestContext, "玩家复活");
        }
    }

    public void OnTerritoryChanged(ushort territoryId)
    {
        lock (_gate)
        {
            if (_disposed || (_lastTerritoryId != 0 && _lastTerritoryId == territoryId))
            {
                return;
            }

            _lastTerritoryId = territoryId;
            _combatActive = false;
            HardResetNoLock(_latestContext, $"区域切换至 {territoryId}");
            _historyReliable = false;
            RefreshLatestContextNoLock(_latestContext);
        }
    }

    public bool RecentlyAcknowledged(uint actionId, int withinMs)
    {
        lock (_gate)
        {
            if (withinMs < 0)
            {
                return false;
            }

            return TryGetLastActionSuccessNoLock(actionId, out var success)
                && _clock.NowMs - success.AcknowledgedAtMs <= withinMs;
        }
    }

    public bool RecentlyUsed(
        uint actionId,
        int withinMs = BlmDecisionPrimitives.DefaultRecentlyUsedWindowMs)
    {
        lock (_gate)
        {
            return TryGetLastActionSuccessNoLock(actionId, out var success)
                && BlmDecisionPrimitives.RecentlyUsed(
                    _clock.NowMs,
                    success.OccurredAtMs,
                    withinMs);
        }
    }

    public bool TryGetLastAcknowledgedAction(
        uint actionId,
        out BlmActionSuccess success)
    {
        lock (_gate)
        {
            return TryGetLastActionSuccessNoLock(actionId, out success);
        }
    }

    public void DisposeState()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            HardResetNoLock(_latestContext, "Rotation Dispose");
            _historyReliable = false;
            _disposed = true;
            RefreshLatestContextNoLock(_latestContext);
        }
    }

    private bool ApplyLifecycleEdgesNoLock(BlmContext context)
    {
        if (!context.IsAvailable)
        {
            return false;
        }

        if (_playerEntityId != 0
            && context.PlayerEntityId != 0
            && context.PlayerEntityId != _playerEntityId)
        {
            HardResetNoLock(context, "玩家实体变化");
            _historyReliable = false;
            return true;
        }

        if (_jobId != 0 && context.JobId != 0 && context.JobId != _jobId)
        {
            HardResetNoLock(context, "职业变化");
            _historyReliable = false;
            return true;
        }

        if (!context.IsAlive && !_dead)
        {
            ApplyDeathNoLock(context, "玩家死亡（Tick 自愈）");
            return true;
        }

        if (context.IsAlive && _dead)
        {
            ApplyReviveNoLock(context, "玩家复活（Tick 自愈）");
            return true;
        }

        if (context.InCombat && !_combatActive)
        {
            BeginCombatNoLock(context, "进入新战斗（Tick 自愈）");
            return true;
        }

        if (!context.InCombat && _combatActive)
        {
            EndCombatNoLock(context, "战斗结束（Tick 自愈）");
            return true;
        }

        if (context.AcrState == AcrState.Off && _lastAcrState != AcrState.Off)
        {
            HardResetNoLock(context, "ACR 关闭");
            _historyReliable = context.Phase == BlmPhase.Neutral;
            return true;
        }

        if (_combatActive
            && _lastObservedPhase != BlmPhase.Neutral
            && context.Phase == BlmPhase.Neutral)
        {
            HardResetNoLock(context, "战斗中元素状态异常归零");
            _historyReliable = false;
            return true;
        }

        return false;
    }

    private void BeginCombatNoLock(BlmContext context, string reason)
    {
        _combatActive = true;
        _combatSerial++;
        HardResetNoLock(context, reason);
        _historyReliable = context.Phase == BlmPhase.Neutral;
        RefreshLatestContextNoLock(context);
    }

    private void EndCombatNoLock(BlmContext context, string reason)
    {
        _combatActive = false;
        HardResetNoLock(context, reason);
        _historyReliable = context.Phase == BlmPhase.Neutral;
        RefreshLatestContextNoLock(context);
    }

    private void ApplyDeathNoLock(BlmContext context, string reason)
    {
        _dead = true;
        HardResetNoLock(context, reason);
        _historyReliable = false;
        RefreshLatestContextNoLock(context);
    }

    private void ApplyReviveNoLock(BlmContext context, string reason)
    {
        _dead = false;
        HardResetNoLock(context, reason);
        _historyReliable = false;
        RefreshLatestContextNoLock(context);
    }

    private void HardResetNoLock(BlmContext context, string reason)
    {
        _stateGeneration++;
        _fire4Count = 0;
        _fire4CountSinceManafont = 0;
        _paradoxUsedFireSerial = 0;
        _paradoxUsedIceSerial = 0;
        _manafontActiveThisFire = false;
        _manafontUseSerial = 0;
        _lastGcdId = 0;
        _lastGcdAtMs = 0;
        _lastGcdStartedAtMs = 0;
        _lastOgcdId = 0;
        _lastOgcdAtMs = 0;
        _lastAckActionId = 0;
        _lastAckGlobalSequence = 0;
        _lastAckAtMs = 0;
        _lastAckPhaseBefore = BlmPhase.Neutral;
        _lastAckGeneration = 0;
        _lastGaugeReconciledActionId = 0;
        _lastGaugeReconciledAtMs = 0;
        _lastAckAwaitingGauge = null;
        _pendingManafont = null;
        ClearPendingIssuedActionNoLock();
        _seenSequences.Clear();
        _seenSequenceOrder.Clear();
        _lastZeroSequenceByAction.Clear();
        _zeroSequenceOrder.Clear();
        Array.Clear(_acknowledgedActionHistory);
        _acknowledgedActionHistoryStart = 0;
        _acknowledgedActionHistoryCount = 0;
        _lastActionSuccessByAlias.Clear();
        _lastObservedPhase = context.Phase;
        InitializePhaseSerialsNoLock(context.Phase);
        _playerEntityId = context.PlayerEntityId != 0 ? context.PlayerEntityId : _playerEntityId;
        _jobId = context.JobId != 0 ? context.JobId : _jobId;
        _lastAcrState = context.AcrState;
        _lastResetReason = reason;
    }

    private void ApplyPhaseEdgeNoLock(BlmPhase current)
    {
        if (current == _lastObservedPhase)
        {
            return;
        }

        if (_lastObservedPhase == BlmPhase.Fire && current != BlmPhase.Fire)
        {
            _fire4Count = 0;
            _fire4CountSinceManafont = 0;
            _manafontActiveThisFire = false;
            _pendingManafont = null;
        }

        if (current == BlmPhase.Fire)
        {
            _firePhaseSerial++;
            _fire4Count = 0;
            if (!_manafontActiveThisFire)
            {
                _fire4CountSinceManafont = 0;
            }

            _historyReliable = true;
        }
        else if (current == BlmPhase.Ice)
        {
            _icePhaseSerial++;
            _fire4Count = 0;
            _fire4CountSinceManafont = 0;
            _manafontActiveThisFire = false;
            _historyReliable = true;
        }
    }

    private void ApplyGcdFactsNoLock(BlmActionEffectAck ack)
    {
        if (BlmSkillBook.ActionIdsMatch(BLMSkill.炽炎, ack.ActionId, _normalizer)
            && ack.PhaseBefore == BlmPhase.Fire
            && ack.PhaseSerialBefore == _firePhaseSerial)
        {
            _fire4Count++;
            if (_manafontActiveThisFire)
            {
                _fire4CountSinceManafont++;
            }

            return;
        }

        if (!BlmSkillBook.ActionIdsMatch(BLMSkill.悖论, ack.ActionId, _normalizer))
        {
            return;
        }

        if (ack.PhaseBefore == BlmPhase.Fire
            && ack.PhaseSerialBefore == _firePhaseSerial)
        {
            _paradoxUsedFireSerial = ack.PhaseSerialBefore;
        }
        else if (ack.PhaseBefore == BlmPhase.Ice
                 && ack.PhaseSerialBefore == _icePhaseSerial)
        {
            _paradoxUsedIceSerial = ack.PhaseSerialBefore;
        }
    }

    private void ApplyOgcdFactsNoLock(BlmActionEffectAck ack)
    {
        if (!BlmSkillBook.ActionIdsMatch(BLMSkill.魔泉, ack.ActionId, _normalizer))
        {
            return;
        }

        _pendingManafont = new PendingManafontSync(
            ack.CombatSerial,
            ack.StateGeneration,
            ack.ActionId,
            ack.GlobalSequence,
            ack.ReceivedAtMs,
            ack.ReceivedAtMs + ManafontReconcileTimeoutMs,
            ack.PhaseBefore,
            ack.PhaseSerialBefore);
    }

    private void ReconcileManafontNoLock(BlmContext context)
    {
        if (_pendingManafont is not { } pending)
        {
            return;
        }

        var now = _clock.NowMs;
        if (pending.StateGeneration != _stateGeneration
            || pending.CombatSerial != _combatSerial
            || now > pending.DeadlineMs
            || !context.InFire
            || pending.PhaseBefore != BlmPhase.Fire
            || pending.PhaseSerialBefore != _firePhaseSerial)
        {
            _pendingManafont = null;
            return;
        }

        if (!context.HasManafontResourcesRestored)
        {
            return;
        }

        _manafontActiveThisFire = true;
        _manafontUseSerial++;
        _fire4CountSinceManafont = 0;
        _paradoxUsedFireSerial = 0;
        _pendingManafont = null;
    }

    private bool IsDuplicateNoLock(BlmActionEffectAck ack)
    {
        if (ack.GlobalSequence == 0)
        {
            PruneZeroSequenceEventsNoLock(ack.ReceivedAtMs);
            if (_lastZeroSequenceByAction.TryGetValue(ack.ActionId, out var previous))
            {
                if (ack.ReceivedAtMs <= previous.ReceivedAtMs
                    || ack.ReceivedAtMs - previous.ReceivedAtMs
                        <= ZeroSequenceDedupeWindowMs)
                {
                    return true;
                }
            }

            var accepted = new ZeroSequenceEvent(
                ack.ActionId,
                ack.ReceivedAtMs,
                ++_zeroSequenceSerial);
            _lastZeroSequenceByAction[ack.ActionId] = accepted;
            _zeroSequenceOrder.Enqueue(accepted);
            while (_zeroSequenceOrder.Count > ZeroSequenceDedupeCapacity)
            {
                RemoveZeroSequenceEventNoLock(_zeroSequenceOrder.Dequeue());
            }

            return false;
        }

        var key = new SequenceEventKey(ack.StateGeneration, ack.SourceId, ack.GlobalSequence);
        if (!_seenSequences.Add(key))
        {
            return true;
        }

        _seenSequenceOrder.Enqueue(key);
        while (_seenSequenceOrder.Count > SeenSequenceCapacity)
        {
            _seenSequences.Remove(_seenSequenceOrder.Dequeue());
        }

        return false;
    }

    private void PruneZeroSequenceEventsNoLock(long receivedAtMs)
    {
        while (_zeroSequenceOrder.TryPeek(out var oldest)
               && receivedAtMs - oldest.ReceivedAtMs > ZeroSequenceDedupeWindowMs)
        {
            RemoveZeroSequenceEventNoLock(_zeroSequenceOrder.Dequeue());
        }
    }

    private void RemoveZeroSequenceEventNoLock(ZeroSequenceEvent candidate)
    {
        if (_lastZeroSequenceByAction.TryGetValue(candidate.ActionId, out var latest)
            && latest.Serial == candidate.Serial
            && latest.ReceivedAtMs == candidate.ReceivedAtMs)
        {
            _lastZeroSequenceByAction.Remove(candidate.ActionId);
        }
    }

    private BlmIssuedActionMetadata? TakeMatchingIssuedActionNoLock(
        BlmActionEffectAck ack,
        bool isKnownGcd,
        bool isTrackedOgcd)
    {
        if (_pendingIssuedAction is not { } pending)
        {
            return null;
        }

        if (pending.StateGeneration != _stateGeneration
            || ack.ReceivedAtMs > pending.DeadlineAtMs)
        {
            ClearPendingIssuedActionNoLock();
            return null;
        }

        var isNewerThanPending = ack.ReceivedAtMs >= pending.IssuedAtMs
            && (ack.GlobalSequence == 0
                || IsSequenceNewer(
                    ack.GlobalSequence,
                    pending.AckSequenceBaseline));
        if (!isNewerThanPending)
        {
            return null;
        }

        var actionMatches = ack.ActionId == pending.RequestedId
            || ack.ActionId == pending.AdjustedAtIssue;
        var gcdStartedBeforeIssue = isKnownGcd
            && ack.ObservedGcdStartedAtMs > 0
            && ack.ObservedGcdStartedAtMs < pending.IssuedAtMs;
        if (actionMatches && !gcdStartedBeforeIssue)
        {
            ClearPendingIssuedActionNoLock();
            return pending;
        }

        var newGcdSupersedesPending = isKnownGcd
            && ack.ObservedGcdStartedAtMs >= pending.IssuedAtMs;
        var sameChannelOgcdSupersedesPending = isTrackedOgcd && !pending.IsGcd;
        if (newGcdSupersedesPending || sameChannelOgcdSupersedesPending)
        {
            ClearPendingIssuedActionNoLock();
        }

        return null;
    }

    private void ExpireIssuedActionNoLock(long nowMs)
    {
        if (_pendingIssuedAction is { } pending
            && (pending.StateGeneration != _stateGeneration
                || nowMs > pending.DeadlineAtMs))
        {
            ClearPendingIssuedActionNoLock();
        }
    }

    private void ReconcilePendingHardcastNoLock(
        BlmContext context,
        long nowMs)
    {
        if (_pendingIssuedAction is not { IsGcd: true, WasInstant: false } pending)
        {
            ResetPendingHardcastObservationNoLock();
            return;
        }

        var isMatchingHardcast = context.IsCasting
            && context.CurrentCastingActionId != 0
            && (BlmSkillBook.ActionIdsMatch(
                    pending.RequestedId,
                    context.CurrentCastingActionId,
                    _normalizer)
                || BlmSkillBook.ActionIdsMatch(
                    pending.AdjustedAtIssue,
                    context.CurrentCastingActionId,
                    _normalizer));
        if (isMatchingHardcast)
        {
            _pendingHardcastObserved = true;
            _pendingHardcastLastRemainSeconds =
                Math.Max(0f, context.CastRemainSeconds);
            _pendingHardcastEndedAtMs = 0;
            return;
        }

        if (!_pendingHardcastObserved)
        {
            if (nowMs - pending.IssuedAtMs >= HardcastStartTimeoutMs)
            {
                ClearPendingIssuedActionNoLock();
            }

            return;
        }

        if (_pendingHardcastLastRemainSeconds
            > HardcastCompletionRemainThresholdSeconds)
        {
            ClearPendingIssuedActionNoLock();
            return;
        }

        if (_pendingHardcastEndedAtMs == 0)
        {
            _pendingHardcastEndedAtMs = nowMs;
            return;
        }

        if (nowMs - _pendingHardcastEndedAtMs >= HardcastCompletionAckGraceMs)
        {
            ClearPendingIssuedActionNoLock();
        }
    }

    private void ClearPendingIssuedActionNoLock()
    {
        _pendingIssuedAction = null;
        ResetPendingHardcastObservationNoLock();
    }

    private void ResetPendingHardcastObservationNoLock()
    {
        _pendingHardcastObserved = false;
        _pendingHardcastLastRemainSeconds = 0f;
        _pendingHardcastEndedAtMs = 0;
    }

    private void RecordActionSuccessNoLock(
        BlmActionEffectAck ack,
        bool isKnownGcd,
        BlmIssuedActionMetadata? issuedMetadata)
    {
        var requestedId = ack.ActionId;
        var adjustedAtIssue = NormalizeOrOriginalNoLock(ack.ActionId);
        var occurredAtMs = ack.ReceivedAtMs;
        var wasInstant = false;
        var isGcd = isKnownGcd;
        if (issuedMetadata is { } issued)
        {
            requestedId = issued.RequestedId;
            adjustedAtIssue = issued.AdjustedAtIssue;
            occurredAtMs = issued.IssuedAtMs;
            wasInstant = issued.WasInstant;
            isGcd = issued.IsGcd;
            if (issued.IsGcd && HasCompleteObservedGcdFacts(ack))
            {
                occurredAtMs = ack.ObservedGcdStartedAtMs;
                wasInstant = BlmDecisionPrimitives.WasGcdObservedInstant(
                    ack.ObservedGcdRemainMs,
                    ack.HasHasteAtAck);
            }
        }
        else if (isKnownGcd)
        {
            if (ack.ObservedGcdStartedAtMs > 0
                && ack.ObservedGcdStartedAtMs <= ack.ReceivedAtMs)
            {
                occurredAtMs = ack.ObservedGcdStartedAtMs;
            }

            if (float.IsFinite(ack.ObservedGcdRemainMs)
                && ack.ObservedGcdRemainMs > 0f)
            {
                wasInstant = BlmDecisionPrimitives.WasGcdObservedInstant(
                    ack.ObservedGcdRemainMs,
                    ack.HasHasteAtAck);
            }
        }

        if (_acknowledgedActionHistoryCount == AcknowledgedActionHistoryCapacity)
        {
            var evicted = _acknowledgedActionHistory[_acknowledgedActionHistoryStart];
            RemoveActionSuccessAliasNoLock(evicted.RequestedId, evicted.Serial);
            RemoveActionSuccessAliasNoLock(evicted.AdjustedAtIssue, evicted.Serial);
            RemoveActionSuccessAliasNoLock(evicted.ActualAckId, evicted.Serial);

            _acknowledgedActionHistoryStart =
                (_acknowledgedActionHistoryStart + 1)
                % AcknowledgedActionHistoryCapacity;
            _acknowledgedActionHistoryCount--;
        }

        var success = new BlmActionSuccess(
            ack.StateGeneration,
            ++_actionSuccessSerial,
            requestedId,
            adjustedAtIssue,
            ack.ActionId,
            ack.GlobalSequence,
            occurredAtMs,
            ack.ReceivedAtMs,
            wasInstant,
            isGcd);
        var insertAt = (_acknowledgedActionHistoryStart
                + _acknowledgedActionHistoryCount)
            % AcknowledgedActionHistoryCapacity;
        _acknowledgedActionHistory[insertAt] = success;
        _acknowledgedActionHistoryCount++;
        IndexActionSuccessAliasNoLock(success.RequestedId, success);
        IndexActionSuccessAliasNoLock(success.AdjustedAtIssue, success);
        IndexActionSuccessAliasNoLock(success.ActualAckId, success);
    }

    private static bool HasCompleteObservedGcdFacts(BlmActionEffectAck ack)
        => ack.ObservedGcdStartedAtMs > 0
            && ack.ObservedGcdStartedAtMs <= ack.ReceivedAtMs
            && float.IsFinite(ack.ObservedGcdRemainMs)
            && ack.ObservedGcdRemainMs > 0f;

    private bool TryGetLastActionSuccessNoLock(
        uint actionId,
        out BlmActionSuccess success)
    {
        if (TryGetActionSuccessAliasNoLock(actionId, out success))
        {
            return true;
        }

        var normalizedActionId = NormalizeOrOriginalNoLock(actionId);
        if (normalizedActionId != actionId
            && TryGetActionSuccessAliasNoLock(normalizedActionId, out success))
        {
            return true;
        }

        success = default;
        return false;
    }

    private uint NormalizeOrOriginalNoLock(uint actionId)
    {
        var normalizedActionId = _normalizer.Normalize(actionId);
        return normalizedActionId == 0 ? actionId : normalizedActionId;
    }

    private bool TryGetActionSuccessAliasNoLock(
        uint alias,
        out BlmActionSuccess success)
        => _lastActionSuccessByAlias.TryGetValue(alias, out success)
            && success.StateGeneration == _stateGeneration;

    private void IndexActionSuccessAliasNoLock(
        uint alias,
        BlmActionSuccess success)
    {
        if (alias != 0)
        {
            _lastActionSuccessByAlias[alias] = success;
        }
    }

    private void RemoveActionSuccessAliasNoLock(uint alias, long serial)
    {
        if (alias != 0
            && _lastActionSuccessByAlias.TryGetValue(alias, out var latest)
            && latest.Serial == serial)
        {
            _lastActionSuccessByAlias.Remove(alias);
        }
    }

    private static bool IsSequenceNewer(uint value, uint baseline)
        => unchecked((int)(value - baseline)) > 0;

    private void InitializePhaseSerialsNoLock(BlmPhase phase)
    {
        _firePhaseSerial = phase == BlmPhase.Fire ? 1 : 0;
        _icePhaseSerial = phase == BlmPhase.Ice ? 1 : 0;
        _paradoxUsedFireSerial = 0;
        _paradoxUsedIceSerial = 0;
    }

    private void UpdateGcdStartedAtNoLock(BlmContext context)
    {
        if (BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(
                context.CapturedAtMs,
                context.GcdTotalSeconds,
                context.GcdRemainSeconds,
                out var gcdStartedAtMs))
        {
            _lastGcdStartedAtMs = gcdStartedAtMs;
        }
    }

    private BlmTrackerSnapshot BuildSnapshotNoLock() => new()
    {
        CombatSerial = _combatSerial,
        StateGeneration = _stateGeneration,
        IsCombatActive = _combatActive,
        HistoryReliable = _historyReliable,
        FirePhaseSerial = _firePhaseSerial,
        IcePhaseSerial = _icePhaseSerial,
        ParadoxUsedFireSerial = _paradoxUsedFireSerial,
        ParadoxUsedIceSerial = _paradoxUsedIceSerial,
        Fire4Count = _fire4Count,
        Fire4CountSinceManafont = _fire4CountSinceManafont,
        ManafontActiveThisFire = _manafontActiveThisFire,
        ManafontUseSerial = _manafontUseSerial,
        LastObservedPhase = _lastObservedPhase,
        LastGcdId = _lastGcdId,
        LastGcdAtMs = _lastGcdAtMs,
        LastGcdStartedAtMs = _lastGcdStartedAtMs,
        LastOgcdId = _lastOgcdId,
        LastOgcdAtMs = _lastOgcdAtMs,
        LastAckActionId = _lastAckActionId,
        LastAckGlobalSequence = _lastAckGlobalSequence,
        LastAckAtMs = _lastAckAtMs,
        LastAckPhaseBefore = _lastAckPhaseBefore,
        LastAckGeneration = _lastAckGeneration,
        AcknowledgedActionHistoryCount = _acknowledgedActionHistoryCount,
        ZeroSequenceDedupeCount = _lastZeroSequenceByAction.Count,
        HasPendingIssuedAction = _pendingIssuedAction is not null,
        PendingIssuedActionId = _pendingIssuedAction?.AdjustedAtIssue ?? 0,
        PendingIssuedActionDeadlineAtMs = _pendingIssuedAction?.DeadlineAtMs ?? 0,
        PendingGaugeReconcile = _lastAckAwaitingGauge is not null
            || _pendingManafont is not null,
        LastGaugeReconciledActionId = _lastGaugeReconciledActionId,
        LastGaugeReconciledAtMs = _lastGaugeReconciledAtMs,
        LastResetReason = _lastResetReason,
    };

    private void RefreshLatestContextNoLock(BlmContext context)
        => _latestContext = context with { Tracker = BuildSnapshotNoLock() };

    private static bool IsTrackedOgcd(uint actionId) => actionId is
        BLMSkill.黑魔纹 or BLMSkill.魔泉 or BLMSkill.三连咏唱 or BLMSkill.详述
        or BLMSkill.魔纹步 or BLMSkill.魔纹重置 or BLMSkill.以太步 or BLMSkill.魔罩
        or BLMSkill.星灵移位
        or MageUniversalSkill.即刻咏唱 or MageUniversalSkill.醒梦
        or MageUniversalSkill.沉稳咏唱 or MageUniversalSkill.昏乱;

    private readonly record struct SequenceEventKey(
        long StateGeneration,
        uint SourceId,
        uint GlobalSequence);

    private readonly record struct ZeroSequenceEvent(
        uint ActionId,
        long ReceivedAtMs,
        long Serial);

    private readonly record struct PendingManafontSync(
        long CombatSerial,
        long StateGeneration,
        uint ActionId,
        uint GlobalSequence,
        long AcknowledgedAtMs,
        long DeadlineMs,
        BlmPhase PhaseBefore,
        long PhaseSerialBefore);
}
