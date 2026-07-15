namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckFoul(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 70 || context.PolyglotStacks <= 0)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (context.Level < 80)
        {
            if (context.IsMoving)
            {
                return BlmResolverCheckResult.Reject(-5);
            }

            return Foul(input, 2);
        }

        if (!HasEligibleAoeTarget(input, allowTwoTargets: true))
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (context.IsMoving
            && !context.HasInstantCast
            && input.Settings.MoveXenoglossyEnabled)
        {
            return Foul(input, 700);
        }

        if (input.Settings.DumpPolyglotEnabled)
        {
            return Foul(input, 666);
        }

        if (context.Level >= 98)
        {
            if (context.PolyglotStacks >= 3)
            {
                return Foul(input, 2);
            }

            if (context.PolyglotStacks == 2 && context.PolyglotTimerMs < 8000)
            {
                return Foul(input, 2);
            }

            if (context.PolyglotStacks >= 1
                && Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.详述,
                    1))
            {
                return Foul(input, 3);
            }

            if (context.InFire
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
                        2))
                {
                    return Foul(input, 4);
                }
            }

            return BlmResolverCheckResult.Reject(-99);
        }

        if (context.PolyglotStacks >= 2)
        {
            if (context.PolyglotTimerMs < 8000)
            {
                return Foul(input, 2);
            }

            if (context.Level >= 86
                && Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.详述,
                    1))
            {
                return Foul(input, 3);
            }

            return Foul(input, 2);
        }

        return context.PolyglotTimerMs <= 6000
            ? Foul(input, 2)
            : BlmResolverCheckResult.Reject(-99);
    }

    private static BlmResolverCheckResult Foul(
        BlmResolverInput input,
        int checkCode)
    {
        if (!IsAoeGcdUnlocked(input, BLMSkill.秽浊))
        {
            return BlmResolverCheckResult.Reject(-103);
        }

        return input.Context.IsAoeMode
            ? AoeGcd(input, BLMSkill.秽浊, checkCode)
            : Gcd(input, BLMSkill.秽浊, checkCode);
    }
}
