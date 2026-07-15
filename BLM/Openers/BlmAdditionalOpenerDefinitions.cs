using System.Collections.Immutable;

namespace LosPr.BLM.Openers;

internal static class BlmAdditionalOpenerDefinitions
{
    public static BlmOpenerPlan BuildCountdown(
        BlmOpenerVariant variant,
        int level,
        bool dotEnabled,
        uint potionId,
        bool noTriplecast = false)
        => variant switch
        {
            BlmOpenerVariant.Level70 when level is >= 70 and <= 79
                => BuildLevel70(dotEnabled, potionId, noTriplecast),
            BlmOpenerVariant.Level80 when level is >= 80 and <= 89
                => BuildLevel80(dotEnabled, potionId, noTriplecast),
            BlmOpenerVariant.Level90 when level is >= 90 and <= 99
                => BuildLevel90(dotEnabled, potionId, noTriplecast),
            BlmOpenerVariant.Standard57 when level == 100
                => BlmOpener57Definition.Build(
                    BlmOpenerMode.HighEndCountdown,
                    dotEnabled,
                    potionId,
                    noTriplecast),
            BlmOpenerVariant.Flare when level == 100
                => BuildLevel100Flare(dotEnabled, potionId, noTriplecast),
            _ => throw new ArgumentOutOfRangeException(
                nameof(variant),
                variant,
                $"等级 {level} 不支持该起手。"),
        };

    public static bool SupportsLevel(BlmOpenerVariant variant, int level)
        => variant switch
        {
            BlmOpenerVariant.Level70 => level is >= 70 and <= 79,
            BlmOpenerVariant.Level80 => level is >= 80 and <= 89,
            BlmOpenerVariant.Level90 => level is >= 90 and <= 99,
            BlmOpenerVariant.Standard57 or BlmOpenerVariant.Flare => level == 100,
            _ => false,
        };

    private static BlmOpenerPlan BuildLevel70(
        bool dotEnabled,
        uint potionId,
        bool noTriplecast)
    {
        var steps = ImmutableArray.CreateBuilder<BlmOpenerStep>(24);
        steps.Add(Gcd("Fire3.Entry", BLMSkill.爆炎, BlmOpenerCheckpoint.FireEntry));
        AddThunder(steps, "Thunder.Open", dotEnabled, BLMSkill.闪雷);
        steps.Add(Gcd("Fire4.Pre.1", BLMSkill.炽炎));
        AddPotion(steps, potionId);
        steps.Add(OffGcd("LeyLines", BLMSkill.黑魔纹, BlmOpenerCheckpoint.LeyLines));
        steps.Add(Gcd("Fire4.Pre.2", BLMSkill.炽炎));
        if (potionId == 0)
        {
            steps.Add(OffGcd(
                "Triplecast.Pre",
                BLMSkill.三连咏唱,
                BlmOpenerCheckpoint.Triplecast));
        }

        steps.Add(Gcd("Fire4.Pre.3", BLMSkill.炽炎));
        steps.Add(Gcd("Fire4.Pre.4", BLMSkill.炽炎));
        steps.Add(OffGcd("Manafont", BLMSkill.魔泉, BlmOpenerCheckpoint.Manafont));
        for (var ordinal = 1; ordinal <= 7; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Post.{ordinal}", BLMSkill.炽炎));
        }

        AddThunder(steps, "Thunder.Refresh", dotEnabled, BLMSkill.闪雷);
        return CreatePlan(
            BlmOpenerVariant.Level70,
            "70–79级高难4+7起手",
            70,
            79,
            4,
            7,
            noTriplecast,
            dotEnabled,
            potionId,
            steps);
    }

