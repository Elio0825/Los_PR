using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.Data;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

internal static class BlmDebugPanel
{
    public static void Draw(
        BlackMageSettingsStore store,
        BlmUiSnapshot snapshot,
        BlmDebugSnapshot debugSnapshot,
        float scale,
        bool reduceMotion,
        Action openLogDirectory,
        Action clearDebugView)
    {
        if (store.Settings.ShowAdvancedDebug)
        {
            LosSection.Draw(
                "system_live_debug",
                "实时调试",
                () => DrawAdaptivePair(
                    "system_live_debug_pair",
                    () => DrawRecentActions(debugSnapshot, scale),
                    () => DrawResolverRuntime(snapshot, scale),
                    scale),
                "循环决策、技能交付与服务器回执分层显示",
                scale);

            LosSection.Draw(
                "system_debug_log",
                "调试日志",
                () => DrawDebugLog(
                    debugSnapshot,
                    openLogDirectory,
                    clearDebugView,
                    scale),
                "同时生成易读中文日志与原始 JSONL，后台安全写入",
                scale);

            LosSection.Draw(
                "system_tracker_diagnostics",
                "状态追踪诊断",
                () => DrawTrackerDiagnostics(snapshot, scale),
                "只读事实快照；不会推进状态或修改战斗数据",
                scale);
        }
    }

