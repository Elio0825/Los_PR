using Lumina.Excel.Sheets;

namespace LosPr.BLM.Core;

internal sealed record BlmDotSnapshot
{
    public uint StatusId { get; init; }
    public float RemainingMs { get; init; }
    public float ExpectedDurationMs { get; init; }

    public bool IsMissing => RemainingMs <= 0;
    public bool IsExpiring => RemainingMs is > 0 and < 3000;
}

internal readonly record struct BlmDotRule(uint StatusId, float DurationMs);

internal readonly record struct BlmDutyComposition(int MembersPerParty, int PartyCount)
{
    public bool IsSinglePartyEightPlayer => MembersPerParty == 8 && PartyCount == 1;
}

internal sealed record BlmActionAvailability
{
    public uint ActionId { get; init; }
    public bool IsUnlocked { get; init; }
    public bool IsAvailable { get; init; }
    public float Charges { get; init; }
    public int MaxCharges { get; init; } = 1;
    public float CooldownRemainSeconds { get; init; }
    public float RecastTotalSeconds { get; init; }
    public float NextChargeRemainSeconds { get; init; }

    public bool IsReady => IsAvailable
        && (MaxCharges > 1 ? Charges >= 1f : CooldownRemainSeconds <= 0f);

    public float NextChargeProgress => RecastTotalSeconds <= 0f || Charges >= MaxCharges
        ? 1f
        : Math.Clamp(1f - NextChargeRemainSeconds / RecastTotalSeconds, 0f, 1f);
}

internal sealed record BlmContext
{
    private static readonly object DutyCompositionCacheGate = new();
    private static uint _cachedDutyTerritoryId = uint.MaxValue;
    private static BlmDutyComposition _cachedDutyComposition;

    public static BlmContext Unavailable { get; } = new();

    public long CapturedAtMs { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; }
    public bool IsAvailable { get; init; }
    public string AvailabilityText { get; init; } = "等待游戏状态";
    public string? CaptureError { get; init; }
    public BlmTrackerSnapshot Tracker { get; init; } = BlmTrackerSnapshot.Empty;

    public AcrState AcrState { get; init; } = AcrState.Off;
    public uint PlayerEntityId { get; init; }
    public uint JobId { get; init; }
    public int Level { get; init; }
    public BlmDutyComposition DutyComposition { get; init; }
    public long Mp { get; init; }
    public long MaxMp { get; init; }
    public bool IsMoving { get; init; }
    public bool InCombat { get; init; }
    public bool IsAlive { get; init; }
    public bool IsCasting { get; init; }
    public uint CurrentCastingActionId { get; init; }
    public bool CanAct { get; init; }

    public int ActionQueueWindowMs { get; init; } =
        BlmDecisionPrimitives.DefaultActionQueueWindowMs;
    public float GcdTotalSeconds { get; init; }
    public float GcdRemainSeconds { get; init; }
    public float CastTotalSeconds { get; init; }
    public float CastRemainSeconds { get; init; }
    public float AnimationLockSeconds { get; init; }

    public bool HasTarget { get; init; }
    public bool HasValidTarget { get; init; }
    public bool InRange { get; init; }
    public uint TargetEntityId { get; init; }
    public string TargetName { get; init; } = "无目标";
    public long TargetHp { get; init; }
    public long TargetMaxHp { get; init; }
    public float TargetDistance { get; init; }
    public int EnemyCount { get; init; }
    public uint AoeTargetId { get; init; }
    public bool AoeTargetCanUseAttack { get; init; }
    public int AoeTargetHitCount { get; init; }
    public bool AoeTargetIsCurrentTarget { get; init; }
    public BlmDotSnapshot SingleTargetDot { get; init; } = new();
    public BlmDotSnapshot AoeDot { get; init; } = new();

    public BlmPhase Phase { get; init; }
    public int AfStacks { get; init; }
    public int IceStacks { get; init; }
    public int UmbralHearts { get; init; }
    public int AstralSoul { get; init; }
    public bool HasParadox { get; init; }
    public bool HasFirestarter { get; init; }
    public bool HasThunderhead { get; init; }
    public int PolyglotStacks { get; init; }
    public int MaxPolyglot { get; init; }
    public int PolyglotTimerMs { get; init; }

