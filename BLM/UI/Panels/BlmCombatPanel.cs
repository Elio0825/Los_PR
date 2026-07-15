using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.Openers;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

internal static class BlmCombatPanel
{
    private static readonly (BlmOpenerSelection Value, string Label, string Detail)[] OpenerOptions =
    [
        (BlmOpenerSelection.None, "不启用起手", "进战后直接交给普通 Resolver"),
        (BlmOpenerSelection.Level70, BlmLevel70Opener.Name, "70–79级高难 4+7"),
        (BlmOpenerSelection.Level80, BlmLevel80Opener.Name, "80–89级高难 4+7"),
        (BlmOpenerSelection.Level90, BlmLevel90Opener.Name, "90–99级高难 4+6"),
        (BlmOpenerSelection.Standard57, BlmLevel100Opener.Name, "支持高难倒计时与日常进战"),
        (BlmOpenerSelection.Flare, BlmLevel100FlareOpener.Name, "高难核爆与双耀星方案"),
    ];

    public static void Draw(BlackMageSettingsStore store, float scale, bool reduceMotion)
    {
        DrawAdaptivePair(
            "battle_core",
            () => DrawOpenerSettings(store, scale, reduceMotion),
            () => DrawBattleSettings(store, scale, reduceMotion),
            scale);
        DrawDangerLoop(store, scale);
    }

