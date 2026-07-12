using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

public static class LosHeader
{
    public static void Draw(
        string id,
        string title,
        string subtitle,
        string statusText,
        LosStatusTone statusTone,
        string? badge = null,
        float scale = 1f,
        bool reduceMotion = false)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(
            MathF.Max(1f, ImGui.GetContentRegionAvail().X),
            LosMetrics.Scale(LosMetrics.HeaderHeight, scale));
        var drawList = ImGui.GetWindowDrawList();
        var compact = LosMetrics.IsCompact(size.X, scale);

        drawList.AddRectFilled(
            pos,
            pos + size,
            LosPalette.ToUInt(LosTheme.ApplyBackgroundOpacity(LosPalette.Surface)));
        drawList.AddRectFilled(
            pos,
            pos + new Vector2(LosMetrics.Scale(4f, scale), size.Y),
            LosPalette.ToUInt(LosPalette.Primary));
        drawList.AddLine(
            pos + new Vector2(0f, size.Y - 1f),
            pos + new Vector2(size.X, size.Y - 1f),
            LosPalette.ToUInt(LosPalette.Separator),
            1f);
        drawList.AddLine(
            pos + new Vector2(LosMetrics.Scale(4f, scale), 1f),
            pos + new Vector2(LosMetrics.Scale(52f, scale), 1f),
            LosPalette.ToUInt(LosPalette.Cyan),
            LosMetrics.Scale(2f, scale));

        var rightPadding = LosMetrics.Scale(16f, scale);
        var statusWidth = LosMetrics.Scale(LosMetrics.StatusPillWidth, scale);
        var statusHeight = LosMetrics.Scale(26f, scale);
        var statusPos = new Vector2(
            pos.X + size.X - rightPadding - statusWidth,
            pos.Y + ((size.Y - statusHeight) * 0.5f));

        var showBadge = !compact && !string.IsNullOrWhiteSpace(badge);
        var badgeWidth = showBadge ? LosMetrics.Scale(76f, scale) : 0f;
        var gap = showBadge ? LosMetrics.Scale(10f, scale) : 0f;
        var titleLeft = pos.X + LosMetrics.Scale(20f, scale);
        var textRight = statusPos.X - gap - badgeWidth - LosMetrics.Scale(16f, scale);
        var textWidth = MathF.Max(20f, textRight - titleLeft);

        var fittedTitle = LosComponents.FitText(title, textWidth);
        var titleSize = ImGui.CalcTextSize(fittedTitle);
        var titleY = compact
            ? pos.Y + ((size.Y - titleSize.Y) * 0.5f)
            : pos.Y + LosMetrics.Scale(17f, scale);
        drawList.AddText(
            new Vector2(titleLeft, titleY),
            LosPalette.ToUInt(LosPalette.TextPrimary),
            fittedTitle);

        if (!compact && !string.IsNullOrWhiteSpace(subtitle))
        {
            var fittedSubtitle = LosComponents.FitText(subtitle, textWidth);
            drawList.AddText(
                new Vector2(titleLeft, titleY + titleSize.Y + LosMetrics.Scale(5f, scale)),
                LosPalette.ToUInt(LosPalette.TextMuted),
                fittedSubtitle);
        }

        if (showBadge)
        {
            var badgePos = new Vector2(
                statusPos.X - gap - badgeWidth,
                pos.Y + ((size.Y - statusHeight) * 0.5f));
            DrawBadge(drawList, badgePos, new Vector2(badgeWidth, statusHeight), badge!, scale);
        }

        DrawStatus(drawList, statusPos, new Vector2(statusWidth, statusHeight), statusText, statusTone, scale, reduceMotion);

        ImGui.PushID(id);
        try
        {
            ImGui.InvisibleButton("##header_surface", size);
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private static void DrawBadge(ImDrawListPtr drawList, Vector2 pos, Vector2 size, string text, float scale)
    {
        var rounding = MathF.Min(LosMetrics.Scale(5f, scale), size.Y * 0.5f);
        drawList.AddRectFilled(pos, pos + size, LosPalette.ToUInt(LosPalette.CyanMuted), rounding);
        drawList.AddRect(
            pos,
            pos + size,
            LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.52f)),
            rounding,
            ImDrawFlags.None,
            1f);

        var fitted = LosComponents.FitText(text, size.X - LosMetrics.Scale(14f, scale));
        var textSize = ImGui.CalcTextSize(fitted);
        drawList.AddText(pos + ((size - textSize) * 0.5f), LosPalette.ToUInt(LosPalette.Cyan), fitted);
    }

    private static void DrawStatus(
        ImDrawListPtr drawList,
        Vector2 pos,
        Vector2 size,
        string text,
        LosStatusTone tone,
        float scale,
        bool reduceMotion)
    {
        var color = LosComponents.StatusColor(tone);
        var rounding = size.Y * 0.5f;
        drawList.AddRectFilled(pos, pos + size, LosPalette.ToUInt(LosComponents.StatusMutedColor(tone)), rounding);
        drawList.AddRect(
            pos,
            pos + size,
            LosPalette.ToUInt(LosPalette.WithAlpha(color, 0.55f)),
            rounding,
            ImDrawFlags.None,
            1f);

        var pulse = reduceMotion
            ? 1f
            : 0.72f + (((MathF.Sin((float)ImGui.GetTime() * 3f) + 1f) * 0.5f) * 0.28f);
        var dotX = pos.X + LosMetrics.Scale(13f, scale);
        drawList.AddCircleFilled(
            new Vector2(dotX, pos.Y + (size.Y * 0.5f)),
            LosMetrics.Scale(3f, scale),
            LosPalette.ToUInt(LosPalette.WithAlpha(color, pulse)),
            16);

        var textLeft = dotX + LosMetrics.Scale(8f, scale);
        var fitted = LosComponents.FitText(text, (pos.X + size.X) - textLeft - LosMetrics.Scale(8f, scale));
        var textSize = ImGui.CalcTextSize(fitted);
        drawList.AddText(
            new Vector2(textLeft, pos.Y + ((size.Y - textSize.Y) * 0.5f)),
            LosPalette.ToUInt(color),
            fitted);
    }
}
