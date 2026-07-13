namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckAutoManaward(BlmResolverInput input)
    {
        if (!input.Settings.AutoMitigationEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔罩))
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (!input.Context.HasTarget)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (!input.DefensiveCast.TargetCastIsBossAoeWithin3Seconds)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (input.Context.GcdRemainMs < 100d)
        {
            return BlmResolverCheckResult.Reject(-89);
        }

        return Self(input, BLMSkill.魔罩, 0);
    }
}
