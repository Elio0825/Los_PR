using System.Linq;

namespace LosPr.BLM.Resolvers.Level100;

internal static class Level100ResolverFacts
{
    public static BlmResolverActionFact? Action(
        BlmResolverInput input,
        uint actionId)
    {
        foreach (var action in input.Actions)
        {
            if (action.RequestedActionId == actionId)
            {
                return action;
            }
        }

        foreach (var action in input.Actions)
        {
            if (action.EffectiveActionId == actionId)
            {
                return action;
            }
        }

        return null;
    }

    public static uint EffectiveActionId(BlmResolverInput input, uint actionId)
        => Action(input, actionId)?.EffectiveActionId ?? actionId;

    public static bool IsReadyWithCanCast(
        BlmResolverInput input,
        uint actionId,
        bool? canCastOverride = null)
    {
        var action = Action(input, actionId);
        return action is not null
            && BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                action.IsUnlocked,
                canCastOverride ?? action.CanCast,
                RecentlyUsed(
                    input,
                    actionId,
                    BlmDecisionPrimitives.AeAssistAbilityRepeatGuardMs),
                action.Charges,
                action.CooldownRemainMs);
    }

    public static bool RecentlyUsed(
        BlmResolverInput input,
        uint actionId,
        int withinMs = BlmDecisionPrimitives.DefaultRecentlyUsedWindowMs)
    {
        foreach (var success in input.RecentHistory)
        {
            if (ActionMatches(input, success, actionId)
                && BlmDecisionPrimitives.RecentlyUsed(
                    input.Context.CapturedAtMs,
                    success.OccurredAtMs,
                    withinMs))
            {
                return true;
            }
        }

        return false;
    }

    public static bool PreviousGcdMatches(BlmResolverInput input, uint actionId)
        => input.PreviousGcd is { IsGcd: true } success
            && ActionMatches(input, success, actionId);

    public static bool ActionMatches(
        BlmResolverInput input,
        BlmActionSuccess success,
        uint actionId)
    {
        var action = Action(input, actionId);
        var effectiveId = action?.EffectiveActionId ?? actionId;
        return success.RequestedId == actionId
            || success.RequestedId == effectiveId
            || success.AdjustedAtIssue == actionId
            || success.AdjustedAtIssue == effectiveId
            || success.ActualAckId == actionId
            || success.ActualAckId == effectiveId;
    }

    public static uint LastOgcdActionId(BlmResolverInput input)
    {
        BlmActionSuccess? latest = null;
        foreach (var success in input.RecentHistory)
        {
            if (success.IsGcd
                || latest is not null
                    && success.AcknowledgedAtMs < latest.Value.AcknowledgedAtMs)
            {
                continue;
            }

            latest = success;
        }

        return latest is null
            ? 0
            : Level100ResolverEngine.EffectiveActionId(latest.Value);
    }

    public static bool CooldownInNextGcdWindows(
        BlmResolverInput input,
        uint actionId,
        int count)
    {
        var action = Action(input, actionId);
        if (action is null || !TryGetCurrentGcdStartedAtMs(input, out var startedAtMs))
        {
            return false;
        }

        return BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
            action.CooldownRemainMs,
            count,
            input.Context.CapturedAtMs,
            startedAtMs,
            BlmDecisionPrimitives.NormalizeActionQueueWindowMs(
                input.Context.ActionQueueWindowMs));
    }

    public static bool TryGetCurrentGcdStartedAtMs(
        BlmResolverInput input,
        out long startedAtMs)
    {
        if (input.PreviousGcd is { IsGcd: true } previous
            && previous.OccurredAtMs > 0
            && previous.OccurredAtMs <= input.Context.CapturedAtMs)
        {
            startedAtMs = previous.OccurredAtMs;
            return true;
        }

        if (BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(
                input.Context.CapturedAtMs,
                input.Context.GcdTotalSeconds,
                input.Context.GcdRemainSeconds,
                out startedAtMs))
        {
            return true;
        }

        return false;
    }

    public static BlmResolverDotTargetFact? CurrentTarget(BlmResolverInput input)
    {
        foreach (var target in input.DotTargets)
        {
            if (target.EntityId == input.Context.CurrentTargetId)
            {
                return target;
            }
        }

        return null;
    }

    public static bool IsBelowDotHpThreshold(
        BlmResolverInput input,
        BlmResolverDotTargetFact target)
        => target.HpRatio < Math.Max(0, input.Settings.DotHpThresholdPercent) / 100f;

    public static bool NeedsDot(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        int thresholdMs)
        => input.Settings.DotEnabled
            && !IsBelowDotHpThreshold(input, target)
            && target.SingleTargetDotRemainingMs <= thresholdMs
            && target.AoeDotRemainingMs <= thresholdMs;

    public static bool IsSingleTargetIceReadyToTranspose(
        BlmResolverInput input)
    {
        var context = input.Context;
        return context.IsSingleTargetMode
            && context.Level is >= 90 and <= 100
            && context.InIce
            && context.UmbralIceStacks == 3
            && (PreviousGcdIsAcknowledgedIceFour(input)
                || context.UmbralHearts == 3
                    && context.MaxMp > 0
                    && context.Mp >= context.MaxMp * 0.95);
    }

    public static bool PreviousGcdIsAcknowledgedIceFour(
        BlmResolverInput input)
        => PreviousGcdMatches(input, BLMSkill.冰澈);
}
