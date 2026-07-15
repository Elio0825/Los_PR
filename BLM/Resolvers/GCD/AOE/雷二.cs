namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckAoeThunder(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 26)
        {
            return BlmResolverCheckResult.Reject(-50);
        }

        if (!HasEligibleAoeTarget(input, allowTwoTargets: true))
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!input.Settings.DotEnabled)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (!context.InIce)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (context.IsCasting)
        {
            return BlmResolverCheckResult.Reject(-30);
        }

        var target = Level100ResolverFacts.CurrentTarget(input);
        if (target is null)
        {
            return BlmResolverCheckResult.Reject(-31);
        }

        if (Level100ResolverFacts.IsBelowDotHpThreshold(input, target))
        {
            return BlmResolverCheckResult.Reject(-32);
        }

        if (!context.HasThunderhead)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        if (!Level100ResolverFacts.NeedsDot(input, target, 3500))
        {
            return BlmResolverCheckResult.Reject(-8);
        }

        return AoeHostGcd(input, BLMSkill.震雷, 1, isAreaAction: true);
    }
}
