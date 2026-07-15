using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Components;

internal enum LosStatusTone
{
    Neutral,
    Info,
    Success,
    Warning,
    Danger,
}

internal static class LosComponents
{
    private enum CommandTone
    {
        Primary,
        Secondary,
        Danger,
    }

    private static readonly Dictionary<string, float> TransitionValues = new(StringComparer.Ordinal);

    public static bool Toggle(
        string id,
        ref bool value,
        float scale = 1f,
        bool reduceMotion = false,
        string? tooltip = null)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var size = LosMetrics.Scale(new Vector2(LosMetrics.ToggleWidth, LosMetrics.ToggleHeight), scale);
        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        ImGui.InvisibleButton($"##los_toggle_{id}", size);
        var clicked = ImGui.IsItemClicked();
        if (clicked)
            value = !value;

        var amount = Animate($"toggle:{id}", value ? 1f : 0f, reduceMotion, 14f);
        var hovered = ImGui.IsItemHovered();
        var offColor = hovered ? LosPalette.InputHover : LosPalette.Input;
        var onColor = hovered ? LosPalette.PrimaryHover : LosPalette.Primary;
        var background = LosPalette.Lerp(offColor, onColor, amount);
        var radius = size.Y * 0.5f;

        drawList.AddRectFilled(pos, pos + size, LosPalette.ToUInt(background), radius);
        drawList.AddRect(
            pos,
            pos + size,
            LosPalette.ToUInt(amount > 0.5f ? LosPalette.PrimaryHover : LosPalette.BorderStrong),
            radius,
            ImDrawFlags.None,
            1f);

        var knobRadius = MathF.Max(2f, radius - LosMetrics.Scale(3f, scale));
        var knobMinX = pos.X + radius;
        var knobMaxX = pos.X + size.X - radius;
        var knobX = knobMinX + ((knobMaxX - knobMinX) * amount);
        drawList.AddCircleFilled(
            new Vector2(knobX, pos.Y + radius),
            knobRadius,
            LosPalette.ToUInt(LosPalette.TextPrimary),
            24);

