using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

internal static class BlmStylePanel
{
    public static void Draw(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion,
        Action resetInterface)
    {
        DrawThemeSelector(store, scale, reduceMotion);
        DrawAdaptivePair(
            "style_middle",
            () => DrawAppearance(store, scale),
            () => DrawOverlayScale(store, scale),
            scale);
        DrawAdaptivePair(
            "style_bottom",
            () => DrawMotion(store, scale, reduceMotion),
            () => DrawWindowSettings(store, scale, reduceMotion, resetInterface),
            scale);
    }

    private static void DrawThemeSelector(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        LosCard.Draw(
            "style_theme_card",
            () =>
            {
                var current = store.Settings.UiThemeStyle;
                DrawThemeButton(
                    "theme_amethyst",
                    "紫晶黑猫",
                    "靛蓝 / 紫晶 / 薰衣草；使用本地猫使魔图片。",
                    BlmUiThemeStyle.AmethystCat,
                    current,
                    store,
                    scale,
                    reduceMotion);
                DrawThemeButton(
                    "theme_moonlit",
                    "月影黑猫",
                    "深墨蓝 / 月金 / 星界蓝",
                    BlmUiThemeStyle.MoonlitCat,
                    current,
                    store,
                    scale,
                    reduceMotion);
                DrawThemeButton(
                    "theme_astral",
                    "星界使魔",
                    "夜海蓝 / 奥术紫 / 冷光",
                    BlmUiThemeStyle.AstralFamiliar,
                    current,
                    store,
                    scale,
                    reduceMotion);
                DrawThemeButton(
                    "theme_ember",
                    "余烬魔猫",
                    "暗绯红 / 炉火橙 / 黄铜",
                    BlmUiThemeStyle.EmberFamiliar,
                    current,
                    store,
                    scale,
                    reduceMotion);
            },
            "主题变体",
            "选择一套顺眼的控制台配色",
            height: 240f,
            scale: scale);
    }

    private static void DrawThemeButton(
        string id,
        string label,
        string tooltip,
        BlmUiThemeStyle value,
        BlmUiThemeStyle current,
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        if (LosComponents.SegmentButton(
                id,
                label,
                current == value,
                new Vector2(-1f, 32f * scale),
                scale,
                reduceMotion,
                tooltip))
        {
            store.Update(settings => settings.UiThemeStyle = value);
        }
    }

    private static void DrawAppearance(
        BlackMageSettingsStore store,
        float scale)
    {
        LosCard.Draw(
            "style_appearance_card",
            () =>
            {
                var settings = store.Settings;
                var uiScale = settings.UiScale;
                if (LosComponents.SliderFloat(
                        "整体界面缩放##style_scale",
                        ref uiScale,
                        LosMetrics.MinScale,
                        LosMetrics.MaxScale,
                        "%.2f x",
                        "同时调整页头、卡片、文字间距和控件尺寸。",
                        scale: scale))
                {
                    store.Update(value => value.UiScale = uiScale);
                }

                var opacity = settings.WindowOpacity;
                if (LosComponents.SliderFloat(
                        "背景透明度##style_opacity",
                        ref opacity,
                        0.70f,
                        1.00f,
                        "%.2f",
                        "只影响控制台背景，不影响文字亮度。",
                        scale: scale))
                {
                    store.Update(value => value.WindowOpacity = opacity);
                }

            },
            "界面比例",
            "调整控制台文字和控件大小",
            height: 190f,
            scale: scale);
    }

    private static void DrawMotion(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        LosCard.Draw(
            "style_motion_card",
            () => BlmPanelPrimitives.DrawToggleRow(
                "style_reduce_motion",
                "减少动效",
                "关闭状态脉冲和页签滑动过渡。",
                store.Settings.ReduceMotion,
                value => store.Update(item => item.ReduceMotion = value),
                scale,
                reduceMotion),
            "动效偏好",
            "减少视觉干扰",
            height: 125f,
            scale: scale);
    }

    private static void DrawOverlayScale(
        BlackMageSettingsStore store,
        float scale)
    {
        LosCard.Draw(
            "style_overlay_scale_card",
            () =>
            {
                var qtScale = store.Settings.QtPanelScale;
                if (LosComponents.SliderFloat(
                        "QT 面板大小##style_qt_scale",
                        ref qtScale,
                        0.70f,
                        1.50f,
                        "%.2f x",
                        "单独调整 QT 浮窗，不改变控制台文字。",
                        scale: scale))
                {
                    store.Update(settings => settings.QtPanelScale = qtScale);
                }

                var hotkeyScale = store.Settings.HotkeyPanelScale;
                if (LosComponents.SliderFloat(
                        "Hotkey 面板大小##style_hotkey_scale",
                        ref hotkeyScale,
                        0.70f,
                        1.50f,
                        "%.2f x",
                        "单独调整技能图标浮窗。",
                        scale: scale))
                {
                    store.Update(settings => settings.HotkeyPanelScale = hotkeyScale);
                }
            },
            "浮窗大小",
            "QT 与 Hotkey 可分别调整",
            height: 190f,
            scale: scale);
    }

    private static void DrawWindowSettings(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion,
        Action resetInterface)
    {
        LosCard.Draw(
            "style_window_card",
            () =>
            {
                var settings = store.Settings;
                BlmPanelPrimitives.DrawToggleRow(
                    "style_remember_window",
                    "记住窗口",
                    "保存控制台位置与尺寸。",
                    settings.RememberWindow,
                    value => store.Update(item => item.RememberWindow = value),
                    scale,
                    reduceMotion);
                BlmPanelPrimitives.DrawDivider(scale);
                if (LosComponents.SecondaryButton(
                        "style_reset_window",
                        "恢复默认窗口与风格",
                        size: new Vector2(-1f, 34f * scale),
                        scale: scale,
                        tooltip: "恢复紫晶黑猫主题、100% 缩放与默认窗口位置。"))
                {
                    resetInterface();
                }
            },
            "窗口记忆",
            "默认尺寸 980 × 700；最小 660 × 500",
            height: 150f,
            scale: scale);
    }

    private static void DrawAdaptivePair(
        string id,
        Action left,
        Action right,
        float scale)
    {
        if (ImGui.GetContentRegionAvail().X < LosMetrics.Scale(680f, scale))
        {
            left();
            right();
            return;
        }

        if (!ImGui.BeginTable(
                $"##{id}",
                2,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        try
        {
            ImGui.TableNextColumn();
            left();
            ImGui.TableNextColumn();
            right();
        }
        finally
        {
            ImGui.EndTable();
        }
    }
}
