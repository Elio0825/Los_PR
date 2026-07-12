using LosPr.BLM.Core;
using PromeRotation.Data;

namespace Los.Tests;

internal sealed class FakeClock(long initialMs = 1000) : IBlmClock
{
    public long NowMs { get; private set; } = initialMs;

    public void Advance(long milliseconds)
    {
        if (milliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        NowMs += milliseconds;
    }
}

internal sealed class MappingActionIdNormalizer(
    IReadOnlyDictionary<uint, uint>? mappings = null) : IBlmActionIdNormalizer
{
    private readonly IReadOnlyDictionary<uint, uint> _mappings =
        mappings ?? new Dictionary<uint, uint>();

    public uint Normalize(uint actionId)
        => _mappings.TryGetValue(actionId, out var normalized)
            ? normalized
            : actionId;
}

internal static class TestContext
{
    public static BlmContext Base() => new()
    {
        CapturedAtMs = 1000,
        CapturedAtUtc = DateTimeOffset.UtcNow,
        IsAvailable = true,
        AvailabilityText = "测试状态",
        AcrState = AcrState.On,
        PlayerEntityId = 100,
        JobId = 25,
        Level = 100,
        Mp = 10_000,
        MaxMp = 10_000,
        InCombat = true,
        IsAlive = true,
        CanAct = true,
        GcdTotalSeconds = 2.5f,
        HasTarget = true,
        HasValidTarget = true,
        InRange = true,
        TargetEntityId = 200,
        TargetName = "测试目标",
        TargetHp = 1_000_000,
        TargetMaxHp = 1_000_000,
        EnemyCount = 1,
        SingleTargetDot = new BlmDotSnapshot
        {
            StatusId = BlmBuff.高雷Dot,
            RemainingMs = 10_000,
            ExpectedDurationMs = 30_000,
        },
        AoeDot = new BlmDotSnapshot
        {
            StatusId = BlmBuff.高雷二Dot,
            RemainingMs = 10_000,
            ExpectedDurationMs = 24_000,
        },
        MaxPolyglot = 3,
        PolyglotTimerMs = 20_000,
        DotEnabled = false,
        MoveXenoEnabled = true,
        MoveTriplecastEnabled = true,
        ManafontEnabled = true,
        Transpose = ReadyAction(BLMSkill.星灵移位),
    };

    public static BlmActionAvailability ReadyAction(uint actionId) => new()
    {
        ActionId = actionId,
        IsUnlocked = true,
        IsAvailable = true,
        Charges = 1,
        MaxCharges = 1,
    };
}

internal static class AssertEx
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool condition, string message)
        => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{message}。预期={expected}，实际={actual}");
        }
    }
}