        TooltipIfHovered(tooltip, scale);
        return clicked;
    }

    public static bool SegmentButton(
        string id,
        string label,
        bool selected,
        Vector2 size = default,
        float scale = 1f,
        bool reduceMotion = false,
        string? tooltip = null)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var actualSize = new Vector2(
            size.X > 0f ? size.X : LosMetrics.Scale(96f, scale),
            size.Y > 0f ? size.Y : LosMetrics.Scale(LosMetrics.SegmentHeight, scale));
        var selection = Animate($"segment:{id}", selected ? 1f : 0f, reduceMotion, 16f);

        ImGui.PushStyleColor(
            ImGuiCol.Button,
            LosPalette.Lerp(LosPalette.Button, LosPalette.PrimaryMuted, selection));
        ImGui.PushStyleColor(
            ImGuiCol.ButtonHovered,
            LosPalette.Lerp(LosPalette.ButtonHover, LosPalette.WithAlpha(LosPalette.Primary, 0.28f), selection));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, LosPalette.WithAlpha(LosPalette.Primary, 0.38f));
        ImGui.PushStyleColor(
            ImGuiCol.Text,
            LosPalette.Lerp(LosPalette.TextSecondary, LosPalette.TextPrimary, selection));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, LosMetrics.Scale(LosMetrics.FrameRounding, scale));

        bool clicked;
        try
        {
            var visibleLabel = FitText(label, actualSize.X - LosMetrics.Scale(16f, scale));
            clicked = ImGui.Button($"{visibleLabel}##los_segment_{id}", actualSize);
        }
        finally
        {
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
        }

        TooltipIfHovered(tooltip, scale);
        return clicked;
    }

    public static bool PrimaryButton(
        string id,
        string label,
        string? icon = null,
        Vector2? size = null,
        float scale = 1f,
        string? tooltip = null,
        bool enabled = true)
        => CommandButton(id, label, icon, size, scale, tooltip, enabled, CommandTone.Primary);

    public static bool SecondaryButton(
        string id,
        string label,
        string? icon = null,
        Vector2? size = null,
        float scale = 1f,
        string? tooltip = null,
        bool enabled = true)
        => CommandButton(id, label, icon, size, scale, tooltip, enabled, CommandTone.Secondary);

    public static bool DangerButton(
        string id,
        string label,
        string? icon = null,
        Vector2? size = null,
        float scale = 1f,
        string? tooltip = null,
        bool enabled = true)
        => CommandButton(id, label, icon, size, scale, tooltip, enabled, CommandTone.Danger);

    public static bool SliderFloat(
        string label,
        ref float value,
        float min,
        float max,
        string format = "%.1f",
        string? tooltip = null,
        float width = 0f,
        float scale = 1f)
    {
        var actualWidth = LosMetrics.Scale(width > 0f ? width : LosMetrics.SliderWidth, scale);
        ImGui.SetNextItemWidth(actualWidth);
        PushCompactSliderStyle(scale);
        bool changed;
        try
        {
            changed = ImGui.SliderFloat(label, ref value, min, max, format);
        }
        finally
        {
            ImGui.PopStyleVar(4);
        }

        TooltipIfHovered(tooltip, scale);
        return changed;
    }

    public static bool SliderInt(
        string label,
        ref int value,
        int min,
        int max,
        string format = "%d",
        string? tooltip = null,
        float width = 0f,
        float scale = 1f)
    {
        var actualWidth = LosMetrics.Scale(width > 0f ? width : LosMetrics.SliderWidth, scale);
        ImGui.SetNextItemWidth(actualWidth);
        PushCompactSliderStyle(scale);
        bool changed;
        try
        {
            changed = ImGui.SliderInt(label, ref value, min, max, format);
        }
        finally
        {
            ImGui.PopStyleVar(4);
        }

        TooltipIfHovered(tooltip, scale);
        return changed;
    }

    public static bool Checkbox(
        string label,
        ref bool value,
        string? tooltip = null,
        float scale = 1f)
    {
        scale = LosMetrics.NormalizeScale(scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, LosMetrics.Scale(new Vector2(4f, 3f), scale));
        bool changed;
        try
        {
            changed = ImGui.Checkbox(label, ref value);
        }
        finally
        {
            ImGui.PopStyleVar();
        }

        TooltipIfHovered(tooltip, scale);
        return changed;
    }

    public static void StatusPill(
        string text,
        LosStatusTone tone,
        float scale = 1f,
        float fixedWidth = 0f)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var fontScale = 0.86f;
        var rawTextSize = ImGui.CalcTextSize(text);
        var desiredWidth = LosMetrics.Scale(30f, scale) + rawTextSize.X;
        var width = fixedWidth > 0f
            ? fixedWidth
            : Math.Clamp(
                desiredWidth,
                LosMetrics.Scale(76f, scale),
                LosMetrics.Scale(220f, scale));
        var height = LosMetrics.Scale(22f, scale);
        var size = new Vector2(width, height);
        var pos = ImGui.GetCursorScreenPos();
        var color = StatusColor(tone);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(pos, pos + size, LosPalette.ToUInt(StatusMutedColor(tone)), height * 0.5f);
        drawList.AddRect(
            pos,
            pos + size,
            LosPalette.ToUInt(LosPalette.WithAlpha(color, 0.58f)),
            height * 0.5f,
            ImDrawFlags.None,
            1f);

        var dotRadius = LosMetrics.Scale(3f, scale);
        var dotX = pos.X + LosMetrics.Scale(12f, scale);
        drawList.AddCircleFilled(new Vector2(dotX, pos.Y + (height * 0.5f)), dotRadius, LosPalette.ToUInt(color), 16);

        var textLeft = dotX + LosMetrics.Scale(8f, scale);
        var fitted = FitText(text, (pos.X + width) - textLeft - LosMetrics.Scale(8f, scale));
        var textSize = ImGui.CalcTextSize(fitted) * fontScale;
        drawList.AddText(
            ImGui.GetFont(),
            ImGui.GetFontSize() * fontScale,
            new Vector2(textLeft, pos.Y + ((height - textSize.Y) * 0.5f)),
            LosPalette.ToUInt(color),
            fitted);
        ImGui.Dummy(size);
    }

    public static void KeyValueRow(
        string label,
        string value,
        float scale = 1f,
        Vector4? valueColor = null,
        float? fixedHeight = null)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var available = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(fixedHeight ?? LosMetrics.RowHeight, scale);
        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var gap = LosMetrics.Scale(12f, scale);
        var labelWidth = MathF.Max(20f, (available - gap) * 0.52f);
        var valueWidth = MathF.Max(20f, available - labelWidth - gap);
        var fittedLabel = FitText(label, labelWidth);
        var fittedValue = FitText(value, valueWidth);
        var labelSize = ImGui.CalcTextSize(fittedLabel);
        var valueSize = ImGui.CalcTextSize(fittedValue);
        var textY = pos.Y + ((height - MathF.Max(labelSize.Y, valueSize.Y)) * 0.5f);

        drawList.AddText(new Vector2(pos.X, textY), LosPalette.ToUInt(LosPalette.TextSecondary), fittedLabel);
        drawList.AddText(
            new Vector2(pos.X + available - valueSize.X, textY),
            LosPalette.ToUInt(valueColor ?? LosPalette.TextPrimary),
            fittedValue);

        ImGui.InvisibleButton($"##kv_{label}", new Vector2(available, height));
        if (ImGui.IsItemHovered() && (fittedLabel != label || fittedValue != value))
            Tooltip($"{label}: {value}", scale);
    }

    public static void ProgressBar(
        string id,
        float fraction,
        string? overlay = null,
        LosStatusTone tone = LosStatusTone.Info,
        float scale = 1f,
        Vector2? size = null)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var requested = size ?? Vector2.Zero;
        var width = requested.X > 0f
            ? requested.X
            : MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var barHeight = requested.Y > 0f
            ? requested.Y
            : LosMetrics.Scale(8f, scale);
        var pos = ImGui.GetCursorScreenPos();
        var amount = float.IsFinite(fraction) ? Math.Clamp(fraction, 0f, 1f) : 0f;
        var color = StatusColor(tone);
        var drawList = ImGui.GetWindowDrawList();
        var labelFontScale = 0.88f;
        var labelHeight = string.IsNullOrWhiteSpace(overlay)
            ? 0f
            : ImGui.GetFontSize() * labelFontScale;
        var labelGap = labelHeight > 0f ? LosMetrics.Scale(5f, scale) : 0f;
        var totalHeight = labelHeight + labelGap + barHeight;
        var actualSize = new Vector2(width, totalHeight);
        var barPosition = pos + new Vector2(0f, labelHeight + labelGap);
        var rounding = MathF.Min(LosMetrics.Scale(4f, scale), barHeight * 0.5f);

        if (!string.IsNullOrWhiteSpace(overlay))
        {
            var fitted = FitText(overlay, width);
            drawList.AddText(
                ImGui.GetFont(),
                ImGui.GetFontSize() * labelFontScale,
                pos,
                LosPalette.ToUInt(LosPalette.TextSecondary),
                fitted);
        }

        drawList.AddRectFilled(
            barPosition,
            barPosition + new Vector2(width, barHeight),
            LosPalette.ToUInt(LosPalette.Track),
            rounding);
        if (amount > 0f)
        {
            var fillWidth = MathF.Max(rounding * 2f, width * amount);
            fillWidth = MathF.Min(fillWidth, width);
            drawList.AddRectFilled(
                barPosition,
                barPosition + new Vector2(fillWidth, barHeight),
                LosPalette.ToUInt(LosPalette.WithAlpha(color, 0.82f)),
                rounding);
        }

        ImGui.InvisibleButton($"##progress_{id}", actualSize);
    }

    internal static void PushCompactSliderStyle(float scale)
    {
        scale = LosMetrics.NormalizeScale(scale);
        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            LosMetrics.Scale(new Vector2(8f, 3f), scale));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, LosMetrics.Scale(8f, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, LosMetrics.Scale(8f, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, LosMetrics.Scale(10f, scale));
    }

    public static void TooltipIfHovered(string? text, float scale = 1f)
    {
        if (!string.IsNullOrWhiteSpace(text) && ImGui.IsItemHovered())
            Tooltip(text, scale);
    }

    public static void Tooltip(string text, float scale = 1f)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(LosMetrics.Scale(LosMetrics.TooltipMaxWidth, scale));
        try
        {
            ImGui.TextUnformatted(text);
        }
        finally
        {
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    public static Vector4 StatusColor(LosStatusTone tone)
        => tone switch
        {
            LosStatusTone.Info => LosPalette.Info,
            LosStatusTone.Success => LosPalette.Success,
            LosStatusTone.Warning => LosPalette.Warning,
            LosStatusTone.Danger => LosPalette.Danger,
            _ => LosPalette.TextSecondary,
        };

    public static Vector4 StatusMutedColor(LosStatusTone tone)
        => tone switch
        {
            LosStatusTone.Info => LosPalette.CyanMuted,
            LosStatusTone.Success => LosPalette.SuccessMuted,
            LosStatusTone.Warning => LosPalette.WarningMuted,
            LosStatusTone.Danger => LosPalette.DangerMuted,
            _ => LosPalette.WithAlpha(LosPalette.TextMuted, 0.12f),
        };

    internal static string FitText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            return string.Empty;
        if (ImGui.CalcTextSize(text).X <= maxWidth)
            return text;

        const string suffix = "...";
        var suffixWidth = ImGui.CalcTextSize(suffix).X;
        if (suffixWidth >= maxWidth)
            return suffix;

        var length = text.Length;
        while (length > 0)
        {
            var candidate = text[..length] + suffix;
            if (ImGui.CalcTextSize(candidate).X <= maxWidth)
                return candidate;
            length--;
        }

        return suffix;
    }

    private static bool CommandButton(
        string id,
        string label,
        string? icon,
        Vector2? size,
        float scale,
        string? tooltip,
        bool enabled,
        CommandTone tone)
    {
        scale = LosMetrics.NormalizeScale(scale);
        var iconOnly = !string.IsNullOrWhiteSpace(icon) && string.IsNullOrWhiteSpace(label);
        var actualSize = size ?? (iconOnly
            ? new Vector2(LosMetrics.Scale(LosMetrics.IconButtonSize, scale))
            : LosMetrics.Scale(new Vector2(LosMetrics.CommandButtonWidth, LosMetrics.ButtonHeight), scale));
        actualSize.X = actualSize.X > 0f
            ? actualSize.X
            : LosMetrics.Scale(LosMetrics.CommandButtonWidth, scale);
        actualSize.Y = actualSize.Y > 0f
            ? actualSize.Y
            : LosMetrics.Scale(LosMetrics.ButtonHeight, scale);

        var visible = string.IsNullOrWhiteSpace(icon)
            ? label
            : string.IsNullOrWhiteSpace(label)
                ? icon
                : $"{icon}  {label}";
        visible = FitText(visible ?? string.Empty, actualSize.X - LosMetrics.Scale(18f, scale));

        var colors = tone switch
        {
            CommandTone.Primary => (LosPalette.Primary, LosPalette.PrimaryHover, LosPalette.PrimaryActive),
            CommandTone.Danger => (LosPalette.Danger, LosPalette.DangerHover, LosPalette.DangerActive),
            _ => (LosPalette.Button, LosPalette.ButtonHover, LosPalette.ButtonActive),
        };

        ImGui.PushStyleColor(ImGuiCol.Button, colors.Item1);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.Item2);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors.Item3);
        ImGui.PushStyleColor(ImGuiCol.Text, LosPalette.TextPrimary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, LosMetrics.Scale(LosMetrics.FrameRounding, scale));

        if (!enabled)
            ImGui.BeginDisabled();

        bool clicked;
        try
        {
            clicked = ImGui.Button($"{visible}##los_command_{id}", actualSize);
        }
        finally
        {
            if (!enabled)
                ImGui.EndDisabled();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(4);
        }

        TooltipIfHovered(tooltip, scale);
        return clicked;
    }

    private static float Animate(string key, float target, bool reduceMotion, float speed)
    {
        if (reduceMotion)
        {
            TransitionValues[key] = target;
            return target;
        }

        if (!TransitionValues.TryGetValue(key, out var current))
            current = target;

        var deltaTime = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.1f);
        current += (target - current) * Math.Clamp(deltaTime * LosMetrics.MotionStep(false, speed), 0f, 1f);
        if (MathF.Abs(target - current) < 0.001f)
            current = target;

        TransitionValues[key] = current;
        return current;
    }
}
