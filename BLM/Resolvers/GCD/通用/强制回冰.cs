namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckForcedIceRecovery(BlmResolverInput input)
    {
        var context = input.Context;
        if (!input.NeedsForcedIceRecovery)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!context.InCombat)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (!context.InFire)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.冰封))
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        return Gcd(input, BLMSkill.冰封, 500);
    }
}
