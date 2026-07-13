namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel1SingleTarget(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 1 or >= 35)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel1SingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : Gcd(input, actionId, (int)actionId);
    }

    private static uint SelectLevel1SingleTargetGcd(BlmResolverInput input)
    {
        if (input.SpecialSequenceActive)
        {
            return 0;
        }

        var context = input.Context;
        if (context.Mp >= context.MaxMp)
        {
            return BLMSkill.火炎;
        }

        return context.InFire && context.Mp >= FireSpellMpCost(context, 800)
            ? BLMSkill.火炎
            : BLMSkill.冰结;
    }
}
