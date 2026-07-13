namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckTriplecast(BlmResolverInput input)
    {
        var context = input.Context;
        var triplecast = Level100ResolverFacts.Action(input, BLMSkill.三连咏唱);
        if (triplecast is null)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (context.Level < 66)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        if (triplecast.Charges < 1f
            || !Level100ResolverFacts.IsReadyWithCanCast(
                input,
                BLMSkill.三连咏唱))
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.Settings.TtkEnabled)
        {
            return Self(input, BLMSkill.三连咏唱, 999);
        }

        if (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰澈)
            || Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.玄冰))
        {
            return BlmResolverCheckResult.Reject(-10);
        }

        if (input.Settings.MoveTriplecastEnabled
            && context.IsMoving
            && !context.HasInstantCast
            && !context.IsCasting
            && ResolverAllowedWeaves(input) > 0)
        {
            return Self(input, BLMSkill.三连咏唱, 50);
        }

        if (context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.IsAoeMode)
        {
            if (context.Level >= 100
                && context.InFire
                && context.Mp < 800
                && context.AstralSoulStacks == 6)
            {
                return BlmResolverCheckResult.Reject(-22);
            }

            return BlmResolverCheckResult.Reject(-234);
        }

        if (context.InFire)
        {
            var manafont = Level100ResolverFacts.Action(input, BLMSkill.魔泉);
            if (input.Settings.ManafontEnabled
                && ((manafont?.CooldownRemainMs ?? double.MaxValue) < 500d
                    || Level100ResolverFacts.CooldownInNextGcdWindows(
                        input,
                        BLMSkill.魔泉,
                        5)))
            {
                return BlmResolverCheckResult.Reject(-8);
            }

            var swiftcast = Level100ResolverFacts.Action(
                input,
                MageUniversalSkill.即刻咏唱);
            if (context.Mp <= 4400
                && input.Settings.TriplecastIntoIceEnabled
                && context.AstralSoulStacks >= 5
                && (swiftcast?.CooldownRemainMs ?? 0d) > 0d
                && !Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    MageUniversalSkill.即刻咏唱,
                    3))
            {
                return Self(input, BLMSkill.三连咏唱, 1);
            }

            return BlmResolverCheckResult.Reject(-10);
        }

        if (context.InIce && context.UmbralIceStacks < 3)
        {
            if (context.Level < 100)
            {
                return BlmResolverCheckResult.Reject(-100);
            }

            if (!context.IsSingleTargetMode)
            {
                return BlmResolverCheckResult.Reject(-234);
            }

            if (context.HasParadox)
            {
                return BlmResolverCheckResult.Reject(-3);
            }

            if (!input.Settings.TriplecastIntoIceEnabled)
            {
                return BlmResolverCheckResult.Reject(-200);
            }

            return Self(input, BLMSkill.三连咏唱, 2);
        }

        return BlmResolverCheckResult.Reject(-99);
    }
}
