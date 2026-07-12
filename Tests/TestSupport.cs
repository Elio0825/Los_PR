using LosPr.BLM.Core;

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
