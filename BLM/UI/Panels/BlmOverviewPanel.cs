using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

internal static class BlmOverviewPanel
{
    private static string? _modeFeedback;
    private static DateTime _modeFeedbackUntilUtc;

    public static void Draw(
        BlackMageSettingsStore store,
        BlmUiSnapshot snapshot,
        float scale,
        bool reduceMotion)
    {
        DrawModeSelection(store, scale, reduceMotion);
        DrawOverviewGrid(
            () => DrawResources(snapshot, scale),
            () => DrawTiming(snapshot, scale),
            () => DrawTarget(snapshot, scale),
            () => DrawEngineStatus(snapshot, scale),
            scale);
    }

    private static void DrawModeSelection(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        var stackButtons = ImGui.GetContentRegionAvail().X < 540f * scale;
        LosCard.Draw(
            "overview_mode_selection",
            () =>
            {
                var mode = store.Settings.CombatMode;
                var gap = 8f * scale;
                var available = ImGui.GetContentRegionAvail().X;
                var stack = stackButtons;
                var buttonWidth = stack
                    ? available
                    : Math.Max(120f * scale, (available - gap * 2f) / 3f);
                var size = new Vector2(buttonWidth, 34f * scale);

                if (LosComponents.SegmentButton(
                        "mode_daily", "日常预设", mode == BlmConsoleMode.Daily,
                        size, scale, reduceMotion, "适用于日随与随机任务；应用日常 QT 默认组合。"))
                {
                    ApplyModePreset(store, BlmConsoleMode.Daily, "已切换日常预设");
                }

                if (!stack)
                    ImGui.SameLine(0f, gap);
                if (LosComponents.SegmentButton(
                        "mode_high_end", "高难预设", mode == BlmConsoleMode.HighEnd,
                        size, scale, reduceMotion, "适用于高难副本；应用高难 QT 默认组合。"))
                {
                    ApplyModePreset(store, BlmConsoleMode.HighEnd, "已切换高难预设");
                }

                if (!stack)
                    ImGui.SameLine(0f, gap);
                if (LosComponents.SecondaryButton(
                        "mode_restore",
                        "恢复默认设置",
                        size: size,
                        scale: scale,
                        tooltip: "恢复当前模式的 QT 默认值。"))
                {
                    ApplyModePreset(store, mode, "已恢复当前模式默认");
                }

                if (_modeFeedback is not null && DateTime.UtcNow <= _modeFeedbackUntilUtc)
                {
                    ImGui.Dummy(new Vector2(0f, 6f * scale));
                    LosComponents.StatusPill(_modeFeedback, LosStatusTone.Success, scale);
                }
            },
            "模式书签",
            "切换预设会立即更新 QT；起手方案在“战斗”页单独选择",
            height: stackButtons ? 235f : 132f,
            scale: scale);
    }

    private static void DrawResources(BlmUiSnapshot snapshot, float scale)
    {
        LosCard.Draw(
            "overview_resources_card",
            () =>
            {
                var phaseTone = snapshot.InAstralFire
                    ? LosStatusTone.Danger
                    : snapshot.InUmbralIce
                        ? LosStatusTone.Info
                        : LosStatusTone.Neutral;
                LosComponents.StatusPill(snapshot.PhaseLabel, phaseTone, scale, 76f * scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.HasParadox ? "悖论就绪" : "无悖论",
                    snapshot.HasParadox ? LosStatusTone.Success : LosStatusTone.Neutral,
                    scale);

                ImGui.Dummy(new Vector2(0f, 10f * scale));
                LosComponents.ProgressBar(
                    "overview_mp",
                    snapshot.MpFraction,
                    $"MP {snapshot.Mp:N0} / {snapshot.MaxMp:N0}",
                    LosStatusTone.Info,
                    scale);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                LosComponents.KeyValueRow("冰晶", $"{snapshot.UmbralHearts} / 3", scale);
                LosComponents.KeyValueRow(
                    "通晓",
                    snapshot.MaximumPolyglot > 0
                        ? $"{snapshot.PolyglotStacks} / {snapshot.MaximumPolyglot}"
                        : "未解锁",
                    scale,
                    snapshot.PolyglotStacks >= snapshot.MaximumPolyglot && snapshot.MaximumPolyglot > 0
                        ? LosPalette.Warning
                        : null);
                LosComponents.KeyValueRow("通晓计时", $"{snapshot.PolyglotTimerSeconds:F1} 秒", scale);
                LosComponents.KeyValueRow("星灵魂", $"{snapshot.AstralSoulStacks} / 6", scale);
            },
            "元素资源",
            snapshot.IsAvailable ? $"Lv.{snapshot.Level}" : snapshot.AvailabilityText,
            height: 280f,
            scale: scale);
    }