    public bool HasSwiftcast { get; init; }
    public float SwiftcastRemainSeconds { get; init; }
    public int TriplecastStacks { get; init; }
    public float TriplecastRemainSeconds { get; init; }
    public bool HasLeyLines { get; init; }
    public bool HasLeyLinesStatus737 { get; init; }
    public bool HasLeyLinesHaste { get; init; }
    public BlmActionAvailability Transpose { get; init; } = new();
    public BlmActionAvailability Swiftcast { get; init; } = new();
    public BlmActionAvailability Triplecast { get; init; } = new();
    public BlmActionAvailability LeyLines { get; init; } = new();
    public BlmActionAvailability Amplifier { get; init; } = new();
    public BlmActionAvailability Manafont { get; init; } = new();

    public bool CompressFireParadox { get; init; }
    public bool SwiftcastEnabled { get; init; }
    public bool TriplecastEnabled { get; init; }
    public bool LeyLinesEnabled { get; init; }
    public bool AmplifierEnabled { get; init; }
    public bool TtkDumpEnabled { get; init; }
    public bool AoeEnabled { get; init; }
    public bool SmartAoeEnabled { get; init; }
    public bool IsAoeMode { get; init; }
    public bool IsForcedSingleTarget { get; init; }
    public bool DotEnabled { get; init; }
    public bool MoveXenoEnabled { get; init; }
    public bool MoveTriplecastEnabled { get; init; }
    public bool ManafontEnabled { get; init; }
    public bool DumpPolyglotEnabled { get; init; }
    public bool FastFlareStarEnabled { get; init; }
    public bool InFire => Phase == BlmPhase.Fire;
    public bool InIce => Phase == BlmPhase.Ice;
    public bool IsMpFull => MaxMp > 0 && Mp == MaxMp;
    public bool AstralSoulFull => AstralSoul >= 6;
    public bool HasTriplecast => TriplecastStacks > 0;
    public bool HasActiveGcd => GcdTotalSeconds > 0f
        && GcdRemainSeconds > 0f
        && GcdRemainSeconds <= GcdTotalSeconds;
    public float GcdElapsedSeconds => HasActiveGcd
        ? GcdTotalSeconds - GcdRemainSeconds
        : 0f;
    public bool HasUsableSwiftcast => HasSwiftcast && SwiftcastRemainSeconds >= 0.75f;
    public bool HasUsableTriplecast => TriplecastStacks > 0 && TriplecastRemainSeconds >= 0.75f;
    public bool SwiftcastReady => Swiftcast.IsReady;
    public bool TriplecastReady => Triplecast.IsReady;
    public bool LeyLinesReady => LeyLines.IsReady;
    public bool AmplifierReady => Amplifier.IsReady;
    public bool ManafontReady => Manafont.IsReady;
    public float SingleTargetDotRemainMs => SingleTargetDot.RemainingMs;
    public float AoeDotRemainMs => AoeDot.RemainingMs;
    public float DotRemainMs => !IsAoeMode
        ? SingleTargetDot.RemainingMs
        : AoeDot.RemainingMs;
    public bool DotExpiring => !IsAoeMode
        ? SingleTargetDot.IsExpiring
        : AoeDot.IsExpiring;
    public bool DotMissing => !IsAoeMode
        ? SingleTargetDot.IsMissing
        : AoeDot.IsMissing;
    public float RequiredInstantB3ReserveSeconds => Math.Max(GcdTotalSeconds, 2.5f) + 0.75f;
    public bool CanPlanInstantB3AfterDespair => Level >= 100
        && ((HasSwiftcast && SwiftcastRemainSeconds >= RequiredInstantB3ReserveSeconds)
            || (TriplecastStacks >= 1
                && TriplecastRemainSeconds >= RequiredInstantB3ReserveSeconds)
            || (SwiftcastEnabled && IsReadyWithin(Swiftcast, GcdTotalSeconds))
            || (TriplecastEnabled && IsReadyWithin(Triplecast, GcdTotalSeconds)));

