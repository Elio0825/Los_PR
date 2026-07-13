namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel35SingleTarget(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 35 or >= 60)
        {
            return BlmResolverCheckResult.Reject(-35);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel35SingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : Gcd(input, actionId, (int)actionId);
    }

    private static uint SelectLevel35SingleTargetGcd(BlmResolverInput input)
    {
        if (input.SpecialSequenceActive)
        {
            return 0;
        }

        var context = input.Context;
        if (context.InIce)
        {
            if (context.UmbralIceStacks < 3)
            {
                return BLMSkill.冰封;
            }

            return context.Mp < context.MaxMp
                ? BLMSkill.冰结
                : BLMSkill.爆炎;
        }

        if (!context.InFire)
        {
            return BLMSkill.爆炎;
        }

        if (context.AstralFireStacks < 3 || context.HasFirestarter)
        {
            return BLMSkill.爆炎;
        }

        return context.Mp >= FireSpellMpCost(context, 800)
            ? BLMSkill.火炎
            : BLMSkill.冰封;
    }
}
