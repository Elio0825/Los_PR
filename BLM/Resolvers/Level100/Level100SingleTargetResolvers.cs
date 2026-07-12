using System.Linq;

namespace LosPr.BLM.Resolvers.Level100;

internal static class Level100SingleTargetResolvers
{
    private const int FireParadoxMpCost = 1600;
    private const int DespairMinMp = 800;

    public static BlmResolverCheckResult Evaluate(
        string resolverId,
        BlmResolverInput input)
        => resolverId switch
        {
            "GCD.TTK" => CheckTtk(input),
            "GCD.快速耀星" => CheckFastFlareStar(input),
            "GCD.强制回冰" => CheckForcedIceRecovery(input),
            "GCD.异言#1" or "GCD.异言#2" => CheckXenoglossy(input),
            "GCD.双DOT" => CheckDoubleDot(input),
            "GCD.雷1" => CheckThunder(input),
            "GCD.瞬发gcd触发器" => CheckInstantGcdTrigger(input),
            "GCD.单体100" => CheckLevel100SingleTarget(input),
            _ => BlmResolverCheckResult.Reject(-999),
        };

    private static BlmResolverCheckResult CheckTtk(BlmResolverInput input)
    {
        var context = input.Context;
        if (!input.Settings.TtkEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (context.PolyglotStacks > 0
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.异言))
        {
            return Gcd(input, BLMSkill.异言, (int)BLMSkill.异言);
        }

        if (context.HasParadox
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.悖论))
        {
            return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
        }

