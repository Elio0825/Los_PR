using System.Linq;

namespace LosPr.BLM.Data;

internal enum BlmConsoleMode
{
    Daily,
    HighEnd,
}

internal enum BlmOpenerSelection
{
    None,
    Level70,
    Level80,
    Level90,
    Standard57,
    Flare,
}

internal enum BlmUiThemeStyle
{
    MoonlitCat,
    AstralFamiliar,
    EmberFamiliar,
    AmethystCat,
}

internal sealed class BlmKeyBinding
{
    public string Key { get; set; } = string.Empty;

    public bool Ctrl { get; set; }

    public bool Shift { get; set; }

    public bool Alt { get; set; }
}

internal sealed class BlackMageSettings
{
    public const float DefaultWindowWidth = 980f;
    public const float DefaultWindowHeight = 700f;
    public const float MinimumWindowWidth = 660f;
    public const float MinimumWindowHeight = 500f;
    public const float MaximumWindowWidth = 3840f;
    public const float MaximumWindowHeight = 2160f;
    public const float MinimumUiScale = 0.80f;
    public const float MaximumUiScale = 1.40f;

    public bool ConsoleOpen { get; set; } = true;

    public int UiLayoutVersion { get; set; }

    public bool RememberWindow { get; set; } = true;

    public float WindowX { get; set; } = -1f;

    public float WindowY { get; set; } = -1f;

    public float WindowWidth { get; set; } = DefaultWindowWidth;

    public float WindowHeight { get; set; } = DefaultWindowHeight;

    public float QuickControlWindowX { get; set; } = -1f;

    public float QuickControlWindowY { get; set; } = -1f;

    public float QuickQtWindowX { get; set; } = -1f;

    public float QuickQtWindowY { get; set; } = -1f;

    public float QuickHotkeyWindowX { get; set; } = -1f;

    public float QuickHotkeyWindowY { get; set; } = -1f;

    public float UiScale { get; set; } = 1f;

    public float QtPanelScale { get; set; } = 1f;

    public float HotkeyPanelScale { get; set; } = 1f;

    public float WindowOpacity { get; set; } = 0.96f;

    public bool ReduceMotion { get; set; }

    public BlmUiThemeStyle UiThemeStyle { get; set; } = BlmUiThemeStyle.AmethystCat;

    public BlmConsoleMode CombatMode { get; set; } = BlmConsoleMode.Daily;

    public BlmOpenerSelection OpenerSelection { get; set; }

    public bool OpenerPotionEnabled { get; set; } = true;

    public int OpenerPolicyVersion { get; set; }

    public bool OpenerNoTriplecast { get; set; }

    public bool CompressFireParadoxEnabled { get; set; } = true;

    public int DotHpThresholdPercent { get; set; } = 3;

    public float MoveTriplecastSeconds { get; set; } = 1.5f;

    public float StationaryLeyLinesSeconds { get; set; } = 3f;

    public bool DangerLoopEnabled { get; set; }

    public bool DangerRiskAnimationLock { get; set; }

    public bool DangerRiskDrModule { get; set; }

    public bool DangerRiskEnforcement { get; set; }

    public int ActiveTab { get; set; }

    public bool ShowAdvancedDebug { get; set; }

    public bool DecisionLogging { get; set; }

    public int DebugTraceSetupVersion { get; set; }

    public int MinimumEnabledLevel { get; set; } = 1;

    public Dictionary<string, bool> QtStates { get; set; } = new(StringComparer.Ordinal);

    public HashSet<string> HiddenQtKeys { get; set; } = new(StringComparer.Ordinal);

