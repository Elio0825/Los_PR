namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
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

        if (context.Level >= 98)
        {
            if (context.PolyglotStacks == 3
                && context.PolyglotTimerMs <= 10_000)
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
        }

        if (context.Level < 98 && context.PolyglotStacks == 2)
        {
            if (context.PolyglotTimerMs < 8000)
            {
                return Gcd(input, BLMSkill.异言, 2);
            }

            if (context.Level >= 86
                && Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.详述,
                    1))
            {
                return Gcd(input, BLMSkill.异言, 3);
            }
        }

        return BlmResolverCheckResult.Reject(-99);
    }
}
