namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static int ResolverAllowedWeaves(BlmResolverInput input)
    {
        var previous = input.PreviousGcd;
        if (previous is not { IsGcd: true } success)
        {
            return 0;
        }

        return BlmDecisionPrimitives.ResolverAllowedWeaves(
            new BlmResolverAllowedWeavesInput(
                Level100ResolverEngine.EffectiveActionId(success),
                input.Context.Level,
                success.WasInstant,
                input.Settings.ReducedAnimationLockEnabled));
    }

    private static BlmResolverCheckResult Self(
        BlmResolverInput input,
        uint requestedActionId,
        int checkCode)
        => new(
            Level100ResolverFacts.EffectiveActionId(input, requestedActionId),
            checkCode,
            input.Context.PlayerEntityId,
            BlmResolverTargetKind.Self);

    private static BlmResolverCheckResult CurrentTarget(
        BlmResolverInput input,
        uint requestedActionId,
        int checkCode)
        => new(
            Level100ResolverFacts.EffectiveActionId(input, requestedActionId),
            checkCode,
            input.Context.CurrentTargetId,
            BlmResolverTargetKind.CurrentTarget);
}
