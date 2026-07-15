using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using LosPr.BLM.Data;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;
using PromeRotation;
using PromeRotation.Data;

namespace LosPr.BLM.UI;

internal sealed class BlmQuickOverlay : IDisposable
{
    private const int Columns = 3;
    private const float ControlWidth = 300f;
    private const float ControlHeight = 78f;
    private const float QtButtonWidth = 106f;
    private const float QtButtonHeight = 42f;
    private const float QtCellHeight = 55f;
    private const float QtGapX = 8f;
    private const float QtPanelPadding = 12f;
    private const int HotkeyColumns = 4;
    private const float HotkeySlotWidth = 76f;
    private const float HotkeySlotHeight = 70f;
    private const float HotkeyIconSize = 62f;
    private const float HotkeyGapX = 6f;
    private const float HotkeyGapY = 8f;
    private const float HotkeyPanelPadding = 12f;
    private const float HotkeyPanelTop = 18f;
    private const float HotkeyPanelBottom = 10f;
    private const string ControlWindowTitle = "Los 控制###LosPr_BlmQuickControl";
    private const string QtWindowTitle = "Los QT###LosPr_BlmQuickQt";
    private const string HotkeyWindowTitle = "Los Hotkey###LosPr_BlmQuickHotkey";

    private static readonly ImGuiWindowFlags OverlayWindowFlags =
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoSavedSettings;

    private readonly BlackMageSettingsStore _store;
    private readonly string[] _qtKeys;
    private readonly string[] _qtIds;
    private readonly string[] _hotkeyIds;
    private readonly Func<bool> _isConsoleOpen;
    private readonly Action _toggleConsole;
    private bool _active;
    private bool _disposed;
    private bool _controlPlacementPending = true;
    private bool _qtPlacementPending = true;
    private bool _hotkeyPlacementPending = true;
    private Vector2 _lastControlPosition = new(float.NaN, float.NaN);
    private Vector2 _lastQtPosition = new(float.NaN, float.NaN);
    private Vector2 _lastHotkeyPosition = new(float.NaN, float.NaN);
    private DateTime _nextHostWarningUtc;

