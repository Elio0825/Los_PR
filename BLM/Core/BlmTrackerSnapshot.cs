using System.Collections.Immutable;

namespace LosPr.BLM.Core;

internal sealed record BlmTrackerDecisionSnapshot
{
    public static BlmTrackerDecisionSnapshot Empty { get; } = new();

    public BlmTrackerSnapshot Snapshot { get; init; } = BlmTrackerSnapshot.Empty;
    public ImmutableArray<BlmActionSuccess> RecentHistory { get; init; } = [];
    public BlmActionSuccess? PreviousGcd { get; init; }
}

internal sealed record BlmTrackerSnapshot
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
    public bool ManafontActiveThisFire { get; init; }
    public long ManafontUseSerial { get; init; }
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
    public bool HasPendingIssuedAction { get; init; }
    public uint PendingIssuedActionId { get; init; }
    public long PendingIssuedActionDeadlineAtMs { get; init; }
    public bool PendingGaugeReconcile { get; init; }
    public uint LastGaugeReconciledActionId { get; init; }
    public long LastGaugeReconciledAtMs { get; init; }
    public string LastResetReason { get; init; } = "初始化";
    public bool ParadoxUsedThisFire => FirePhaseSerial > 0
        && ParadoxUsedFireSerial == FirePhaseSerial;

    public bool ParadoxUsedThisIce => IcePhaseSerial > 0
        && ParadoxUsedIceSerial == IcePhaseSerial;
}

internal readonly record struct BlmActionEffectAck(
    long CombatSerial,
    long StateGeneration,
    uint SourceId,
    uint ActionId,
    uint GlobalSequence,
    long ReceivedAtMs,
    BlmPhase PhaseBefore,
    long PhaseSerialBefore,
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
        long phaseSerialBefore)
        : this(
            combatSerial,
            stateGeneration,
            sourceId,
            actionId,
            globalSequence,
            receivedAtMs,
            phaseBefore,
            phaseSerialBefore,
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
        out long phaseSerialBefore)
    {
        combatSerial = CombatSerial;
        stateGeneration = StateGeneration;
        sourceId = SourceId;
        actionId = ActionId;
        globalSequence = GlobalSequence;
        receivedAtMs = ReceivedAtMs;
        phaseBefore = PhaseBefore;
        phaseSerialBefore = PhaseSerialBefore;
    }
}
