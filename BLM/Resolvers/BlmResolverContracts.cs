using System.Collections.Immutable;

namespace LosPr.BLM.Resolvers;

internal enum BlmResolverChannel
{
    Gcd,
    Always,
    OffGcd,
}

internal enum BlmResolverTargetKind
{
    CurrentTarget,
    Self,
    SpecifiedTarget,
    Potion,
}

internal enum BlmResolverManifestDisposition
{
    Active,
    RejectSingleTarget,
    Inactive,
}

internal readonly record struct BlmResolverManifestEntry(
    int Order,
    BlmResolverChannel Channel,
    string ResolverId,
    BlmResolverManifestDisposition Disposition);

internal readonly record struct BlmResolverSettingDefinition(
    string PropertyName,
    string LosAeSource,
    string DefaultValue);

internal sealed record BlmResolverSettings
{
    public static BlmResolverSettings Default { get; } = new();

    public static ImmutableArray<BlmResolverSettingDefinition> Mapping { get; } =
    [
        new(nameof(ManafontEnabled), "QT:魔泉", "true"),
        new(nameof(DumpPolyglotEnabled), "QT:倾泻资源", "false"),
        new(nameof(FastFlareStarEnabled), "QT:快速耀星", "false"),
        new(nameof(TtkEnabled), "QT:TTK", "false"),
        new(nameof(DotEnabled), "QT:Dot", "true"),
        new(nameof(DoubleDotEnabled), "QT:双DOT", "false"),
        new(nameof(MoveXenoglossyEnabled), "QT:移动通晓", "true"),
        new(nameof(SwiftcastIntoIceEnabled), "QT:即刻进冰", "true"),
        new(nameof(TriplecastIntoIceEnabled), "QT:三连进冰", "true"),
        new(nameof(MoveTriplecastEnabled), "QT:三连走位", "true"),
        new(nameof(AmplifierEnabled), "QT:详述", "true"),
        new(nameof(LeyLinesEnabled), "QT:黑魔纹", "true"),
        new(nameof(AutoMitigationEnabled), "QT:自动减伤", "true"),
        new(nameof(PotionEnabled), "QT:爆发药", "false"),
        new(nameof(CompressFireParadox), "BlackMageSetting:压缩火悖论", "true"),
        new(nameof(ReducedAnimationLockEnabled), "BlackMageSetting:动画锁模式 == 1", "true"),
        new(nameof(DotHpThresholdPercent), "BlackMageSetting:不上Dot阈值", "3"),
    ];

    public bool ManafontEnabled { get; init; } = true;
    public bool DumpPolyglotEnabled { get; init; }
    public bool FastFlareStarEnabled { get; init; }
    public bool TtkEnabled { get; init; }
    public bool DotEnabled { get; init; } = true;
    public bool DoubleDotEnabled { get; init; }
    public bool MoveXenoglossyEnabled { get; init; } = true;
    public bool SwiftcastIntoIceEnabled { get; init; } = true;
    public bool TriplecastIntoIceEnabled { get; init; } = true;
    public bool MoveTriplecastEnabled { get; init; } = true;
    public bool AmplifierEnabled { get; init; } = true;
    public bool LeyLinesEnabled { get; init; } = true;
    public bool AutoMitigationEnabled { get; init; } = true;
    public bool PotionEnabled { get; init; }
    public bool CompressFireParadox { get; init; } = true;
    public bool ReducedAnimationLockEnabled { get; init; } = true;
    public int DotHpThresholdPercent { get; init; } = 3;
}

