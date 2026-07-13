namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckFastFlareStar(BlmResolverInput input)
    {
        var context = input.Context;
        if (!input.Settings.FastFlareStarEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (context.Level < 100)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (!context.InFire)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.AstralFireStacks < 3)
        {
            if (context.HasFirestarter)
            {
                return Gcd(input, BLMSkill.爆炎, (int)BLMSkill.爆炎);
            }

            if (context.HasParadox && context.Mp >= FireParadoxMpCost)
            {
                return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
            }

            return Gcd(input, BLMSkill.爆炎, (int)BLMSkill.爆炎);
        }

        var predictedSoul = Math.Clamp(
            context.AstralSoulStacks
                + (!context.IsCasting ? 0 : input.CurrentCastingActionId switch
                {
                    BLMSkill.炽炎 => 1,
                    BLMSkill.核爆 => 3,
                    _ => 0,
                }),
            0,
            6);

        if (predictedSoul >= 6)
        {
            if (context.HasParadox && context.Mp >= FireParadoxMpCost)
            {
                return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
            }

            return Gcd(input, BLMSkill.耀星, (int)BLMSkill.耀星);
        }

        if (predictedSoul == 3 && context.Mp >= DespairMinMp)
        {
            return Gcd(input, BLMSkill.核爆, (int)BLMSkill.核爆);
        }

        if (predictedSoul == 2
            && context.HasParadox
            && context.Mp >= FireParadoxMpCost)
        {
            return Gcd(input, BLMSkill.悖论, (int)BLMSkill.悖论);
        }

        if (predictedSoul < 6 && !IsLowMpForFire4(context))
        {
            return Gcd(input, BLMSkill.炽炎, (int)BLMSkill.炽炎);
        }

        return BlmResolverCheckResult.Reject(-5);
    }
}
