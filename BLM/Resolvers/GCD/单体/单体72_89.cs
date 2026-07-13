namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel72SingleTarget(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 72 or >= 90)
        {
            return BlmResolverCheckResult.Reject(-72);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel72SingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : Gcd(input, actionId, (int)actionId);
    }

    private static uint SelectLevel72SingleTargetGcd(BlmResolverInput input)
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

            return context.UmbralHearts < 3
                ? BLMSkill.冰澈
                : BLMSkill.爆炎;
        }

        if (!context.InFire)
        {
            return BLMSkill.冰封;
        }

        if (context.Mp < DespairMinMp)
        {
            return BLMSkill.冰封;
        }

        if (context.AstralFireStacks < 3)
        {
            return BLMSkill.爆炎;
        }

        var fire4Count = Math.Max(0, input.Level100Loop.Fire4Count);
        if (fire4Count < 7
            && context.Mp >= FireSpellMpCost(context, 800) + DespairMinMp)
        {
            return BLMSkill.炽炎;
        }

        return context.Mp >= DespairMinMp
            ? BLMSkill.绝望
            : BLMSkill.冰封;
    }
}
