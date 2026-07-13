namespace LosPr.BLM.Resolvers.Level100;

internal static class Level90SingleTargetResolvers
{
    private const int FireParadoxMpCost = 1600;
    private const int DespairMinMp = 800;

    public static BlmResolverCheckResult Evaluate(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level is < 90 or >= 100)
        {
            return BlmResolverCheckResult.Reject(-90);
        }

        if (!context.IsSingleTargetMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.IsMoving && !context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        var actionId = SelectSingleTargetGcd(input);
        return actionId == 0
            ? BlmResolverCheckResult.Reject(-1)
            : new BlmResolverCheckResult(
                Level100ResolverFacts.EffectiveActionId(input, actionId),
                (int)actionId,
                context.CurrentTargetId,
                BlmResolverTargetKind.CurrentTarget);
    }

    private static uint SelectSingleTargetGcd(BlmResolverInput input)
    {
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

            // PR 不迁移“压缩冰悖论”持久化设置，默认保持 los-ae 非压缩行为。
            return BLMSkill.悖论;
        }

        if (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰封)
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
        var (fire4Count, fireParadoxUsed) = ProjectLoopFacts(input);
        var movingWithInstant = context.IsMoving && context.HasInstantCast;

        if (context.AstralFireStacks < 3)
        {
            if (context.HasFirestarter)
            {
                return BLMSkill.爆炎;
            }

            if (context.HasParadox && context.Mp >= FireParadoxMpCost)
            {
                return BLMSkill.悖论;
            }

            return context.Mp < DespairMinMp
                ? BLMSkill.冰封
                : BLMSkill.爆炎;
        }

        if (!fireParadoxUsed
            && context.HasParadox
            && context.Mp >= FireParadoxMpCost
            && fire4Count is >= 3 and < 6
            && (!input.Settings.CompressFireParadox || movingWithInstant))
        {
            return BLMSkill.悖论;
        }

        if (fire4Count < 6 && !IsLowMpForFire4(context))
        {
            return BLMSkill.炽炎;
        }

        if (input.Settings.CompressFireParadox
            && !fireParadoxUsed
            && context.HasParadox
            && context.Mp >= FireParadoxMpCost
            && !movingWithInstant)
        {
            return BLMSkill.悖论;
        }

        return context.Mp >= DespairMinMp
            ? BLMSkill.绝望
            : BLMSkill.冰封;
    }

    private static (int Fire4Count, bool FireParadoxUsed) ProjectLoopFacts(
        BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.InCombat || !context.IsAlive || !context.InFire)
        {
            return (0, false);
        }

        var fallback = (
            Math.Max(0, input.Level100Loop.Fire4Count),
            input.Level100Loop.FireParadoxUsed);
        var projectedFire4Count = 0;
        for (var index = input.RecentHistory.Length - 1; index >= 0; index--)
        {
            var success = input.RecentHistory[index];
            if (success.StateGeneration != input.StateGeneration)
            {
                continue;
            }

            if (!success.IsGcd)
            {
                continue;
            }

            if (Level100ResolverFacts.ActionMatches(input, success, BLMSkill.炽炎))
            {
                projectedFire4Count++;
                continue;
            }

            if (Level100ResolverFacts.ActionMatches(input, success, BLMSkill.悖论))
            {
                return (Math.Min(projectedFire4Count, fallback.Item1), fallback.Item2);
            }

            if (Level100ResolverFacts.ActionMatches(input, success, BLMSkill.爆炎)
                || Level100ResolverFacts.ActionMatches(input, success, BLMSkill.绝望)
                || Level100ResolverFacts.ActionMatches(input, success, BLMSkill.冰封))
            {
                return (Math.Min(projectedFire4Count, fallback.Item1), false);
            }
        }

        if (input.PreviousGcd is { IsGcd: true } previous
            && previous.StateGeneration == input.StateGeneration)
        {
            if (Level100ResolverFacts.ActionMatches(input, previous, BLMSkill.悖论))
            {
                return (0, fallback.Item2);
            }

            if (Level100ResolverFacts.ActionMatches(input, previous, BLMSkill.爆炎)
                || Level100ResolverFacts.ActionMatches(input, previous, BLMSkill.绝望)
                || Level100ResolverFacts.ActionMatches(input, previous, BLMSkill.冰封))
            {
                return (0, false);
            }
        }

        return fallback;
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
