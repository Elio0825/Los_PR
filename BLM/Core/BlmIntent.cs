namespace LosPr.BLM.Core;

public enum RotationMode
{
    None,
    SingleTarget,
    TwoTargetAoe,
    ThreePlusAoe,
}

public enum TransitionKind
{
    None,
    IceToFire,
    FireToIce,
    ManafontExtension,
}

public enum TransitionStep
{
    None,
    CommitIceGcd,
    UseTranspose,
    UseAfParadox,
    UseFirestarterF3,
    CommitFireFinisher,
    UseInstantBuff,
    UseIceParadoxWait,
    UseTransposeDespair,
    UseBlizzard3,
    UseManafont,
}

public enum TransitionDeliveryChannel
{
    None,
    Gcd,
    OffGcd,
    OffGcdOrAlways,
}

public enum BlmQueueChannel
{
    None,
    Gcd,
    OffGcd,
    Always,
}

public enum IceToFireRoute
{
    None,
    ExistingFirestarter,
    Af1ParadoxRecovery,
    B4TransposeDespair,
    AoeFireEntry,
}

public enum TransitionStage
{
    None,
    Requested,
    Queued,
    Confirmed,
    Completed,
    Cancelled,
}

public enum TransitionExpectation
{
    AckOnly,
    RemainInIce,
    RemainInFire,
    AstralFireOne,
    AstralFireOneWithParadox,
    AstralFireThree,
    FireFinisherReady,
    UmbralIceOne,
    UmbralIceThree,
    FirestarterPresent,
    IceReadyWithFirestarter,
    IceReadyForAf1Paradox,
    IceReadyForTransposeDespair,
    AoeIceResourcesReady,
    SwiftcastPresent,
    TriplecastPresent,
    ManafontResourcesRestored,
}

public sealed record BlmIntent
{
    public static BlmIntent Empty { get; } = new();

    public long StateGeneration { get; init; }
    public TransitionKind Kind { get; init; } = TransitionKind.None;
    public TransitionStep Step { get; init; } = TransitionStep.None;
    public TransitionStage Stage { get; init; } = TransitionStage.None;
    public TransitionDeliveryChannel DeliveryChannel { get; init; } = TransitionDeliveryChannel.None;
    public RotationMode ModeAtRequest { get; init; } = RotationMode.None;
    public IceToFireRoute IceToFireRoute { get; init; } = IceToFireRoute.None;
    public uint TargetEntityIdAtRequest { get; init; }
    public long Serial { get; init; }
    public int StepIndex { get; init; }
    public uint ExpectedActionId { get; init; }
    public uint ExpectedAdjustedActionId { get; init; }
    public uint QueuedAfterGlobalSequence { get; init; }
    public long QueuedAtMs { get; init; }
    public long QueuedDeadlineAtMs { get; init; }
    public TransitionExpectation Expectation { get; init; } = TransitionExpectation.AckOnly;
    public uint AcknowledgedActionId { get; init; }
    public uint AcknowledgedGlobalSequence { get; init; }
    public long AcknowledgedAtMs { get; init; }
    public long SetAtMs { get; init; }
    public long ExpireAtMs { get; init; }
    public long TransitionExpireAtMs { get; init; }
    public string Reason { get; init; } = string.Empty;

    public bool IsActive => Kind != TransitionKind.None
        && Stage is TransitionStage.Requested
            or TransitionStage.Queued
            or TransitionStage.Confirmed;

    public bool IsTerminal => Stage is TransitionStage.Completed or TransitionStage.Cancelled;

    public bool HasExpectedAck => ExpectedActionId != 0 && AcknowledgedActionId != 0;

    public BlmTransitionAckToken AckToken => Stage == TransitionStage.Queued
        ? new BlmTransitionAckToken(
            StateGeneration,
            Serial,
            StepIndex,
            ExpectedActionId,
            ExpectedAdjustedActionId,
            QueuedAfterGlobalSequence,
            QueuedAtMs,
            QueuedDeadlineAtMs)
        : default;
}

public readonly record struct BlmTransitionAckToken(
    long StateGeneration,
    long Serial,
    int StepIndex,
    uint ExpectedActionId,
    uint ExpectedAdjustedActionId,
    uint QueuedAfterGlobalSequence,
    long QueuedAtMs,
    long DeadlineMs)
{
    public bool IsValid => StateGeneration > 0
        && Serial > 0
        && StepIndex >= 0
        && ExpectedActionId != 0
        && QueuedAtMs > 0
        && DeadlineMs >= QueuedAtMs;
}