internal sealed record BlmResolverContextFacts
{
    public long CapturedAtMs { get; init; }
    public bool IsAvailable { get; init; }
    public bool AcrEnabled { get; init; }
    public uint PlayerEntityId { get; init; }
    public int Level { get; init; }
    public long Mp { get; init; }
    public long MaxMp { get; init; }
    public bool InCombat { get; init; }
    public bool IsAlive { get; init; }
    public bool CanAct { get; init; }
    public bool IsMoving { get; init; }
    public bool IsCasting { get; init; }
    public bool IsSingleTargetMode { get; init; }
    public int EnemyCount { get; init; }
    public uint AoeTargetId { get; init; }
    public bool AoeTargetCanUseAttack { get; init; }
    public int AoeTargetHitCount { get; init; }
    public bool AoeTargetIsCurrentTarget { get; init; }
    public bool HasTarget { get; init; }
    public bool CanUseAttackActionOnTarget { get; init; }
    public uint CurrentTargetId { get; init; }
    public int ActionQueueWindowMs { get; init; } =
        BlmDecisionPrimitives.DefaultActionQueueWindowMs;
    public float GcdTotalSeconds { get; init; }
    public float GcdRemainSeconds { get; init; }
    public float AnimationLockSeconds { get; init; }
    public BlmPhase Phase { get; init; }
    public int AstralFireStacks { get; init; }
    public int UmbralIceStacks { get; init; }
    public int UmbralHearts { get; init; }
    public int AstralSoulStacks { get; init; }
    public bool HasParadox { get; init; }
    public bool HasFirestarter { get; init; }
    public bool HasThunderhead { get; init; }
    public int PolyglotStacks { get; init; }
    public int MaxPolyglotStacks { get; init; }
    public int PolyglotTimerMs { get; init; }
    public bool HasSwiftcast { get; init; }
    public int TriplecastStacks { get; init; }
    public bool HasLeyLinesStatus737 { get; init; }
    public bool HasLeyLinesHaste738 { get; init; }

    public bool InFire => Phase == BlmPhase.Fire;
    public bool InIce => Phase == BlmPhase.Ice;
    public bool IsAoeMode => !IsSingleTargetMode;
    public bool IsTwoTargetAoe => IsAoeMode && EnemyCount == 2;
    public bool IsThreePlusAoe => IsAoeMode && EnemyCount >= 3;
    public int RequiredAoeHitCount => IsTwoTargetAoe ? 2 : IsThreePlusAoe ? 3 : 0;
    public bool HasInstantCast =>
        BlmDecisionPrimitives.HasInstantCast(HasSwiftcast, TriplecastStacks);
    public double GcdRemainMs => Math.Max(0d, GcdRemainSeconds * 1000d);
}

internal sealed record BlmResolverActionFact
{
    public uint RequestedActionId { get; init; }
    public uint AdjustedActionId { get; init; }
    public bool IsUnlocked { get; init; }
    public bool CanCast { get; init; }
    public float Charges { get; init; }
    public int MaxCharges { get; init; } = 1;
    public double CooldownRemainMs { get; init; }
    public double RecastTotalMs { get; init; }

    public uint EffectiveActionId => AdjustedActionId == 0
        ? RequestedActionId
        : AdjustedActionId;
}

internal sealed record BlmResolverDotTargetFact
{
    public uint EntityId { get; init; }
    public bool IsValid { get; init; }
    public bool IsTargetable { get; init; }
    public bool IsAlive { get; init; }
    public bool CanUseAttackActionOn { get; init; }
    public bool IsInDotRange { get; init; }
    public bool IsBlacklisted { get; init; }
    public bool CanCastDot { get; init; }
    public long CurrentHp { get; init; }
    public long MaxHp { get; init; }
    public float Distance { get; init; }
    public float SingleTargetDotRemainingMs { get; init; }
    public float AoeDotRemainingMs { get; init; }

    public float HpRatio => MaxHp <= 0
        ? 0f
        : Math.Clamp((float)CurrentHp / MaxHp, 0f, 1f);
    public bool HasOwnedDot => SingleTargetDotRemainingMs > 0f
        || AoeDotRemainingMs > 0f;
    public float MaxOwnedDotRemainingMs => Math.Max(
        SingleTargetDotRemainingMs,
        AoeDotRemainingMs);
}

internal readonly record struct BlmDoubleDotMemory(
    uint LastTargetId,
    long LastCastAtMs);

internal sealed record BlmLevel100LoopFacts
{
    public int Fire4Count { get; init; }
    public bool FireParadoxUsed { get; init; }
    public bool RecoveringAfterSpecial { get; init; }
}

internal sealed record BlmCasualCombatFacts
{
    public bool IsCasualDutyNonBoss { get; init; }
    public float NearbyEnemiesTotalHpRatio { get; init; }
    public float NearbyEnemiesAverageTtkMs { get; init; }
}

