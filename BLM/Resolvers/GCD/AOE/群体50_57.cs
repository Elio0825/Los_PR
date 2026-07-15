namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel50Aoe(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 50 or >= 58)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!HasEligibleAoeTarget(input, allowTwoTargets: false))
        {
            return BlmResolverCheckResult.Reject(-101);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-102);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel50AoeGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : AoeHostGcd(input, actionId, (int)actionId, isAreaAction: true);
    }

    private static uint SelectLevel50AoeGcd(BlmResolverInput input)
    {
        var context = input.Context;
        var fireCost = FireSpellMpCost(context, 1500);
        if (!context.InFire && !context.InIce)
        {
            return context.Mp >= fireCost ? BLMSkill.烈炎 : BLMSkill.冰冻;
        }

        if (context.InFire)
        {
            if (context.Mp >= fireCost)
            {
                return BLMSkill.烈炎;
            }

            return context.Mp >= DespairMinMp ? BLMSkill.核爆 : BLMSkill.冰冻;
        }

        if (context.Mp < context.MaxMp)
        {
            return IsAoeGcdUnlocked(input, BLMSkill.玄冰)
                ? BLMSkill.玄冰
                : BLMSkill.冰冻;
        }

        if (IsAoeGcdUnlocked(input, BLMSkill.烈炎))
        {
            return BLMSkill.烈炎;
        }

        return IsAoeGcdUnlocked(input, BLMSkill.玄冰)
            ? BLMSkill.玄冰
            : BLMSkill.冰冻;
    }
}
