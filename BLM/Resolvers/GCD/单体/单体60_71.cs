namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel60SingleTarget(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 60 or >= 72)
        {
            return BlmResolverCheckResult.Reject(-60);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel60SingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : Gcd(input, actionId, (int)actionId);
    }

    private static uint SelectLevel60SingleTargetGcd(BlmResolverInput input)
    {
        if (input.SpecialSequenceActive)
        {
            return 0;
        }

        var context = input.Context;
        if (context.InIce)
        {
            if (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰封))
            {
                return context.UmbralHearts < 3
                    ? BLMSkill.冰澈
                    : BLMSkill.爆炎;
            }

            if (context.UmbralIceStacks < 3)
            {
                return BLMSkill.冰封;
            }

            return context.UmbralHearts < 3
                ? BLMSkill.冰澈
                : BLMSkill.爆炎;
        }

        if (!context.InFire)
        {
            return BLMSkill.冰封;
        }

        if (context.Mp < 800
            && Level100ResolverFacts.LastOgcdActionId(input) != BLMSkill.魔泉)
        {
            return BLMSkill.冰封;
        }

        if (context.AstralFireStacks < 3)
        {
            return BLMSkill.爆炎;
        }

        return context.Mp >= FireSpellMpCost(context, 800)
            ? BLMSkill.炽炎
            : BLMSkill.冰封;
    }
}
