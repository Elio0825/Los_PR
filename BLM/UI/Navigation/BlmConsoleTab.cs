namespace LosPr.BLM.UI.Navigation;

public enum BlmConsoleTab
{
    Overview,
    Combat,
    Control,
    System,
}

public static class BlmConsoleTabInfo
{
    public static IReadOnlyList<string> Labels { get; } =
    [
        "概览",
        "作战",
        "控制",
        "系统",
    ];

    public static BlmConsoleTab FromIndex(int index)
        => (BlmConsoleTab)Math.Clamp(index, 0, Labels.Count - 1);
}
