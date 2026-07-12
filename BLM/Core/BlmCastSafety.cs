namespace LosPr.BLM.Core;

public static class BlmCastSafety
{
    public static bool CanCastNow(
        BlmContext context,
        uint actionId,
        BlmDecisionPolicy policy)
        => !context.IsMoving || CanCastWhileMoving(context, actionId, policy);

    public static bool CanCastWhileMoving(
        BlmContext context,
        uint actionId,
        BlmDecisionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        if (actionId == 0)
        {
            return false;
        }

        if (actionId is BLMSkill.悖论 or BLMSkill.异言 or BLMSkill.高闪雷
            || (actionId == BLMSkill.绝望 && context.Level >= 100)
            || (actionId == BLMSkill.爆炎 && context.HasFirestarter))
        {
            return true;
        }

        var requiredRemaining = RequiredInstantRemainingSeconds(context, policy);
        return (context.HasSwiftcast
                && context.SwiftcastRemainSeconds >= requiredRemaining)
            || (context.TriplecastStacks > 0
                && context.TriplecastRemainSeconds >= requiredRemaining);
    }

    public static float RequiredInstantRemainingSeconds(
        BlmContext context,
        BlmDecisionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);
        var safety = Math.Max(0f, policy.InstantCastSafetySeconds);
        return Math.Max(
            0.75f,
            Math.Max(0f, context.GcdRemainSeconds)
                + Math.Max(0f, context.AnimationLockSeconds)
                + safety);
    }
}
