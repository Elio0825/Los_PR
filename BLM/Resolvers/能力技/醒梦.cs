namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckLucidDreaming(BlmResolverInput input)
    {
        var lucid = Level100ResolverFacts.Action(input, MageUniversalSkill.醒梦);
        if (lucid is null || lucid.CooldownRemainMs > 0d)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        return input.Settings.TtkEnabled
            ? Self(input, MageUniversalSkill.醒梦, 1)
            : BlmResolverCheckResult.Reject(-99);
    }
}
