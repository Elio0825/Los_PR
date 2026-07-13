namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckLevel100SingleTarget(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level != 100)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectLevel100SingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : Gcd(input, actionId, (int)actionId);
    }

    private static uint SelectLevel100SingleTargetGcd(BlmResolverInput input)
    {
        if (input.SpecialSequenceActive)
        {
            return 0;
        }

        if (input.Context.InFire)
        {
            return SelectFireGcd(input);
        }

        if (input.Context.InIce)
        {
            return SelectIceGcd(input);
        }

        return BLMSkill.冰封;
    }

    private static uint SelectIceGcd(BlmResolverInput input)
    {
        var context = input.Context;
        if (input.Level100Loop.RecoveringAfterSpecial)
        {
            return Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰封)
                ? BLMSkill.冰澈
                : BLMSkill.冰封;
        }

        if (context.UmbralIceStacks < 3)
        {
            if (context.HasParadox
                && !context.HasInstantCast
                && HasIncomingIceInstant(input))
            {
                return BLMSkill.悖论;
            }

            return BLMSkill.冰封;
        }

        if (context.UmbralHearts < 3)
        {
            return BLMSkill.冰澈;
        }

        if (context.HasParadox)
        {
            if (input.Settings.SkipIceParadox)
            {
                return 0;
            }

            if (!context.HasInstantCast && HasIncomingIceInstant(input))
            {
                return BLMSkill.悖论;
            }

            // PR 不迁移“压缩冰悖论”持久化设置，默认保持 los-ae 非压缩行为。
            return BLMSkill.悖论;
        }

        if (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰封)
            && context.UmbralHearts >= 3
            && context.MaxMp > 0
            && context.Mp < context.MaxMp * 0.95)
        {
            return BLMSkill.冰澈;
        }

        return 0;
    }

    private static bool HasIncomingIceInstant(BlmResolverInput input)
    {
        var swiftSoon = Level100ResolverFacts.CooldownInNextGcdWindows(
                input,
                MageUniversalSkill.即刻咏唱,
                1);
        var triplecast = Level100ResolverFacts.Action(input, BLMSkill.三连咏唱);
        var tripleSoon = input.Settings.TriplecastIntoIceEnabled
            && (Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.三连咏唱,
                    1)
                || BlmDecisionPrimitives.HasAnyChargeProgress(
                    triplecast?.Charges ?? 0f));
        return swiftSoon || tripleSoon;
    }

    private static uint SelectFireGcd(BlmResolverInput input)
    {
        var context = input.Context;
        var (fire4Count, fireParadoxUsed) = SanitizeLoopFacts(input);
        var movingWithInstant = context.IsMoving && context.HasInstantCast;
        var hasParadox = context.HasParadox;

        if (context.AstralFireStacks < 3)
        {
            if (context.HasFirestarter)
            {
                return BLMSkill.爆炎;
            }

            if (hasParadox && context.Mp >= FireParadoxMpCost)
            {
                return BLMSkill.悖论;
            }

            if (context.Mp < DespairMinMp)
            {
                return BLMSkill.冰封;
            }

            return BLMSkill.爆炎;
        }

        var soulFull = context.AstralSoulStacks >= 6;
        var lowMpForFire4 = IsLowMpForFire4(context);
        if (!fireParadoxUsed
            && hasParadox
            && context.Mp >= FireParadoxMpCost
            && fire4Count >= 3
            && fire4Count < 6
            && (!input.Settings.CompressFireParadox || movingWithInstant))
        {
            return BLMSkill.悖论;
        }

        if (!soulFull && fire4Count < 6 && !lowMpForFire4)
        {
            return BLMSkill.炽炎;
        }

        if (input.Settings.CompressFireParadox
            && !fireParadoxUsed
            && hasParadox
            && context.Mp >= FireParadoxMpCost
            && !movingWithInstant)
        {
            return BLMSkill.悖论;
        }

        if (soulFull)
        {
            return BLMSkill.耀星;
        }

        return context.Mp >= DespairMinMp
            ? BLMSkill.绝望
            : BLMSkill.冰封;
    }

    private static (int Fire4Count, bool FireParadoxUsed) SanitizeLoopFacts(
        BlmResolverInput input)
    {
        var context = input.Context;
        var fire4Count = Math.Max(0, input.Level100Loop.Fire4Count);
        var fireParadoxUsed = input.Level100Loop.FireParadoxUsed;
        if (!context.InCombat
            || !context.IsAlive
            || (!context.InFire && !context.InIce)
            || (!context.InFire && (fire4Count > 0 || fireParadoxUsed)))
        {
            fire4Count = 0;
            fireParadoxUsed = false;
        }

        if (fire4Count < context.AstralSoulStacks)
        {
            fire4Count = context.AstralSoulStacks;
        }

        if (context.Mp <= 3000 && !context.InIce)
        {
            fire4Count = 0;
            fireParadoxUsed = false;
        }

        return (fire4Count, fireParadoxUsed);
    }

    private static bool IsLowMpForFire4(BlmResolverContextFacts context)
    {
        var fire4Cost = context.InFire
            && context.AstralFireStacks > 0
            && context.UmbralHearts == 0
                ? 1600
                : 800;
        return context.Mp < fire4Cost + DespairMinMp;
    }
}
