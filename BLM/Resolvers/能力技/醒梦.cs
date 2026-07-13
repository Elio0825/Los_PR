namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckLucidDreaming(BlmResolverInput input)
    {
        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                MageUniversalSkill.醒梦))
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        return input.Settings.TtkEnabled
            ? Self(input, MageUniversalSkill.醒梦, 1)
            : BlmResolverCheckResult.Reject(-99);
    }
}
