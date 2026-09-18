namespace LosPr.BLM.Resolvers;

internal readonly record struct BlmMotionRuntimeObservation(
    long StateGeneration,
    long CapturedAtMs,
    bool IsAvailable,
    bool AcrEnabled,
    bool InCombat,
    bool IsAlive,
    bool IsMoving,
    long LastGcdReadyAtMs);

internal sealed record BlmMotionRuntimeState
{
    public static BlmMotionRuntimeState Empty { get; } = new();

    public long StateGeneration { get; init; }
    public long ValidSinceMs { get; init; }
    public long MovingSinceMs { get; init; }
    public long StationarySinceMs { get; init; }
    public bool IsMoving { get; init; }
    public double MovingDurationMs { get; init; }
    public double StationaryDurationMs { get; init; }
    public double GcdStarvationMs { get; init; }
}

internal sealed class BlmMotionRuntimeMemory
{
    private readonly object _gate = new();
    private BlmMotionRuntimeState _state = BlmMotionRuntimeState.Empty;

    public BlmMotionRuntimeState Advance(BlmMotionRuntimeObservation observation)
    {
        lock (_gate)
        {
            _state = Reduce(_state, observation);
            return _state;
        }
    }

    public BlmMotionRuntimeState GetSnapshot()
    {
        lock (_gate)
        {
            return _state;
        }
    }

    public static BlmMotionRuntimeState Reduce(
        BlmMotionRuntimeState state,
        BlmMotionRuntimeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(state);
        var generation = Math.Max(0, observation.StateGeneration);
        var valid = generation > 0
            && observation.CapturedAtMs > 0
            && observation.IsAvailable
            && observation.AcrEnabled
            && observation.InCombat
            && observation.IsAlive;
        if (!valid)
        {
            return new BlmMotionRuntimeState { StateGeneration = generation };
        }

        var sameGeneration = state.StateGeneration == generation;
        var validSince = sameGeneration && state.ValidSinceMs > 0
            ? state.ValidSinceMs
            : observation.CapturedAtMs;
        var phaseChanged = !sameGeneration || state.IsMoving != observation.IsMoving;
        var movingSince = observation.IsMoving
            ? phaseChanged || state.MovingSinceMs <= 0
                ? observation.CapturedAtMs
                : state.MovingSinceMs
            : 0;
        var stationarySince = observation.IsMoving
            ? 0
            : phaseChanged || state.StationarySinceMs <= 0
                ? observation.CapturedAtMs
                : state.StationarySinceMs;

        // GCD 空转以上一发 GCD 的转好时刻为基准（瞬发 GCD 由调用方按成功时刻
        // 加 GCD 总长折算，读条 GCD 直接用成功时刻），但不得早于本代际首个
        // 有效观测，避免上一场战斗的旧 Ack 把空转计时撑大。读条尝试（含被
        // 拉断）不清零，只有 GCD 真正成功才清零。
        var gcdStarveBase = Math.Max(validSince, observation.LastGcdReadyAtMs);

        return new BlmMotionRuntimeState
        {
            StateGeneration = generation,
            ValidSinceMs = validSince,
            MovingSinceMs = movingSince,
            StationarySinceMs = stationarySince,
            IsMoving = observation.IsMoving,
            MovingDurationMs = observation.IsMoving
                ? DurationSince(observation.CapturedAtMs, movingSince)
                : 0d,
            StationaryDurationMs = observation.IsMoving
                ? 0d
                : DurationSince(observation.CapturedAtMs, stationarySince),
            GcdStarvationMs = DurationSince(observation.CapturedAtMs, gcdStarveBase),
        };
    }

    private static double DurationSince(long nowMs, long sinceMs)
        => sinceMs <= 0 ? 0d : Math.Max(0d, nowMs - sinceMs);
}
