namespace LosPr.BLM.Core;

public enum BlmDecisionTarget
{
    None,
    CurrentTarget,
}

public enum BlmDecisionLayer
{
    None,
    Transition,
    Recovery,
    Override,
    Strategy,
}

public enum BlmNoActionReason
{
    None,
    SnapshotUnavailable,
    Disabled,
    UnsupportedLevelOrMode,
    NoTarget,
    OutOfRange,
    CannotAct,
    HighPriorityQueue,
    MovementNoSafeGcd,
    WaitingForTranspose,
    WaitingForGaugeReconcile,
    WaitingForIceMpGain,
}

public enum BlmFollowUpKind
{
    None,
    FlareStarAfterMovementDespair,
}

public sealed record BlmTransitionRequest(
    TransitionKind Kind,
    TransitionStep Step,
    TransitionDeliveryChannel DeliveryChannel,
    uint ExpectedActionId,
    TransitionExpectation Expectation,
    RotationMode ModeAtRequest,
    IceToFireRoute IceToFireRoute,
    long StepExpireMs,
    long TotalExpireMs);

// Step 3 assigns a serial and activates this proposal only after the trigger Ack.
public sealed record BlmFollowUpRequest(
    BlmFollowUpKind Kind,
    long StateGeneration,
    long CombatSerial,
    long FirePhaseSerial,
    uint TargetEntityId,
    uint TriggerActionId,
    uint RequiredActionId,
    uint RequestedAfterGlobalSequence,
    long ExpireAfterMs,
    string Reason);

public sealed record BlmDecisionPolicy
{
    public static BlmDecisionPolicy Default { get; } = new();

    public bool Enabled { get; init; } = true;
    public bool HighPriorityQueueActive { get; init; }
    public bool AllowEmergencyFirestarter { get; init; }
    public bool AllowMovementDespairReorder { get; init; } = true;
    public float InstantCastSafetySeconds { get; init; } = 0.25f;
    public float ThunderRefreshThresholdMs { get; init; } = 3000f;
    public int PolyglotOvercapThresholdMs { get; init; } = 7000;
}

public sealed record BlmDecisionInput
{
    public BlmContext Context { get; init; } = BlmContext.Unavailable;
    public BlmDecisionPolicy Policy { get; init; } = BlmDecisionPolicy.Default;
}

public sealed record BlmDecision
{
    private BlmDecision()
    {
    }

    public uint ActionId { get; init; }
    public BlmDecisionTarget Target { get; init; }
    public uint TargetEntityId { get; init; }
    public string RuleId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public long StateGeneration { get; init; }
    public BlmDecisionLayer Layer { get; init; }
    public BlmNoActionReason NoActionReason { get; init; }
    public BlmTransitionRequest? TransitionRequest { get; init; }
    public BlmFollowUpRequest? FollowUpRequest { get; init; }

    public bool HasAction => ActionId != 0;
    public bool IsNoAction => !HasAction;

    public static BlmDecision Gcd(
        BlmContext context,
        uint actionId,
        string ruleId,
        string reason,
        BlmDecisionLayer layer,
        BlmTransitionRequest? transitionRequest = null,
        BlmFollowUpRequest? followUpRequest = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        if (actionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actionId));
        }

        if (transitionRequest is not null && followUpRequest is not null)
        {
            throw new ArgumentException(
                "同一个 GCD Decision 不能同时创建 Transition 与 Follow-up。");
        }

        if (transitionRequest is not null
            && transitionRequest.ExpectedActionId != actionId)
        {
            throw new ArgumentException(
                "Transition 的首个 ExpectedActionId 必须等于本次真实 GCD。",
                nameof(transitionRequest));
        }

        if (followUpRequest is not null
            && followUpRequest.TriggerActionId != actionId)
        {
            throw new ArgumentException(
                "Follow-up 的触发动作必须等于本次真实 GCD。",
                nameof(followUpRequest));
        }

        return new BlmDecision
        {
            ActionId = actionId,
            Target = BlmDecisionTarget.CurrentTarget,
            TargetEntityId = context.TargetEntityId,
            RuleId = ruleId,
            Reason = reason,
            StateGeneration = context.Tracker.StateGeneration,
            Layer = layer,
            TransitionRequest = transitionRequest,
            FollowUpRequest = followUpRequest,
        };
    }

    public static BlmDecision NoAction(
        BlmContext context,
        BlmNoActionReason noActionReason,
        string ruleId,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        if (noActionReason == BlmNoActionReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(noActionReason));
        }

        return new BlmDecision
        {
            RuleId = ruleId,
            Reason = reason,
            StateGeneration = context.Tracker.StateGeneration,
            NoActionReason = noActionReason,
        };
    }
}

public static class BlmRuleId
{
    public const string SnapshotUnavailable = "NA.SNAPSHOT_UNAVAILABLE";
    public const string Disabled = "NA.DISABLED";
    public const string UnsupportedScope = "NA.UNSUPPORTED_SCOPE";
    public const string NoTarget = "NA.NO_TARGET";
    public const string OutOfRange = "NA.OUT_OF_RANGE";
    public const string CannotAct = "NA.CANNOT_ACT";
    public const string HighPriorityQueue = "NA.HIGH_PRIORITY_QUEUE";
    public const string MovementNoSafeGcd = "NA.MOVEMENT_NO_SAFE_GCD";
    public const string WaitingForTranspose = "ST.ICE.WAIT_TRANSPOSE";
    public const string WaitingForGaugeReconcile = "ST.ICE.WAIT_GAUGE";
    public const string WaitingForIceMpGain = "ST.ICE.WAIT_MP_GAIN";
    public const string NeutralBlizzard3 = "ST.RECOVERY.NEUTRAL_B3";
    public const string UiLowBlizzard3 = "ST.RECOVERY.UI_LT3_B3";
    public const string Af1FirestarterFire3 = "ST.RECOVERY.AF1_FIRESTARTER_F3";
    public const string Af1Paradox = "ST.RECOVERY.AF1_PARADOX";
    public const string AfLowHardFire3 = "ST.RECOVERY.AF_LT3_HARD_F3";
    public const string FireResourceRecovery = "ST.RECOVERY.FIRE_RESOURCE_B3";
    public const string MoveDespairReorder = "ST.MOVE.DESPAIR_THEN_FLARE_STAR";
    public const string MoveEmergencyFirestarter = "ST.MOVE.EMERGENCY_FIRESTARTER";
    public const string FlareStar = "ST.FIRE.FLARE_STAR";
    public const string PolyglotOvercap = "ST.OVERRIDE.XENO_OVERCAP";
    public const string PolyglotMoveFill = "ST.MOVE.XENO_FILL";
    public const string ThunderRefresh = "ST.OVERRIDE.THUNDER_REFRESH";
    public const string IceBlizzard4 = "ST.ICE.B4_HEARTS";
    public const string IceParadox = "ST.ICE.PARADOX";
    public const string IceMpRecovery = "ST.ICE.MP_RECOVERY_B4";
    public const string IceFiller = "ST.ICE.FILLER";
    public const string IceHardFire3 = "ST.ICE.HARD_F3_ENTRY";
    public const string FireParadox = "ST.FIRE.AF_PARADOX";
    public const string Fire4 = "ST.FIRE.F4";
    public const string Despair = "ST.FIRE.DESPAIR";
    public const string FireBlizzard3 = "ST.FIRE.B3_EXIT";
}
