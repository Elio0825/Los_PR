using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

internal static class LosHeader
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
        drawList.AddLine(
            pos + new Vector2(0f, size.Y - 1f),
            pos + new Vector2(size.X, size.Y - 1f),
            LosPalette.ToUInt(LosPalette.Separator),
            1f);
        DrawConstellation(drawList, pos, size, scale);

        var iconPosition = pos + LosMetrics.Scale(new Vector2(28f, 16f), scale);
        var iconSize = LosMetrics.Scale(new Vector2(56f, 56f), scale);
        drawList.AddRectFilled(
            iconPosition,
            iconPosition + iconSize,
            LosPalette.ToUInt(LosPalette.PrimaryMuted),
            LosMetrics.Scale(14f, scale));
        drawList.AddRect(
            iconPosition,
            iconPosition + iconSize,
            LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.PrimaryHover, 0.55f)),
            LosMetrics.Scale(14f, scale),
            ImDrawFlags.None,
            1f);
        DrawBlackMageSigil(
            drawList,
            iconPosition + (iconSize * 0.5f),
            scale);

        var rightPadding = LosMetrics.Scale(28f, scale);
        var statusWidth = LosMetrics.Scale(LosMetrics.StatusPillWidth, scale);
        var statusHeight = LosMetrics.Scale(26f, scale);
        var statusPos = new Vector2(
            pos.X + size.X - rightPadding - statusWidth,
            pos.Y + ((size.Y - statusHeight) * 0.5f));

        var showBadge = !compact && !string.IsNullOrWhiteSpace(badge);
        var badgeWidth = showBadge ? LosMetrics.Scale(76f, scale) : 0f;
        var gap = showBadge ? LosMetrics.Scale(10f, scale) : 0f;
        var titleLeft = iconPosition.X + iconSize.X + LosMetrics.Scale(16f, scale);
        var textRight = statusPos.X - gap - badgeWidth - LosMetrics.Scale(16f, scale);
        var textWidth = MathF.Max(20f, textRight - titleLeft);

        var fittedTitle = LosComponents.FitText(title, textWidth);
        var titleFontSize = ImGui.GetFontSize() * 1.12f;
        var titleSize = ImGui.CalcTextSize(fittedTitle) * 1.12f;
        var titleY = compact
            ? pos.Y + ((size.Y - titleSize.Y) * 0.5f)
            : pos.Y + LosMetrics.Scale(15f, scale);
        drawList.AddText(
            ImGui.GetFont(),
            titleFontSize,
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

    private static void DrawBlackMageSigil(
        ImDrawListPtr drawList,
        Vector2 center,
        float scale)
    {
        var outer = LosMetrics.Scale(20f, scale);
        var core = LosMetrics.Scale(8f, scale);
        var rayStart = LosMetrics.Scale(13f, scale);
        var rayEnd = LosMetrics.Scale(19f, scale);
        for (var index = 0; index < 6; index++)
        {
            var angle = -MathF.PI * 0.5f + index * (MathF.PI / 3f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(
                center + direction * rayStart,
                center + direction * rayEnd,
                LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.78f)),
                LosMetrics.Scale(1.5f, scale));
        }

        drawList.AddCircle(
            center,
            outer,
            LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Primary, 0.62f)),
            32,
            LosMetrics.Scale(1.2f, scale));

        drawList.AddQuadFilled(
            center + new Vector2(0f, -core),
            center + new Vector2(core, 0f),
            center + new Vector2(0f, core),
            center + new Vector2(-core, 0f),
            LosPalette.ToUInt(LosPalette.Cyan));
        drawList.AddCircleFilled(
            center,
            LosMetrics.Scale(2.5f, scale),
            LosPalette.ToUInt(LosPalette.TextPrimary),
            16);
    }

    private static void DrawConstellation(
        ImDrawListPtr drawList,
        Vector2 pos,
        Vector2 size,
        float scale)
    {
        if (size.X < LosMetrics.Scale(520f, scale))
            return;

        var points = new[]
        {
            pos + new Vector2(size.X * 0.56f, LosMetrics.Scale(18f, scale)),
            pos + new Vector2(size.X * 0.64f, LosMetrics.Scale(34f, scale)),
            pos + new Vector2(size.X * 0.72f, LosMetrics.Scale(14f, scale)),
            pos + new Vector2(size.X * 0.80f, LosMetrics.Scale(32f, scale)),
        };
        var lineColor = LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.14f));
        var starColor = LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.MoonGold, 0.34f));
        for (var index = 0; index < points.Length - 1; index++)
        {
            drawList.AddLine(points[index], points[index + 1], lineColor, 1f);
        }

        foreach (var point in points)
        {
            drawList.AddCircleFilled(point, LosMetrics.Scale(1.8f, scale), starColor, 10);
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
