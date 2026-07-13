namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    private const int FireParadoxMpCost = 1600;
    private const int DespairMinMp = 800;

    private static bool DotSufficient(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => IsProtectedByDoubleDotMemory(input, target, nowMs)
            || target.MaxOwnedDotRemainingMs > 3000f;

    private static bool DotExists(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => IsProtectedByDoubleDotMemory(input, target, nowMs)
            || target.MaxOwnedDotRemainingMs > 500f;

    private static bool DotMissing(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => target.HasOwnedDot
            ? !DotExists(input, target, nowMs)
            : true;

    private static bool DotExpiring(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => DotExists(input, target, nowMs)
            && !DotSufficient(input, target, nowMs);

    private static bool IsProtectedByDoubleDotMemory(
        BlmResolverInput input,
        BlmResolverDotTargetFact target,
        long nowMs)
        => target.EntityId == input.DoubleDotMemory.LastTargetId
            && nowMs - input.DoubleDotMemory.LastCastAtMs < 3500;

    private static BlmResolverCheckResult Gcd(
        BlmResolverInput input,
        uint requestedActionId,
        int checkCode)
        => new(
            Level100ResolverFacts.EffectiveActionId(input, requestedActionId),
            checkCode,
            input.Context.CurrentTargetId,
            BlmResolverTargetKind.CurrentTarget);
}
