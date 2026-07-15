using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

internal static class LosSection
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

internal static class LosCard
{
    private static readonly Dictionary<string, CardMeasurement> BackgroundMeasurements =
        new(StringComparer.Ordinal);

    private readonly record struct CardMeasurement(
        float Width,
        float Scale,
        float MinimumHeight,
        float ActualHeight);

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
        var minimumHeight = LosMetrics.Scale(
            (height > 0f ? height : LosMetrics.DefaultCardHeight)
            * LosMetrics.CardHeightMultiplier,
            scale);
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var position = ImGui.GetCursorScreenPos();
        var predictedHeight = GetPredictedHeight(id, width, scale, minimumHeight);
        var cardMaximum = position + new Vector2(width, predictedHeight);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(
            position,
            cardMaximum,
            LosPalette.ToUInt(LosTheme.ApplyBackgroundOpacity(LosPalette.Card)),
            LosMetrics.Scale(LosMetrics.CardRounding, scale));
        if (border)
        {
            drawList.AddRect(
                position,
                cardMaximum,
                LosPalette.ToUInt(LosPalette.Border),
                LosMetrics.Scale(LosMetrics.CardRounding, scale),
                ImDrawFlags.None,
                1f);
        }

        DrawJobSigilWatermark(position, width, scale);

