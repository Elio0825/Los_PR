namespace LosPr.BLM.Resolvers;

public readonly record struct BlmResolverRuntimeObservation(
    long StateGeneration,
    long CapturedAtMs,
    bool IsAvailable,
    bool AcrEnabled,
    bool InCombat,
    bool IsAlive,
    bool IsCasting,
    float GcdTotalSeconds,
    long PreviousGcdOccurredAtMs,
    long PreviousGcdSerial,
    bool HasAvailableInstantGcd,
    bool SwiftcastCurrentlyAvailable);

public sealed record BlmResolverRuntimeState
{
    public static BlmResolverRuntimeState Empty { get; } = new();

    public long StateGeneration { get; init; }
    public long PreviousGcdSerial { get; init; }
    public long IdleSinceMs { get; init; }
    public bool IsIdle { get; init; }
    public bool NeedsForcedIceRecovery { get; init; }
}

public sealed class BlmResolverRuntimeMemory
{
    private readonly object _gate = new();
    private BlmResolverRuntimeState _state = BlmResolverRuntimeState.Empty;

    public BlmResolverRuntimeState Advance(BlmResolverRuntimeObservation observation)
    {
        lock (_gate)
        {
            _state = Reduce(_state, observation);
            return _state;
        }
    }

    public BlmResolverRuntimeState GetSnapshot()
    {
        lock (_gate)
        {
            return _state;
        }
    }

    public static BlmResolverRuntimeState Reduce(
        BlmResolverRuntimeState state,
        BlmResolverRuntimeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(state);
        var previousGcdSerial = Math.Max(0, observation.PreviousGcdSerial);
        var observedNewGcd = state.PreviousGcdSerial > 0
            && previousGcdSerial != state.PreviousGcdSerial;
        var reset = observation.StateGeneration <= 0
            || observation.StateGeneration != state.StateGeneration
            || !observation.IsAvailable
            || !observation.AcrEnabled
            || !observation.InCombat
            || !observation.IsAlive
            || observation.IsCasting
            || observedNewGcd;
        if (reset)
        {
            return new BlmResolverRuntimeState
            {
                StateGeneration = Math.Max(0, observation.StateGeneration),
                PreviousGcdSerial = previousGcdSerial,
            };
        }

        var gcdTotalMs = float.IsFinite(observation.GcdTotalSeconds)
            ? Math.Max(0d, observation.GcdTotalSeconds * 1000d)
            : 0d;
        var hasUsableGcdOrigin = gcdTotalMs > 0d
            && observation.PreviousGcdSerial > 0
            && observation.PreviousGcdOccurredAtMs > 0
            && observation.PreviousGcdOccurredAtMs <= observation.CapturedAtMs;
        if (!hasUsableGcdOrigin)
        {
            return new BlmResolverRuntimeState
            {
                StateGeneration = observation.StateGeneration,
                PreviousGcdSerial = previousGcdSerial,
            };
        }

        var elapsedMs = observation.CapturedAtMs - observation.PreviousGcdOccurredAtMs;
        var isIdle = elapsedMs >= 2d * gcdTotalMs;
        if (!isIdle)
        {
            return new BlmResolverRuntimeState
            {
                StateGeneration = observation.StateGeneration,
                PreviousGcdSerial = previousGcdSerial,
            };
        }

        var idleSinceMs = state.IsIdle
            && state.IdleSinceMs > 0
            && state.PreviousGcdSerial == previousGcdSerial
                ? state.IdleSinceMs
                : observation.CapturedAtMs;
        var idleDurationMs = Math.Max(0, observation.CapturedAtMs - idleSinceMs);
        var forcedRecoveryThresholdMs = Math.Max(5000d, 3d * gcdTotalMs);
        return new BlmResolverRuntimeState
        {
            StateGeneration = observation.StateGeneration,
            PreviousGcdSerial = previousGcdSerial,
            IdleSinceMs = idleSinceMs,
            IsIdle = true,
            NeedsForcedIceRecovery = idleDurationMs >= forcedRecoveryThresholdMs
                && !observation.HasAvailableInstantGcd
                && !observation.SwiftcastCurrentlyAvailable,
        };
    }
}
