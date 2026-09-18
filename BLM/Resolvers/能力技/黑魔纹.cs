namespace LosPr.BLM.Resolvers.Level100;

internal static partial class Level100AbilityResolvers
{
    private static BlmResolverCheckResult CheckLeyLines(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 52)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        // 4B 尚无可靠的日常副本/TTK事实；低等级单层黑魔纹生产保守关闭。
        if (context.Level < 90)
        {
            return BlmResolverCheckResult.Reject(-89);
        }

        if (!input.Settings.LeyLinesEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (context.StationaryDurationMs
            < input.Settings.StationaryLeyLinesSeconds * 1000d)
        {
            return BlmResolverCheckResult.Reject(-13);
        }

        if (input.CasualCombat.IsCasualDutyNonBoss && ShouldHoldCasualBurst(input))
        {
            return BlmResolverCheckResult.Reject(-12);
        }

        if (Level100ResolverFacts.RecentlyUsed(input, BLMSkill.黑魔纹, 2000))
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        var leyLines = Level100ResolverFacts.Action(input, BLMSkill.黑魔纹);
        // 与三连咏唱同理：PR 的 CanCast 在充能期间误报 false，
        // 双充能技能以完整充能层数判定就绪。
        if (leyLines is not { IsUnlocked: true }
            || (!leyLines.CanCast
                && !BlmDecisionPrimitives.HasReadyCharge(leyLines.Charges)))
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (leyLines.Charges < 1f
            && leyLines.CooldownRemainMs
                > BlmDecisionPrimitives.AeAssistAbilityQueueToleranceMs)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.CasualCombat.IsCasualDutyNonBoss
            && Math.Floor(leyLines.Charges) <= 1d)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        if (context.HasLeyLinesStatus737)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (context.GcdRemainMs < 500d)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        return Self(input, BLMSkill.黑魔纹, 1);
    }

    private static bool ShouldHoldCasualBurst(BlmResolverInput input)
        => input.CasualCombat.NearbyEnemiesTotalHpRatio is > 0f and < 0.15f
            || input.CasualCombat.NearbyEnemiesAverageTtkMs is > 0f and < 12_000f;
}
