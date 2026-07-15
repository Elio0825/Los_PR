namespace LosPr.BLM.UI.Navigation;

internal enum BlmConsoleTab
{
    Overview,
    Battle,
    Style,
    Hotkeys,
    Debug,
}

internal static class BlmConsoleTabInfo
{
    public static IReadOnlyList<string> Labels { get; } =
    [
        "概览",
        "战斗",
        "风格",
        "热键",
        "Debug",
    ];

    public static BlmConsoleTab FromIndex(int index)
        => (BlmConsoleTab)Math.Clamp(index, 0, Labels.Count - 1);
}
