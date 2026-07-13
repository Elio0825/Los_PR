namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckPotion(BlmResolverInput input)
    {
        if (!input.Settings.PotionEnabled)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (!input.IsPotionAvailable)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        return new BlmResolverCheckResult(
            0,
            1,
            0,
            BlmResolverTargetKind.Potion);
    }
}
