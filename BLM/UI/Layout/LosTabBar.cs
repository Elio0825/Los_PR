using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

public static class LosTabBar
{
    private static readonly Dictionary<string, float> IndicatorPositions = new(StringComparer.Ordinal);

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
        var tabWidth = size.X / labels.Count;
        var changed = false;

        drawList.AddRectFilled(
            pos,
            pos + size,
            LosPalette.ToUInt(LosTheme.ApplyBackgroundOpacity(LosPalette.Content)));
        drawList.AddLine(
            pos + new Vector2(0f, size.Y - 1f),
            pos + new Vector2(size.X, size.Y - 1f),
            LosPalette.ToUInt(LosPalette.Separator),
            1f);

        for (var index = 0; index < labels.Count; index++)
        {
            var tabPos = pos + new Vector2(index * tabWidth, 0f);
            var tabSize = new Vector2(tabWidth, size.Y);
            ImGui.SetCursorScreenPos(tabPos);
            ImGui.InvisibleButton($"##los_tab_{id}_{index}", tabSize);
            var hovered = ImGui.IsItemHovered();

            if (ImGui.IsItemClicked())
            {
                selectedIndex = index;
                changed = true;
            }

            if (hovered && index != selectedIndex)
            {
                drawList.AddRectFilled(
                    tabPos,
                    tabPos + tabSize,
                    LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.07f)));
            }

            var fitted = LosComponents.FitText(labels[index], tabWidth - LosMetrics.Scale(16f, scale));
            var textSize = ImGui.CalcTextSize(fitted);
            drawList.AddText(
                tabPos + ((tabSize - textSize) * 0.5f),
                LosPalette.ToUInt(index == selectedIndex ? LosPalette.TextPrimary : LosPalette.TextMuted),
                fitted);
        }

        var targetX = selectedIndex * tabWidth;
        var indicatorX = Animate(id, targetX, reduceMotion);
        var indicatorHeight = LosMetrics.Scale(3f, scale);
        var indicatorPos = pos + new Vector2(indicatorX, size.Y - indicatorHeight);
        drawList.AddRectFilled(
            indicatorPos,
            indicatorPos + new Vector2(tabWidth, indicatorHeight),
            LosPalette.ToUInt(LosPalette.Primary),
            LosMetrics.Scale(1.5f, scale));
        drawList.AddRectFilled(
            indicatorPos,
            indicatorPos + new Vector2(MathF.Min(tabWidth, LosMetrics.Scale(36f, scale)), indicatorHeight),
            LosPalette.ToUInt(LosPalette.Cyan),
            LosMetrics.Scale(1.5f, scale));

        ImGui.SetCursorScreenPos(pos + new Vector2(0f, size.Y));
        return changed;
    }

    private static float Animate(string id, float target, bool reduceMotion)
    {
        if (reduceMotion)
        {
            IndicatorPositions[id] = target;
            return target;
        }

        if (!IndicatorPositions.TryGetValue(id, out var current))
            current = target;
        var deltaTime = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);
        current += (target - current) * Math.Clamp(deltaTime * 14f, 0f, 1f);
        if (MathF.Abs(target - current) < 0.1f)
            current = target;
        IndicatorPositions[id] = current;
        return current;
    }
}
