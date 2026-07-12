using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;
using PromeRotation.Data;

namespace LosPr.BLM.UI.Panels;

public static class BlmOverviewPanel
{
    public static void Draw(BlmUiSnapshot snapshot, float scale, bool reduceMotion)
    {
        DrawRunControl(snapshot, scale, reduceMotion);
        DrawAdaptivePair(
            "overview_resources",
            () => DrawResources(snapshot, scale),
            () => DrawTiming(snapshot, scale),
            scale);
        DrawAdaptivePair(
            "overview_context",
            () => DrawTarget(snapshot, scale),
            () => DrawEngineStatus(snapshot, scale),
            scale);
    }

    private static void DrawRunControl(BlmUiSnapshot snapshot, float scale, bool reduceMotion)
    {
        LosCard.Draw(
            "overview_run_control",
            () =>
            {
                var tone = snapshot.AcrState switch
                {
                    AcrState.On => LosStatusTone.Success,
                    AcrState.Hold => LosStatusTone.Warning,
                    _ => LosStatusTone.Neutral,
                };
                LosComponents.StatusPill($"ACR {snapshot.AcrStateLabel}", tone, scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.InCombat ? "战斗中" : "非战斗",
                    snapshot.InCombat ? LosStatusTone.Danger : LosStatusTone.Neutral,
                    scale);

                ImGui.Dummy(new Vector2(0f, 10f * scale));
                var gap = 8f * scale;
                var available = ImGui.GetContentRegionAvail().X;
                var buttonWidth = Math.Max(96f * scale, (available - gap * 2f) / 3f);
                var size = new Vector2(buttonWidth, 36f * scale);

                if (LosComponents.SegmentButton(
                        "acr_on", "On 开启", snapshot.AcrState == AcrState.On,
                        size, scale, reduceMotion, "允许 PR 调用当前 ACR 的决策入口。"))
                    SetAcrState(AcrState.On);
                ImGui.SameLine(0f, gap);
                if (LosComponents.SegmentButton(
                        "acr_hold", "Hold 保持", snapshot.AcrState == AcrState.Hold,
                        size, scale, reduceMotion, "暂时停止 ACR 决策，保留当前设置。"))
                    SetAcrState(AcrState.Hold);
                ImGui.SameLine(0f, gap);
                if (LosComponents.SegmentButton(
                        "acr_off", "Off 关闭", snapshot.AcrState == AcrState.Off,
                        size, scale, reduceMotion, "关闭 PR 的 ACR 调度。"))
                    SetAcrState(AcrState.Off);
            },
            "运行控制",
            "直接控制 PR 全局 ACR 状态",
            height: 155f,
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
            height: 295f,
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
            height: 250f,
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
            },
            "当前目标",
            snapshot.InCombat ? "战斗状态已建立" : "等待进入战斗",
            height: 220f,
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
            "智能引擎",
            "事实层状态；当前不会生成技能决策",
            height: 320f,
            scale: scale);
    }

    private static void DrawAdaptivePair(
        string id,
        Action left,
        Action right,
        float scale)
    {
        var available = ImGui.GetContentRegionAvail().X;
        if (available < 720f * scale)
        {
            left();
            right();
            return;
        }

        if (!ImGui.BeginTable($"##{id}", 2,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            return;

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

    private static void SetAcrState(AcrState state)
    {
        try
        {
            PromeSettings.Instance.EnableAcr = state;
        }
        catch
        {
            // PR 尚未完成初始化时保持 UI 可用，下一帧继续读取。
        }
    }

    private static string FormatAck(BlmUiSnapshot snapshot)
    {
        if (snapshot.LastAckActionId == 0)
            return "暂无";

        var sequence = snapshot.LastAckSequence == 0
            ? "无序列"
            : $"Seq {snapshot.LastAckSequence}";
        var age = snapshot.LastAckAgeMs < 0
            ? string.Empty
            : $" / {snapshot.LastAckAgeMs} ms 前";
        return $"{snapshot.LastAckActionId} / {sequence}{age}";
    }
}