    private static BlmOpenerPlan BuildLevel80(
        bool dotEnabled,
        uint potionId,
        bool noTriplecast)
    {
        var steps = ImmutableArray.CreateBuilder<BlmOpenerStep>(28);
        steps.Add(Gcd("Fire3.Entry", BLMSkill.爆炎, BlmOpenerCheckpoint.FireEntry));
        AddThunder(steps, "Thunder.Open", dotEnabled, BLMSkill.闪雷);
        steps.Add(Gcd("Fire4.Pre.1", BLMSkill.炽炎));
        AddPotion(steps, potionId);
        steps.Add(OffGcd("LeyLines", BLMSkill.黑魔纹, BlmOpenerCheckpoint.LeyLines));
        steps.Add(Gcd("Fire4.Pre.2", BLMSkill.炽炎));
        steps.Add(OffGcd(
            "Triplecast.Pre",
            BLMSkill.三连咏唱,
            BlmOpenerCheckpoint.Triplecast));
        steps.Add(Gcd("Fire4.Pre.3", BLMSkill.炽炎));
        steps.Add(Gcd("Fire4.Pre.4", BLMSkill.炽炎));
        steps.Add(Gcd("Despair.Pre", BLMSkill.绝望));
        steps.Add(OffGcd("Manafont", BLMSkill.魔泉, BlmOpenerCheckpoint.Manafont));
        for (var ordinal = 1; ordinal <= 6; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Post.{ordinal}", BLMSkill.炽炎));
        }

        AddThunder(steps, "Thunder.Refresh", dotEnabled, BLMSkill.闪雷);
        if (!noTriplecast)
        {
            steps.Add(OffGcd(
                "Triplecast.Post",
                BLMSkill.三连咏唱,
                BlmOpenerCheckpoint.Triplecast));
        }

        steps.Add(Gcd("Fire4.Post.7", BLMSkill.炽炎));
        steps.Add(Gcd("Despair.Post", BLMSkill.绝望));
        if (!noTriplecast)
        {
            steps.Add(OffGcd(
                "Transpose",
                BLMSkill.星灵移位,
                BlmOpenerCheckpoint.Transpose));
        }

        return CreatePlan(
            BlmOpenerVariant.Level80,
            "80–89级高难4+7起手",
            80,
            89,
            4,
            7,
            noTriplecast,
            dotEnabled,
            potionId,
            steps);
    }

    private static BlmOpenerPlan BuildLevel90(
        bool dotEnabled,
        uint potionId,
        bool noTriplecast)
    {
        var steps = ImmutableArray.CreateBuilder<BlmOpenerStep>(28);
        steps.Add(Gcd("Fire3.Entry", BLMSkill.爆炎, BlmOpenerCheckpoint.FireEntry));
        AddThunder(steps, "Thunder.Open", dotEnabled, BLMSkill.闪雷);
        steps.Add(OffGcd(
            "Triplecast.Pre",
            BLMSkill.三连咏唱,
            BlmOpenerCheckpoint.Triplecast));
        steps.Add(OffGcd("Amplifier", BLMSkill.详述));
        steps.Add(Gcd("Fire4.Pre.1", BLMSkill.炽炎));
        AddPotion(steps, potionId);
        steps.Add(OffGcd("LeyLines", BLMSkill.黑魔纹, BlmOpenerCheckpoint.LeyLines));
        for (var ordinal = 2; ordinal <= 4; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Pre.{ordinal}", BLMSkill.炽炎));
        }

        steps.Add(Gcd("Despair.Pre", BLMSkill.绝望));
        steps.Add(Gcd("Xenoglossy", BLMSkill.异言));
        steps.Add(OffGcd("Manafont", BLMSkill.魔泉, BlmOpenerCheckpoint.Manafont));
        for (var ordinal = 1; ordinal <= 6; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Post.{ordinal}", BLMSkill.炽炎));
        }

        AddThunder(steps, "Thunder.Refresh", dotEnabled, BLMSkill.闪雷);
        steps.Add(Gcd("Paradox", BLMSkill.悖论));
        if (!noTriplecast)
        {
            steps.Add(OffGcd(
                "Triplecast.Post",
                BLMSkill.三连咏唱,
                BlmOpenerCheckpoint.Triplecast));
        }

        steps.Add(Gcd("Despair.Post", BLMSkill.绝望));
        if (!noTriplecast)
        {
            steps.Add(OffGcd(
                "Transpose",
                BLMSkill.星灵移位,
                BlmOpenerCheckpoint.Transpose));
        }

        return CreatePlan(
            BlmOpenerVariant.Level90,
            "90–99级高难4+6起手",
            90,
            99,
            4,
            6,
            noTriplecast,
            dotEnabled,
            potionId,
            steps);
    }

