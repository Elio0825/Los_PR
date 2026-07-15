namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel1Aoe(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 12 or >= 35)
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

        var iceAction = BLMSkill.冰冻;
        var fireAction = context.Level >= 18 ? BLMSkill.烈炎 : BLMSkill.火炎;
        var fireCost = FireSpellMpCost(context, context.Level >= 18 ? 1500 : 800);
        uint actionId;
        if (context.InIce)
        {
            actionId = context.Mp < context.MaxMp ? iceAction : fireAction;
        }
        else if (context.InFire)
        {
            actionId = context.Mp >= fireCost ? fireAction : iceAction;
        }
        else if (context.MaxMp > 0 && context.Mp < context.MaxMp * 0.5)
        {
            actionId = iceAction;
        }
        else
        {
            actionId = context.Mp >= fireCost ? fireAction : iceAction;
        }

        return AoeHostGcd(
            input,
            actionId,
            (int)actionId,
            isAreaAction: actionId != BLMSkill.火炎);
    }
}
