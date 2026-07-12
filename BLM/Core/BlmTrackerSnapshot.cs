namespace LosPr.BLM.Core;

public sealed record BlmTrackerSnapshot
{
    public static BlmTrackerSnapshot Empty { get; } = new();

    public long CombatSerial { get; init; }
    public long StateGeneration { get; init; }
    public bool IsCombatActive { get; init; }
    public bool HistoryReliable { get; init; }
    public long FirePhaseSerial { get; init; }
    public long IcePhaseSerial { get; init; }
    public long ParadoxUsedFireSerial { get; init; }
    public long ParadoxUsedIceSerial { get; init; }
    public int Fire4Count { get; init; }
    public int Fire4CountSinceManafont { get; init; }
    public bool FirestarterDebt { get; init; }
    public bool ManafontActiveThisFire { get; init; }
    public long ManafontUseSerial { get; init; }
    public string CurrentPlanId { get; init; } = string.Empty;
    public BlmPhase LastObservedPhase { get; init; }
    public uint LastGcdId { get; init; }
    public long LastGcdAtMs { get; init; }
    public long LastGcdStartedAtMs { get; init; }
    public uint LastOgcdId { get; init; }
    public long LastOgcdAtMs { get; init; }
    public uint LastAckActionId { get; init; }
    public uint LastAckGlobalSequence { get; init; }
    public long LastAckAtMs { get; init; }
    public BlmPhase LastAckPhaseBefore { get; init; }
    public long LastAckGeneration { get; init; }
    public int AcknowledgedActionHistoryCount { get; init; }
    public int ZeroSequenceDedupeCount { get; init; }
    public bool PendingGaugeReconcile { get; init; }
    public uint LastGaugeReconciledActionId { get; init; }
    public long LastGaugeReconciledAtMs { get; init; }
    public string LastResetReason { get; init; } = "初始化";
    public BlmIntent Transition { get; init; } = BlmIntent.Empty;
    public BlmFollowUpIntent FollowUp { get; init; } = BlmFollowUpIntent.Empty;

    public bool ParadoxUsedThisFire => FirePhaseSerial > 0
        && ParadoxUsedFireSerial == FirePhaseSerial;

    public bool ParadoxUsedThisIce => IcePhaseSerial > 0
        && ParadoxUsedIceSerial == IcePhaseSerial;
}

public readonly record struct BlmActionEffectAck(
    long CombatSerial,
    long StateGeneration,
    uint SourceId,
    uint ActionId,
    uint GlobalSequence,
    long ReceivedAtMs,
    BlmPhase PhaseBefore,
    long PhaseSerialBefore,
    BlmTransitionAckToken TransitionToken,
    BlmFollowUpAckToken FollowUpToken,
    long ObservedGcdStartedAtMs,
    float ObservedGcdRemainMs,
    bool HasHasteAtAck)
{
    public BlmActionEffectAck(
        long combatSerial,
        long stateGeneration,
        uint sourceId,
        uint actionId,
        uint globalSequence,
        long receivedAtMs,
        BlmPhase phaseBefore,
        long phaseSerialBefore,
        BlmTransitionAckToken transitionToken,
        BlmFollowUpAckToken followUpToken)
        : this(
            combatSerial,
            stateGeneration,
            sourceId,
            actionId,
            globalSequence,
            receivedAtMs,
            phaseBefore,
            phaseSerialBefore,
            transitionToken,
            followUpToken,
            0,
            0f,
            false)
    {
    }

    public void Deconstruct(
        out long combatSerial,
        out long stateGeneration,
        out uint sourceId,
        out uint actionId,
        out uint globalSequence,
        out long receivedAtMs,
        out BlmPhase phaseBefore,
        out long phaseSerialBefore,
        out BlmTransitionAckToken transitionToken,
        out BlmFollowUpAckToken followUpToken)
    {
        combatSerial = CombatSerial;
        stateGeneration = StateGeneration;
        sourceId = SourceId;
        actionId = ActionId;
        globalSequence = GlobalSequence;
        receivedAtMs = ReceivedAtMs;
        phaseBefore = PhaseBefore;
        phaseSerialBefore = PhaseSerialBefore;
        transitionToken = TransitionToken;
        followUpToken = FollowUpToken;
    }
}
