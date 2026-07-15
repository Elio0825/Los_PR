using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

internal static class LosTabBar
{
    public static bool Draw(
        string id,
        IReadOnlyList<string> labels,
        ref int selectedIndex,
        float scale = 1f,
        bool reduceMotion = false)
    {
        if (labels.Count == 0)
            return false;

        scale = LosMetrics.NormalizeScale(scale);
        selectedIndex = Math.Clamp(selectedIndex, 0, labels.Count - 1);
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(
            MathF.Max(1f, ImGui.GetContentRegionAvail().X),
            LosMetrics.Scale(LosMetrics.TabBarHeight, scale));
        var drawList = ImGui.GetWindowDrawList();
        var changed = false;
        var horizontalPadding = LosMetrics.Scale(20f, scale);
        var gap = LosMetrics.Scale(12f, scale);
        var availableWidth = MathF.Max(1f, size.X - (horizontalPadding * 2f));
        var idealTabWidth = LosMetrics.Scale(104f, scale);
        var tabWidth = MathF.Min(
            idealTabWidth,
            MathF.Max(
                LosMetrics.Scale(68f, scale),
                (availableWidth - (gap * (labels.Count - 1))) / labels.Count));
        var tabsWidth = (tabWidth * labels.Count) + (gap * (labels.Count - 1));
        var tabsLeft = pos.X + MathF.Max(horizontalPadding, (size.X - tabsWidth) * 0.5f);
        var tabHeight = LosMetrics.Scale(34f, scale);
        var tabTop = pos.Y + ((size.Y - tabHeight) * 0.5f);

        drawList.AddRectFilled(
            pos,
            pos + size,
            LosPalette.ToUInt(LosTheme.ApplyBackgroundOpacity(LosPalette.Surface)));
        drawList.AddLine(
            pos + new Vector2(0f, size.Y - 1f),
            pos + new Vector2(size.X, size.Y - 1f),
            LosPalette.ToUInt(LosPalette.Separator),
            1f);

        for (var index = 0; index < labels.Count; index++)
        {
            var tabPos = new Vector2(tabsLeft + (index * (tabWidth + gap)), tabTop);
            var tabSize = new Vector2(tabWidth, tabHeight);
            ImGui.SetCursorScreenPos(tabPos);
            ImGui.InvisibleButton($"##los_tab_{id}_{index}", tabSize);
            var hovered = ImGui.IsItemHovered();

            if (ImGui.IsItemClicked())
            {
                selectedIndex = index;
                changed = true;
            }

            if (index == selectedIndex)
            {
                drawList.AddRectFilled(
                    tabPos,
                    tabPos + tabSize,
                    LosPalette.ToUInt(LosPalette.Primary),
                    tabHeight * 0.5f);
            }
            else if (hovered)
            {
                drawList.AddRectFilled(
                    tabPos,
                    tabPos + tabSize,
                    LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.10f)),
                    tabHeight * 0.5f);
            }

            var fitted = LosComponents.FitText(labels[index], tabWidth - LosMetrics.Scale(16f, scale));
            var textSize = ImGui.CalcTextSize(fitted);
            drawList.AddText(
                tabPos + ((tabSize - textSize) * 0.5f),
                LosPalette.ToUInt(index == selectedIndex ? LosPalette.TextPrimary : LosPalette.TextMuted),
                fitted);
        }

        _ = reduceMotion;

        ImGui.SetCursorScreenPos(pos + new Vector2(0f, size.Y));
        return changed;
    }

}
