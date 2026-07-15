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
            "GCD.秽浊" => CheckFoul(input),
            "GCD.双DOT" => CheckDoubleDot(input),
            "GCD.雷1" => CheckThunder(input),
            "GCD.雷2" => CheckAoeThunder(input),
            "GCD.瞬发gcd触发器" => CheckInstantGcdTrigger(input),
            "GCD.群体100" => CheckLevel100Aoe(input),
            "GCD.群体58_99" => CheckLevel58Aoe(input),
            "GCD.群体50_57" => CheckLevel50Aoe(input),
            "GCD.群体35_49" => CheckLevel35Aoe(input),
            "GCD.群体1_34" => CheckLevel1Aoe(input),
            "GCD.单体100" => CheckLevel100SingleTarget(input),
            "GCD.单体90_99" => Level90SingleTargetResolvers.Evaluate(input),
            "GCD.单体72_89" => CheckLevel72SingleTarget(input),
            "GCD.单体60_71" => CheckLevel60SingleTarget(input),
            "GCD.单体35_59" => CheckLevel35SingleTarget(input),
            "GCD.单体1_34" => CheckLevel1SingleTarget(input),
            _ => BlmResolverCheckResult.Reject(-999),
        };
}