        if (context.AstralSoulStacks == 6
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.耀星))
        {
            return Gcd(input, BLMSkill.耀星, (int)BLMSkill.耀星);
        }

        if (context.Mp >= DespairMinMp
            && context.InFire
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.绝望))
        {
            return Gcd(input, BLMSkill.绝望, (int)BLMSkill.绝望);
        }

        return BlmResolverCheckResult.Reject(-1);
    }

    private static BlmResolverCheckResult CheckFastFlareStar(BlmResolverInput input)
    {
        var context = input.Context;
        if (!input.Settings.FastFlareStarEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (context.Level < 100)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (!context.InFire)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.AstralFireStacks < 3)
        {
            if (context.HasFirestarter)
            {
                return Gcd(input, BLMSkill.爆炎, (int)BLMSkill.爆炎);
            }

            if (context.HasParadox && context.Mp >= FireParadoxMpCost)
            {
                return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
            }

            return Gcd(input, BLMSkill.爆炎, (int)BLMSkill.爆炎);
        }

        var predictedSoul = Math.Clamp(
            context.AstralSoulStacks
                + (!context.IsCasting ? 0 : input.CurrentCastingActionId switch
                {
                    BLMSkill.炽炎 => 1,
                    BLMSkill.核爆 => 3,
                    _ => 0,
                }),
            0,
            6);

        if (predictedSoul >= 6)
        {
            if (context.HasParadox && context.Mp >= FireParadoxMpCost)
            {
                return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
            }

            return Gcd(input, BLMSkill.耀星, (int)BLMSkill.耀星);
        }

        if (predictedSoul == 3 && context.Mp >= DespairMinMp)
        {
            return Gcd(input, BLMSkill.核爆, (int)BLMSkill.核爆);
        }

        if (predictedSoul == 2
            && context.HasParadox
            && context.Mp >= FireParadoxMpCost)
        {
            return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
        }

        if (predictedSoul < 6 && !IsLowMpForFire4(context))
        {
            return Gcd(input, BLMSkill.炽炎, (int)BLMSkill.炽炎);
        }

        return BlmResolverCheckResult.Reject(-5);
    }

    private static BlmResolverCheckResult CheckForcedIceRecovery(BlmResolverInput input)
    {
        var context = input.Context;
        if (!input.NeedsForcedIceRecovery)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!context.InCombat)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (!context.InFire)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.冰封))
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        return Gcd(input, BLMSkill.冰封, 500);
    }

    private static BlmResolverCheckResult CheckXenoglossy(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 80)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.Settings.DumpPolyglotEnabled && context.PolyglotStacks > 0)
        {
            return Gcd(input, BLMSkill.异言, 666);
        }

        if (context.PolyglotStacks == 3 && context.PolyglotTimerMs <= 10_000)
        {
            return Gcd(input, BLMSkill.异言, 2);
        }

        if (context.PolyglotStacks == 3
            && Level100ResolverFacts.CooldownInNextGcdWindows(
                input,
                BLMSkill.详述,
                1))
        {
            return Gcd(input, BLMSkill.异言, 3);
        }

        if (context.InFire
            && context.PolyglotStacks > 0
            && context.Mp < DespairMinMp
            && context.AstralSoulStacks != 6
            && input.Settings.ManafontEnabled)
        {
            var manafont = Level100ResolverFacts.Action(input, BLMSkill.魔泉);
            if (manafont is { CooldownRemainMs: < 300d }
                && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔泉))
            {
                return BlmResolverCheckResult.Reject(-3);
            }

            if (Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.魔泉,
                    context.PolyglotStacks))
            {
                return Gcd(input, BLMSkill.异言, 4);
            }
        }

        return BlmResolverCheckResult.Reject(-99);
    }

    private static BlmResolverCheckResult CheckDoubleDot(BlmResolverInput input)
    {
        var context = input.Context;
        var settings = input.Settings;
        if (!settings.DotEnabled || !settings.DoubleDotEnabled)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (settings.TtkEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.IsCasting)
        {
            return BlmResolverCheckResult.Reject(-30);
        }

        if (!context.HasThunderhead)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!input.CanSpecifyDotTarget)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        var dotActionId = Level100ResolverFacts.EffectiveActionId(input, BLMSkill.闪雷);
        if (dotActionId == 0)
        {
            return BlmResolverCheckResult.Reject(-8);
        }

        var minimumHpRatio = Math.Max(0, settings.DotHpThresholdPercent) / 100f;
        var targets = input.DotTargets
            .Where(static target => target.IsValid)
            .Where(static target => target.IsTargetable && target.IsAlive)
            .Where(static target => target.CanUseAttackActionOn && target.IsInDotRange)
            .Where(static target => !target.IsBlacklisted)
            .Where(target => target.HpRatio >= minimumHpRatio)
            .ToList();
        if (targets.Count < 2)
        {
            return BlmResolverCheckResult.Reject(-200);
        }

        var nowMs = context.CapturedAtMs;
        if (targets.Count(target => DotSufficient(input, target, nowMs)) >= 2)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        var candidates = targets
            .Where(target => DotMissing(input, target, nowMs)
                || DotExpiring(input, target, nowMs))
            .ToList();
        if (candidates.Count == 0)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        var currentTarget = targets.FirstOrDefault(
            target => target.EntityId == context.CurrentTargetId);
        if (currentTarget is { IsValid: true }
            && DotSufficient(input, currentTarget, nowMs)
            && candidates.Count > 1)
        {
            candidates.RemoveAll(target => target.EntityId == currentTarget.EntityId);
        }

        var nonRepeat = candidates
            .Where(target => target.EntityId != input.DoubleDotMemory.LastTargetId
                || nowMs - input.DoubleDotMemory.LastCastAtMs >= 2500)
            .ToList();
        var pickFrom = nonRepeat.Count > 0 ? nonRepeat : candidates;
        var chosen = pickFrom
            .OrderByDescending(target => DotMissing(input, target, nowMs) ? 1 : 0)
            .ThenByDescending(static target => target.MaxHp)
            .ThenByDescending(static target => target.CurrentHp)
            .ThenBy(static target => target.Distance)
            .FirstOrDefault();
        if (chosen is null)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                BLMSkill.闪雷,
                chosen.CanCastDot))
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        return new BlmResolverCheckResult(
            dotActionId,
            35,
            chosen.EntityId,
            BlmResolverTargetKind.SpecifiedTarget);
    }

    private static BlmResolverCheckResult CheckThunder(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!input.Settings.DotEnabled)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.Settings.TtkEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.IsCasting)
        {
            return BlmResolverCheckResult.Reject(-30);
        }

        var target = Level100ResolverFacts.CurrentTarget(input);
        if (target is null)
        {
            return BlmResolverCheckResult.Reject(-31);
        }

        if (Level100ResolverFacts.IsBelowDotHpThreshold(input, target))
        {
            return BlmResolverCheckResult.Reject(-32);
        }

        if (Level100ResolverFacts.NeedsDot(input, target, 3500)
            && context.HasThunderhead)
        {
            return Gcd(input, BLMSkill.闪雷, 1);
        }

        return BlmResolverCheckResult.Reject(-99);
    }

    private static BlmResolverCheckResult CheckInstantGcdTrigger(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (!context.IsMoving && !input.IsIdle)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        var actionId = SelectAvailableInstantGcd(input);
        if (actionId == 0)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (Level100ResolverFacts.RecentlyUsed(input, actionId))
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        return Gcd(input, actionId, 1);
    }

    internal static bool HasAvailableInstantGcd(BlmResolverInput input)
        => SelectAvailableInstantGcd(input) != 0;

    private static uint SelectAvailableInstantGcd(BlmResolverInput input)
    {
        var context = input.Context;
        var target = Level100ResolverFacts.CurrentTarget(input);
        if (input.Settings.DotEnabled
            && target is not null
            && !Level100ResolverFacts.IsBelowDotHpThreshold(input, target)
            && Level100ResolverFacts.NeedsDot(input, target, 3500)
            && context.HasThunderhead)
        {
            return BLMSkill.闪雷;
        }

        if (context.InFire
            && context.AstralFireStacks < 3
            && context.HasFirestarter)
        {
            return BLMSkill.爆炎;
        }

        if (context.HasParadox)
        {
            if (context.InFire && context.Mp >= 2400)
            {
                return BLMSkill.悖论;
            }

            if (context.InIce && !input.Settings.SkipIceParadox)
            {
                return BLMSkill.悖论;
            }
        }

        if (context.InFire
            && context.Mp is >= DespairMinMp and < 2400
            && context.Level >= 100)
        {
            return BLMSkill.绝望;
        }

        if (context.PolyglotStacks >= 1
            && context.Level >= 80
            && input.Settings.MoveXenoglossyEnabled)
        {
            return BLMSkill.异言;
        }

        if (input.Settings.DotEnabled
            && target is not null
            && !Level100ResolverFacts.IsBelowDotHpThreshold(input, target)
            && Level100ResolverFacts.NeedsDot(input, target, 6000)
            && context.HasThunderhead)
        {
            return BLMSkill.闪雷;
        }

        var swiftcast = Level100ResolverFacts.Action(input, MageUniversalSkill.即刻咏唱);
        var triplecast = Level100ResolverFacts.Action(input, BLMSkill.三连咏唱);
        if (swiftcast is { CooldownRemainMs: > 0d }
            && (triplecast?.Charges ?? 0f) < 1f)
        {
            if (context.HasFirestarter && context.InFire)
            {
                return BLMSkill.爆炎;
            }

            if (input.Settings.DotEnabled && context.HasThunderhead)
            {
                return BLMSkill.闪雷;
            }
        }

        return 0;
    }

    private static BlmResolverCheckResult CheckLevel100SingleTarget(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level != 100)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel100SingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : Gcd(input, actionId, (int)actionId);
    }

    private static uint SelectLevel100SingleTargetGcd(BlmResolverInput input)
    {
        if (input.SpecialSequenceActive)
        {
            return 0;
        }

        if (input.Context.InFire)
        {
            return SelectFireGcd(input);
        }

        if (input.Context.InIce)
        {
            return SelectIceGcd(input);
        }

        return BLMSkill.冰封;
    }

    private static uint SelectIceGcd(BlmResolverInput input)
    {
        var context = input.Context;
        if (input.Level100Loop.RecoveringAfterSpecial)
        {
            return Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰封)
                ? BLMSkill.冰澈
                : BLMSkill.冰封;
        }

        if (context.UmbralIceStacks < 3)
        {
            if (context.HasParadox
                && !context.HasInstantCast
                && HasIncomingIceInstant(input))
            {
                return BLMSkill.悖论;
            }

            return BLMSkill.冰封;
        }

        if (context.UmbralHearts < 3)
        {
            return BLMSkill.冰澈;
        }

        if (context.HasParadox)
        {
            if (input.Settings.SkipIceParadox)
            {
                return 0;
            }

            if (!context.HasInstantCast && HasIncomingIceInstant(input))
            {
                return BLMSkill.悖论;
            }

            // PR 不迁移“压缩冰悖论”持久化设置，默认保持 los-ae 非压缩行为。
            return BLMSkill.悖论;
        }

        if (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰封)
            && context.UmbralHearts >= 3
            && context.MaxMp > 0
            && context.Mp < context.MaxMp * 0.95)
        {
            return BLMSkill.冰澈;
        }

        return 0;
    }

    private static bool HasIncomingIceInstant(BlmResolverInput input)
    {
        var swiftSoon = Level100ResolverFacts.CooldownInNextGcdWindows(
                input,
                MageUniversalSkill.即刻咏唱,
                1);
        var triplecast = Level100ResolverFacts.Action(input, BLMSkill.三连咏唱);
        var tripleSoon = input.Settings.TriplecastIntoIceEnabled
            && (Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.三连咏唱,
                    1)
                || BlmDecisionPrimitives.HasAnyChargeProgress(
                    triplecast?.Charges ?? 0f));
        return swiftSoon || tripleSoon;
    }

    private static uint SelectFireGcd(BlmResolverInput input)
    {
        var context = input.Context;
        var (fire4Count, fireParadoxUsed) = SanitizeLoopFacts(input);
        var movingWithInstant = context.IsMoving && context.HasInstantCast;
        var hasParadox = context.HasParadox;

        if (context.AstralFireStacks < 3)
        {
            if (context.HasFirestarter)
            {
                return BLMSkill.爆炎;
            }

            if (hasParadox && context.Mp >= FireParadoxMpCost)
            {
                return BLMSkill.悖论;
            }

            if (context.Mp < DespairMinMp)
            {
                return BLMSkill.冰封;
            }

            return BLMSkill.爆炎;
        }

        var soulFull = context.AstralSoulStacks >= 6;
        var lowMpForFire4 = IsLowMpForFire4(context);
        if (!fireParadoxUsed
            && hasParadox
            && context.Mp >= FireParadoxMpCost
            && fire4Count >= 3
            && fire4Count < 6
            && (!input.Settings.CompressFireParadox || movingWithInstant))
        {
            return BLMSkill.悖论;
        }

        if (!soulFull && fire4Count < 6 && !lowMpForFire4)
        {
            return BLMSkill.炽炎;
        }

        if (input.Settings.CompressFireParadox
            && !fireParadoxUsed
            && hasParadox
            && context.Mp >= FireParadoxMpCost
            && !movingWithInstant)
        {
            return BLMSkill.悖论;
        }

        if (soulFull)
        {
            return BLMSkill.耀星;
        }

        return context.Mp >= DespairMinMp
            ? BLMSkill.绝望
            : BLMSkill.冰封;
    }

    private static (int Fire4Count, bool FireParadoxUsed) SanitizeLoopFacts(
        BlmResolverInput input)
    {
        var context = input.Context;
        var fire4Count = Math.Max(0, input.Level100Loop.Fire4Count);
        var fireParadoxUsed = input.Level100Loop.FireParadoxUsed;
        if (!context.InCombat
            || !context.IsAlive
            || (!context.InFire && !context.InIce)
            || (!context.InFire && (fire4Count > 0 || fireParadoxUsed)))
        {
            fire4Count = 0;
            fireParadoxUsed = false;
        }

        if (fire4Count < context.AstralSoulStacks)
        {
            fire4Count = context.AstralSoulStacks;
        }

        if (context.Mp <= 3000 && !context.InIce)
        {
            fire4Count = 0;
            fireParadoxUsed = false;
        }

        return (fire4Count, fireParadoxUsed);
    }

    private static bool IsLowMpForFire4(BlmResolverContextFacts context)
    {
        var fire4Cost = context.InFire
            && context.AstralFireStacks > 0
            && context.UmbralHearts == 0
                ? 1600
                : 800;
        return context.Mp < fire4Cost + DespairMinMp;
    }

    private static bool DotSufficient(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => IsProtectedByDoubleDotMemory(input, target, nowMs)
            || target.MaxOwnedDotRemainingMs > 3000f;

    private static bool DotExists(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => IsProtectedByDoubleDotMemory(input, target, nowMs)
            || target.MaxOwnedDotRemainingMs > 500f;

    private static bool DotMissing(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => target.HasOwnedDot
            ? !DotExists(input, target, nowMs)
            : true;

    private static bool DotExpiring(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => DotExists(input, target, nowMs)
            && !DotSufficient(input, target, nowMs);

    private static bool IsProtectedByDoubleDotMemory(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => target.EntityId == input.DoubleDotMemory.LastTargetId
            && nowMs - input.DoubleDotMemory.LastCastAtMs < 3500;

    private static BlmResolverCheckResult Gcd(
        BlmResolverInput input,
        uint requestedActionId,
        int checkCode)
        => new(
            Level100ResolverFacts.EffectiveActionId(input, requestedActionId),
            checkCode,
            input.Context.CurrentTargetId,
            BlmResolverTargetKind.CurrentTarget);
}

internal static class Level100ResolverFacts
{
    public static BlmResolverActionFact? Action(
        BlmResolverInput input,
        uint actionId)
    {
        foreach (var action in input.Actions)
        {
            if (action.RequestedActionId == actionId)
            {
                return action;
            }
        }

        foreach (var action in input.Actions)
        {
            if (action.EffectiveActionId == actionId)
            {
                return action;
            }
        }

        return null;
    }

    public static uint EffectiveActionId(BlmResolverInput input, uint actionId)
        => Action(input, actionId)?.EffectiveActionId ?? actionId;

    public static bool IsReadyWithCanCast(
        BlmResolverInput input,
        uint actionId,
        bool? canCastOverride = null)
    {
        var action = Action(input, actionId);
        return action is not null
            && BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                action.IsUnlocked,
                canCastOverride ?? action.CanCast,
                RecentlyUsed(
                    input,
                    actionId,
                    BlmDecisionPrimitives.AeAssistAbilityRepeatGuardMs),
                action.Charges,
                action.CooldownRemainMs);
    }

    public static bool RecentlyUsed(
        BlmResolverInput input,
        uint actionId,
        int withinMs = BlmDecisionPrimitives.DefaultRecentlyUsedWindowMs)
    {
        foreach (var success in input.RecentHistory)
        {
            if (ActionMatches(input, success, actionId)
                && BlmDecisionPrimitives.RecentlyUsed(
                    input.Context.CapturedAtMs,
                    success.OccurredAtMs,
                    withinMs))
            {
                return true;
            }
        }

        return false;
    }

    public static bool PreviousGcdMatches(BlmResolverInput input, uint actionId)
        => input.PreviousGcd is { IsGcd: true } success
            && ActionMatches(input, success, actionId);

    public static bool ActionMatches(
        BlmResolverInput input,
        BlmActionSuccess success,
        uint actionId)
    {
        var action = Action(input, actionId);
        var effectiveId = action?.EffectiveActionId ?? actionId;
        return success.RequestedId == actionId
            || success.RequestedId == effectiveId
            || success.AdjustedAtIssue == actionId
            || success.AdjustedAtIssue == effectiveId
            || success.ActualAckId == actionId
            || success.ActualAckId == effectiveId;
    }

    public static uint LastOgcdActionId(BlmResolverInput input)
    {
        BlmActionSuccess? latest = null;
        foreach (var success in input.RecentHistory)
        {
            if (success.IsGcd
                || latest is not null
                    && success.AcknowledgedAtMs < latest.Value.AcknowledgedAtMs)
            {
                continue;
            }

            latest = success;
        }

        return latest is null
            ? 0
            : Level100ResolverEngine.EffectiveActionId(latest.Value);
    }

    public static bool CooldownInNextGcdWindows(
        BlmResolverInput input,
        uint actionId,
        int count)
    {
        var action = Action(input, actionId);
        if (action is null || !TryGetCurrentGcdStartedAtMs(input, out var startedAtMs))
        {
            return false;
        }

        return BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
            action.CooldownRemainMs,
            count,
            input.Context.CapturedAtMs,
            startedAtMs,
            BlmDecisionPrimitives.NormalizeActionQueueWindowMs(
                input.Context.ActionQueueWindowMs));
    }

    public static bool TryGetCurrentGcdStartedAtMs(
        BlmResolverInput input,
        out long startedAtMs)
    {
        if (input.PreviousGcd is { IsGcd: true } previous
            && previous.OccurredAtMs > 0
            && previous.OccurredAtMs <= input.Context.CapturedAtMs)
        {
            startedAtMs = previous.OccurredAtMs;
            return true;
        }

        if (BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(
                input.Context.CapturedAtMs,
                input.Context.GcdTotalSeconds,
                input.Context.GcdRemainSeconds,
                out startedAtMs))
        {
            return true;
        }

        return false;
    }

    public static BlmResolverDotTargetFact? CurrentTarget(BlmResolverInput input)
    {
        foreach (var target in input.DotTargets)
        {
            if (target.EntityId == input.Context.CurrentTargetId)
            {
                return target;
            }
        }

        return null;
    }

    public static bool IsBelowDotHpThreshold(
        BlmResolverInput input,
        BlmResolverDotTargetFact target)
        => target.HpRatio < Math.Max(0, input.Settings.DotHpThresholdPercent) / 100f;

    public static bool NeedsDot(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        int thresholdMs)
        => input.Settings.DotEnabled
            && !IsBelowDotHpThreshold(input, target)
            && target.SingleTargetDotRemainingMs <= thresholdMs
            && target.AoeDotRemainingMs <= thresholdMs;
}
