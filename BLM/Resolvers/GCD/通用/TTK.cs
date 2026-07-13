namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckTtk(BlmResolverInput input)
    {
        var context = input.Context;
        if (!input.Settings.TtkEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (context.PolyglotStacks > 0
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.异言))
        {
            return Gcd(input, BLMSkill.异言, (int)BLMSkill.异言);
        }

        if (context.HasParadox
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.悖论))
        {
            return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
        }

        if (context.AstralSoulStacks == 6
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.耀星))
        {
            return Gcd(input, BLMSkill.耀星, (int)BLMSkill.耀星);
        }

        if (context.Mp >= DespairMinMp
            && context.InFire
            && Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.绝望))
        {
            return Gcd(input, BLMSkill.绝望, (int)BLMSkill.绝望);
        }

        return BlmResolverCheckResult.Reject(-1);
    }
}