    public bool HasManafontResourcesRestored => Level >= 30
        && InFire
        && AfStacks == 3
        && IsMpFull
        && HasThunderhead
        && (Level < 58 || UmbralHearts == 3)
        && (Level < 90 || HasParadox);

    public static BlmContext Capture(IBlmClock? clock = null)
    {
        clock ??= SystemBlmClock.Instance;
        var capturedAtMs = clock.NowMs;
        var capturedAtUtc = DateTimeOffset.UtcNow;
        var acrState = ReadAcrState();

        try
        {
            var me = PRCore.Me;
            if (me is null)
            {
                return new BlmContext
                {
                    CapturedAtMs = capturedAtMs,
                    CapturedAtUtc = capturedAtUtc,
                    AcrState = acrState,
                    AvailabilityText = "等待角色登录",
                };
            }

            var level = me.Level;
            var dutyComposition = ReadDutyComposition();
            var gauge = Svc.Gauges.Get<BLMGauge>();
            var phase = gauge.InAstralFire
                ? BlmPhase.Fire
                : gauge.InUmbralIce
                    ? BlmPhase.Ice
                    : BlmPhase.Neutral;
            var target = PRCore.Target;
            var hasTarget = target is not null;
            var hasValidTarget = IsValidTarget(target);
            var targetDistance = target is null ? 0f : DistanceBetween(me, target);
            var enemyCount = hasValidTarget
                ? CountEnemiesAroundTarget(me, target!, 25f, 5f)
                : 0;
            var aoeEnabled = ReadQt("AOE");
            var smartAoeEnabled = ReadQt("智能AOE");
            var isAoeMode = ShouldUseAoeMode(
                level,
                enemyCount,
                aoeEnabled,
                smartAoeEnabled);
            var aoeTarget = SelectAoeTarget(
                me,
                target,
                hasValidTarget,
                enemyCount,
                smartAoeEnabled && isAoeMode);
            var hasLeyLinesStatus737 = me.HasStatus(BlmBuff.黑魔纹);
            var hasLeyLinesHaste = me.HasStatus(BlmBuff.咏速);

            return new BlmContext
            {
                CapturedAtMs = capturedAtMs,
                CapturedAtUtc = capturedAtUtc,
                IsAvailable = true,
                AvailabilityText = "状态已连接",
                AcrState = acrState,
                PlayerEntityId = me.EntityId,
                JobId = me.ClassJob.RowId,
                Level = level,
                DutyComposition = dutyComposition,
                Mp = me.CurrentMp,
                MaxMp = me.MaxMp,
                IsMoving = MoveManager.IsLocalPlayerMoving,
                InCombat = PRGameData.IsInCombat(),
                IsAlive = !me.IsDead && me.CurrentHp > 0,
                IsCasting = me.IsCasting,
                CurrentCastingActionId = me.IsCasting ? me.CastActionId : 0,
                CanAct = !me.IsDead && me.CurrentHp > 0 && !PRGameData.IsPlayerOccupied(),
                ActionQueueWindowMs = ReadActionQueueWindowMs(),
                GcdTotalSeconds = Math.Max(0f, ActionHelper.GetGcdTotal()),
                GcdRemainSeconds = Math.Max(0f, ActionHelper.GetGcdRemain()),
                CastTotalSeconds = Math.Max(0f, ActionHelper.GetCastTimeTotal()),
                CastRemainSeconds = Math.Max(0f, ActionHelper.GetCastTimeRemain()),
                AnimationLockSeconds = Math.Max(0f, ActionHelper.GetAnimationLock()),
                HasTarget = hasTarget,
                HasValidTarget = hasValidTarget,
                InRange = hasValidTarget && targetDistance <= PRGameData.GetCurrentAttackRange(25f),
                TargetEntityId = target?.EntityId ?? 0,
                TargetName = target?.Name.TextValue ?? "无目标",
                TargetHp = target?.CurrentHp ?? 0,
                TargetMaxHp = target?.MaxHp ?? 0,
                TargetDistance = targetDistance,
                EnemyCount = enemyCount,
                AoeTargetId = aoeTarget.EntityId,
                AoeTargetCanUseAttack = aoeTarget.CanUseAttack,
                AoeTargetHitCount = aoeTarget.HitCount,
                AoeTargetIsCurrentTarget = aoeTarget.IsCurrentTarget,
                SingleTargetDot = ReadDot(me, target, SingleTargetDotRuleForLevel(level)),
                AoeDot = ReadDot(me, target, AoeDotRuleForLevel(level)),
                Phase = phase,
                AfStacks = gauge.AstralFireStacks,
                IceStacks = gauge.UmbralIceStacks,
                UmbralHearts = gauge.UmbralHearts,
                AstralSoul = gauge.AstralSoulStacks,
                HasParadox = gauge.IsParadoxActive,
                HasFirestarter = me.HasStatus(BlmBuff.火苗),
                HasThunderhead = me.HasStatus(BlmBuff.雷首),
                PolyglotStacks = gauge.PolyglotStacks,
                MaxPolyglot = level >= 98 ? 3 : level >= 80 ? 2 : level >= 70 ? 1 : 0,
                PolyglotTimerMs = gauge.EnochianTimer,
                HasSwiftcast = me.HasStatus(BlmBuff.即刻),
                SwiftcastRemainSeconds = GetSelfStatusRemainingSeconds(me, BlmBuff.即刻),
                TriplecastStacks = GetSelfStatusStacks(me, BlmBuff.三连),
                TriplecastRemainSeconds = GetSelfStatusRemainingSeconds(me, BlmBuff.三连),
                HasLeyLines = hasLeyLinesStatus737 || hasLeyLinesHaste,
                HasLeyLinesStatus737 = hasLeyLinesStatus737,
                HasLeyLinesHaste = hasLeyLinesHaste,
                Transpose = CaptureAction(BLMSkill.星灵移位, level),
                Swiftcast = CaptureAction(MageUniversalSkill.即刻咏唱, level),
                Triplecast = CaptureAction(BLMSkill.三连咏唱, level),
                LeyLines = CaptureAction(BLMSkill.黑魔纹, level),
                Amplifier = CaptureAction(BLMSkill.详述, level),
                Manafont = CaptureAction(BLMSkill.魔泉, level),
                CompressFireParadox = true,
                SwiftcastEnabled = ReadQt("即刻进冰"),
                TriplecastEnabled = ReadQt("三连进冰"),
                LeyLinesEnabled = ReadQt("黑魔纹"),
                AmplifierEnabled = ReadQt("详述"),
                TtkDumpEnabled = ReadQt("TTK"),
                AoeEnabled = aoeEnabled,
                SmartAoeEnabled = smartAoeEnabled,
                IsAoeMode = isAoeMode,
                IsForcedSingleTarget = !aoeEnabled,
                DotEnabled = ReadQt("Dot"),
                MoveXenoEnabled = ReadQt("移动通晓"),
                MoveTriplecastEnabled = ReadQt("移动三连"),
                ManafontEnabled = ReadQt("魔泉"),
                DumpPolyglotEnabled = ReadQt("倾泻资源"),
                FastFlareStarEnabled = ReadQt("快速耀星"),
            };
        }
        catch (Exception exception)
        {
            return new BlmContext
            {
                CapturedAtMs = capturedAtMs,
                CapturedAtUtc = capturedAtUtc,
                AcrState = acrState,
                AvailabilityText = "状态读取暂不可用",
                CaptureError = $"{exception.GetType().Name}: {exception.Message}",
            };
        }
    }

