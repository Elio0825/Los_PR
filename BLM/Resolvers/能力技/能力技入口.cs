namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    public static BlmResolverCheckResult Evaluate(
        string resolverId,
        BlmResolverInput input)
        => resolverId switch
        {
            "Ability.星灵移位" => CheckTranspose(input),
            "Ability.即刻" => CheckSwiftcast(input),
            "Ability.三连咏唱" => CheckTriplecast(input),
            "Ability.醒梦" => CheckLucidDreaming(input),
            "Ability.详述" => CheckAmplifier(input),
            "Ability.墨泉" => CheckManafont(input),
            "Ability.黑魔纹" => CheckLeyLines(input),
            "Ability.Auto昏乱" => CheckAutoAddle(input),
            "Ability.Auto魔罩" => CheckAutoManaward(input),
            "Ability.爆发药" => CheckPotion(input),
            _ => BlmResolverCheckResult.Reject(-999),
        };
}