    public BlmQuickOverlay(
        BlackMageSettingsStore store,
        IEnumerable<string> qtKeys,
        Func<bool> isConsoleOpen,
        Action toggleConsole)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(qtKeys);
        _qtKeys = [.. qtKeys];
        _qtIds = new string[_qtKeys.Length];
        for (var index = 0; index < _qtIds.Length; index++)
            _qtIds[index] = $"##los_qt_{index}";
        _hotkeyIds = new string[BlmHotkeyCatalog.Entries.Count];
        for (var index = 0; index < _hotkeyIds.Length; index++)
            _hotkeyIds[index] = $"##los_hotkey_{BlmHotkeyCatalog.Entries[index].Key}";
        _isConsoleOpen = isConsoleOpen ?? throw new ArgumentNullException(nameof(isConsoleOpen));
        _toggleConsole = toggleConsole ?? throw new ArgumentNullException(nameof(toggleConsole));
    }

    public void Activate()
    {
        if (_disposed || _active)
            return;

        _active = true;
        _controlPlacementPending = true;
        _qtPlacementPending = true;
        _hotkeyPlacementPending = true;
        HideNativeWindows();
    }

    public void EnsureActive()
    {
        if (!_active)
            Activate();
        else
            HideNativeWindows();
    }

    public void Deactivate()
    {
        if (!_active)
            return;

        _active = false;
        BlmHotkeyCatalog.ClearPending();
        RestoreNativeWindows();
        _store.SaveNow();
    }

    public void Draw()
    {
        if (_disposed || !_active)
            return;

        HideNativeWindows();
        var settings = _store.Settings;
        LosPalette.ApplyStyle(settings.UiThemeStyle);
        var scale = LosMetrics.NormalizeScale(settings.UiScale);
        var qtScale = OverlayScale(scale * settings.QtPanelScale);
        var hotkeyScale = OverlayScale(scale * settings.HotkeyPanelScale);
        BlmKeyBindingManager.ProcessBindings(_store);
        DrawControlWindow(settings, scale);
        DrawQtWindow(settings, qtScale);
        DrawHotkeyWindow(settings, hotkeyScale);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Deactivate();
        _disposed = true;
    }

    private void DrawControlWindow(BlackMageSettings settings, float scale)
    {
        var size = Scale(new Vector2(ControlWidth, ControlHeight), scale);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ApplyInitialPosition(
            control: true,
            settings,
            size,
            ref _controlPlacementPending);

        PushOverlayWindowStyle();
        try
        {
            var open = true;
            var visible = ImGui.Begin(ControlWindowTitle, ref open, OverlayWindowFlags);
            try
            {
                if (!visible)
                    return;

                ImGui.SetWindowFontScale(scale);
                var position = ImGui.GetWindowPos();
                DrawControlChrome(position, size, scale);
                DrawControlButtons(position, scale);
                HandleWindowDrag();
                CapturePosition(control: true, settings, ImGui.GetWindowPos());
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            PopOverlayWindowStyle();
        }
    }

    private void DrawQtWindow(BlackMageSettings settings, float scale)
    {
        var visibleCount = _qtKeys.Count(key => !settings.HiddenQtKeys.Contains(key));
        if (visibleCount == 0)
            return;

        var rows = Math.Max(1, (int)Math.Ceiling(visibleCount / (double)Columns));
        var panelWidth = (QtButtonWidth * Columns)
            + (QtGapX * (Columns - 1))
            + (QtPanelPadding * 2f);
        var panelHeight = (QtCellHeight * rows)
            + (QtPanelPadding * 2f);
        var size = Scale(new Vector2(panelWidth, panelHeight), scale);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ApplyInitialPosition(
            control: false,
            settings,
            size,
            ref _qtPlacementPending);

        PushOverlayWindowStyle();
        try
        {
            var open = true;
            var visible = ImGui.Begin(QtWindowTitle, ref open, OverlayWindowFlags);
            try
            {
                if (!visible)
                    return;

                ImGui.SetWindowFontScale(scale);
                var position = ImGui.GetWindowPos();
                DrawQtChrome(position, size, scale);
                DrawQtButtons(position, settings, scale);
                HandleWindowDrag();
                CapturePosition(control: false, settings, ImGui.GetWindowPos());
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            PopOverlayWindowStyle();
        }
    }

    private void DrawHotkeyWindow(BlackMageSettings settings, float scale)
    {
        var visibleCount = BlmHotkeyCatalog.Entries.Count(
            entry => !settings.HiddenHotkeyKeys.Contains(entry.Key));
        if (visibleCount == 0)
            return;

        var rows = Math.Max(1, (int)Math.Ceiling(
            visibleCount / (double)HotkeyColumns));
        var panelWidth = (HotkeySlotWidth * HotkeyColumns)
            + (HotkeyGapX * (HotkeyColumns - 1))
            + (HotkeyPanelPadding * 2f);
        var panelHeight = HotkeyPanelTop
            + (HotkeySlotHeight * rows)
            + (HotkeyGapY * (rows - 1))
            + HotkeyPanelBottom;
        var size = Scale(new Vector2(panelWidth, panelHeight), scale);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ApplyInitialHotkeyPosition(settings, size, scale);

        PushOverlayWindowStyle();
        try
        {
            var open = true;
            var visible = ImGui.Begin(HotkeyWindowTitle, ref open, OverlayWindowFlags);
            try
            {
                if (!visible)
                    return;

                ImGui.SetWindowFontScale(scale);
                var position = ImGui.GetWindowPos();
                DrawHotkeyChrome(position, size, scale);
                DrawHotkeyButtons(position, settings, scale);
                HandleWindowDrag();
                CaptureHotkeyPosition(settings, ImGui.GetWindowPos());
            }
            finally
            {
                ImGui.End();
            }
        }
        finally
        {
            PopOverlayWindowStyle();
        }
    }

    private void DrawControlChrome(Vector2 position, Vector2 size, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bodyMin = position + Scale(new Vector2(2f, 11f), scale);
        var bodyMax = position + size - Scale(new Vector2(2f, 2f), scale);
        DrawPanelBody(drawList, bodyMin, bodyMax, scale);

        var earColor = LosPalette.ToUInt(LosPalette.Card);
        var earBorder = LosPalette.ToUInt(WithAlpha(LosPalette.Cyan, 0.58f));
        var innerEar = LosPalette.ToUInt(new Vector4(1f, 0.54f, 0.80f, 0.34f));
        DrawCatEar(
            drawList,
            position + Scale(new Vector2(30f, 15f), scale),
            position + Scale(new Vector2(43f, 1f), scale),
            position + Scale(new Vector2(72f, 15f), scale),
            earColor,
            earBorder,
            innerEar,
            scale);
        DrawCatEar(
            drawList,
            position + Scale(new Vector2(228f, 15f), scale),
            position + Scale(new Vector2(257f, 1f), scale),
            position + Scale(new Vector2(270f, 15f), scale),
            earColor,
            earBorder,
            innerEar,
            scale);
    }

    private void DrawControlButtons(Vector2 position, float scale)
    {
        var mainMin = position + Scale(new Vector2(14f, 24f), scale);
        var mainSize = Scale(new Vector2(196f, 42f), scale);
        var settingsMin = position + Scale(new Vector2(218f, 24f), scale);
        var settingsSize = Scale(new Vector2(68f, 42f), scale);

        var mainInteraction = InteractAt("##los_acr_state", mainMin, mainSize);
        if (mainInteraction.LeftClicked)
        {
            var state = PromeSettings.Instance.EnableAcr;
            PromeSettings.Instance.EnableAcr = state == AcrState.Off
                ? AcrState.On
                : AcrState.Off;
        }
        else if (mainInteraction.RightClicked)
        {
            var state = PromeSettings.Instance.EnableAcr;
            if (state == AcrState.On)
                PromeSettings.Instance.EnableAcr = AcrState.Hold;
            else if (state == AcrState.Hold)
                PromeSettings.Instance.EnableAcr = AcrState.On;
        }

        if (mainInteraction.Hovered)
            ImGui.SetTooltip("左键：开启/关闭；右键：停手/恢复");
        DrawAcrStateButton(mainMin, mainSize, mainInteraction, scale);

        var settingsInteraction = InteractAt("##los_open_console", settingsMin, settingsSize);
        if (settingsInteraction.LeftClicked)
            _toggleConsole();
        if (settingsInteraction.Hovered)
            ImGui.SetTooltip(_isConsoleOpen() ? "关闭 Los 黑魔控制台" : "打开 Los 黑魔控制台");
        DrawSettingsButton(settingsMin, settingsSize, settingsInteraction, scale);
    }

    private static void DrawAcrStateButton(
        Vector2 minimum,
        Vector2 size,
        ButtonInteraction interaction,
        float scale)
    {
        var state = PromeSettings.Instance.EnableAcr;
        var label = state switch
        {
            AcrState.On => "运行中",
            AcrState.Hold => "停手中",
            _ => "已关闭",
        };
        var color = state switch
        {
            AcrState.On => LosPalette.Primary,
            AcrState.Hold => LosPalette.Arcane,
            _ => LosPalette.Danger,
        };
        DrawCommandButton(minimum, size, color, interaction, scale);

        var drawList = ImGui.GetWindowDrawList();
        var faceCenter = minimum + Scale(new Vector2(31f, 21f), scale);
        DrawControlCatFace(drawList, faceCenter, color, scale);
        var statusColor = state switch
        {
            AcrState.On => LosPalette.Success,
            AcrState.Hold => LosPalette.Warning,
            _ => LosPalette.Danger,
        };
        drawList.AddCircleFilled(
            minimum + Scale(new Vector2(55f, 21f), scale),
            Scale(3.2f, scale),
            LosPalette.ToUInt(statusColor),
            16);
        DrawCenteredText(
            drawList,
            minimum + Scale(new Vector2(68f, 0f), scale),
            minimum + size,
            label,
            LosPalette.TextPrimary,
            scale);
    }

    private void DrawSettingsButton(
        Vector2 minimum,
        Vector2 size,
        ButtonInteraction interaction,
        float scale)
    {
        var color = _isConsoleOpen() ? LosPalette.Primary : LosPalette.Button;
        DrawCommandButton(minimum, size, color, interaction, scale);
        var drawList = ImGui.GetWindowDrawList();
        DrawBell(
            drawList,
            minimum + Scale(new Vector2(13f, 21f), scale),
            scale);
        DrawCenteredText(
            drawList,
            minimum + Scale(new Vector2(24f, 0f), scale),
            minimum + size,
            "设置",
            LosPalette.TextPrimary,
            scale);
    }

    private void DrawQtChrome(Vector2 position, Vector2 size, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var minimum = position + Scale(new Vector2(1f), scale);
        var maximum = position + size - Scale(new Vector2(1f), scale);
        DrawPanelBody(drawList, minimum, maximum, scale);
    }

    private void DrawQtButtons(
        Vector2 position,
        BlackMageSettings settings,
        float scale)
    {
        var start = position + Scale(
            new Vector2(QtPanelPadding, QtPanelPadding),
            scale);
        var cellSize = Scale(new Vector2(QtButtonWidth, QtCellHeight), scale);
        var visibleIndex = 0;
        for (var index = 0; index < _qtKeys.Length; index++)
        {
            var key = _qtKeys[index];
            if (settings.HiddenQtKeys.Contains(key))
                continue;

            var column = visibleIndex % Columns;
            var row = visibleIndex / Columns;
            var cellMin = start + Scale(
                new Vector2(
                    column * (QtButtonWidth + QtGapX),
                    row * QtCellHeight),
                scale);
            var interaction = InteractAt(_qtIds[index], cellMin, cellSize);
            var enabled = PromeSettings.Instance.GetQt(key);
            if (interaction.LeftClicked)
            {
                enabled = !enabled;
                PromeSettings.Instance.SetQt(key, enabled);
                var persistedValue = enabled;
                _store.Update(settings => settings.QtStates[key] = persistedValue);
            }

            var bindingLabel = settings.QtBindings.TryGetValue(key, out var binding)
                ? BlmKeyBindingManager.FormatCompact(binding)
                : null;
            if (interaction.Hovered)
            {
                var bindingHint = bindingLabel is null ? string.Empty : $"\n快捷键：{bindingLabel}";
                ImGui.SetTooltip($"{key}：{(enabled ? "开启" : "关闭")}{bindingHint}");
            }

            DrawQtButton(cellMin, key, bindingLabel, enabled, interaction, scale);
            visibleIndex++;
        }
    }

    private static void DrawHotkeyChrome(Vector2 position, Vector2 size, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bodyMin = position + Scale(new Vector2(1f, 9f), scale);
        var bodyMax = position + size - Scale(new Vector2(1f), scale);
        DrawPanelBody(drawList, bodyMin, bodyMax, scale);

        var earColor = LosPalette.ToUInt(LosPalette.Card);
        var earBorder = LosPalette.ToUInt(WithAlpha(LosPalette.Cyan, 0.62f));
        var innerEar = LosPalette.ToUInt(new Vector4(1f, 0.54f, 0.80f, 0.34f));
        DrawCatEar(
            drawList,
            position + Scale(new Vector2(44f, 13f), scale),
            position + Scale(new Vector2(62f, 1f), scale),
            position + Scale(new Vector2(92f, 13f), scale),
            earColor,
            earBorder,
            innerEar,
            scale);
        DrawCatEar(
            drawList,
            position + new Vector2(size.X, 0f) + Scale(new Vector2(-92f, 13f), scale),
            position + new Vector2(size.X, 0f) + Scale(new Vector2(-62f, 1f), scale),
            position + new Vector2(size.X, 0f) + Scale(new Vector2(-44f, 13f), scale),
            earColor,
            earBorder,
            innerEar,
            scale);
    }

    private void DrawHotkeyButtons(
        Vector2 position,
        BlackMageSettings settings,
        float scale)
    {
        var start = position + Scale(
            new Vector2(HotkeyPanelPadding, HotkeyPanelTop),
            scale);
        var slotSize = Scale(new Vector2(HotkeySlotWidth, HotkeySlotHeight), scale);
        var visibleIndex = 0;
        for (var index = 0; index < BlmHotkeyCatalog.Entries.Count; index++)
        {
            var definition = BlmHotkeyCatalog.Entries[index];
            if (settings.HiddenHotkeyKeys.Contains(definition.Key))
                continue;

            var column = visibleIndex % HotkeyColumns;
            var row = visibleIndex / HotkeyColumns;
            var slotMin = start + Scale(
                new Vector2(
                    column * (HotkeySlotWidth + HotkeyGapX),
                    row * (HotkeySlotHeight + HotkeyGapY)),
                scale);
            var interaction = InteractAt(_hotkeyIds[index], slotMin, slotSize);
            if (interaction.LeftClicked)
                BlmHotkeyCatalog.TryActivate(definition);
            var bindingLabel = settings.HotkeyBindings.TryGetValue(definition.Key, out var binding)
                ? BlmKeyBindingManager.FormatCompact(binding)
                : null;
            if (interaction.Hovered)
            {
                var bindingHint = bindingLabel is null ? string.Empty : $"\n快捷键：{bindingLabel}";
                ImGui.SetTooltip(BlmHotkeyCatalog.BuildTooltip(definition) + bindingHint);
            }

            DrawHotkeyButton(slotMin, definition, bindingLabel, interaction, scale);
            visibleIndex++;
        }
    }

    private static void DrawHotkeyButton(
        Vector2 slotMinimum,
        in BlmHotkeyDefinition definition,
        string? bindingLabel,
        ButtonInteraction interaction,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = Scale(new Vector2(HotkeyIconSize), scale);
        var iconMinimum = slotMinimum + Scale(
            new Vector2((HotkeySlotWidth - HotkeyIconSize) * 0.5f, 2f),
            scale);
        var iconMaximum = iconMinimum + iconSize;
        var frameMinimum = iconMinimum - Scale(new Vector2(2f), scale);
        var frameMaximum = iconMaximum + Scale(new Vector2(2f), scale);
        var rounding = Scale(11f, scale);
        var available = BlmHotkeyCatalog.IsAvailable(definition);
        var pending = BlmHotkeyCatalog.IsPending(definition);
        var cooldown = BlmHotkeyCatalog.GetCooldown(definition);
        var charges = BlmHotkeyCatalog.GetCharges(definition);
        IDalamudTextureWrap? icon = BlmHotkeyCatalog.ResolveIcon(definition);

        drawList.AddRectFilled(
            frameMinimum + Scale(new Vector2(1f, 2f), scale),
            frameMaximum + Scale(new Vector2(1f, 2f), scale),
            LosPalette.ToUInt(new Vector4(0f, 0f, 0f, 0.28f)),
            rounding);
        drawList.AddRectFilled(
            frameMinimum,
            frameMaximum,
            LosPalette.ToUInt(interaction.Hovered
                ? WithAlpha(LosPalette.Cyan, 0.18f)
                : WithAlpha(LosPalette.Input, 0.74f)),
            rounding);

        if (icon is not null)
        {
            drawList.AddImage(icon.Handle, iconMinimum, iconMaximum);
        }
        else
        {
            drawList.AddRectFilled(
                iconMinimum,
                iconMaximum,
                LosPalette.ToUInt(WithAlpha(LosPalette.Background, 0.92f)),
                Scale(9f, scale));
            DrawCenteredText(
                drawList,
                iconMinimum,
                iconMaximum,
                "?",
                LosPalette.TextSecondary,
                scale);
        }

        if (!available)
        {
            drawList.AddRectFilled(
                iconMinimum,
                iconMaximum,
                LosPalette.ToUInt(new Vector4(0.025f, 0.020f, 0.060f, 0.60f)),
                Scale(9f, scale));
        }

        if (cooldown > 0.05f && charges == 0)
        {
            drawList.AddRectFilled(
                iconMinimum,
                iconMaximum,
                LosPalette.ToUInt(new Vector4(0.020f, 0.015f, 0.045f, 0.46f)),
                Scale(9f, scale));
            var cooldownText = cooldown >= 10f
                ? MathF.Ceiling(cooldown).ToString("0")
                : cooldown.ToString("0.0");
            DrawHotkeyBadge(
                drawList,
                iconMinimum + Scale(new Vector2(3f, HotkeyIconSize - 19f), scale),
                cooldownText,
                LosPalette.Warning,
                alignRight: false,
                scale);
        }

        if (charges > 0)
        {
            DrawHotkeyBadge(
                drawList,
                iconMaximum - Scale(new Vector2(3f, 19f), scale),
                charges.ToString(),
                LosPalette.Arcane,
                alignRight: true,
                scale);
        }

        if (bindingLabel is not null)
        {
            DrawBindingBadge(
                drawList,
                iconMinimum + Scale(new Vector2(3f), scale),
                bindingLabel,
                scale);
        }

        var border = pending
            ? LosPalette.Warning
            : interaction.Hovered
                ? LosPalette.Cyan
                : LosPalette.Border;
        drawList.AddRect(
            frameMinimum,
            frameMaximum,
            LosPalette.ToUInt(WithAlpha(border, pending ? 0.96f : 0.78f)),
            rounding,
            ImDrawFlags.None,
            Scale(pending || interaction.Hovered ? 2f : 1.2f, scale));
        drawList.AddRect(
            iconMinimum + Scale(new Vector2(1f), scale),
            iconMaximum - Scale(new Vector2(1f), scale),
            LosPalette.ToUInt(WithAlpha(LosPalette.TextPrimary, 0.16f)),
            Scale(8f, scale),
            ImDrawFlags.None,
            Scale(1f, scale));

        if (pending)
        {
            drawList.AddRect(
                frameMinimum - Scale(new Vector2(2f), scale),
                frameMaximum + Scale(new Vector2(2f), scale),
                LosPalette.ToUInt(WithAlpha(LosPalette.Warning, 0.46f)),
                Scale(13f, scale),
                ImDrawFlags.None,
                Scale(1.4f, scale));
        }

    }

    private static void DrawBindingBadge(
        ImDrawListPtr drawList,
        Vector2 minimum,
        string text,
        float scale)
    {
        var font = ImGui.GetFont();
        var fontScale = 0.66f;
        var fontSize = ImGui.GetFontSize() * fontScale;
        var textSize = ImGui.CalcTextSize(text) * fontScale;
        var padding = Scale(new Vector2(3f, 1f), scale);
        var maximum = minimum + textSize + (padding * 2f);
        drawList.AddRectFilled(
            minimum,
            maximum,
            LosPalette.ToUInt(new Vector4(0.025f, 0.020f, 0.070f, 0.90f)),
            Scale(4f, scale));
        drawList.AddRect(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(LosPalette.Cyan, 0.82f)),
            Scale(4f, scale),
            ImDrawFlags.None,
            Scale(1f, scale));
        drawList.AddText(
            font,
            fontSize,
            minimum + padding,
            LosPalette.ToUInt(LosPalette.TextPrimary),
            text);
    }

    private static void DrawHotkeyBadge(
        ImDrawListPtr drawList,
        Vector2 anchor,
        string text,
        Vector4 color,
        bool alignRight,
        float scale)
    {
        var textSize = ImGui.CalcTextSize(text);
        var padding = Scale(new Vector2(4f, 1f), scale);
        var size = textSize + (padding * 2f);
        var minimum = alignRight
            ? new Vector2(anchor.X - size.X, anchor.Y)
            : anchor;
        var maximum = minimum + size;
        drawList.AddRectFilled(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(color, 0.88f)),
            Scale(5f, scale));
        drawList.AddRect(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(LosPalette.TextPrimary, 0.42f)),
            Scale(5f, scale));
        drawList.AddText(
            minimum + padding + Scale(new Vector2(1f), scale),
            LosPalette.ToUInt(new Vector4(0f, 0f, 0f, 0.78f)),
            text);
        drawList.AddText(
            minimum + padding,
            LosPalette.ToUInt(LosPalette.TextPrimary),
            text);
    }

    private static void DrawQtButton(
        Vector2 cellMinimum,
        string label,
        string? bindingLabel,
        bool enabled,
        ButtonInteraction interaction,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bodyMin = cellMinimum + Scale(new Vector2(0f, 7f), scale);
        var bodySize = Scale(new Vector2(QtButtonWidth, QtButtonHeight), scale);
        var bodyMax = bodyMin + bodySize;
        var fill = enabled ? LosPalette.Primary : LosPalette.Input;
        var border = enabled ? LosPalette.Cyan : LosPalette.Border;
        var outerEar = LosPalette.ToUInt(fill);
        var earBorder = LosPalette.ToUInt(WithAlpha(border, interaction.Hovered ? 0.95f : 0.72f));
        var innerEar = LosPalette.ToUInt(new Vector4(1f, 0.54f, 0.80f, enabled ? 0.44f : 0.16f));

        DrawCatEar(
            drawList,
            cellMinimum + Scale(new Vector2(10f, 10f), scale),
            cellMinimum + Scale(new Vector2(20f, 0f), scale),
            cellMinimum + Scale(new Vector2(42f, 10f), scale),
            outerEar,
            earBorder,
            innerEar,
            scale);
        DrawCatEar(
            drawList,
            cellMinimum + Scale(new Vector2(64f, 10f), scale),
            cellMinimum + Scale(new Vector2(86f, 0f), scale),
            cellMinimum + Scale(new Vector2(96f, 10f), scale),
            outerEar,
            earBorder,
            innerEar,
            scale);

        drawList.AddRectFilled(
            bodyMin + Scale(new Vector2(1f, 3f), scale),
            bodyMax + Scale(new Vector2(1f, 3f), scale),
            LosPalette.ToUInt(new Vector4(0f, 0f, 0f, 0.22f)),
            Scale(13f, scale));
        drawList.AddRectFilled(
            bodyMin,
            bodyMax,
            LosPalette.ToUInt(fill),
            Scale(13f, scale));
        if (enabled)
        {
            drawList.AddRectFilled(
                bodyMin + Scale(new Vector2(1f), scale),
                bodyMax - Scale(new Vector2(1f, 13f), scale),
                LosPalette.ToUInt(WithAlpha(LosPalette.PrimaryHover, 0.62f)),
                Scale(12f, scale));
        }

        if (interaction.Hovered)
        {
            drawList.AddRectFilled(
                bodyMin,
                bodyMax,
                LosPalette.ToUInt(WithAlpha(LosPalette.Cyan, interaction.Active ? 0.18f : 0.10f)),
                Scale(13f, scale));
        }

        drawList.AddRect(
            bodyMin,
            bodyMax,
            earBorder,
            Scale(13f, scale),
            ImDrawFlags.None,
            enabled || interaction.Hovered ? Scale(1.5f, scale) : Scale(1f, scale));
        DrawTail(drawList, bodyMin, bodyMax, border, enabled, scale);

        var fitted = LosComponents.FitText(label, bodySize.X - Scale(14f, scale));
        DrawCenteredText(
            drawList,
            bindingLabel is null
                ? bodyMin
                : bodyMin + Scale(new Vector2(0f, 8f), scale),
            bodyMax,
            fitted,
            enabled ? LosPalette.TextPrimary : LosPalette.TextSecondary,
            scale);
        if (bindingLabel is not null)
        {
            DrawBindingBadge(
                drawList,
                bodyMin + Scale(new Vector2(4f, 3f), scale),
                bindingLabel,
                scale);
        }
    }

    private static void DrawPanelBody(
        ImDrawListPtr drawList,
        Vector2 minimum,
        Vector2 maximum,
        float scale)
    {
        var rounding = Scale(18f, scale);
        drawList.AddRectFilled(
            minimum + Scale(new Vector2(2f, 3f), scale),
            maximum + Scale(new Vector2(2f, 3f), scale),
            LosPalette.ToUInt(new Vector4(0f, 0f, 0f, 0.22f)),
            rounding);
        drawList.AddRectFilled(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(LosPalette.Background, 0.95f)),
            rounding);
        drawList.AddRectFilled(
            minimum + Scale(new Vector2(2f), scale),
            maximum - Scale(new Vector2(2f), scale),
            LosPalette.ToUInt(WithAlpha(LosPalette.Card, 0.88f)),
            Scale(16f, scale));
        drawList.AddRect(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(LosPalette.Cyan, 0.68f)),
            rounding,
            ImDrawFlags.None,
            Scale(1.2f, scale));
        drawList.AddRect(
            minimum + Scale(new Vector2(3f), scale),
            maximum - Scale(new Vector2(3f), scale),
            LosPalette.ToUInt(WithAlpha(LosPalette.TextPrimary, 0.10f)),
            Scale(15f, scale),
            ImDrawFlags.None,
            Scale(1f, scale));
    }

    private static void DrawCommandButton(
        Vector2 minimum,
        Vector2 size,
        Vector4 color,
        ButtonInteraction interaction,
        float scale)
    {
        var maximum = minimum + size;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            minimum + Scale(new Vector2(1f, 2f), scale),
            maximum + Scale(new Vector2(1f, 2f), scale),
            LosPalette.ToUInt(new Vector4(0f, 0f, 0f, 0.24f)),
            Scale(14f, scale));
        drawList.AddRectFilled(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(color, interaction.Active ? 0.72f : 0.88f)),
            Scale(14f, scale));
        drawList.AddRectFilled(
            minimum + Scale(new Vector2(1f), scale),
            maximum - Scale(new Vector2(1f, 12f), scale),
            LosPalette.ToUInt(WithAlpha(LosPalette.PrimaryHover, interaction.Hovered ? 0.38f : 0.22f)),
            Scale(13f, scale));
        drawList.AddRect(
            minimum,
            maximum,
            LosPalette.ToUInt(WithAlpha(LosPalette.Cyan, interaction.Hovered ? 0.92f : 0.68f)),
            Scale(14f, scale),
            ImDrawFlags.None,
            interaction.Hovered ? Scale(1.6f, scale) : Scale(1.1f, scale));
    }

    private static void DrawCatEar(
        ImDrawListPtr drawList,
        Vector2 left,
        Vector2 tip,
        Vector2 right,
        uint fill,
        uint border,
        uint inner,
        float scale)
    {
        drawList.AddTriangleFilled(left, tip, right, fill);
        drawList.AddLine(left, tip, border, Scale(1f, scale));
        drawList.AddLine(tip, right, border, Scale(1f, scale));
        var innerLeft = Vector2.Lerp(left, tip, 0.38f);
        var innerRight = Vector2.Lerp(right, tip, 0.38f);
        var innerBottom = Vector2.Lerp(left, right, 0.5f);
        drawList.AddTriangleFilled(innerLeft, tip + Scale(new Vector2(0f, 2f), scale), innerRight, inner);
        drawList.AddLine(innerLeft, innerBottom, inner, Scale(1f, scale));
        drawList.AddLine(innerRight, innerBottom, inner, Scale(1f, scale));
    }

    private static void DrawTail(
        ImDrawListPtr drawList,
        Vector2 minimum,
        Vector2 maximum,
        Vector4 color,
        bool enabled,
        float scale)
    {
        var tailColor = LosPalette.ToUInt(WithAlpha(color, enabled ? 0.88f : 0.42f));
        var start = new Vector2(minimum.X + Scale(8f, scale), maximum.Y - Scale(5f, scale));
        var bendOne = new Vector2(minimum.X - Scale(1f, scale), maximum.Y + Scale(1f, scale));
        var bendTwo = new Vector2(minimum.X - Scale(2f, scale), maximum.Y + Scale(8f, scale));
        var tip = new Vector2(minimum.X + Scale(5f, scale), maximum.Y + Scale(11f, scale));
        var thickness = Scale(2.2f, scale);
        drawList.AddLine(start, bendOne, tailColor, thickness);
        drawList.AddLine(bendOne, bendTwo, tailColor, thickness);
        drawList.AddLine(bendTwo, tip, tailColor, thickness);
        drawList.AddCircleFilled(tip, Scale(1.8f, scale), tailColor, 12);
    }

    private static void DrawControlCatFace(
        ImDrawListPtr drawList,
        Vector2 center,
        Vector4 color,
        float scale)
    {
        var faceColor = LosPalette.ToUInt(WithAlpha(LosPalette.TextPrimary, 0.88f));
        var noseColor = LosPalette.ToUInt(new Vector4(1f, 0.68f, 0.86f, 0.94f));
        drawList.AddCircleFilled(
            center + Scale(new Vector2(-6f, -4f), scale),
            Scale(2.1f, scale),
            faceColor,
            12);
        drawList.AddCircleFilled(
            center + Scale(new Vector2(6f, -4f), scale),
            Scale(2.1f, scale),
            faceColor,
            12);
        var nose = center + Scale(new Vector2(0f, 2f), scale);
        var noseSize = Scale(2.8f, scale);
        drawList.AddQuadFilled(
            nose + new Vector2(0f, -noseSize),
            nose + new Vector2(noseSize, 0f),
            nose + new Vector2(0f, noseSize),
            nose + new Vector2(-noseSize, 0f),
            noseColor);

        var whiskerColor = LosPalette.ToUInt(WithAlpha(color, 0.72f));
        for (var index = -1; index <= 1; index++)
        {
            var offsetY = Scale(index * 4f, scale);
            drawList.AddLine(
                center + new Vector2(Scale(-6f, scale), offsetY + Scale(2f, scale)),
                center + new Vector2(Scale(-20f, scale), offsetY),
                whiskerColor,
                Scale(1f, scale));
            drawList.AddLine(
                center + new Vector2(Scale(6f, scale), offsetY + Scale(2f, scale)),
                center + new Vector2(Scale(20f, scale), offsetY),
                whiskerColor,
                Scale(1f, scale));
        }
    }

    private static void DrawBell(ImDrawListPtr drawList, Vector2 center, float scale)
    {
        var color = LosPalette.ToUInt(LosPalette.MoonGold);
        var outline = LosPalette.ToUInt(WithAlpha(LosPalette.TextPrimary, 0.72f));
        var radius = Scale(6f, scale);
        drawList.AddCircleFilled(center, radius, LosPalette.ToUInt(WithAlpha(LosPalette.MoonGold, 0.22f)), 18);
        drawList.AddQuadFilled(
            center + new Vector2(0f, -radius),
            center + new Vector2(radius, 0f),
            center + new Vector2(0f, radius),
            center + new Vector2(-radius, 0f),
            color);
        drawList.AddCircle(center, radius + Scale(2f, scale), outline, 20, Scale(1f, scale));
    }

    private static void DrawPaw(
        ImDrawListPtr drawList,
        Vector2 center,
        Vector4 color,
        float scale)
    {
        var fill = LosPalette.ToUInt(WithAlpha(color, 0.90f));
        drawList.AddCircleFilled(center + Scale(new Vector2(0f, 3f), scale), Scale(4.5f, scale), fill, 16);
        drawList.AddCircleFilled(center + Scale(new Vector2(-5f, -4f), scale), Scale(1.8f, scale), fill, 10);
        drawList.AddCircleFilled(center + Scale(new Vector2(-1.7f, -6f), scale), Scale(1.8f, scale), fill, 10);
        drawList.AddCircleFilled(center + Scale(new Vector2(1.7f, -6f), scale), Scale(1.8f, scale), fill, 10);
        drawList.AddCircleFilled(center + Scale(new Vector2(5f, -4f), scale), Scale(1.8f, scale), fill, 10);
    }

    private static void DrawCenteredText(
        ImDrawListPtr drawList,
        Vector2 minimum,
        Vector2 maximum,
        string text,
        Vector4 color,
        float scale)
    {
        var textSize = ImGui.CalcTextSize(text);
        var position = minimum + ((maximum - minimum - textSize) * 0.5f);
        drawList.AddText(
            position + Scale(new Vector2(1f), scale),
            LosPalette.ToUInt(new Vector4(0f, 0f, 0f, 0.66f)),
            text);
        drawList.AddText(position, LosPalette.ToUInt(color), text);
    }

    private static ButtonInteraction InteractAt(string id, Vector2 minimum, Vector2 size)
    {
        ImGui.SetCursorScreenPos(minimum);
        ImGui.InvisibleButton(id, size);
        return new ButtonInteraction(
            ImGui.IsItemHovered(),
            ImGui.IsItemActive(),
            ImGui.IsItemClicked(ImGuiMouseButton.Left),
            ImGui.IsItemClicked(ImGuiMouseButton.Right));
    }

    private void ApplyInitialPosition(
        bool control,
        BlackMageSettings settings,
        Vector2 size,
        ref bool pending)
    {
        if (!pending)
            return;

        pending = false;
        Vector2 position;
        if (settings.RememberWindow
            && TryReadSavedPosition(control, settings, out var savedPosition))
        {
            position = ClampToViewport(savedPosition, size);
        }
        else
        {
            position = DefaultPosition(control, size, scale: settings.UiScale);
        }

        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
    }

    private void ApplyInitialHotkeyPosition(
        BlackMageSettings settings,
        Vector2 size,
        float scale)
    {
        if (!_hotkeyPlacementPending)
            return;

        _hotkeyPlacementPending = false;
        Vector2 position;
        if (settings.RememberWindow
            && TryReadHotkeyPosition(settings, out var savedPosition))
        {
            position = ClampToViewport(savedPosition, size);
        }
        else
        {
            position = DefaultHotkeyPosition(settings, size, scale);
        }

        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
    }

    private static bool TryReadSavedPosition(
        bool control,
        BlackMageSettings settings,
        out Vector2 position)
    {
        position = control
            ? new Vector2(settings.QuickControlWindowX, settings.QuickControlWindowY)
            : new Vector2(settings.QuickQtWindowX, settings.QuickQtWindowY);
        return position.X != -1f
            && position.Y != -1f
            && float.IsFinite(position.X)
            && float.IsFinite(position.Y);
    }

    private static bool TryReadHotkeyPosition(
        BlackMageSettings settings,
        out Vector2 position)
    {
        position = new Vector2(
            settings.QuickHotkeyWindowX,
            settings.QuickHotkeyWindowY);
        return position.X != -1f
            && position.Y != -1f
            && float.IsFinite(position.X)
            && float.IsFinite(position.Y);
    }

    private static Vector2 DefaultPosition(bool control, Vector2 size, float scale)
    {
        var viewport = ImGui.GetMainViewport();
        var centerX = viewport.Pos.X + ((viewport.Size.X - size.X) * 0.5f);
        var controlSize = Scale(new Vector2(ControlWidth, ControlHeight), scale);
        var controlY = viewport.Pos.Y + MathF.Max(52f, viewport.Size.Y * 0.20f);
        return control
            ? new Vector2(centerX, controlY)
            : new Vector2(centerX, controlY + controlSize.Y + Scale(12f, scale));
    }

    private static Vector2 DefaultHotkeyPosition(
        BlackMageSettings settings,
        Vector2 size,
        float scale)
    {
        var qtScale = OverlayScale(settings.UiScale * settings.QtPanelScale);
        var visibleQtCount = BlackMageRotation.QtList.Keys.Count(
            key => !settings.HiddenQtKeys.Contains(key));
        if (visibleQtCount == 0)
        {
            var controlScale = LosMetrics.NormalizeScale(settings.UiScale);
            var controlSize = Scale(new Vector2(ControlWidth, ControlHeight), controlScale);
            var controlPosition = settings.RememberWindow
                && TryReadSavedPosition(control: true, settings, out var savedControlPosition)
                    ? ClampToViewport(savedControlPosition, controlSize)
                    : DefaultPosition(control: true, controlSize, controlScale);
            return ClampToViewport(
                new Vector2(
                    controlPosition.X,
                    controlPosition.Y + controlSize.Y + Scale(12f, controlScale)),
                size);
        }

        var qtRows = Math.Max(1, (int)Math.Ceiling(
            visibleQtCount / (double)Columns));
        var qtSize = Scale(
            new Vector2(
                (QtButtonWidth * Columns)
                    + (QtGapX * (Columns - 1))
                    + (QtPanelPadding * 2f),
                (QtCellHeight * qtRows)
                    + (QtPanelPadding * 2f)),
            qtScale);
        var qtPosition = settings.RememberWindow
            && TryReadSavedPosition(control: false, settings, out var savedQtPosition)
                ? ClampToViewport(savedQtPosition, qtSize)
                : DefaultPosition(control: false, qtSize, qtScale);
        var gap = Scale(12f, MathF.Max(scale, qtScale));
        var viewport = ImGui.GetMainViewport();
        var right = new Vector2(qtPosition.X + qtSize.X + gap, qtPosition.Y);
        if (right.X + size.X <= viewport.Pos.X + viewport.Size.X)
            return ClampToViewport(right, size);

        var left = new Vector2(qtPosition.X - size.X - gap, qtPosition.Y);
        if (left.X >= viewport.Pos.X)
            return ClampToViewport(left, size);

        return ClampToViewport(
            new Vector2(qtPosition.X, qtPosition.Y + qtSize.Y + gap),
            size);
    }

    private static Vector2 ClampToViewport(Vector2 position, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        var maximum = viewport.Pos + Vector2.Max(Vector2.Zero, viewport.Size - size);
        return Vector2.Clamp(position, viewport.Pos, maximum);
    }

    private void CapturePosition(bool control, BlackMageSettings settings, Vector2 position)
    {
        if (!settings.RememberWindow
            || !float.IsFinite(position.X)
            || !float.IsFinite(position.Y))
        {
            return;
        }

        ref var lastPosition = ref (control
            ? ref _lastControlPosition
            : ref _lastQtPosition);
        if (Vector2.DistanceSquared(lastPosition, position) < 0.25f)
            return;

        lastPosition = position;
        var persisted = position;
        _store.Update(value =>
        {
            if (control)
            {
                value.QuickControlWindowX = persisted.X;
                value.QuickControlWindowY = persisted.Y;
            }
            else
            {
                value.QuickQtWindowX = persisted.X;
                value.QuickQtWindowY = persisted.Y;
            }
        });
    }

    private void CaptureHotkeyPosition(BlackMageSettings settings, Vector2 position)
    {
        if (!settings.RememberWindow
            || !float.IsFinite(position.X)
            || !float.IsFinite(position.Y)
            || Vector2.DistanceSquared(_lastHotkeyPosition, position) < 0.25f)
        {
            return;
        }

        _lastHotkeyPosition = position;
        var persisted = position;
        _store.Update(value =>
        {
            value.QuickHotkeyWindowX = persisted.X;
            value.QuickHotkeyWindowY = persisted.Y;
        });
    }

    private static void HandleWindowDrag()
    {
        if (!ImGui.IsWindowHovered()
            || ImGui.IsAnyItemHovered()
            || !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            return;
        }

        ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta, ImGuiCond.Always);
    }

    private void HideNativeWindows()
    {
        try
        {
            foreach (var key in _qtKeys)
                PromeSettings.Instance.HiddenQts.Add(key);
            Plugin.Instance.CloseQtWindow();
        }
        catch (Exception exception)
        {
            LogHostWarning(exception, "隐藏 PR 本体快捷窗口失败");
        }
    }

    private void RestoreNativeWindows()
    {
        try
        {
            foreach (var key in _qtKeys)
                PromeSettings.Instance.HiddenQts.Remove(key);
            Plugin.Instance.OpenQtWindow();
        }
        catch (Exception exception)
        {
            LogHostWarning(exception, "恢复 PR 本体快捷窗口失败");
        }
    }

    private void LogHostWarning(Exception exception, string message)
    {
        var now = DateTime.UtcNow;
        if (now < _nextHostWarningUtc)
            return;

        _nextHostWarningUtc = now.AddSeconds(5);
        Svc.Log.Warning(exception, $"[Los] {message}。");
    }

    private static void PushOverlayWindowStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
    }

    private static void PopOverlayWindowStyle()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
    }

    private static float OverlayScale(float scale)
        => float.IsFinite(scale) ? Math.Clamp(scale, 0.55f, 2f) : 1f;

    private static Vector4 WithAlpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, Math.Clamp(alpha, 0f, 1f));

    private static float Scale(float value, float scale)
        => LosMetrics.Scale(value, scale);

    private static Vector2 Scale(Vector2 value, float scale)
        => LosMetrics.Scale(value, scale);

    private readonly record struct ButtonInteraction(
        bool Hovered,
        bool Active,
        bool LeftClicked,
        bool RightClicked);
}
