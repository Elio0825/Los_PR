using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

public static class LosSection
{
    public static void Draw(
        string id,
        string title,
        Action content,
        string? description = null,
        float scale = 1f)
    {
        ArgumentNullException.ThrowIfNull(content);
        scale = LosMetrics.NormalizeScale(scale);
        ImGui.PushID(id);
        try
        {
            DrawHeader(title, description, scale);
            content();
            ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(LosMetrics.SectionSpacing, scale)));
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private static void DrawHeader(string title, string? description, float scale)
    {
        var pos = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(LosMetrics.SectionHeaderHeight, scale);
        var drawList = ImGui.GetWindowDrawList();
        var markerHeight = LosMetrics.Scale(18f, scale);
        var textLeft = pos.X + LosMetrics.Scale(11f, scale);
        var textWidth = width - LosMetrics.Scale(11f, scale);
        var fittedTitle = LosComponents.FitText(title, textWidth);
        var titleSize = ImGui.CalcTextSize(fittedTitle);
        var hasDescription = !string.IsNullOrWhiteSpace(description);
        var titleY = hasDescription
            ? pos.Y + LosMetrics.Scale(5f, scale)
            : pos.Y + ((height - titleSize.Y) * 0.5f);

        drawList.AddRectFilled(
            new Vector2(pos.X, titleY),
            new Vector2(pos.X + LosMetrics.Scale(3f, scale), titleY + markerHeight),
            LosPalette.ToUInt(LosPalette.Primary),
            LosMetrics.Scale(1.5f, scale));
        drawList.AddText(new Vector2(textLeft, titleY), LosPalette.ToUInt(LosPalette.TextPrimary), fittedTitle);

        if (hasDescription)
        {
            var fittedDescription = LosComponents.FitText(description!, textWidth);
            drawList.AddText(
                new Vector2(textLeft, titleY + titleSize.Y + LosMetrics.Scale(4f, scale)),
                LosPalette.ToUInt(LosPalette.TextMuted),
                fittedDescription);
        }

        ImGui.Dummy(new Vector2(width, height));
    }
}

public static class LosCard
{
    public static void Draw(
        string id,
        Action content,
        string? title = null,
        string? subtitle = null,
        float height = 0f,
        float scale = 1f,
        bool border = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        scale = LosMetrics.NormalizeScale(scale);
        var actualHeight = LosMetrics.Scale(
            height > 0f ? height : LosMetrics.DefaultCardHeight,
            scale);

        ImGui.PushID(id);
        LosTheme.PushCardStyle(scale, border);
        try
        {
            var began = ImGui.BeginChild(
                "##card",
                new Vector2(0f, actualHeight),
                border,
                ImGuiWindowFlags.NoCollapse);
            if (began)
            {
                DrawHeader(title, subtitle, scale);
                content();
            }
        }
        finally
        {
            ImGui.EndChild();
            LosTheme.PopCardStyle();
            ImGui.PopID();
        }

        ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(LosMetrics.CardSpacing, scale)));
    }

    private static void DrawHeader(string? title, string? subtitle, float scale)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var pos = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
        var height = LosMetrics.Scale(hasSubtitle ? 43f : 27f, scale);
        var drawList = ImGui.GetWindowDrawList();
        var fittedTitle = LosComponents.FitText(title, width);
        var titleSize = ImGui.CalcTextSize(fittedTitle);

        drawList.AddText(pos, LosPalette.ToUInt(LosPalette.TextPrimary), fittedTitle);
        if (hasSubtitle)
        {
            var fittedSubtitle = LosComponents.FitText(subtitle!, width);
            drawList.AddText(
                new Vector2(pos.X, pos.Y + titleSize.Y + LosMetrics.Scale(4f, scale)),
                LosPalette.ToUInt(LosPalette.TextMuted),
                fittedSubtitle);
        }

        drawList.AddLine(
            new Vector2(pos.X, pos.Y + height - LosMetrics.Scale(6f, scale)),
            new Vector2(pos.X + width, pos.Y + height - LosMetrics.Scale(6f, scale)),
            LosPalette.ToUInt(LosPalette.Separator),
            1f);
        ImGui.Dummy(new Vector2(width, height));
    }
}