        ImGui.PushID(id);
        ImGui.PushStyleVar(
            ImGuiStyleVar.CellPadding,
            new Vector2(LosMetrics.Scale(LosMetrics.CardPadding, scale)));
        try
        {
            var flags = ImGuiTableFlags.SizingStretchProp
                | ImGuiTableFlags.NoSavedSettings;
            if (ImGui.BeginTable("##card_layout", 1, flags, new Vector2(width, 0f)))
            {
                try
                {
                    ImGui.TableSetupColumn("content", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    DrawHeader(title, subtitle, scale);
                    DrawInsetContent(content, scale);
                }
                finally
                {
                    ImGui.EndTable();
                }
            }
        }
        finally
        {
            ImGui.PopStyleVar();
            ImGui.PopID();
        }

        var contentHeight = MathF.Max(0f, ImGui.GetItemRectMax().Y - position.Y);
        var actualHeight = MathF.Max(minimumHeight, contentHeight);
        if (!float.IsFinite(actualHeight))
            actualHeight = minimumHeight;
        BackgroundMeasurements[id] = new CardMeasurement(
            width,
            scale,
            minimumHeight,
            actualHeight);

        if (actualHeight > contentHeight)
        {
            ImGui.SetCursorScreenPos(position + new Vector2(0f, actualHeight));
            ImGui.Dummy(new Vector2(width, 0f));
        }

        ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(LosMetrics.CardSpacing, scale)));
    }

    private static void DrawInsetContent(Action content, float scale)
    {
        var available = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var inset = LosMetrics.Scale(LosMetrics.CardPadding, scale);
        if (available <= (inset * 2f) + 1f)
        {
            content();
            return;
        }

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
        try
        {
            var flags = ImGuiTableFlags.SizingStretchProp
                | ImGuiTableFlags.NoSavedSettings;
            if (!ImGui.BeginTable("##card_content_inset", 3, flags, new Vector2(available, 0f)))
                return;

            try
            {
                ImGui.TableSetupColumn("left", ImGuiTableColumnFlags.WidthFixed, inset);
                ImGui.TableSetupColumn("body", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("right", ImGuiTableColumnFlags.WidthFixed, inset);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();

                ImGui.PushStyleVar(
                    ImGuiStyleVar.CellPadding,
                    LosMetrics.Scale(new Vector2(LosMetrics.CardPadding, 4f), scale));
                try
                {
                    content();
                }
                finally
                {
                    ImGui.PopStyleVar();
                }

                ImGui.TableNextColumn();
            }
            finally
            {
                ImGui.EndTable();
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    private static float GetPredictedHeight(
        string id,
        float width,
        float scale,
        float minimumHeight)
    {
        if (!BackgroundMeasurements.TryGetValue(id, out var measurement))
            return minimumHeight;

        var sameLayout = MathF.Abs(measurement.Width - width) < 0.5f
            && MathF.Abs(measurement.Scale - scale) < 0.001f
            && MathF.Abs(measurement.MinimumHeight - minimumHeight) < 0.5f;
        return sameLayout
            ? MathF.Max(minimumHeight, measurement.ActualHeight)
            : minimumHeight;
    }

    private static void DrawJobSigilWatermark(Vector2 cardPosition, float cardWidth, float scale)
    {
        var center = cardPosition + new Vector2(
            cardWidth - LosMetrics.Scale(22f, scale),
            LosMetrics.Scale(20f, scale));
        var drawList = ImGui.GetWindowDrawList();
        var inner = LosMetrics.Scale(4f, scale);
        var outer = LosMetrics.Scale(10f, scale);
        var color = LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.18f));
        for (var index = 0; index < 6; index++)
        {
            var angle = index * (MathF.PI / 3f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            drawList.AddLine(
                center + direction * inner,
                center + direction * outer,
                color,
                1f);
        }

        drawList.AddCircleFilled(
            center,
            LosMetrics.Scale(2.5f, scale),
            LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Primary, 0.26f)),
            12);
    }

    private static void DrawHeader(string? title, string? subtitle, float scale)
    {
        if (string.IsNullOrWhiteSpace(title))
            return;

        var pos = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
        var height = LosMetrics.Scale(hasSubtitle ? 62f : 48f, scale);
        var drawList = ImGui.GetWindowDrawList();
        var chipSize = LosMetrics.Scale(40f, scale);
        var gap = LosMetrics.Scale(13f, scale);
        var diamondSize = LosMetrics.Scale(9f, scale);
        var textLeft = pos.X + chipSize + gap;
        var textWidth = MathF.Max(20f, width - chipSize - gap - LosMetrics.Scale(34f, scale));
        var fittedTitle = LosComponents.FitText(title, textWidth);
        var titleSize = ImGui.CalcTextSize(fittedTitle);

        drawList.AddRectFilled(
            pos,
            pos + new Vector2(chipSize),
            LosPalette.ToUInt(LosPalette.PrimaryMuted),
            LosMetrics.Scale(10f, scale));
        var monogram = MonogramFor(title);
        var monogramFontSize = ImGui.GetFontSize() * 0.72f;
        var monogramSize = ImGui.CalcTextSize(monogram) * 0.72f;
        drawList.AddText(
            ImGui.GetFont(),
            monogramFontSize,
            pos + ((new Vector2(chipSize) - monogramSize) * 0.5f),
            LosPalette.ToUInt(LosPalette.PrimaryHover),
            monogram);

        drawList.AddText(
            new Vector2(textLeft, pos.Y + LosMetrics.Scale(2f, scale)),
            LosPalette.ToUInt(LosPalette.TextPrimary),
            fittedTitle);
        if (hasSubtitle)
        {
            var fittedSubtitle = LosComponents.FitText(subtitle!, textWidth);
            var subtitleFontSize = ImGui.GetFontSize() * 0.88f;
            drawList.AddText(
                ImGui.GetFont(),
                subtitleFontSize,
                new Vector2(textLeft, pos.Y + titleSize.Y + LosMetrics.Scale(7f, scale)),
                LosPalette.ToUInt(LosPalette.TextMuted),
                fittedSubtitle);
        }

        var diamondCenter = new Vector2(
            pos.X + width - LosMetrics.Scale(13f, scale),
            pos.Y + LosMetrics.Scale(14f, scale));
        drawList.AddQuad(
            diamondCenter + new Vector2(0f, -diamondSize),
            diamondCenter + new Vector2(diamondSize, 0f),
            diamondCenter + new Vector2(0f, diamondSize),
            diamondCenter + new Vector2(-diamondSize, 0f),
            LosPalette.ToUInt(LosPalette.PrimaryHover),
            1.5f);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static string MonogramFor(string title)
        => title switch
        {
            "起手书签" => "OP",
            "循环咒式" => "RT",
            "禁忌页" => "!",
            "黑猫值班台" => "CAT",
            "模式书签" => "MD",
            "元素资源" => "EL",
            "调度时序" => "TM",
            "当前目标" => "TG",
            "循环状态" => "ST",
            "QT 按键" => "QT",
            "技能 Hotkey" => "HK",
            "浮窗大小" => "SZ",
            "主题变体" => "TH",
            "界面比例" => "UI",
            "动效偏好" => "FX",
            "窗口记忆" => "WN",
            "诊断" => "DG",
            "最近动作与资源" => "AC",
            "Resolver 与回执" => "RS",
            "日志状态与最近事件" => "LG",
            "Tracker / Ack" => "TR",
            _ => "BLM",
        };
}
