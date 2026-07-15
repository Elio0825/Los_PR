using System.Linq;

namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private static BlmResolverCheckResult CheckDoubleDot(BlmResolverInput input)
    {
        var context = input.Context;
        var settings = input.Settings;
        if (context.IsAoeMode)
        {
            return BlmResolverCheckResult.Reject(-234);
        }

        if (!settings.DotEnabled || !settings.DoubleDotEnabled)
        {
            return BlmResolverCheckResult.Reject(-100);
        }

        if (settings.TtkEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.IsCasting)
        {
            return BlmResolverCheckResult.Reject(-30);
        }

        if (!context.HasThunderhead)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!input.CanSpecifyDotTarget)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        var dotActionId = Level100ResolverFacts.EffectiveActionId(input, BLMSkill.闪雷);
        if (dotActionId == 0)
        {
            return BlmResolverCheckResult.Reject(-8);
        }

        var minimumHpRatio = Math.Max(0, settings.DotHpThresholdPercent) / 100f;
        var targets = input.DotTargets
            .Where(static target => target.IsValid)
            .Where(static target => target.IsTargetable && target.IsAlive)
            .Where(static target => target.CanUseAttackActionOn && target.IsInDotRange)
            .Where(static target => !target.IsBlacklisted)
            .Where(target => target.HpRatio >= minimumHpRatio)
            .ToList();
        if (targets.Count < 2)
        {
            return BlmResolverCheckResult.Reject(-200);
        }

        var nowMs = context.CapturedAtMs;
        if (targets.Count(target => DotSufficient(input, target, nowMs)) >= 2)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        var candidates = targets
            .Where(target => DotMissing(input, target, nowMs)
                || DotExpiring(input, target, nowMs))
            .ToList();
        if (candidates.Count == 0)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        var currentTarget = targets.FirstOrDefault(
            target => target.EntityId == context.CurrentTargetId);
        if (currentTarget is { IsValid: true }
            && DotSufficient(input, currentTarget, nowMs)
            && candidates.Count > 1)
        {
            candidates.RemoveAll(target => target.EntityId == currentTarget.EntityId);
        }

        var nonRepeat = candidates
            .Where(target => target.EntityId != input.DoubleDotMemory.LastTargetId
                || nowMs - input.DoubleDotMemory.LastCastAtMs >= 2500)
            .ToList();
        var pickFrom = nonRepeat.Count > 0 ? nonRepeat : candidates;
        var chosen = pickFrom
            .OrderByDescending(target => DotMissing(input, target, nowMs) ? 1 : 0)
            .ThenByDescending(static target => target.MaxHp)
            .ThenByDescending(static target => target.CurrentHp)
            .ThenBy(static target => target.Distance)
            .FirstOrDefault();
        if (chosen is null)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                BLMSkill.闪雷,
                chosen.CanCastDot))
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        return new BlmResolverCheckResult(
            dotActionId,
            35,
            chosen.EntityId,
            BlmResolverTargetKind.SpecifiedTarget);
    }
}
