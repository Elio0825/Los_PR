namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckLeyLines(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 52)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        if (!input.Settings.LeyLinesEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (input.CasualCombat.IsCasualDutyNonBoss && ShouldHoldCasualBurst(input))
        {
            return BlmResolverCheckResult.Reject(-12);
        }

        if (Level100ResolverFacts.RecentlyUsed(input, BLMSkill.黑魔纹, 2000))
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        var leyLines = Level100ResolverFacts.Action(input, BLMSkill.黑魔纹);
        if (leyLines is null)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (leyLines.Charges < 1f)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.CasualCombat.IsCasualDutyNonBoss
            && Math.Floor(leyLines.Charges) <= 1d)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        if (context.HasLeyLinesStatus737)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (context.GcdRemainMs < 500d)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        return Self(input, BLMSkill.黑魔纹, 1);
    }

    private static bool ShouldHoldCasualBurst(BlmResolverInput input)
        => input.CasualCombat.NearbyEnemiesTotalHpRatio is > 0f and < 0.15f
            || input.CasualCombat.NearbyEnemiesAverageTtkMs is > 0f and < 12_000f;
}
