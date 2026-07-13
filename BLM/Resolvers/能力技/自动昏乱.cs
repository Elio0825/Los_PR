namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckAutoAddle(BlmResolverInput input)
    {
        if (!input.Settings.AutoMitigationEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                MageUniversalSkill.昏乱))
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (!input.Context.HasTarget)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        var manawardReady = Level100ResolverFacts.IsReadyWithCanCast(
            input,
            BLMSkill.魔罩);
        if (!input.DefensiveCast.TargetCastIsDeathSentenceWithin3Seconds
            && !(input.DefensiveCast.TargetCastIsBossAoeWithin3Seconds
                && !manawardReady))
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (input.Context.GcdRemainMs < 100d)
        {
            return BlmResolverCheckResult.Reject(-89);
        }

        return CurrentTarget(input, MageUniversalSkill.昏乱, 0);
    }
}
