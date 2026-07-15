namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel35Aoe(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 35 or >= 50)
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

        var actionId = SelectLevel35AoeGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : AoeHostGcd(input, actionId, (int)actionId, isAreaAction: true);
    }

    private static uint SelectLevel35AoeGcd(BlmResolverInput input)
    {
        var context = input.Context;
        var fireCost = FireSpellMpCost(context, 1500);
        if (!context.InFire && !context.InIce)
        {
            return context.Mp >= fireCost ? BLMSkill.烈炎 : BLMSkill.冰冻;
        }

        if (context.InFire)
        {
            return context.Mp >= fireCost ? BLMSkill.烈炎 : BLMSkill.冰冻;
        }

        if (context.Level < 40)
        {
            return context.Mp < context.MaxMp ? BLMSkill.冰冻 : BLMSkill.烈炎;
        }

        if (context.Mp < 9000)
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