    private static void DrawTiming(BlmUiSnapshot snapshot, float scale)
    {
        LosCard.Draw(
            "overview_timing_card",
            () =>
            {
                LosComponents.ProgressBar(
                    "overview_gcd",
                    snapshot.GcdFraction,
                    $"GCD {snapshot.GcdRemainSeconds:F2} 秒",
                    LosStatusTone.Info,
                    scale);
                ImGui.Dummy(new Vector2(0f, 10f * scale));

                if (snapshot.IsCasting)
                {
                    LosComponents.ProgressBar(
                        "overview_cast",
                        snapshot.CastFraction,
                        $"读条 {snapshot.CastRemainSeconds:F2} 秒",
                        LosStatusTone.Warning,
                        scale);
                }
                else
                {
                    LosComponents.ProgressBar(
                        "overview_cast_idle",
                        0f,
                        "未在读条",
                        LosStatusTone.Neutral,
                        scale);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                LosComponents.KeyValueRow("GCD 总时长", $"{snapshot.GcdTotalSeconds:F2} 秒", scale);
                LosComponents.KeyValueRow("动画锁", $"{snapshot.AnimationLockSeconds:F3} 秒", scale,
                    snapshot.AnimationLockSeconds > 0f ? LosPalette.Warning : LosPalette.TextSecondary);
                LosComponents.KeyValueRow("角色状态", snapshot.IsAlive ? "可行动" : "无法行动", scale,
                    snapshot.IsAlive ? LosPalette.Success : LosPalette.Danger);
            },
            "调度时序",
            "只读采样，不干预技能队列",
            height: 280f,
            scale: scale);
    }

    private static void DrawTarget(BlmUiSnapshot snapshot, float scale)
    {
        LosCard.Draw(
            "overview_target_card",
            () =>
            {
                LosComponents.StatusPill(
                    snapshot.CanAttackTarget ? "可攻击" : snapshot.HasTarget ? "不可攻击" : "无目标",
                    snapshot.CanAttackTarget ? LosStatusTone.Success : LosStatusTone.Neutral,
                    scale);
                ImGui.Dummy(new Vector2(0f, 10f * scale));
                LosComponents.KeyValueRow("目标", snapshot.TargetName, scale);
                LosComponents.KeyValueRow(
                    "距离",
                    snapshot.HasTarget ? $"{snapshot.TargetDistance:F1} yalms" : "--",
                    scale);
                LosComponents.KeyValueRow(
                    "生命值",
                    snapshot.HasTarget && snapshot.TargetMaxHp > 0
                        ? $"{snapshot.TargetHp:N0} / {snapshot.TargetMaxHp:N0}"
                        : "--",
                    scale);
                LosComponents.KeyValueRow("副本人数", snapshot.DutySizeLabel, scale);
                LosComponents.KeyValueRow(
                    "有效目标数（PR）",
                    snapshot.ValidEnemyCount.ToString(),
                    scale);
                LosComponents.KeyValueRow(
                    "AOE 判定",
                    snapshot.AoeDecisionLabel,
                    scale,
                    snapshot.IsAoeMode ? LosPalette.Success : LosPalette.TextSecondary);
            },
            "当前目标",
            snapshot.InCombat ? "战斗状态已建立" : "等待进入战斗",
            height: 340f,
            scale: scale);
    }

    private static void DrawEngineStatus(BlmUiSnapshot snapshot, float scale)
    {
        LosCard.Draw(
            "overview_engine_card",
            () =>
            {
                LosComponents.StatusPill(
                    snapshot.IsFactLayerConnected ? "事实层已连接" : "事实层未连接",
                    snapshot.IsFactLayerConnected ? LosStatusTone.Success : LosStatusTone.Warning,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.DecisionStatus,
                    LosStatusTone.Warning,
                    scale);
                ImGui.Dummy(new Vector2(0f, 10f * scale));

                LosComponents.StatusPill(
                    snapshot.HistoryReliable ? "历史可信" : "等待真实换相",
                    snapshot.HistoryReliable ? LosStatusTone.Success : LosStatusTone.Warning,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.PendingGaugeReconcile ? "等待下一 Tick" : "Gauge 已对账",
                    snapshot.PendingGaugeReconcile ? LosStatusTone.Warning : LosStatusTone.Info,
                    scale);

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                LosComponents.KeyValueRow(
                    "状态代次",
                    $"Combat {snapshot.CombatSerial} / Generation {snapshot.StateGeneration}",
                    scale);
                LosComponents.KeyValueRow("最近 Ack", FormatAck(snapshot), scale);
                LosComponents.KeyValueRow(
                    "动作后事实",
                    snapshot.PendingGaugeReconcile ? "等待下一 Framework Tick" : "已完成 Gauge 对账",
                    scale,
                    snapshot.PendingGaugeReconcile ? LosPalette.Warning : LosPalette.TextSecondary);

                if (!snapshot.IsAvailable)
                {
                    BlmPanelPrimitives.DrawDivider(scale);
                    ImGui.TextColored(LosPalette.Warning, snapshot.AvailabilityText);
                    if (!string.IsNullOrWhiteSpace(snapshot.CaptureError))
                        BlmPanelPrimitives.DrawMuted(snapshot.CaptureError);
                }
            },
            "循环状态",
            "资源跟踪、动作回执与状态代次",
            height: 280f,
            scale: scale);
    }

    private static void DrawOverviewGrid(
        Action topLeft,
        Action topRight,
        Action bottomLeft,
        Action bottomRight,
        float scale)
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (available < 760f * scale)
        {
            topLeft();
            topRight();
            bottomLeft();
            bottomRight();
            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.CellPadding,
            LosMetrics.Scale(new Vector2(8f, 0f), scale));
        try
        {
            if (!ImGui.BeginTable(
                    "##overview_four_card_grid",
                    2,
                    ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                return;
            }

            try
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                topLeft();
                ImGui.TableNextColumn();
                topRight();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                bottomLeft();
                ImGui.TableNextColumn();
                bottomRight();
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

    private static void ApplyModePreset(
        BlackMageSettingsStore store,
        BlmConsoleMode mode,
        string feedback)
    {
        foreach (var (key, value) in BlackMageRotation.PresetFor(mode))
        {
            BlmPanelPrimitives.SafeSetQt(key, value, store);
        }

        store.Update(settings => BlackMageRotation.ApplyModeDefaults(settings, mode));
        _modeFeedback = feedback;
        _modeFeedbackUntilUtc = DateTime.UtcNow.AddSeconds(2.5);
    }

    private static string FormatAck(BlmUiSnapshot snapshot)
    {
        if (snapshot.LastAckActionId == 0)
        {
            return "暂无";
        }

        var sequence = snapshot.LastAckSequence == 0
            ? "无序列"
            : $"Seq {snapshot.LastAckSequence}";
        var age = snapshot.LastAckAgeMs < 0
            ? string.Empty
            : $" / {snapshot.LastAckAgeMs} ms 前";
        return $"{snapshot.LastAckActionId} / {sequence}{age}";
    }
}
