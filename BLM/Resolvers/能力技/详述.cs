namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckAmplifier(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 86)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!input.Settings.AmplifierEnabled)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.详述))
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (context.PolyglotStacks == 3)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (context.PolyglotStacks == 2 && context.PolyglotTimerMs < 4000)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (context.GcdRemainMs < 500d)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        return Self(input, BLMSkill.详述, 1);
    }
}
