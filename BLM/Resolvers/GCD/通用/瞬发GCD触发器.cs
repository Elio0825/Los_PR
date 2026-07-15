namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckInstantGcdTrigger(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.IsAoeMode)
        {
            return CheckAoeInstantGcdTrigger(input);
        }

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

    private static BlmResolverCheckResult CheckAoeInstantGcdTrigger(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        var plan = Level100AbilityResolvers.BuildAoeTransposePlan(input);
        if (plan.Disposition is BlmAoeTransposeDisposition.CastNow
            or BlmAoeTransposeDisposition.Hold)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        var isTransposeFill = plan.Disposition == BlmAoeTransposeDisposition.Fill;
        if (!isTransposeFill && !context.IsMoving && !input.IsIdle)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        var actionId = isTransposeFill
            ? plan.FillActionId
            : SelectAvailableAoeInstantGcd(input, transposeFill: false);
        if (actionId == 0)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (Level100ResolverFacts.RecentlyUsed(input, actionId))
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (!IsAoeGcdUnlocked(input, actionId))
        {
            return BlmResolverCheckResult.Reject(-103);
        }

        return IsAreaAoeAction(actionId)
            ? AoeGcd(input, actionId, 1)
            : Gcd(input, actionId, 1);
    }

    internal static bool HasAvailableInstantGcd(BlmResolverInput input)
        => SelectAvailableInstantGcd(input) != 0;

    private static uint SelectAvailableInstantGcd(BlmResolverInput input)
        => input.Context.IsAoeMode
            ? SelectAvailableAoeInstantGcd(input, transposeFill: false)
            : SelectAvailableSingleTargetInstantGcd(input);

    internal static uint SelectAvailableAoeInstantGcdForTranspose(
        BlmResolverInput input)
        => SelectAvailableAoeInstantGcd(input, transposeFill: true);

    private static uint SelectAvailableSingleTargetInstantGcd(
        BlmResolverInput input)
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

            if (context.InIce)
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

    private static uint SelectAvailableAoeInstantGcd(
        BlmResolverInput input,
        bool transposeFill)
    {
        var context = input.Context;
        var target = Level100ResolverFacts.CurrentTarget(input);
        if (transposeFill
            && context.Level >= 80
            && context.PolyglotStacks >= 1
            && IsActionUnlocked(input, BLMSkill.秽浊))
        {
            return Level100ResolverFacts.EffectiveActionId(input, BLMSkill.秽浊);
        }

        if (ShouldUseAoeThunder(input, target, 3500))
        {
            return Level100ResolverFacts.EffectiveActionId(input, BLMSkill.震雷);
        }

        if (context.IsTwoTargetAoe
            && context.InFire
            && context.AstralFireStacks < 3
            && context.HasFirestarter)
        {
            return BLMSkill.爆炎;
        }

        if (context.HasParadox)
        {
            if (context.InFire && context.Mp >= 4100)
            {
                return BLMSkill.悖论;
            }

            if (context.InIce)
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

        if (context.Level >= 80
            && context.PolyglotStacks >= 1
            && input.Settings.MoveXenoglossyEnabled
            && IsActionUnlocked(input, BLMSkill.秽浊))
        {
            return Level100ResolverFacts.EffectiveActionId(input, BLMSkill.秽浊);
        }

        if (ShouldUseAoeThunder(input, target, 6000))
        {
            return Level100ResolverFacts.EffectiveActionId(input, BLMSkill.震雷);
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

            if (CanUseAoeThunderFallback(input))
            {
                return Level100ResolverFacts.EffectiveActionId(input, BLMSkill.震雷);
            }
        }

        return 0;
    }

    private static bool ShouldUseAoeThunder(
        BlmResolverInput input,
        BlmResolverDotTargetFact? target,
        double thresholdMs)
        => input.Context.Level >= 26
            && input.Settings.DotEnabled
            && input.Context.HasThunderhead
            && target is not null
            && !Level100ResolverFacts.IsBelowDotHpThreshold(input, target)
            && target.SingleTargetDotRemainingMs <= thresholdMs
            && target.AoeDotRemainingMs <= thresholdMs
            && IsActionUnlocked(input, BLMSkill.震雷);

    private static bool CanUseAoeThunderFallback(BlmResolverInput input)
        => input.Context.Level >= 26
            && input.Settings.DotEnabled
            && input.Context.HasThunderhead
            && IsActionUnlocked(input, BLMSkill.震雷);

    private static bool IsActionUnlocked(BlmResolverInput input, uint actionId)
        => Level100ResolverFacts.Action(input, actionId)
            is { IsUnlocked: true };

    private static bool IsAreaAoeAction(uint actionId)
        => actionId is BLMSkill.烈炎
            or BLMSkill.冰冻
            or BLMSkill.玄冰
            or BLMSkill.核爆
            or BLMSkill.秽浊
            or BLMSkill.震雷
            or BLMSkill.霹雷
            or BLMSkill.高烈炎
            or BLMSkill.高冰冻
            or BLMSkill.高震雷
            or BLMSkill.耀星;
}
