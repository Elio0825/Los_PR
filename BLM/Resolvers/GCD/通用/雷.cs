namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckThunder(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!input.Settings.DotEnabled)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.Settings.TtkEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
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

        if (Level100ResolverFacts.NeedsDot(input, target, 3500)
            && context.HasThunderhead)
        {
            return Gcd(input, BLMSkill.闪雷, 1);
        }

        return BlmResolverCheckResult.Reject(-99);
    }
}
