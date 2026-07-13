namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100SingleTargetResolvers
{
    public static BlmResolverCheckResult Evaluate(
        string resolverId,
        BlmResolverInput input)
        => resolverId switch
        {
            "GCD.TTK" => CheckTtk(input),
            "GCD.快速耀星" => CheckFastFlareStar(input),
            "GCD.强制回冰" => CheckForcedIceRecovery(input),
            "GCD.异言#1" or "GCD.异言#2" => CheckXenoglossy(input),
            "GCD.双DOT" => CheckDoubleDot(input),
            "GCD.雷1" => CheckThunder(input),
            "GCD.瞬发gcd触发器" => CheckInstantGcdTrigger(input),
            "GCD.单体100" => CheckLevel100SingleTarget(input),
            _ => BlmResolverCheckResult.Reject(-999),
        };
}
