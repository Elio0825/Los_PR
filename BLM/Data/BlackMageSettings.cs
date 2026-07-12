namespace LosPr.BLM.Data;

public sealed class BlackMageSettings
{
    public const float DefaultWindowWidth = 960f;
    public const float DefaultWindowHeight = 660f;
    public const float MinimumWindowWidth = 760f;
    public const float MinimumWindowHeight = 520f;
    public const float MaximumWindowWidth = 3840f;
    public const float MaximumWindowHeight = 2160f;
    public const float MinimumUiScale = 0.80f;
    public const float MaximumUiScale = 1.40f;

    public bool ConsoleOpen { get; set; } = true;

    public bool RememberWindow { get; set; } = true;

    public float WindowX { get; set; } = -1f;

    public float WindowY { get; set; } = -1f;

    public float WindowWidth { get; set; } = DefaultWindowWidth;

    public float WindowHeight { get; set; } = DefaultWindowHeight;

    public float UiScale { get; set; } = 1f;

    public float WindowOpacity { get; set; } = 0.96f;

    public bool ReduceMotion { get; set; }

    public int ActiveTab { get; set; }

    public bool ShowAdvancedDebug { get; set; }

    public bool DecisionLogging { get; set; }

    public int DebugTraceSetupVersion { get; set; }

    public int MinimumEnabledLevel { get; set; } = 1;

    public Dictionary<string, bool> QtStates { get; set; } = new(StringComparer.Ordinal);

    public void Normalize()
    {
        if (DebugTraceSetupVersion < 1)
        {
            ShowAdvancedDebug = true;
            DecisionLogging = true;
            DebugTraceSetupVersion = 1;
        }

        WindowX = NormalizeCoordinate(WindowX);
        WindowY = NormalizeCoordinate(WindowY);
        WindowWidth = NormalizeFinite(
            WindowWidth,
            DefaultWindowWidth,
            MinimumWindowWidth,
            MaximumWindowWidth);
        WindowHeight = NormalizeFinite(
            WindowHeight,
            DefaultWindowHeight,
            MinimumWindowHeight,
            MaximumWindowHeight);
        UiScale = NormalizeFinite(UiScale, 1f, MinimumUiScale, MaximumUiScale);
        WindowOpacity = NormalizeFinite(WindowOpacity, 0.96f, 0.70f, 1f);
        ActiveTab = Math.Clamp(ActiveTab, 0, 3);
        MinimumEnabledLevel = Math.Clamp(MinimumEnabledLevel, 1, 100);
        if (QtStates is null)
        {
            QtStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        }
        else if (!ReferenceEquals(QtStates.Comparer, StringComparer.Ordinal))
        {
            QtStates = new Dictionary<string, bool>(QtStates, StringComparer.Ordinal);
        }
    }

    private static float NormalizeCoordinate(float value)
    {
        if (!float.IsFinite(value))
        {
            return -1f;
        }

        return value == -1f ? value : Math.Clamp(value, -32768f, 32768f);
    }

    private static float NormalizeFinite(float value, float fallback, float minimum, float maximum)
        => float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
