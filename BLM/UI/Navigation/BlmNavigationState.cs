using System;

namespace LosPr.BLM.UI.Navigation;

internal sealed class BlmNavigationState
{
    public BlmNavigationState(int initialIndex)
    {
        SelectedIndex = Normalize(initialIndex);
    }

    public int SelectedIndex { get; private set; }

    public BlmConsoleTab CurrentTab => BlmConsoleTabInfo.FromIndex(SelectedIndex);

    public bool Select(int index)
    {
        var normalized = Normalize(index);
        if (normalized == SelectedIndex)
            return false;

        SelectedIndex = normalized;
        return true;
    }

    private static int Normalize(int index)
        => Math.Clamp(index, 0, BlmConsoleTabInfo.Labels.Count - 1);
}