    public HashSet<string> HiddenHotkeyKeys { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, BlmKeyBinding> QtBindings { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, BlmKeyBinding> HotkeyBindings { get; set; } = new(StringComparer.Ordinal);

    public void Normalize()
    {
        if (UiLayoutVersion < 2)
        {
            if (MathF.Abs(WindowWidth - 960f) < 0.5f
                && MathF.Abs(WindowHeight - 660f) < 0.5f)
            {
                WindowWidth = DefaultWindowWidth;
                WindowHeight = DefaultWindowHeight;
            }

            UiLayoutVersion = 2;
        }

        if (UiLayoutVersion < 3)
        {
            if (MathF.Abs(WindowWidth - 860f) < 0.5f
                && MathF.Abs(WindowHeight - 620f) < 0.5f)
            {
                WindowWidth = DefaultWindowWidth;
                WindowHeight = DefaultWindowHeight;
            }

            UiThemeStyle = BlmUiThemeStyle.AmethystCat;
            UiLayoutVersion = 3;
        }

        if (UiLayoutVersion < 4)
        {
            if (ActiveTab >= 3)
            {
                ActiveTab = 4;
            }

            UiLayoutVersion = 4;
        }

        if (DebugTraceSetupVersion < 1)
        {
            ShowAdvancedDebug = true;
            DecisionLogging = true;
            DebugTraceSetupVersion = 1;
        }

        WindowX = NormalizeCoordinate(WindowX);
        WindowY = NormalizeCoordinate(WindowY);
        QuickControlWindowX = NormalizeCoordinate(QuickControlWindowX);
        QuickControlWindowY = NormalizeCoordinate(QuickControlWindowY);
        QuickQtWindowX = NormalizeCoordinate(QuickQtWindowX);
        QuickQtWindowY = NormalizeCoordinate(QuickQtWindowY);
        QuickHotkeyWindowX = NormalizeCoordinate(QuickHotkeyWindowX);
        QuickHotkeyWindowY = NormalizeCoordinate(QuickHotkeyWindowY);
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
        QtPanelScale = NormalizeFinite(QtPanelScale, 1f, 0.70f, 1.50f);
        HotkeyPanelScale = NormalizeFinite(HotkeyPanelScale, 1f, 0.70f, 1.50f);
        WindowOpacity = NormalizeFinite(WindowOpacity, 0.96f, 0.70f, 1f);
        ActiveTab = Math.Clamp(ActiveTab, 0, 4);
        MinimumEnabledLevel = Math.Clamp(MinimumEnabledLevel, 1, 100);
        DotHpThresholdPercent = Math.Clamp(DotHpThresholdPercent, 0, 100);
        MoveTriplecastSeconds = NormalizeFinite(MoveTriplecastSeconds, 1.5f, 0f, 10f);
        StationaryLeyLinesSeconds = NormalizeFinite(StationaryLeyLinesSeconds, 3f, 0f, 30f);
        if (!Enum.IsDefined(UiThemeStyle))
        {
            UiThemeStyle = BlmUiThemeStyle.MoonlitCat;
        }

        if (!Enum.IsDefined(CombatMode))
        {
            CombatMode = BlmConsoleMode.Daily;
        }

        if (OpenerPolicyVersion < 1)
        {
            if (CombatMode == BlmConsoleMode.HighEnd)
            {
                OpenerPotionEnabled = true;
            }

            OpenerPolicyVersion = 1;
        }

        if (!Enum.IsDefined(OpenerSelection))
        {
            OpenerSelection = BlmOpenerSelection.None;
        }

        if (!(DangerRiskAnimationLock && DangerRiskDrModule && DangerRiskEnforcement))
        {
            DangerLoopEnabled = false;
        }

        if (QtStates is null)
        {
            QtStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        }
        else if (!ReferenceEquals(QtStates.Comparer, StringComparer.Ordinal))
        {
            QtStates = new Dictionary<string, bool>(QtStates, StringComparer.Ordinal);
        }

        HiddenQtKeys = NormalizeSet(HiddenQtKeys);
        HiddenHotkeyKeys = NormalizeSet(HiddenHotkeyKeys);
        QtBindings = NormalizeBindings(QtBindings);
        HotkeyBindings = NormalizeBindings(HotkeyBindings);
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

    private static HashSet<string> NormalizeSet(HashSet<string>? values)
        => values is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(values.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);

    private static Dictionary<string, BlmKeyBinding> NormalizeBindings(
        Dictionary<string, BlmKeyBinding>? values)
    {
        var result = new Dictionary<string, BlmKeyBinding>(StringComparer.Ordinal);
        if (values is null)
            return result;

        foreach (var (key, binding) in values)
        {
            if (!string.IsNullOrWhiteSpace(key)
                && binding is not null
                && !string.IsNullOrWhiteSpace(binding.Key))
            {
                binding.Key = binding.Key.Trim();
                result[key] = binding;
            }
        }

        return result;
    }
}