    internal static bool ShouldUseAoeMode(
        int level,
        int enemyCount,
        bool aoeEnabled,
        bool smartAoeEnabled)
    {
        if (!aoeEnabled)
        {
            return false;
        }

        if (smartAoeEnabled && level >= 58 && enemyCount >= 2)
        {
            return true;
        }

        return level >= 12 && enemyCount >= 3;
    }

    private static BlmDutyComposition ReadDutyComposition()
    {
        try
        {
            var territoryId = Svc.ClientState.TerritoryType;
            lock (DutyCompositionCacheGate)
            {
                if (_cachedDutyTerritoryId == territoryId)
                {
                    return _cachedDutyComposition;
                }
            }

            var territories = Svc.Data.GetExcelSheet<TerritoryType>();
            var composition = default(BlmDutyComposition);
            if (territories is not null
                && territories.TryGetRow(territoryId, out var territory)
                && territory.ContentFinderCondition.ValueNullable is { } content
                && content.ContentMemberType.ValueNullable is { } memberType)
            {
                composition = new BlmDutyComposition(
                    memberType.MembersPerParty,
                    memberType.PartyCount);
            }

            lock (DutyCompositionCacheGate)
            {
                _cachedDutyTerritoryId = territoryId;
                _cachedDutyComposition = composition;
            }

            return composition;
        }
        catch
        {
            return default;
        }
    }

