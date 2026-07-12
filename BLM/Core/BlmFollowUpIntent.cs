namespace LosPr.BLM.Core;

public enum BlmFollowUpStage
{
    None,
    AwaitingTriggerAck,
    TriggerAcknowledged,
    Active,
    RequiredQueued,
    RequiredAcknowledged,
    Completed,
    Cancelled,
}

public sealed record BlmFollowUpIntent
{
    public static BlmFollowUpIntent Empty { get; } = new();

    public BlmFollowUpKind Kind { get; init; }
    public BlmFollowUpStage Stage { get; init; }
    public long StateGeneration { get; init; }
    public long CombatSerial { get; init; }
    public long FirePhaseSerial { get; init; }
    public long Serial { get; init; }
    public uint TargetEntityId { get; init; }
    public uint TriggerActionId { get; init; }
    public uint RequiredActionId { get; init; }
    public uint RequestedAfterGlobalSequence { get; init; }
    public uint QueuedAfterGlobalSequence { get; init; }
    public long RequestedAtMs { get; init; }
    public long QueuedAtMs { get; init; }
    public uint TriggerAckGlobalSequence { get; init; }
    public long TriggerAckAtMs { get; init; }
    public uint RequiredAckGlobalSequence { get; init; }
    public long RequiredAckAtMs { get; init; }
    public long StageDeadlineAtMs { get; init; }
    public long TotalExpireAtMs { get; init; }
    public string Reason { get; init; } = string.Empty;

    public bool IsPending => Stage is BlmFollowUpStage.AwaitingTriggerAck
        or BlmFollowUpStage.TriggerAcknowledged
        or BlmFollowUpStage.Active
        or BlmFollowUpStage.RequiredQueued
        or BlmFollowUpStage.RequiredAcknowledged;

    public bool IsTerminal => Stage is BlmFollowUpStage.Completed
        or BlmFollowUpStage.Cancelled;
}

public readonly record struct BlmFollowUpAckToken(
    long StateGeneration,
    long CombatSerial,
    long Serial,
    BlmFollowUpStage StageAtCapture,
    uint ExpectedActionId,
    uint SequenceBaseline,
    long RegisteredAtMs,
    long DeadlineAtMs)
{
    public bool IsValid => StateGeneration > 0
        && CombatSerial > 0
        && Serial > 0
        && (StageAtCapture is BlmFollowUpStage.AwaitingTriggerAck
            or BlmFollowUpStage.Active
            or BlmFollowUpStage.RequiredQueued)
        && ExpectedActionId != 0
        && RegisteredAtMs >= 0
        && DeadlineAtMs >= RegisteredAtMs;
}
