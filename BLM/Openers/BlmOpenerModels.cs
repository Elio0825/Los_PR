using System.Collections.Immutable;
using LosPr.BLM.Resolvers;

namespace LosPr.BLM.Openers;

internal enum BlmOpenerMode
{
    None,
    HighEndCountdown,
    DailyInCombat,
}

internal enum BlmOpenerVariant
{
    None,
    Level70,
    Level80,
    Level90,
    Standard57,
    Flare,
}

internal enum BlmOpenerStatus
{
    Idle,
    Armed,
    Executing,
    Draining,
    Completed,
    Cancelled,
}

internal enum BlmOpenerStepKind
{
    Gcd,
    OffGcd,
    Item,
}

internal enum BlmOpenerCheckpoint
{
    None,
    FireEntry,
    Swiftcast,
    LeyLines,
    Manafont,
    Triplecast,
    Transpose,
    IceEntry,
}

internal readonly record struct BlmOpenerStep(
    string Id,
    uint ActionId,
    BlmOpenerStepKind Kind,
    bool TargetsEnemy,
    BlmOpenerCheckpoint Checkpoint = BlmOpenerCheckpoint.None)
{
    public BlmResolverChannel Channel => Kind == BlmOpenerStepKind.Gcd
        ? BlmResolverChannel.Gcd
        : BlmResolverChannel.OffGcd;
}

internal readonly record struct BlmOpenerPolicy(
    bool Enabled,
    bool HighEndPotionEnabled,
    bool Level70To89Enabled = false,
    bool Level100FlareEnabled = false,
    bool Level90To99Enabled = false,
    bool NoTriplecast = false,
    bool DailyInCombatEnabled = false,
    bool HighEndCountdownEnabled = true);

internal sealed record BlmOpenerPlan
{
    public BlmOpenerVariant Variant { get; init; }
    public BlmOpenerMode Mode { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public int MinimumLevel { get; init; }
    public int MaximumLevel { get; init; }
    public int ExpectedFire4BeforeManafont { get; init; }
    public int ExpectedFire4AfterManafont { get; init; }
    public bool NoTriplecast { get; init; }
    public bool DotEnabled { get; init; }
    public uint PotionId { get; init; }
    public ImmutableArray<BlmOpenerStep> Steps { get; init; } = [];
}

internal sealed record BlmOpenerSnapshot
{
    public BlmOpenerVariant Variant { get; init; }
    public BlmOpenerMode Mode { get; init; }
    public BlmOpenerStatus Status { get; init; }
    public int StepIndex { get; init; }
    public int StepCount { get; init; }
    public string StepId { get; init; } = string.Empty;
    public uint ExpectedActionId { get; init; }
    public bool HasPendingAction { get; init; }
    public bool OwnsExecution { get; init; }
    public int Fire4BeforeManafont { get; init; }
    public int Fire4AfterManafont { get; init; }
    public string LastReason { get; init; } = "尚未启动";
}
