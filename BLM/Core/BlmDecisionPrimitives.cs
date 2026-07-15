namespace LosPr.BLM.Core;

internal readonly record struct BlmActionSuccess(
    long StateGeneration,
    long Serial,
    uint RequestedId,
    uint AdjustedAtIssue,
    uint ActualAckId,
    uint GlobalSequence,
    long OccurredAtMs,
    long AcknowledgedAtMs,
    bool WasInstant,
    bool IsGcd);

internal readonly record struct BlmIssuedActionMetadata(
    long StateGeneration,
    uint RequestedId,
    uint AdjustedAtIssue,
    long IssuedAtMs,
    uint AckSequenceBaseline,
    long DeadlineAtMs,
    bool WasInstant,
    bool IsGcd);

internal readonly record struct BlmResolverAllowedWeavesInput(
    uint NormalizedLastGcdActionId,
    int Level,
    bool WasConfirmedInstant,
    bool ReducedAnimationLockEnabled = false);

internal static class BlmDecisionPrimitives
{
    public const int DefaultRecentlyUsedWindowMs = 1200;
    public const int AeAssistGcdWindowMs = 2500;
    public const int AeAssistLastGcdOffsetMs = 300;
    public const int AeAssistAbilityQueueToleranceMs = 100;
    public const int AeAssistAbilityRepeatGuardMs = 500;
    public const int AeAssistInstantThresholdMs = 1700;
    public const int AeAssistHasteInstantThresholdMs = 1500;
    public const int DefaultActionQueueWindowMs = 300;
    public const int MinimumActionQueueWindowMs = 50;
    public const int MaximumActionQueueWindowMs = 1000;

    public static bool RecentlyUsed(
        long nowMs,
        long lastSuccessAtMs,
        int withinMs = DefaultRecentlyUsedWindowMs)
        => nowMs - lastSuccessAtMs < withinMs;

    public static int NormalizeActionQueueWindowMs(int actionQueueWindowMs)
        => Math.Clamp(
            actionQueueWindowMs,
            MinimumActionQueueWindowMs,
            MaximumActionQueueWindowMs);

    public static bool AbilityCooldownInNextGcdWindows(
        double cooldownRemainMs,
        int count,
        long nowMs,
        long lastGcdStartedAtMs,
        int actionQueueInMs)
        => nowMs - (lastGcdStartedAtMs + AeAssistLastGcdOffsetMs)
            + cooldownRemainMs
            < AeAssistGcdWindowMs * (count + 1d) - actionQueueInMs;

    public static bool AbilityCooldownInNextGcdWindows(
        BlmActionAvailability action,
        int count,
        long nowMs,
        long lastGcdStartedAtMs,
        int actionQueueInMs)
    {
        ArgumentNullException.ThrowIfNull(action);
        return AbilityCooldownInNextGcdWindows(
            action.CooldownRemainSeconds * 1000d,
            count,
            nowMs,
            lastGcdStartedAtMs,
            actionQueueInMs);
    }

    public static bool AbilityCooldownInNextGcdWindows(
        BlmContext context,
        BlmActionAvailability action,
        int count)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        if (!TryGetCurrentGcdStartedAtMs(
                context.CapturedAtMs,
                context.GcdTotalSeconds,
                context.GcdRemainSeconds,
                out var gcdStartedAtMs))
        {
            gcdStartedAtMs = context.Tracker.LastGcdStartedAtMs;
        }

        return gcdStartedAtMs > 0
            && gcdStartedAtMs <= context.CapturedAtMs
            && AbilityCooldownInNextGcdWindows(
                action,
                count,
                context.CapturedAtMs,
                gcdStartedAtMs,
                context.ActionQueueWindowMs);
    }

    public static bool TryGetCurrentGcdStartedAtMs(
        long capturedAtMs,
        float gcdTotalSeconds,
        float gcdRemainSeconds,
        out long gcdStartedAtMs)
    {
        gcdStartedAtMs = 0;
        if (capturedAtMs < 0
            || !float.IsFinite(gcdTotalSeconds)
            || !float.IsFinite(gcdRemainSeconds)
            || gcdTotalSeconds <= 0f
            || gcdRemainSeconds <= 0f
            || gcdRemainSeconds > gcdTotalSeconds)
        {
            return false;
        }

        var elapsedMs = (gcdTotalSeconds - gcdRemainSeconds) * 1000d;
        if (elapsedMs < 0d || elapsedMs > capturedAtMs)
        {
            return false;
        }

        gcdStartedAtMs = capturedAtMs
            - (long)Math.Round(elapsedMs, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool HasReadyCharge(float charges) => charges >= 1f;

    public static bool HasAnyChargeProgress(float charges) => charges > 0f;

    public static bool IsAbilityReadyWithCanCast(
        bool isUnlocked,
        bool canCast,
        bool recentlyUsed,
        float charges,
        double cooldownRemainMs)
        => isUnlocked
            && canCast
            && !recentlyUsed
            && (HasReadyCharge(charges)
                || cooldownRemainMs <= AeAssistAbilityQueueToleranceMs);

    public static bool HasInstantCast(bool hasSwiftcast, int triplecastStacks)
        => hasSwiftcast || triplecastStacks > 0;

    public static bool WasGcdObservedInstant(float gcdRemainMs, bool hasHaste)
        => gcdRemainMs >= (hasHaste
            ? AeAssistHasteInstantThresholdMs
            : AeAssistInstantThresholdMs);

    public static bool CanWeaveNow(
        bool isCasting,
        float gcdRemainSeconds,
        float animationLockSeconds)
        => !isCasting
            && gcdRemainSeconds > Math.Max(0.6f, animationLockSeconds + 0.05f);

    public static int ExecutorWeaveSlots(
        bool hasConfirmedGcd,
        bool wasConfirmedInstant)
        => !hasConfirmedGcd ? 0 : wasConfirmedInstant ? 2 : 1;

    public static int ResolverAllowedWeaves(BlmResolverAllowedWeavesInput input)
    {
        if (input.NormalizedLastGcdActionId == 0)
        {
            return 0;
        }

        if (input.WasConfirmedInstant
            || input.NormalizedLastGcdActionId is BLMSkill.悖论
                or BLMSkill.高闪雷
                or BLMSkill.高震雷
                or BLMSkill.异言
                or BLMSkill.秽浊
            || (input.NormalizedLastGcdActionId == BLMSkill.绝望
                && input.Level >= 100))
        {
            return 2;
        }

        if (input.NormalizedLastGcdActionId is BLMSkill.爆炎 or BLMSkill.冰封)
        {
            return 1;
        }

        return input.ReducedAnimationLockEnabled ? 1 : 0;
    }

    public static int RemainingResolverAllowedWeaves(
        BlmResolverAllowedWeavesInput input,
        int usedWeaves)
        => Math.Max(0, ResolverAllowedWeaves(input) - Math.Max(0, usedWeaves));
}