    private static void DrawOpenerSettings(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        LosCard.Draw(
            "battle_opener_card",
            () =>
            {
                var settings = store.Settings;
                ImGui.TextColored(LosPalette.TextSecondary, "起手方案");
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.BeginCombo("##battle_opener_combo", OpenerLabel(settings.OpenerSelection)))
                {
                    foreach (var option in OpenerOptions)
                    {
                        var selected = option.Value == settings.OpenerSelection;
                        if (ImGui.Selectable($"{option.Label}##{option.Value}", selected))
                        {
                            store.Update(value => value.OpenerSelection = option.Value);
                        }

                        LosComponents.TooltipIfHovered(option.Detail, scale);
                        if (selected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(10f, scale)));
                DrawOpenerToggleGrid(
                    store,
                    settings,
                    scale,
                    reduceMotion);
                ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(10f, scale)));
                DrawInsetNote(
                    "当前书签摘要",
                    OpenerDetail(settings.OpenerSelection),
                    scale);
            },
            "起手书签",
            "全部方案与策略直接可见",
            height: 320f,
            scale: scale);
    }

    private static void DrawOpenerToggleGrid(
        BlackMageSettingsStore store,
        BlackMageSettings settings,
        float scale,
        bool reduceMotion)
    {
        if (ImGui.GetContentRegionAvail().X < LosMetrics.Scale(450f, scale))
        {
            var potionEnabled = settings.OpenerPotionEnabled;
            if (DrawInsetToggle(
                    "opener_potion",
                    "起手爆发药",
                    potionEnabled ? "高难起手将使用背包内最优药水" : "高难起手不会使用爆发药",
                    ref potionEnabled,
                    scale,
                    reduceMotion))
            {
                store.Update(value => value.OpenerPotionEnabled = potionEnabled);
            }

            ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(8f, scale)));
            var noTriplecast = settings.OpenerNoTriplecast;
            if (DrawInsetToggle(
                    "opener_no_triple",
                    "起手不三连",
                    "标准 5+7 仍保留星灵",
                    ref noTriplecast,
                    scale,
                    reduceMotion))
            {
                store.Update(value => value.OpenerNoTriplecast = noTriplecast);
            }

            return;
        }

        var gap = LosMetrics.Scale(10f, scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(gap * 0.5f, 0f));
        try
        {
            if (!ImGui.BeginTable(
                    "##opener_toggle_grid",
                    2,
                    ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                return;
            }

            try
            {
                ImGui.TableNextColumn();
                var potionEnabled = settings.OpenerPotionEnabled;
                if (DrawInsetToggle(
                        "opener_potion",
                        "起手爆发药",
                        potionEnabled ? "高难起手将使用背包内最优药水" : "高难起手不会使用爆发药",
                        ref potionEnabled,
                        scale,
                        reduceMotion))
                {
                    store.Update(value => value.OpenerPotionEnabled = potionEnabled);
                }

                ImGui.TableNextColumn();
                var noTriplecast = settings.OpenerNoTriplecast;
                if (DrawInsetToggle(
                        "opener_no_triple",
                        "起手不三连",
                        "标准 5+7 仍保留星灵",
                        ref noTriplecast,
                        scale,
                        reduceMotion))
                {
                    store.Update(value => value.OpenerNoTriplecast = noTriplecast);
                }
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

    private static void DrawBattleSettings(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        LosCard.Draw(
            "battle_rotation_card",
            () =>
            {
                var compressed = store.Settings.CompressFireParadoxEnabled;
                if (DrawInsetToggle(
                    "battle_compressed_fire_paradox",
                    "压缩火悖论",
                    compressed ? "当前开启" : "当前关闭",
                    ref compressed,
                    scale,
                    reduceMotion))
                {
                    store.Update(settings => settings.CompressFireParadoxEnabled = compressed);
                }

                ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(8f, scale)));
                DrawDotThreshold(store, scale);
                DrawReservedDurationSliders(store, scale);
            },
            "循环咒式",
            "火悖论、DOT 与移动施法设置",
            height: 360f,
            scale: scale);
    }

    private static void DrawDangerLoop(BlackMageSettingsStore store, float scale)
    {
        LosCard.Draw(
            "battle_danger_card",
            () =>
            {
                var settings = store.Settings;
                var accepted = settings.DangerRiskAnimationLock
                    && settings.DangerRiskDrModule
                    && settings.DangerRiskEnforcement;
                if (!accepted && settings.DangerLoopEnabled)
                {
                    store.Update(item => item.DangerLoopEnabled = false);
                }

                DrawDangerBody(store, settings, accepted, scale);
            },
            "禁忌页",
            "危险循环与风险确认",
            height: 342f,
            scale: scale,
            border: true);
    }

    private static void DrawDangerBody(
        BlackMageSettingsStore store,
        BlackMageSettings settings,
        bool accepted,
        float scale)
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (available < LosMetrics.Scale(700f, scale))
        {
            DrawDangerMaster(store, settings, accepted, scale);
            ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(10f, scale)));
            DrawRiskList(store, settings, scale);
            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.CellPadding,
            new Vector2(LosMetrics.Scale(8f, scale), 0f));
        try
        {
            if (!ImGui.BeginTable(
                    "##danger_layout",
                    2,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            {
                return;
            }

            try
            {
                ImGui.TableSetupColumn(
                    "master",
                    ImGuiTableColumnFlags.WidthFixed,
                    LosMetrics.Scale(286f, scale));
                ImGui.TableSetupColumn("risks", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawDangerMaster(store, settings, accepted, scale);
                ImGui.TableNextColumn();
                DrawRiskList(store, settings, scale);
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

    private static void DrawDangerMaster(
        BlackMageSettingsStore store,
        BlackMageSettings settings,
        bool accepted,
        float scale)
    {
        var position = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(226f, scale);
        var padding = LosMetrics.Scale(18f, scale);
        var rounding = LosMetrics.Scale(16f, scale);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            position,
            position + new Vector2(width, height),
            LosPalette.ToUInt(LosPalette.Lerp(LosPalette.Card, LosPalette.Danger, 0.18f)),
            rounding);
        drawList.AddRect(
            position,
            position + new Vector2(width, height),
            LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Danger, 0.42f)),
            rounding,
            ImDrawFlags.None,
            1f);
        drawList.AddText(
            position + new Vector2(padding, padding),
            LosPalette.ToUInt(LosPalette.TextPrimary),
            "开挂循环");
        drawList.AddText(
            position + new Vector2(padding, padding + LosMetrics.Scale(28f, scale)),
            LosPalette.ToUInt(LosPalette.Danger),
            "实验功能");
        drawList.AddText(
            position + new Vector2(padding, padding + LosMetrics.Scale(62f, scale)),
            LosPalette.ToUInt(LosPalette.TextMuted),
            accepted ? "风险确认完成，可解锁主开关。" : "勾满右侧风险项后解锁主开关。");

        var controlY = position.Y + height - LosMetrics.Scale(50f, scale);
        drawList.AddLine(
            new Vector2(position.X + padding, controlY - LosMetrics.Scale(14f, scale)),
            new Vector2(position.X + width - padding, controlY - LosMetrics.Scale(14f, scale)),
            LosPalette.ToUInt(LosPalette.Separator),
            1f);
        drawList.AddText(
            new Vector2(position.X + padding, controlY + LosMetrics.Scale(3f, scale)),
            LosPalette.ToUInt(accepted ? LosPalette.TextPrimary : LosPalette.TextDisabled),
            "主开关");

        ImGui.SetCursorScreenPos(new Vector2(
            position.X + width - padding - LosMetrics.Scale(LosMetrics.ToggleWidth, scale),
            controlY));
        ImGui.BeginDisabled(!accepted);
        try
        {
            var enabled = accepted && settings.DangerLoopEnabled;
            if (LosComponents.Toggle(
                    "danger_master",
                    ref enabled,
                    scale,
                    settings.ReduceMotion,
                    accepted ? "危险循环暂未开放。" : "完成三项确认后解锁。"))
            {
                store.Update(item => item.DangerLoopEnabled = accepted && enabled);
            }
        }
        finally
        {
            ImGui.EndDisabled();
        }

        ImGui.SetCursorScreenPos(position + new Vector2(0f, height));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    private static void DrawRiskList(
        BlackMageSettingsStore store,
        BlackMageSettings settings,
        float scale)
    {
        DrawRiskCheckbox(
            "danger_animation_lock",
            "请确保 FuckAnimationLock 已启用并选择三插",
            settings.DangerRiskAnimationLock,
            value => store.Update(item => item.DangerRiskAnimationLock = value),
            scale);
        DrawRiskCheckbox(
            "danger_dr_module",
            "请确保 DR“减少能力技动画锁”模块已启用",
            settings.DangerRiskDrModule,
            value => store.Update(item => item.DangerRiskDrModule = value),
            scale);
        DrawRiskCheckbox(
            "danger_enforcement",
            "接受循环触电及被小警察出警的风险",
            settings.DangerRiskEnforcement,
            value => store.Update(item => item.DangerRiskEnforcement = value),
            scale);

        var acceptedCount = (settings.DangerRiskAnimationLock ? 1 : 0)
            + (settings.DangerRiskDrModule ? 1 : 0)
            + (settings.DangerRiskEnforcement ? 1 : 0);
        var pillWidth = LosMetrics.Scale(150f, scale);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, ImGui.GetContentRegionAvail().X - pillWidth));
        LosComponents.StatusPill(
            $"{acceptedCount} / 3 已确认",
            acceptedCount == 3 ? LosStatusTone.Success : LosStatusTone.Danger,
            scale,
            pillWidth);
    }

    private static void DrawRiskCheckbox(
        string id,
        string text,
        bool value,
        Action<bool> onChanged,
        float scale)
    {
        var position = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(54f, scale);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(
            position,
            position + new Vector2(width, height),
            LosPalette.ToUInt(LosPalette.Input),
            LosMetrics.Scale(12f, scale));
        drawList.AddRect(
            position,
            position + new Vector2(width, height),
            LosPalette.ToUInt(LosPalette.Border),
            LosMetrics.Scale(12f, scale),
            ImDrawFlags.None,
            1f);

        ImGui.SetCursorScreenPos(position + new Vector2(
            LosMetrics.Scale(16f, scale),
            (height - ImGui.GetFrameHeight()) * 0.5f));
        ImGui.PushID(id);
        try
        {
            var mutable = value;
            if (ImGui.Checkbox(text, ref mutable))
            {
                onChanged(mutable);
            }
        }
        finally
        {
            ImGui.PopID();
        }

        ImGui.SetCursorScreenPos(position + new Vector2(0f, height + LosMetrics.Scale(8f, scale)));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    private static void DrawDotThreshold(BlackMageSettingsStore store, float scale)
    {
        var threshold = store.Settings.DotHpThresholdPercent;
        if (DrawInsetSliderInt(
                "battle_dot_hp_threshold",
                "不上 DOT 阈值",
                "目标血量低于阈值时不续 DOT",
                ref threshold,
                0,
                100,
                "%d%%",
                scale))
        {
            store.Update(settings => settings.DotHpThresholdPercent = threshold);
        }
    }

    private static void DrawReservedDurationSliders(
        BlackMageSettingsStore store,
        float scale)
    {
        var moveSeconds = store.Settings.MoveTriplecastSeconds;
        if (DrawInsetSliderFloat(
                "battle_move_triplecast_seconds",
                "三连走位秒数",
                "连续走位达到阈值时使用",
                ref moveSeconds,
                0f,
                10f,
                "%.1f 秒",
                scale))
        {
            store.Update(settings => settings.MoveTriplecastSeconds = moveSeconds);
        }

        var stationarySeconds = store.Settings.StationaryLeyLinesSeconds;
        if (DrawInsetSliderFloat(
                "battle_stationary_ley_lines_seconds",
                "原地黑魔纹秒数",
                "可原地输出达到阈值时使用",
                ref stationarySeconds,
                0f,
                30f,
                "%.1f 秒",
                scale))
        {
            store.Update(settings => settings.StationaryLeyLinesSeconds = stationarySeconds);
        }

    }

    private static bool DrawInsetToggle(
        string id,
        string title,
        string description,
        ref bool value,
        float scale,
        bool reduceMotion)
    {
        var position = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(66f, scale);
        var padding = LosMetrics.Scale(16f, scale);
        DrawInsetBackground(position, new Vector2(width, height), scale);

        var drawList = ImGui.GetWindowDrawList();
        var textWidth = MathF.Max(
            20f,
            width - (padding * 3f) - LosMetrics.Scale(LosMetrics.ToggleWidth, scale));
        drawList.AddText(
            position + new Vector2(padding, LosMetrics.Scale(10f, scale)),
            LosPalette.ToUInt(LosPalette.TextPrimary),
            LosComponents.FitText(title, textWidth));
        drawList.AddText(
            position + new Vector2(padding, LosMetrics.Scale(36f, scale)),
            LosPalette.ToUInt(LosPalette.TextMuted),
            LosComponents.FitText(description, textWidth));

        ImGui.SetCursorScreenPos(new Vector2(
            position.X + width - padding - LosMetrics.Scale(LosMetrics.ToggleWidth, scale),
            position.Y + ((height - LosMetrics.Scale(LosMetrics.ToggleHeight, scale)) * 0.5f)));
        var changed = LosComponents.Toggle(id, ref value, scale, reduceMotion, description);
        ImGui.SetCursorScreenPos(position + new Vector2(0f, height));
        ImGui.Dummy(new Vector2(width, 0f));
        return changed;
    }

    private static void DrawInsetNote(string title, string detail, float scale)
    {
        var position = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(64f, scale);
        var padding = LosMetrics.Scale(16f, scale);
        DrawInsetBackground(position, new Vector2(width, height), scale, elevated: true);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(
            position + new Vector2(padding, LosMetrics.Scale(10f, scale)),
            LosPalette.ToUInt(LosPalette.Cyan),
            title);
        drawList.AddText(
            position + new Vector2(padding, LosMetrics.Scale(36f, scale)),
            LosPalette.ToUInt(LosPalette.TextSecondary),
            LosComponents.FitText(detail, width - (padding * 2f)));
        ImGui.SetCursorScreenPos(position + new Vector2(0f, height));
        ImGui.Dummy(new Vector2(width, 0f));
    }

    private static bool DrawInsetSliderInt(
        string id,
        string title,
        string description,
        ref int value,
        int minimum,
        int maximum,
        string format,
        float scale)
    {
        var position = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(66f, scale);
        var padding = LosMetrics.Scale(16f, scale);
        DrawInsetBackground(position, new Vector2(width, height), scale);
        DrawInsetSliderLabels(position, width, padding, title, description, scale);

        var controlWidth = Math.Clamp(
            width * 0.44f,
            LosMetrics.Scale(150f, scale),
            LosMetrics.Scale(270f, scale));
        ImGui.SetCursorScreenPos(new Vector2(
            position.X + width - padding - controlWidth,
            position.Y + ((height - ImGui.GetFrameHeight()) * 0.5f)));
        ImGui.SetNextItemWidth(controlWidth);
        LosComponents.PushCompactSliderStyle(scale);
        bool changed;
        try
        {
            changed = ImGui.SliderInt($"##{id}", ref value, minimum, maximum, format);
        }
        finally
        {
            ImGui.PopStyleVar(4);
        }

        ImGui.SetCursorScreenPos(position + new Vector2(0f, height + LosMetrics.Scale(8f, scale)));
        ImGui.Dummy(new Vector2(width, 0f));
        return changed;
    }

    private static bool DrawInsetSliderFloat(
        string id,
        string title,
        string description,
        ref float value,
        float minimum,
        float maximum,
        string format,
        float scale)
    {
        var position = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var height = LosMetrics.Scale(66f, scale);
        var padding = LosMetrics.Scale(16f, scale);
        DrawInsetBackground(position, new Vector2(width, height), scale);
        DrawInsetSliderLabels(position, width, padding, title, description, scale);

        var controlWidth = Math.Clamp(
            width * 0.44f,
            LosMetrics.Scale(150f, scale),
            LosMetrics.Scale(270f, scale));
        ImGui.SetCursorScreenPos(new Vector2(
            position.X + width - padding - controlWidth,
            position.Y + ((height - ImGui.GetFrameHeight()) * 0.5f)));
        ImGui.SetNextItemWidth(controlWidth);
        LosComponents.PushCompactSliderStyle(scale);
        bool changed;
        try
        {
            changed = ImGui.SliderFloat($"##{id}", ref value, minimum, maximum, format);
        }
        finally
        {
            ImGui.PopStyleVar(4);
        }

        ImGui.SetCursorScreenPos(position + new Vector2(0f, height + LosMetrics.Scale(8f, scale)));
        ImGui.Dummy(new Vector2(width, 0f));
        return changed;
    }

    private static void DrawInsetSliderLabels(
        Vector2 position,
        float width,
        float padding,
        string title,
        string description,
        float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var textWidth = MathF.Max(LosMetrics.Scale(120f, scale), width * 0.45f - padding);
        drawList.AddText(
            position + new Vector2(padding, LosMetrics.Scale(10f, scale)),
            LosPalette.ToUInt(LosPalette.TextPrimary),
            LosComponents.FitText(title, textWidth));
        drawList.AddText(
            position + new Vector2(padding, LosMetrics.Scale(36f, scale)),
            LosPalette.ToUInt(LosPalette.TextMuted),
            LosComponents.FitText(description, textWidth));
    }

    private static void DrawInsetBackground(
        Vector2 position,
        Vector2 size,
        float scale,
        bool elevated = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = LosMetrics.Scale(12f, scale);
        drawList.AddRectFilled(
            position,
            position + size,
            LosPalette.ToUInt(elevated
                ? LosPalette.Lerp(LosPalette.Card, LosPalette.Primary, 0.12f)
                : LosPalette.Input),
            rounding);
        drawList.AddRect(
            position,
            position + size,
            LosPalette.ToUInt(LosPalette.Border),
            rounding,
            ImDrawFlags.None,
            1f);
    }

    private static void DrawAdaptivePair(
        string id,
        Action left,
        Action right,
        float scale)
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (available < LosMetrics.Scale(760f, scale))
        {
            left();
            right();
            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.CellPadding,
            new Vector2(LosMetrics.Scale(8f, scale), 0f));
        try
        {
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
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    private static string OpenerLabel(BlmOpenerSelection selection)
    {
        foreach (var option in OpenerOptions)
        {
            if (option.Value == selection)
                return option.Label;
        }

        return "不启用起手";
    }

    private static string OpenerDetail(BlmOpenerSelection selection)
    {
        foreach (var option in OpenerOptions)
        {
            if (option.Value == selection)
                return option.Detail;
        }

        return "进战后直接交给普通 Resolver";
    }
}
