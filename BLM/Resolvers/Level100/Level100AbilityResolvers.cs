namespace LosPr.BLM.Resolvers.Level100;

internal static class Level100AbilityResolvers
{
    private const double TransposeHoldWindowMs = 2000d;

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

    public static bool ShouldHoldGcdForTranspose(BlmResolverInput input)
    {
        var transpose = Level100ResolverFacts.Action(input, BLMSkill.星灵移位);
        return transpose is { IsUnlocked: true }
            && transpose.CooldownRemainMs <= TransposeHoldWindowMs
            && IsSingleTargetTransposeRouteRequired(input);
    }

    private static BlmResolverCheckResult CheckTranspose(BlmResolverInput input)
    {
        var transpose = Level100ResolverFacts.Action(input, BLMSkill.星灵移位);
        if (transpose is null || !transpose.IsUnlocked)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!input.Context.InFire && !input.Context.InIce)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (transpose.CooldownRemainMs > 0d)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        var checkCode = CheckSingleTargetTranspose(input);
        return checkCode < 0
            ? BlmResolverCheckResult.Reject(checkCode)
            : Self(input, BLMSkill.星灵移位, checkCode);
    }

    private static int CheckSingleTargetTranspose(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 90)
        {
            return -90;
        }

        if (input.Settings.TtkEnabled)
        {
            if (context.InFire && context.Mp < 800)
            {
                return 88;
            }

            if (context.InIce && !context.HasParadox)
            {
                return 99;
            }
        }

        if (context.InFire)
        {
            if (ShouldDeferTransposeToManafont(input))
            {
                return -66;
            }

            if (context.Mp >= 800)
            {
                return -3;
            }

            if (context.AstralSoulStacks == 6)
            {
                return -4;
            }

            if (!HasFireToIceInstantAssurance(input))
            {
                return -5;
            }

            return 4;
        }

        if (context.InIce)
        {
            var readyToLeaveIce = context.UmbralIceStacks == 3
                && context.UmbralHearts == 3;
            if (context.HasParadox
                && readyToLeaveIce
                && !input.Settings.SkipIceParadox)
            {
                return -3;
            }

            if (context.UmbralIceStacks != 3)
            {
                return -4;
            }

            if (context.UmbralHearts != 3)
            {
                return -6;
            }

            return 4;
        }

        return -99;
    }

    private static bool IsSingleTargetTransposeRouteRequired(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.IsSingleTargetMode || context.Level < 90)
        {
            return false;
        }

        if (input.Settings.TtkEnabled)
        {
            if (context.InFire && context.Mp < 800)
            {
                return true;
            }

            if (context.InIce && !context.HasParadox)
            {
                return true;
            }
        }

        if (context.InFire)
        {
            return !ShouldDeferTransposeToManafont(input)
                && context.Mp < 800
                && context.AstralSoulStacks != 6
                && HasFireToIceInstantAssurance(input);
        }

        if (!context.InIce)
        {
            return false;
        }

        var readyToLeaveIce = context.UmbralIceStacks == 3
            && context.UmbralHearts == 3;
        return readyToLeaveIce
            && (!context.HasParadox || input.Settings.SkipIceParadox);
    }

    private static bool ShouldDeferTransposeToManafont(BlmResolverInput input)
        => input.Settings.ManafontEnabled
            && (Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.魔泉,
                    input.Context.PolyglotStacks)
                || Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔泉)
                || Level100ResolverFacts.RecentlyUsed(input, BLMSkill.魔泉));

    private static bool HasFireToIceInstantAssurance(BlmResolverInput input)
    {
        if (input.Context.HasInstantCast)
        {
            return true;
        }

        var swiftSoon = Level100ResolverFacts.CooldownInNextGcdWindows(
                input,
                MageUniversalSkill.即刻咏唱,
                1);
        var triplecast = Level100ResolverFacts.Action(input, BLMSkill.三连咏唱);
        var tripleSoon = input.Settings.TriplecastIntoIceEnabled
            && (Level100ResolverFacts.CooldownInNextGcdWindows(
                    input,
                    BLMSkill.三连咏唱,
                    1)
                || BlmDecisionPrimitives.HasAnyChargeProgress(
                    triplecast?.Charges ?? 0f));
        return swiftSoon || tripleSoon;
    }

    private static BlmResolverCheckResult CheckSwiftcast(BlmResolverInput input)
    {
        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                MageUniversalSkill.即刻咏唱))
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (input.Context.HasInstantCast)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.Context.Level < 50)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        if (!input.Context.InIce || input.Context.UmbralIceStacks >= 3)
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (Level100ResolverFacts.RecentlyUsed(input, BLMSkill.冰封)
            || Level100ResolverFacts.RecentlyUsed(input, BLMSkill.冰冻))
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        return Self(input, MageUniversalSkill.即刻咏唱, 5);
    }

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

    private static BlmResolverCheckResult CheckLucidDreaming(BlmResolverInput input)
    {
        var lucid = Level100ResolverFacts.Action(input, MageUniversalSkill.醒梦);
        if (lucid is null || lucid.CooldownRemainMs > 0d)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        return input.Settings.TtkEnabled
            ? Self(input, MageUniversalSkill.醒梦, 1)
            : BlmResolverCheckResult.Reject(-99);
    }

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

    private static BlmResolverCheckResult CheckManafont(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.InFire)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (context.AstralSoulStacks == 6)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (input.SpecialSequenceActive)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (context.AstralFireStacks < 3)
        {
            return BlmResolverCheckResult.Reject(-5);
        }

        if (context.Mp >= 800)
        {
            return BlmResolverCheckResult.Reject(-6);
        }

        if (!input.Settings.ManafontEnabled)
        {
            return BlmResolverCheckResult.Reject(-7);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔泉))
        {
            return BlmResolverCheckResult.Reject(-8);
        }

        if (context.GcdRemainMs < 500d)
        {
            return BlmResolverCheckResult.Reject(-9);
        }

        if (context.UmbralIceStacks >= 3)
        {
            return BlmResolverCheckResult.Reject(-10);
        }

        var allowedWeaves = ResolverAllowedWeaves(input);
        if (allowedWeaves <= 0
            && (Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.核爆)
                || Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.耀星)))
        {
            allowedWeaves = 1;
        }

        if (allowedWeaves <= 0)
        {
            return BlmResolverCheckResult.Reject(-11);
        }

        if ((Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.冰澈)
                || Level100ResolverFacts.PreviousGcdMatches(input, BLMSkill.玄冰))
            && Level100ResolverFacts.LastOgcdActionId(input) == BLMSkill.星灵移位)
        {
            return BlmResolverCheckResult.Reject(-12);
        }

        return Self(input, BLMSkill.魔泉, 1);
    }

    private static BlmResolverCheckResult CheckLeyLines(BlmResolverInput input)
    {
        var context = input.Context;
        if (context.Level < 52)
        {
            return BlmResolverCheckResult.Reject(-80);
        }

        if (!input.Settings.LeyLinesEnabled)
        {
            return BlmResolverCheckResult.Reject(-5);
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
        if (leyLines is null)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (leyLines.Charges < 1f)
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

    private static BlmResolverCheckResult CheckAutoAddle(BlmResolverInput input)
    {
        if (!input.Settings.AutoMitigationEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(
                input,
                MageUniversalSkill.昏乱))
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (!input.Context.HasTarget)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        var manawardReady = Level100ResolverFacts.IsReadyWithCanCast(
            input,
            BLMSkill.魔罩);
        if (!input.DefensiveCast.TargetCastIsDeathSentenceWithin3Seconds
            && !(input.DefensiveCast.TargetCastIsBossAoeWithin3Seconds
                && !manawardReady))
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (input.Context.GcdRemainMs < 100d)
        {
            return BlmResolverCheckResult.Reject(-89);
        }

        return CurrentTarget(input, MageUniversalSkill.昏乱, 0);
    }

    private static BlmResolverCheckResult CheckAutoManaward(BlmResolverInput input)
    {
        if (!input.Settings.AutoMitigationEnabled)
        {
            return BlmResolverCheckResult.Reject(-1);
        }

        if (!Level100ResolverFacts.IsReadyWithCanCast(input, BLMSkill.魔罩))
        {
            return BlmResolverCheckResult.Reject(-99);
        }

        if (!input.Context.HasTarget)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        if (!input.DefensiveCast.TargetCastIsBossAoeWithin3Seconds)
        {
            return BlmResolverCheckResult.Reject(-4);
        }

        if (input.Context.GcdRemainMs < 100d)
        {
            return BlmResolverCheckResult.Reject(-89);
        }

        return Self(input, BLMSkill.魔罩, 0);
    }

    private static BlmResolverCheckResult CheckPotion(BlmResolverInput input)
    {
        if (!input.Settings.PotionEnabled)
        {
            return BlmResolverCheckResult.Reject(-2);
        }

        if (!input.IsPotionAvailable)
        {
            return BlmResolverCheckResult.Reject(-3);
        }

        return new BlmResolverCheckResult(
            0,
            1,
            0,
            BlmResolverTargetKind.Potion);
    }

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
