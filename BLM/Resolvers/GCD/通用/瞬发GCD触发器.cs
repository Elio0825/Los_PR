namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
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
}
