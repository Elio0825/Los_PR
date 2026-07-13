namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckManafont(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.InFire)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (context.AstralSoulStacks == 6)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.AstralFireStacks < 3)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (context.Mp >= 800)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!input.Settings.ManafontEnabled)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔泉))
        {
            return BlmResolverCheckResult.Reject(-8);
        }

        if (context.UmbralIceStacks >= 3)
        {
            return BlmResolverCheckResult.Reject(-10);
        }

        var allowedWeaves = ResolverAllowedWeaves(input);
        if (allowedWeaves <= 0
            && (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.核爆)
                || Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.耀星)))
        {
            allowedWeaves = 1;
        }

        if (allowedWeaves <= 0)
        {
            return BlmResolverCheckResult.Reject(-11);
        }

        if ((Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰澈)
                || Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.玄冰))
            && Level100ResolverFacts.LastOgcdActionId(input) == BLMSkill.星灵移位)
        {
            return BlmResolverCheckResult.Reject(-12);
        }

        return Self(input, BLMSkill.魔泉, 1);
    }

    internal static bool ShouldBridgeManafontThroughAlways(
        BlmResolverInput input,
        BlmResolverCandidate? gcdCandidate,
        BlmResolverCandidate? offGcdCandidate)
    {
        if (offGcdCandidate is not { ResolverId: "Ability.墨泉" })
        {
            return false;
        }

        if (gcdCandidate is null
            || !gcdCandidate.ResolverId.StartsWith(
                "GCD.单体",
                StringComparison.Ordinal))
        {
            return false;
        }

        var iceReturnAction = input.Context.Level < 35
            ? BLMSkill.冰结
            : BLMSkill.冰封;
        if (gcdCandidate.ActionId
            != Level100ResolverFacts.EffectiveActionId(input, iceReturnAction))
        {
            return false;
        }

        var context = input.Context;
        return !BlmDecisionPrimitives.CanWeaveNow(
            context.IsCasting,
            context.GcdRemainSeconds,
            context.AnimationLockSeconds);
    }
}
