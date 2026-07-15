using System.Collections.Immutable;

namespace LosPr.BLM.Openers;

internal static class BlmOpener57Definition
{
    public const int PrecastRemainingMs = 3500;

    public static BlmOpenerPlan Build(
        BlmOpenerMode mode,
        bool dotEnabled,
        uint potionId,
        bool noTriplecast = false)
    {
        if (mode is not (BlmOpenerMode.HighEndCountdown or BlmOpenerMode.DailyInCombat))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        var steps = ImmutableArray.CreateBuilder<BlmOpenerStep>(32);
        steps.Add(Gcd("Fire3.Entry", BLMSkill.爆炎, BlmOpenerCheckpoint.FireEntry));
        if (dotEnabled)
        {
            steps.Add(Gcd("Thunder.Open", BLMSkill.高闪雷));
        }

        steps.Add(OffGcd("Swiftcast", MageUniversalSkill.即刻咏唱, BlmOpenerCheckpoint.Swiftcast));
        steps.Add(OffGcd("Amplifier", BLMSkill.详述));
        steps.Add(Gcd("Fire4.Pre.1", BLMSkill.炽炎));
        if (mode == BlmOpenerMode.HighEndCountdown && potionId != 0)
        {
            steps.Add(Item("Potion", potionId));
        }

        steps.Add(OffGcd("LeyLines", BLMSkill.黑魔纹, BlmOpenerCheckpoint.LeyLines));
        for (var ordinal = 2; ordinal <= 5; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Pre.{ordinal}", BLMSkill.炽炎));
        }

        steps.Add(Gcd("Xenoglossy", BLMSkill.异言));
        steps.Add(OffGcd("Manafont", BLMSkill.魔泉, BlmOpenerCheckpoint.Manafont));

        steps.Add(Gcd("Fire4.Post.1", BLMSkill.炽炎));
        steps.Add(Gcd("FlareStar.1", BLMSkill.耀星));
        steps.Add(Gcd("Fire4.Post.2", BLMSkill.炽炎));
        steps.Add(Gcd("Fire4.Post.3", BLMSkill.炽炎));
        if (dotEnabled)
        {
            steps.Add(Gcd("Thunder.Refresh", BLMSkill.高闪雷));
        }

        for (var ordinal = 4; ordinal <= 6; ordinal++)
        {
            steps.Add(Gcd($"Fire4.Post.{ordinal}", BLMSkill.炽炎));
        }

        if (!noTriplecast)
        {
            steps.Add(OffGcd("Triplecast", BLMSkill.三连咏唱, BlmOpenerCheckpoint.Triplecast));
        }

        steps.Add(Gcd("Fire4.Post.7", BLMSkill.炽炎));
        steps.Add(Gcd("FlareStar.2", BLMSkill.耀星));
        steps.Add(Gcd("Despair", BLMSkill.绝望));
        steps.Add(OffGcd("Transpose", BLMSkill.星灵移位, BlmOpenerCheckpoint.Transpose));

        return new BlmOpenerPlan
        {
            Variant = BlmOpenerVariant.Standard57,
            Mode = mode,
            DisplayName = mode == BlmOpenerMode.DailyInCombat
                ? "100级日常无药5+7起手"
                : "100级高难5+7起手",
            MinimumLevel = 100,
            MaximumLevel = 100,
            ExpectedFire4BeforeManafont = 5,
            ExpectedFire4AfterManafont = 7,
            NoTriplecast = noTriplecast,
            DotEnabled = dotEnabled,
            PotionId = mode == BlmOpenerMode.HighEndCountdown ? potionId : 0,
            Steps = steps.ToImmutable(),
        };
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

    private static BlmOpenerStep Item(string id, uint actionId)
        => new(id, actionId, BlmOpenerStepKind.Item, false);
}