internal sealed record BlmDefensiveCastFacts
{
    public bool TargetCastIsDeathSentenceWithin3Seconds { get; init; }
    public bool TargetCastIsBossAoeWithin3Seconds { get; init; }
}

internal sealed record BlmResolverInput
{
    public long StateGeneration { get; init; }
    public BlmResolverContextFacts Context { get; init; } = new();
    public BlmResolverSettings Settings { get; init; } = BlmResolverSettings.Default;
    public ImmutableArray<BlmResolverActionFact> Actions { get; init; } = [];
    public ImmutableArray<BlmActionSuccess> RecentHistory { get; init; } = [];
    public BlmActionSuccess? PreviousGcd { get; init; }
    public int UsedWeaves { get; init; }
    public BlmLevel100LoopFacts Level100Loop { get; init; } = new();
    public uint CurrentCastingActionId { get; init; }
    public bool SpecialSequenceActive { get; init; }
    public bool HighPriorityQueueActive { get; init; }
    public bool PendingGaugeReconcile { get; init; }
    public bool NeedsForcedIceRecovery { get; init; }
    public bool IsIdle { get; init; }
    public ImmutableArray<BlmResolverDotTargetFact> DotTargets { get; init; } = [];
    public BlmDoubleDotMemory DoubleDotMemory { get; init; }
    public bool CanSpecifyDotTarget { get; init; } = true;
    public BlmCasualCombatFacts CasualCombat { get; init; } = new();
    public BlmDefensiveCastFacts DefensiveCast { get; init; } = new();
    public bool IsPotionAvailable { get; init; }
    public BlmResolverFactCoverage FactCoverage { get; init; } =
        BlmResolverFactCoverage.Phase3A;
}

internal sealed record BlmResolverFactCoverage
{
    public static BlmResolverFactCoverage Phase3A { get; } = new();

    public bool CoreContextSupported { get; init; } = true;
    public bool ActionAvailabilitySupported { get; init; } = true;
    public bool TrackerHistorySupported { get; init; } = true;
    public bool MainTargetDotSupported { get; init; } = true;
    public bool ExactWeaveDeliveryChannelSupported { get; init; }
    public bool CasualDutyAndAverageTtkSupported { get; init; }
    public bool DefensiveCastSupported { get; init; }
    public bool PotionSupported { get; init; }
    public bool DotBlacklistSupported { get; init; }
    public bool CompleteMultiTargetDotSupported { get; init; }
    public bool GenerationConsistent { get; init; } = true;

    public bool UsedWeavesApproximate => !ExactWeaveDeliveryChannelSupported;

    public string UnsupportedSummary =>
        "ExactWeaveChannel,CasualDutyAverageTtk,DefensiveCast,Potion,DotBlacklist,MultiTargetDot";
}

internal sealed record BlmResolverCandidate
{
    public uint ActionId { get; init; }
    public uint TargetId { get; init; }
    public BlmResolverTargetKind TargetKind { get; init; }
    public string ResolverId { get; init; } = string.Empty;
    public int ManifestOrder { get; init; }
    public int CheckCode { get; init; }
}

internal sealed record BlmDecisionFrame
{
    public BlmResolverCandidate? GcdCandidate { get; init; }
    public BlmResolverCandidate? AlwaysCandidate { get; init; }
    public BlmResolverCandidate? OffGcdCandidate { get; init; }
    public BlmResolverCandidate? AlwaysBridgeCandidate { get; init; }
    public bool HoldGcdForTranspose { get; init; }
    public bool GcdBlockedByTransposeHold { get; init; }
    public bool GcdBlockedByAlwaysBridge { get; init; }
    public int ExecutorWeaveSlots { get; init; }
    public int ResolverAllowedWeaves { get; init; }
    public int RemainingWeaves { get; init; }
    public bool DeliveryBlocked { get; init; }
    public string BlockReason { get; init; } = string.Empty;
}

internal readonly record struct BlmResolverCheckResult(
    uint ActionId,
    int CheckCode,
    uint TargetId = 0,
    BlmResolverTargetKind TargetKind = BlmResolverTargetKind.CurrentTarget)
{
    public bool IsAccepted => CheckCode >= 0
        && (ActionId != 0 || TargetKind == BlmResolverTargetKind.Potion);

    public static BlmResolverCheckResult Reject(int checkCode)
        => new(0, checkCode);
}