    internal static void DrawCompactControls(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        _ = reduceMotion;
        ImGui.TextColored(LosPalette.Cyan, "诊断开关");
        var settings = store.Settings;
        var advanced = settings.ShowAdvancedDebug;
        if (ImGui.Checkbox("详细面板##debug_compact", ref advanced))
            store.Update(item => item.ShowAdvancedDebug = advanced);

        var logging = settings.DecisionLogging;
        if (ImGui.Checkbox("写入调试日志##debug_log_compact", ref logging))
            store.Update(item => item.DecisionLogging = logging);

        ImGui.TextDisabled("最低启用等级");
        var minimumLevel = settings.MinimumEnabledLevel;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderInt("##debug_min_level_compact", ref minimumLevel, 1, 100, "Lv.%d"))
            store.Update(item => item.MinimumEnabledLevel = minimumLevel);
    }

    private static void DrawRecentActions(BlmDebugSnapshot debug, float scale)
    {
        LosCard.Draw(
            "system_recent_actions_card",
            () =>
            {
                LosComponents.StatusPill(
                    debug.FileLoggingEnabled ? "文件记录中" : "仅内存",
                    debug.FileLoggingEnabled ? LosStatusTone.Success : LosStatusTone.Info,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    debug.WriterHealthy ? "写入正常" : "写入异常",
                    debug.WriterHealthy ? LosStatusTone.Success : LosStatusTone.Danger,
                    scale);

                var decision = FindLast(debug, BlmDebugEventKind.ResolverFrame);
                var dispatch = FindLast(debug, BlmDebugEventKind.DispatchReturned);
                var ack = FindLast(debug, BlmDebugEventKind.AckAccepted);
                var gauge = FindLast(debug, BlmDebugEventKind.GaugeReconciled);
                ImGui.Dummy(new Vector2(0f, 8f * scale));
                LosComponents.KeyValueRow(
                    "最近决策",
                    decision is null
                        ? "暂无"
                        : string.IsNullOrEmpty(decision.RuleId)
                            ? FormatAction(decision)
                            : $"{FormatAction(decision)} / {decision.RuleId}",
                    scale);
                LosComponents.KeyValueRow(
                    "已交付技能",
                    dispatch is null
                        ? "暂无"
                        : $"{FormatAction(dispatch)} / {FormatActionType(dispatch.PActionType)}",
                    scale,
                    LosPalette.Info);
                LosComponents.KeyValueRow(
                    "服务器确认",
                    ack is null
                        ? "暂无"
                        : $"{FormatAction(ack)} / 服务器序列 {ack.GlobalSequence}",
                    scale,
                    LosPalette.Success);
                LosComponents.KeyValueRow(
                    "确认时资源",
                    ack is null ? "暂无" : FormatResources(ack.Resources),
                    scale);
                LosComponents.KeyValueRow(
                    "对账后资源",
                    gauge is null ? "暂无" : FormatResources(gauge.Resources),
                    scale);
                LosComponents.KeyValueRow(
                    "确认时序",
                    ack is null
                        ? "暂无"
                        : $"GCD 剩余 {ack.Resources.GcdRemainSeconds:F2} 秒"
                            + $" / 读条剩余 {ack.Resources.CastRemainSeconds:F2} 秒"
                            + $" / 动画锁 {ack.Resources.AnimationLockSeconds:F3} 秒",
                    scale);
            },
            "最近动作与资源",
            "候选交付不等于实际释放；以服务器确认结果为准",
            height: 415f,
            scale: scale);
    }

    private static void DrawResolverRuntime(BlmUiSnapshot snapshot, float scale)
    {
        LosCard.Draw(
            "system_resolver_runtime_card",
            () =>
            {
                LosComponents.StatusPill(
                    "循环决策工作中",
                    LosStatusTone.Success,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.HasPendingIssuedAction ? "等待服务器确认" : "无待确认动作",
                    snapshot.HasPendingIssuedAction
                        ? LosStatusTone.Warning
                        : LosStatusTone.Neutral,
                    scale);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                LosComponents.KeyValueRow(
                    "决策核心",
                    "90–100级标准单体循环",
                    scale);
                LosComponents.KeyValueRow(
                    "待确认动作",
                    snapshot.HasPendingIssuedAction
                        ? $"{BlmActionNames.Get(snapshot.PendingIssuedActionId)}"
                            + $" ({snapshot.PendingIssuedActionId})"
                        : "暂无",
                    scale);
                LosComponents.KeyValueRow(
                    "确认期限",
                    FormatDeadline(
                        snapshot.PendingIssuedActionDeadlineAtMs,
                        snapshot.CapturedAtMs),
                    scale);
                LosComponents.KeyValueRow(
                    "资源对账",
                    snapshot.PendingGaugeReconcile ? "等待下一帧" : "已同步",
                    scale);
                LosComponents.KeyValueRow(
                    "状态代次",
                    snapshot.StateGeneration.ToString(),
                    scale);

                BlmPanelPrimitives.DrawDivider(scale);
                LosComponents.KeyValueRow(
                    "历史可信",
                    BoolLabel(snapshot.HistoryReliable),
                    scale);
                LosComponents.KeyValueRow(
                    "魔泉使用序号",
                    snapshot.ManafontUseSerial.ToString(),
                    scale);
                LosComponents.KeyValueRow(
                    "火四计数",
                    $"本火段 {snapshot.Fire4Count} / 魔泉后 {snapshot.Fire4CountSinceManafont}",
                    scale);
                LosComponents.KeyValueRow(
                    "最近重置",
                    snapshot.LastResetReason,
                    scale);
            },
            "循环决策与回执",
            "直接展示当前决策、待确认动作与服务器回执",
            height: 415f,
            scale: scale);
    }

    private static void DrawDebugLog(
        BlmDebugSnapshot debug,
        Action openLogDirectory,
        Action clearDebugView,
        float scale)
    {
        LosCard.Draw(
            "system_debug_log_card",
            () =>
            {
                LosComponents.StatusPill(
                    debug.FileLoggingEnabled ? "文件记录开启" : "文件记录关闭",
                    debug.FileLoggingEnabled ? LosStatusTone.Success : LosStatusTone.Neutral,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    debug.WriterHealthy ? "写入正常" : "写入异常",
                    debug.WriterHealthy ? LosStatusTone.Success : LosStatusTone.Danger,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    debug.DroppedCount == 0 ? "无丢失" : $"丢失 {debug.DroppedCount}",
                    debug.DroppedCount == 0 ? LosStatusTone.Info : LosStatusTone.Danger,
                    scale);

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                LosComponents.KeyValueRow(
                    "事件",
                    $"内存 {debug.RecentEvents.Count} / 待写 {debug.PendingCount}"
                        + $" / 已写 {debug.WrittenCount}",
                    scale);
                LosComponents.KeyValueRow(
                    "易读中文日志",
                    EmptyAs(debug.ReadableFilePath, "尚未创建"),
                    scale);
                LosComponents.KeyValueRow(
                    "原始 JSONL",
                    EmptyAs(debug.CurrentFilePath, "尚未创建"),
                    scale);
                LosComponents.KeyValueRow("日志目录", EmptyAs(debug.LogDirectory, "不可用"), scale);
                if (!string.IsNullOrEmpty(debug.LastError))
                {
                    LosComponents.KeyValueRow(
                        "最后错误",
                        debug.LastError,
                        scale,
                        LosPalette.Danger);
                }

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                if (LosComponents.SecondaryButton(
                        "system_open_debug_log",
                        "打开日志目录",
                        size: new Vector2(148f * scale, 34f * scale),
                        scale: scale,
                        tooltip: "打开日志目录；日常查看 .log，排查时保留 .jsonl。"))
                {
                    openLogDirectory();
                }

                ImGui.SameLine();
                if (LosComponents.SecondaryButton(
                        "system_clear_debug_view",
                        "清空面板",
                        size: new Vector2(132f * scale, 34f * scale),
                        scale: scale,
                        tooltip: "只清空内存事件表，不截断或删除 JSONL。"))
                {
                    clearDebugView();
                }

                ImGui.Dummy(new Vector2(0f, 10f * scale));
                DrawEventTable(debug, scale);
            },
            "日志状态与最近事件",
            "事件顺序：循环决策 -> 技能交付 -> 服务器回执 -> 资源对账",
            height: 540f,
            scale: scale);
    }

    private static void DrawEventTable(BlmDebugSnapshot debug, float scale)
    {
        if (debug.RecentEvents.Count == 0)
        {
            ImGui.TextDisabled("暂无调试事件");
            return;
        }

        var flags = ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.NoSavedSettings
            | ImGuiTableFlags.SizingFixedFit
            | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable(
                "##system_debug_events",
                4,
                flags,
                new Vector2(0f, 245f * scale)))
        {
            return;
        }

        try
        {
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 88f * scale);
            ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, 118f * scale);
            ImGui.TableSetupColumn("技能", ImGuiTableColumnFlags.WidthFixed, 132f * scale);
            ImGui.TableSetupColumn("详情", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            var first = Math.Max(0, debug.RecentEvents.Count - 80);
            for (var index = debug.RecentEvents.Count - 1; index >= first; index--)
            {
                var item = debug.RecentEvents[index];
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                DrawFittedCell(item.Utc.ToLocalTime().ToString("HH:mm:ss.fff"), scale);
                ImGui.TableSetColumnIndex(1);
                DrawFittedCell(FormatKind(item.Kind), scale);
                ImGui.TableSetColumnIndex(2);
                DrawFittedCell(FormatAction(item), scale);
                ImGui.TableSetColumnIndex(3);
                DrawFittedCell(FormatEventDetail(item), scale);
            }
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private static void DrawFittedCell(string text, float scale)
    {
        var width = Math.Max(20f, ImGui.GetContentRegionAvail().X - 4f * scale);
        var fitted = LosComponents.FitText(text, width);
        ImGui.TextUnformatted(fitted);
        if (fitted != text)
        {
            LosComponents.TooltipIfHovered(text, scale);
        }
    }

    private static BlmDebugEvent? FindLast(
        BlmDebugSnapshot debug,
        BlmDebugEventKind kind)
    {
        for (var index = debug.RecentEvents.Count - 1; index >= 0; index--)
        {
            if (debug.RecentEvents[index].Kind == kind)
            {
                return debug.RecentEvents[index];
            }
        }

        return null;
    }

    private static string FormatAction(BlmDebugEvent debugEvent)
        => debugEvent.ActionId == 0
            ? "-"
            : $"{debugEvent.ActionName} ({debugEvent.ActionId})";

    private static string FormatEventDetail(BlmDebugEvent debugEvent)
    {
        var summary = string.IsNullOrEmpty(debugEvent.Summary)
            ? BlmDebugHumanFormatter.BuildSummary(debugEvent)
            : debugEvent.Summary;
        var result = string.IsNullOrEmpty(debugEvent.RuleId)
            ? summary
            : $"{summary} | 规则 {debugEvent.RuleId}";
        return debugEvent.Resources.MaxMp > 0
            && debugEvent.Kind is BlmDebugEventKind.Decision
            or BlmDebugEventKind.ResolverFrame
            or BlmDebugEventKind.DispatchReturned
            or BlmDebugEventKind.ActionEffectObserved
            or BlmDebugEventKind.AckAccepted
            or BlmDebugEventKind.AckRejected
            or BlmDebugEventKind.GaugeReconciled
            ? $"{result} | {BlmDebugHumanFormatter.FormatPhase(debugEvent.Resources.Phase)}"
                + $" MP {debugEvent.Resources.Mp}"
                + $" 火/冰层 {debugEvent.Resources.AfStacks}/{debugEvent.Resources.IceStacks}"
                + $" 冰针 {debugEvent.Resources.UmbralHearts} 星极魂 {debugEvent.Resources.AstralSoul}"
            : result;
    }

    private static string FormatKind(BlmDebugEventKind kind)
        => BlmDebugHumanFormatter.FormatEventKind(kind);

    private static string FormatDeadline(long deadlineAtMs, long capturedAtMs)
    {
        if (deadlineAtMs <= 0)
        {
            return "暂无";
        }

        var remain = deadlineAtMs - capturedAtMs;
        return remain >= 0 ? $"剩余 {remain} ms" : $"已超时 {-remain} ms";
    }

    private static string BoolLabel(bool value) => value ? "是" : "否";

    private static string FormatResources(BlmDebugResourceSnapshot resources)
        => BlmDebugHumanFormatter.FormatResources(resources);

    private static string FormatActionType(string actionType) => actionType switch
    {
        "Gcd" => "GCD 技能",
        "OffGcd" => "能力技",
        "Always" => "高优先能力技",
        _ => EmptyAs(actionType, "未知类型"),
    };

    private static string EmptyAs(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static void DrawTrackerDiagnostics(BlmUiSnapshot snapshot, float scale)
    {
        LosCard.Draw(
            "system_tracker_diagnostics_card",
            () =>
            {
                LosComponents.StatusPill(
                    snapshot.IsFactLayerConnected ? "事实层已连接" : "事实层未连接",
                    snapshot.IsFactLayerConnected ? LosStatusTone.Success : LosStatusTone.Warning,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.HistoryReliable ? "历史可信" : "历史不完整",
                    snapshot.HistoryReliable ? LosStatusTone.Success : LosStatusTone.Warning,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.PendingGaugeReconcile ? "等待资源对账" : "对账完成",
                    snapshot.PendingGaugeReconcile ? LosStatusTone.Warning : LosStatusTone.Info,
                    scale);

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                LosComponents.KeyValueRow(
                    "生命周期",
                    $"战斗序号 {snapshot.CombatSerial} / 状态代 {snapshot.StateGeneration}",
                    scale);
                LosComponents.KeyValueRow(
                    "阶段序号",
                    $"火阶段 {snapshot.FirePhaseSerial} / 冰阶段 {snapshot.IcePhaseSerial}",
                    scale);
                LosComponents.KeyValueRow(
                    "火四计数",
                    $"本火段 {snapshot.Fire4Count} / 魔泉后 {snapshot.Fire4CountSinceManafont}",
                    scale);
                LosComponents.KeyValueRow("魔泉使用序号", snapshot.ManafontUseSerial.ToString(), scale);
                LosComponents.KeyValueRow(
                    "最近服务器确认",
                    snapshot.LastAckActionId == 0
                        ? "暂无"
                        : $"{BlmActionNames.Get(snapshot.LastAckActionId)} ({snapshot.LastAckActionId})"
                            + $" / 服务器序列 {snapshot.LastAckSequence}",
                    scale);
                LosComponents.KeyValueRow(
                    "确认时间",
                    FormatTimestamp(snapshot.LastAckAtMs, snapshot.LastAckAgeMs),
                    scale);
                LosComponents.KeyValueRow(
                    "资源对账",
                    snapshot.PendingGaugeReconcile
                        ? "等待下一游戏帧"
                        : FormatGaugeReconcile(snapshot),
                    scale,
                    snapshot.PendingGaugeReconcile ? LosPalette.Warning : LosPalette.TextSecondary);
                LosComponents.KeyValueRow("最近重置", snapshot.LastResetReason, scale);
            },
            "状态追踪与服务器确认",
            "同一 BlmContext 中冻结的诊断字段",
            height: 355f,
            scale: scale);
    }

    private static string FormatGaugeReconcile(BlmUiSnapshot snapshot)
    {
        if (snapshot.LastGaugeReconciledActionId == 0)
            return "暂无动作后对账";

        return $"{BlmActionNames.Get(snapshot.LastGaugeReconciledActionId)}"
            + $" ({snapshot.LastGaugeReconciledActionId})"
            + $" / {FormatAge(snapshot.LastGaugeReconcileAgeMs)}";
    }

    private static string FormatTimestamp(long timestampMs, long ageMs)
        => timestampMs <= 0
            ? "暂无"
            : $"t={timestampMs} / {FormatAge(ageMs)}";

    private static string FormatAge(long ageMs)
        => ageMs < 0 ? "未知" : $"{ageMs} ms 前";

    private static void DrawAdaptivePair(
        string id,
        Action left,
        Action right,
        float scale)
    {
        if (ImGui.GetContentRegionAvail().X < 720f * scale)
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
}
