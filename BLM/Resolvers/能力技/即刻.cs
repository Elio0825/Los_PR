namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckSwiftcast(BlmResolverInput input)
    {
        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                MageUniversalSkill.即刻咏唱))
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.Context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.Context.Level < 50)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        if (!input.Context.InIce || input.Context.UmbralIceStacks >= 3)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (Level100ResolverFacts.RecentlyUsed(input, BLMSkill.冰封)
            || Level100ResolverFacts.RecentlyUsed(input, BLMSkill.冰冻))
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        return Self(input, MageUniversalSkill.即刻咏唱, 5);
    }
}