    private static BlmOpenerPlan BuildLevel100Flare(
        bool dotEnabled,
        uint potionId,
        bool noTriplecast)
    {
        var steps = ImmutableArray.CreateBuilder<BlmOpenerStep>(32);
        steps.Add(Gcd("Fire3.Entry", BLMSkill.爆炎, BlmOpenerCheckpoint.FireEntry));
        AddThunder(steps, "Thunder.Open", dotEnabled, BLMSkill.高闪雷);
        steps.Add(OffGcd(
            "Swiftcast",
            MageUniversalSkill.即刻咏唱,
            BlmOpenerCheckpoint.Swiftcast));
        steps.Add(OffGcd("Amplifier", BLMSkill.详述));
        steps.Add(Gcd("Fire4.Pre.1", BLMSkill.炽炎));
        AddPotion(steps, potionId);
        steps.Add(OffGcd("LeyLines", BLMSkill.黑魔纹, BlmOpenerCheckpoint.LeyLines));
        steps.Add(Gcd("Fire4.Pre.2", BLMSkill.炽炎));
        steps.Add(Gcd("Xenoglossy", BLMSkill.异言));
        steps.Add(Gcd("Fire4.Pre.3", BLMSkill.炽炎));
        steps.Add(Gcd("Fire4.Pre.4", BLMSkill.炽炎));
        steps.Add(Gcd("Despair.Pre", BLMSkill.绝望));
        steps.Add(OffGcd("Manafont", BLMSkill.魔泉, BlmOpenerCheckpoint.Manafont));
        steps.Add(Gcd("Fire4.Post.1", BLMSkill.炽炎));
        steps.Add(Gcd("Fire4.Post.2", BLMSkill.炽炎));
        steps.Add(Gcd("FlareStar.1", BLMSkill.耀星));
        steps.Add(Gcd("Fire4.Post.3", BLMSkill.炽炎));
        AddThunder(steps, "Thunder.Refresh", dotEnabled, BLMSkill.高闪雷);
        for (var ordinal = 4; ordinal <= 6; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Post.{ordinal}", BLMSkill.炽炎));
        }

        steps.Add(Gcd("Paradox", BLMSkill.悖论));
        if (!noTriplecast)
        {
            steps.Add(OffGcd(
                "Triplecast",
                BLMSkill.三连咏唱,
                BlmOpenerCheckpoint.Triplecast));
        }

        steps.Add(Gcd("Flare", BLMSkill.核爆));
        steps.Add(Gcd("FlareStar.2", BLMSkill.耀星));
        if (noTriplecast)
        {
            steps.Add(Gcd("Blizzard3.Exit", BLMSkill.冰封, BlmOpenerCheckpoint.IceEntry));
        }
        else
        {
            steps.Add(OffGcd(
                "Transpose",
                BLMSkill.星灵移位,
                BlmOpenerCheckpoint.Transpose));
        }

        return CreatePlan(
            BlmOpenerVariant.Flare,
            "100级高难核爆起手",
            100,
            100,
            4,
            6,
            noTriplecast,
            dotEnabled,
            potionId,
            steps);
    }

    private static BlmOpenerPlan CreatePlan(
        BlmOpenerVariant variant,
        string displayName,
        int minimumLevel,
        int maximumLevel,
        int expectedPreManafontFire4,
        int expectedPostManafontFire4,
        bool noTriplecast,
        bool dotEnabled,
        uint potionId,
        ImmutableArray<BlmOpenerStep>.Builder steps)
        => new()
        {
            Variant = variant,
            Mode = BlmOpenerMode.HighEndCountdown,
            DisplayName = displayName,
            MinimumLevel = minimumLevel,
            MaximumLevel = maximumLevel,
            ExpectedFire4BeforeManafont = expectedPreManafontFire4,
            ExpectedFire4AfterManafont = expectedPostManafontFire4,
            NoTriplecast = noTriplecast,
            DotEnabled = dotEnabled,
            PotionId = potionId,
            Steps = steps.ToImmutable(),
        };

    private static void AddThunder(
        ImmutableArray<BlmOpenerStep>.Builder steps,
        string id,
        bool dotEnabled,
        uint actionId)
    {
        if (dotEnabled)
        {
            steps.Add(Gcd(id, actionId));
        }
    }

    private static void AddPotion(
        ImmutableArray<BlmOpenerStep>.Builder steps,
        uint potionId)
    {
        if (potionId != 0)
        {
            steps.Add(new BlmOpenerStep(
                "Potion",
                potionId,
                BlmOpenerStepKind.Item,
                false));
        }
    }

    private static BlmOpenerStep Gcd(
        string id,
        uint actionId,
        BlmOpenerCheckpoint checkpoint = BlmOpenerCheckpoint.None)
        => new(id, actionId, BlmOpenerStepKind.Gcd, true, checkpoint);

    private static BlmOpenerStep OffGcd(
        string id,
        uint actionId,
        BlmOpenerCheckpoint checkpoint = BlmOpenerCheckpoint.None)
        => new(id, actionId, BlmOpenerStepKind.OffGcd, false, checkpoint);
}
