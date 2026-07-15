namespace LosPr.BLM.Resolvers.Level100;

internal enum BlmAoeTransposeDirection
{
    None,
    ToFire,
    ToIce,
}

internal enum BlmAoeTransposeDisposition
{
    None,
    CastNow,
    Hold,
    Fill,
    DeferToManafont,
}

internal readonly record struct BlmAoeTransposePlan(
    BlmAoeTransposeDirection Direction,
    BlmAoeTransposeDisposition Disposition,
    uint FillActionId)
{
    public static BlmAoeTransposePlan None { get; } = new(
        BlmAoeTransposeDirection.None,
        BlmAoeTransposeDisposition.None,
        0);
}

internal static partial class Level100AbilityResolvers
{
    private const double TransposeHoldWindowMs = 2000d;

    public static bool ShouldHoldGcdForTranspose(BlmResolverInput input)
    {
        if (input.Context.IsAoeMode)
        {
            var plan = BuildAoeTransposePlan(input);
            return plan.Disposition is BlmAoeTransposeDisposition.CastNow
                or BlmAoeTransposeDisposition.Hold;
        }

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

        if (input.Context.IsAoeMode)
        {
            var plan = BuildAoeTransposePlan(input);
            return plan.Disposition == BlmAoeTransposeDisposition.CastNow
                ? Self(input, BLMSkill.星灵移位, 1)
                : BlmResolverCheckResult.Reject(-1);
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

    internal static BlmAoeTransposePlan BuildAoeTransposePlan(
        BlmResolverInput input)
    {
        var context = input.Context;
        var transpose = Level100ResolverFacts.Action(input, BLMSkill.星灵移位);
        if (!context.IsAoeMode
            || context.Level < 58
            || transpose is not { IsUnlocked: true })
        {
            return BlmAoeTransposePlan.None;
        }

        var direction = BlmAoeTransposeDirection.None;
        if (context.InIce && PendingAoeIceResourceAction(input) == 0)
        {
            direction = BlmAoeTransposeDirection.ToFire;
        }
        else if (context.InFire
            && context.Mp < 800
            && (context.Level < 100 || context.AstralSoulStacks != 6))
        {
            direction = BlmAoeTransposeDirection.ToIce;
        }

        if (direction == BlmAoeTransposeDirection.None)
        {
            return BlmAoeTransposePlan.None;
        }

        if (direction == BlmAoeTransposeDirection.ToIce
            && ShouldDeferAoeTransposeToManafont(input))
        {
            return new BlmAoeTransposePlan(
                direction,
                BlmAoeTransposeDisposition.DeferToManafont,
                0);
        }

        if (transpose.CooldownRemainMs <= 0d)
        {
            return new BlmAoeTransposePlan(
                direction,
                BlmAoeTransposeDisposition.CastNow,
                0);
        }

        if (transpose.CooldownRemainMs <= TransposeHoldWindowMs)
        {
            return new BlmAoeTransposePlan(
                direction,
                BlmAoeTransposeDisposition.Hold,
                0);
        }

        var fillActionId =
            Level100SingleTargetResolvers.SelectAvailableAoeInstantGcdForTranspose(input);
        return new BlmAoeTransposePlan(
            direction,
            fillActionId == 0
                ? BlmAoeTransposeDisposition.Hold
                : BlmAoeTransposeDisposition.Fill,
            fillActionId);
    }

    internal static uint PendingAoeIceResourceAction(BlmResolverInput input)
    {
        var context = input.Context;
        if (!context.IsAoeMode || !context.InIce || context.Level < 58)
        {
            return 0;
        }

        var actionId = context.Level >= 100 && context.IsTwoTargetAoe
            ? BLMSkill.冰澈
            : BLMSkill.玄冰;
        return context.UmbralHearts == 3
            || Level100ResolverFacts.PreviousGcdMatches(input, actionId)
            || Level100ResolverFacts.RecentlyUsed(input, actionId, 2500)
                ? 0
                : actionId;
    }

    private static bool ShouldDeferAoeTransposeToManafont(BlmResolverInput input)
    {
        if (!input.Settings.ManafontEnabled)
        {
            return false;
        }

        var manafontReady = Level100ResolverFacts.IsReadyWithCanCast(
            input,
            BLMSkill.魔泉);
        if (manafontReady)
        {
            return !input.IsIdle || input.Context.GcdRemainMs >= 500d;
        }

        return Level100ResolverFacts.CooldownInNextGcdWindows(
                input,
                BLMSkill.魔泉,
                2)
            || Level100ResolverFacts.RecentlyUsed(input, BLMSkill.魔泉);
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

            if (context.InIce
                && !context.HasParadox
                && Level100ResolverFacts.IsSingleTargetIceReadyToTranspose(input))
            {
                return 99;
            }
        }

        if (context.InFire && context.Level < 100)
        {
            return -91;
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
            var readyToLeaveIce =
                Level100ResolverFacts.IsSingleTargetIceReadyToTranspose(input);
            if (context.HasParadox && readyToLeaveIce)
            {
                return -3;
            }

            return readyToLeaveIce ? 4 : -4;
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

            if (context.InIce
                && !context.HasParadox
                && Level100ResolverFacts.IsSingleTargetIceReadyToTranspose(input))
            {
                return true;
            }
        }

        if (context.InFire && context.Level < 100)
        {
            return false;
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

        var readyToLeaveIce =
            Level100ResolverFacts.IsSingleTargetIceReadyToTranspose(input);
        return readyToLeaveIce && !context.HasParadox;
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
}
