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

        if (context.PolyglotStacks > 0)
        {
            var polyglotAction = context.IsAoeMode || context.Level < 80
                ? BLMSkill.秽浊
                : BLMSkill.异言;
            if (CanUseTtkGcd(input, polyglotAction))
            {
                return context.IsAoeMode
                    ? AoeGcd(input, polyglotAction, (int)polyglotAction)
                    : Gcd(input, polyglotAction, (int)polyglotAction);
            }
        }

        if (context.HasParadox
            && CanUseTtkGcd(input, BLMSkill.悖论))
        {
            return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
        }

        if (context.AstralSoulStacks == 6
            && CanUseTtkGcd(input, BLMSkill.耀星))
        {
            return context.IsAoeMode
                ? AoeGcd(input, BLMSkill.耀星, (int)BLMSkill.耀星)
                : Gcd(input, BLMSkill.耀星, (int)BLMSkill.耀星);
        }

        if (context.Mp >= DespairMinMp
            && context.InFire
            && context.Level >= 100
            && CanUseTtkGcd(input, BLMSkill.绝望))
        {
            return Gcd(input, BLMSkill.绝望, (int)BLMSkill.绝望);
        }

        return BlmResolverCheckResult.Reject(-1);
    }

    private static bool CanUseTtkGcd(
        BlmResolverInput input,
        uint actionId)
        => input.Context.IsAoeMode
            ? IsAoeGcdUnlocked(input, actionId)
            : Level100ResolverFacts.IsReadyWithCanCast(input, actionId);
}