    public static int CountEnemiesAroundTarget(
        IBattleChara me,
        IBattleChara center,
        float castRange,
        float damageRange)
    {
        if (DistanceBetween(me, center) > PRGameData.GetCurrentAttackRange(castRange))
        {
            return 0;
        }

        var enemyCount = TargetHelper.EnemyInRangeTarget(center, damageRange);
        return enemyCount > (uint)int.MaxValue ? int.MaxValue : (int)enemyCount;
    }

    // PR 原生计数真机 A/B 期间停用；完整保留 Los 资格过滤，便于一处切回和对照。
    private static int CountEnemiesAroundTargetWithLosEligibility(
        IBattleChara me,
        IBattleChara center,
        float castRange,
        float damageRange)
    {
        if (!IsLiveAoeObject(center.IsTargetable, center.IsDead, center.CurrentHp)
            || DistanceBetween(me, center) > PRGameData.GetCurrentAttackRange(castRange))
        {
            return 0;
        }

        var count = center.CanUseAttackActionOn() ? 1 : 0;
        for (var index = 0; index < Svc.Objects.Length; index++)
        {
            if (Svc.Objects[index] is not IBattleChara battle
                || battle.EntityId == center.EntityId
                || !IsLiveAoeObject(battle.IsTargetable, battle.IsDead, battle.CurrentHp)
                || battle.ObjectKind == ObjectKind.Pc
                || !battle.StatusFlags.HasFlag(StatusFlags.Hostile))
            {
                continue;
            }

            var distanceFromCenter = Vector3.Distance(center.Position, battle.Position);
            if (distanceFromCenter <= damageRange + battle.HitboxRadius
                && IsCountableAoeEnemy(
                    battle.IsTargetable,
                    battle.IsDead,
                    battle.CurrentHp,
                    battle.StatusFlags.HasFlag(StatusFlags.Hostile),
                    battle.CanUseAttackActionOn()))
            {
                count++;
            }
        }

        return count;
    }

    internal static bool IsLiveAoeObject(
        bool isTargetable,
        bool isDead,
        long currentHp)
        => isTargetable && !isDead && currentHp > 0;

    internal static bool IsCountableAoeEnemy(
        bool isTargetable,
        bool isDead,
        long currentHp,
        bool isHostile,
        bool canUseAttackActionOn)
        => IsLiveAoeObject(isTargetable, isDead, currentHp)
            && isHostile
            && canUseAttackActionOn;

