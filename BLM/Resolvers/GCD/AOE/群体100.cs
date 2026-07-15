namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel100Aoe(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level != 100)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!HasEligibleAoeTarget(input, allowTwoTargets: true))
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

        uint actionId;
        var isAreaAction = true;
        if (!context.InFire && !context.InIce)
        {
            actionId = BLMSkill.冰冻;
        }
        else if (context.InIce)
        {
            actionId = Level100AbilityResolvers.PendingAoeIceResourceAction(input);
            isAreaAction = actionId != BLMSkill.冰澈;
        }
        else if (context.AstralSoulStacks >= 6)
        {
            actionId = BLMSkill.耀星;
        }
        else
        {
            actionId = context.Mp < DespairMinMp ? 0 : BLMSkill.核爆;
        }

        return actionId == 0
            ? BlmResolverCheckResult.Reject(-203)
            : AoeHostGcd(input, actionId, (int)actionId, isAreaAction);
    }
}
