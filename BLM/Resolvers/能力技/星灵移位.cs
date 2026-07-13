namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private const double TransposeHoldWindowMs = 2000d;

    public static bool ShouldHoldGcdForTranspose(BlmResolverInput input)
    {
        var transpose = Level100ResolverFacts.Action(input, BLMSkill.星灵移位);
        return transpose is { IsUnlocked: true }
            && transpose.CooldownRemainMs <= TransposeHoldWindowMs
            && IsSingleTargetTransposeRouteRequired(input);
    }

    private static BlmResolverCheckResult CheckTranspose(BlmResolverInput input)
    {
        var transpose = Level100ResolverFacts.Action(input, BLMSkill.星灵移位);
        if (transpose is null || !transpose.IsUnlocked)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!input.Context.InFire && !input.Context.InIce)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (transpose.CooldownRemainMs > 0d)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        var checkCode = CheckSingleTargetTranspose(input);
        return checkCode < 0
            ? BlmResolverCheckResult.Reject(checkCode)
            : Self(input, BLMSkill.星灵移位, checkCode);
    }

    private static int CheckSingleTargetTranspose(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 90)
        {
            return -90;
        }

        if (input.Settings.TtkEnabled)
        {
            if (context.InFire && context.Mp < 800)
            {
                return 88;
            }

            if (context.InIce && !context.HasParadox)
            {
                return 99;
            }
        }

        if (context.InFire && context.Level < 100)
        {
            return -91;
        }

        if (context.InFire)
        {
            if (ShouldDeferTransposeToManafont(input))
            {
                return -66;
            }

            if (context.Mp >= 800)
            {
                return -3;
            }

            if (context.AstralSoulStacks == 6)
            {
                return -4;
            }

            if (!HasFireToIceInstantAssurance(input))
            {
                return -5;
            }

            return 4;
        }

        if (context.InIce)
        {
            var readyToLeaveIce = context.UmbralIceStacks == 3
                && context.UmbralHearts == 3;
            if (context.HasParadox
                && readyToLeaveIce
                && !input.Settings.SkipIceParadox)
            {
                return -3;
            }

            if (context.UmbralIceStacks != 3)
            {
                return -4;
            }

            if (context.UmbralHearts != 3)
            {
                return -6;
            }

            return 4;
        }

        return -99;
    }

    private static bool IsSingleTargetTransposeRouteRequired(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.IsSingleTargetMode || context.Level < 90)
        {
            return false;
        }

        if (input.Settings.TtkEnabled)
        {
            if (context.InFire && context.Mp < 800)
            {
                return true;
            }

            if (context.InIce && !context.HasParadox)
            {
                return true;
            }
        }

        if (context.InFire && context.Level < 100)
        {
            return false;
        }

        if (context.InFire)
        {
            return !ShouldDeferTransposeToManafont(input)
                && context.Mp < 800
                && context.AstralSoulStacks != 6
                && HasFireToIceInstantAssurance(input);
        }

        if (!context.InIce)
        {
            return false;
        }

        var readyToLeaveIce = context.UmbralIceStacks == 3
            && context.UmbralHearts == 3;
        return readyToLeaveIce
            && (!context.HasParadox || input.Settings.SkipIceParadox);
    }

    private static bool ShouldDeferTransposeToManafont(BlmResolverInput input)
        => input.Settings.ManafontEnabled
            && (Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.魔泉,
                    input.Context.PolyglotStacks)
                || Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔泉)
                || Level100ResolverFacts.RecentlyUsed(input, BLMSkill.魔泉));

    private static bool HasFireToIceInstantAssurance(BlmResolverInput input)
    {
        if (input.Context.HasInstantCast)
        {
            return true;
        }

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
}