    private static AoeTargetSelection SelectAoeTarget(
        IBattleChara me,
        IBattleChara? currentTarget,
        bool currentTargetCanUseAttack,
        int currentTargetHitCount,
        bool smartAoeEnabled)
    {
        if (currentTarget is null || !currentTargetCanUseAttack)
        {
            return default;
        }

        var current = CreateAoeTargetSelection(
            me,
            currentTarget,
            currentTarget.EntityId,
            currentTargetHitCount);
        if (!smartAoeEnabled)
        {
            return current;
        }

        var best = current;
        for (var index = 0; index < Svc.Objects.Length; index++)
        {
            if (Svc.Objects[index] is not IBattleChara candidate
                || candidate.EntityId == currentTarget.EntityId
                || !IsLiveAoeObject(
                    candidate.IsTargetable,
                    candidate.IsDead,
                    candidate.CurrentHp)
                || candidate.ObjectKind == ObjectKind.Pc
                || !candidate.CanUseAttackActionOn())
            {
                continue;
            }

            var selection = CreateAoeTargetSelection(
                me,
                candidate,
                currentTarget.EntityId);
            if (IsBetterAoeTarget(
                selection.HitCount,
                selection.IsCurrentTarget,
                selection.Distance,
                selection.EntityId,
                best.HitCount,
                best.IsCurrentTarget,
                best.Distance,
                best.EntityId))
            {
                best = selection;
            }
        }

        return best;
    }

    internal static bool IsBetterAoeTarget(
        int hitCount,
        bool isCurrentTarget,
        float distance,
        uint entityId,
        int bestHitCount,
        bool bestIsCurrentTarget,
        float bestDistance,
        uint bestEntityId)
        => hitCount > bestHitCount
            || hitCount == bestHitCount
                && (isCurrentTarget && !bestIsCurrentTarget
                    || isCurrentTarget == bestIsCurrentTarget
                        && (distance < bestDistance
                            || distance == bestDistance && entityId < bestEntityId));

    private static AoeTargetSelection CreateAoeTargetSelection(
        IBattleChara me,
        IBattleChara candidate,
        uint currentTargetId,
        int? knownHitCount = null)
    {
        var distance = DistanceBetween(me, candidate);
        var canUseAttack = IsLiveAoeObject(
                candidate.IsTargetable,
                candidate.IsDead,
                candidate.CurrentHp)
            && distance <= PRGameData.GetCurrentAttackRange(25f)
            && candidate.CanUseAttackActionOn();
        return new AoeTargetSelection(
            candidate.EntityId,
            canUseAttack,
            canUseAttack
                ? knownHitCount ?? CountEnemiesAroundTarget(me, candidate, 25f, 5f)
                : 0,
            candidate.EntityId == currentTargetId,
            distance);
    }

    private readonly record struct AoeTargetSelection(
        uint EntityId,
        bool CanUseAttack,
        int HitCount,
        bool IsCurrentTarget,
        float Distance);

    public static BlmDotRule SingleTargetDotRuleForLevel(int level) => level switch
    {
        >= 92 => new BlmDotRule(BlmBuff.高雷Dot, 30000f),
        >= 45 => new BlmDotRule(BlmBuff.暴雷Dot, 27000f),
        >= 6 => new BlmDotRule(BlmBuff.雷一Dot, 24000f),
        _ => default,
    };

    public static BlmDotRule AoeDotRuleForLevel(int level) => level switch
    {
        >= 92 => new BlmDotRule(BlmBuff.高雷二Dot, 24000f),
        >= 64 => new BlmDotRule(BlmBuff.霹雷Dot, 21000f),
        >= 26 => new BlmDotRule(BlmBuff.雷二Dot, 18000f),
        >= 6 => new BlmDotRule(BlmBuff.雷一Dot, 24000f),
        _ => default,
    };

    private static AcrState ReadAcrState()
    {
        try
        {
            return PromeSettings.Instance.EnableAcr;
        }
        catch
        {
            return AcrState.Off;
        }
    }

    private static bool ReadQt(string name)
    {
        try
        {
            return PromeSettings.Instance.GetQt(name);
        }
        catch
        {
            return false;
        }
    }

    private static int ReadActionQueueWindowMs()
    {
        try
        {
            return BlmDecisionPrimitives.NormalizeActionQueueWindowMs(
                PromeSettings.Instance.Hacks.GcdQueueWindowMs);
        }
        catch
        {
            return BlmDecisionPrimitives.DefaultActionQueueWindowMs;
        }
    }

    private static bool IsValidTarget(IBattleChara? target)
        => target is not null
            && target.CanUseAttackActionOn();

    private static float DistanceBetween(IBattleChara source, IBattleChara target)
        => Math.Max(
            0f,
            Vector3.Distance(source.Position, target.Position)
                - source.HitboxRadius
                - target.HitboxRadius);

    private static BlmDotSnapshot ReadDot(
        IBattleChara me,
        IBattleChara? target,
        BlmDotRule rule)
    {
        if (target is null || rule.StatusId == 0)
        {
            return new BlmDotSnapshot
            {
                StatusId = rule.StatusId,
                ExpectedDurationMs = rule.DurationMs,
            };
        }

        foreach (var status in target.StatusList)
        {
            if (status.StatusId == rule.StatusId && status.SourceId == me.EntityId)
            {
                return new BlmDotSnapshot
                {
                    StatusId = rule.StatusId,
                    RemainingMs = Math.Abs(status.RemainingTime) * 1000f,
                    ExpectedDurationMs = rule.DurationMs,
                };
            }
        }

        return new BlmDotSnapshot
        {
            StatusId = rule.StatusId,
            ExpectedDurationMs = rule.DurationMs,
        };
    }

    private static int GetSelfStatusStacks(IBattleChara me, uint statusId)
    {
        foreach (var status in me.StatusList)
        {
            if (status.StatusId == statusId)
            {
                return Math.Max(1, (int)status.Param);
            }
        }

        return 0;
    }

    private static float GetSelfStatusRemainingSeconds(IBattleChara me, uint statusId)
    {
        foreach (var status in me.StatusList)
        {
            if (status.StatusId == statusId)
            {
                return Math.Abs(status.RemainingTime);
            }
        }

        return 0f;
    }

    private static BlmActionAvailability CaptureAction(uint actionId, int level)
    {
        var isUnlocked = BlmSkillBook.IsUnlocked(actionId, level);
        if (!isUnlocked)
        {
            return new BlmActionAvailability
            {
                ActionId = actionId,
                IsUnlocked = false,
                IsAvailable = false,
            };
        }

        try
        {
            var adjusted = ActionHelper.GetAdjustedActionId(actionId);
            if (adjusted == 0)
            {
                adjusted = actionId;
            }

            var isAvailable = ActionHelper.IsActionAvailableByLevelAndQuest(adjusted);
            var maxCharges = Math.Max(1, ActionHelper.GetMaxCharges(adjusted));
            var charges = Math.Max(0f, ActionHelper.GetActionCharges(adjusted));
            var cooldown = Math.Max(0f, ActionHelper.GetActionCooldown(adjusted));
            var recastTotal = Math.Max(0f, ActionHelper.GetActionRecastTime(adjusted));
            var recastElapsed = Math.Max(0f, ActionHelper.GetActionRecastTimeElapsed(adjusted));
            var nextChargeRemain = charges >= maxCharges
                ? 0f
                : Math.Max(cooldown, Math.Max(0f, recastTotal - recastElapsed));

            return new BlmActionAvailability
            {
                ActionId = adjusted,
                IsUnlocked = true,
                IsAvailable = isAvailable,
                Charges = charges,
                MaxCharges = maxCharges,
                CooldownRemainSeconds = cooldown,
                RecastTotalSeconds = recastTotal,
                NextChargeRemainSeconds = nextChargeRemain,
            };
        }
        catch
        {
            return new BlmActionAvailability
            {
                ActionId = actionId,
                IsUnlocked = true,
                IsAvailable = false,
            };
        }
    }

    private static bool IsReadyWithin(
        BlmActionAvailability action,
        float seconds)
    {
        if (!action.IsUnlocked || !action.IsAvailable)
        {
            return false;
        }

        if (action.IsReady)
        {
            return true;
        }

        var remain = action.MaxCharges > 1
            ? action.NextChargeRemainSeconds
            : action.CooldownRemainSeconds;
        return remain <= Math.Max(0f, seconds);
    }
}
