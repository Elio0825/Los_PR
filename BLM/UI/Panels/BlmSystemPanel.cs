using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.Data;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

public static class BlmSystemPanel
{
    public static void Draw(
        BlackMageSettingsStore store,
        BlmUiSnapshot snapshot,
        BlmDebugSnapshot debugSnapshot,
        float scale,
        bool reduceMotion,
        Action resetInterface,
        Action openLogDirectory,
        Action clearDebugView)
    {
        LosSection.Draw(
            "system_preferences",
            "界面与诊断",
            () => DrawAdaptivePair(
                "system_preferences_pair",
                () => DrawAppearance(store, scale, reduceMotion),
                () => DrawDiagnostics(store, scale, reduceMotion),
                scale),
            scale: scale);

        LosSection.Draw(
            "system_maintenance",
            "维护",
            () => DrawMaintenance(store, scale, resetInterface),
            scale: scale);

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
                "Decision、PAction 与服务器 ActionEffect 分层显示",
                scale);

            LosSection.Draw(
                "system_debug_log",
                "Debug 日志",
                () => DrawDebugLog(
                    debugSnapshot,
                    openLogDirectory,
                    clearDebugView,
                    scale),
                "UTF-8 JSONL；文件写入在后台执行",
                scale);

            LosSection.Draw(
                "system_tracker_diagnostics",
                "Tracker 诊断",
                () => DrawTrackerDiagnostics(snapshot, scale),
                "只读事实快照；不会推进状态或修改战斗数据",
                scale);
        }
    }

    private static void DrawAppearance(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        LosCard.Draw(
            "system_appearance_card",
            () =>
            {
                var settings = store.Settings;
                var opacity = settings.WindowOpacity;
                if (LosComponents.SliderFloat(
                        "窗口透明度##system_opacity",
                        ref opacity,
                        0.70f,
                        1.00f,
                        "%.2f",
                        "控制独立控制台背景透明度。",
                        scale: scale))
                {
                    store.Update(value => value.WindowOpacity = opacity);
                }

                var uiScale = settings.UiScale;
                if (LosComponents.SliderFloat(
                        "界面缩放##system_scale",
                        ref uiScale,
                        LosMetrics.MinScale,
                        LosMetrics.MaxScale,
                        "%.2f x",
                        "调整控件和间距；窗口最小尺寸保持不变。",
                        scale: scale))
                {
                    store.Update(value => value.UiScale = uiScale);
                }

                BlmPanelPrimitives.DrawDivider(scale);
                BlmPanelPrimitives.DrawToggleRow(
                    "system_reduce_motion",
                    "减少动效",
                    "关闭非必要的悬停过渡和脉冲效果。",
                    settings.ReduceMotion,
                    value => store.Update(item => item.ReduceMotion = value),
                    scale,
                    reduceMotion);
                BlmPanelPrimitives.DrawDivider(scale);
                BlmPanelPrimitives.DrawToggleRow(
                    "system_remember_window",
                    "记住窗口",
                    "保存控制台位置和尺寸；拖动与缩放采用 500ms 防抖。",
                    settings.RememberWindow,
                    value => store.Update(item => item.RememberWindow = value),
                    scale,
                    reduceMotion);
            },
            "外观",
            "安静、紧凑的黑魔控制台主题",
            height: 300f,
            scale: scale);
    }

    private static void DrawDiagnostics(
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        LosCard.Draw(
            "system_diagnostics_card",
            () =>
            {
                var settings = store.Settings;
                BlmPanelPrimitives.DrawToggleRow(
                    "system_advanced_debug",
                    "高级调试",
                    "显示 Resolver 决策、通用 Pending、Ack 与 Gauge。",
                    settings.ShowAdvancedDebug,
                    value => store.Update(item => item.ShowAdvancedDebug = value),
                    scale,
                    reduceMotion);
                BlmPanelPrimitives.DrawDivider(scale);
                BlmPanelPrimitives.DrawToggleRow(
                    "system_decision_log",
                    "决策日志",
                    "将 Debug 事件写入 UTF-8 JSONL；关闭后仍保留内存面板。",
                    settings.DecisionLogging,
                    value => store.Update(item => item.DecisionLogging = value),
                    scale,
                    reduceMotion);
                BlmPanelPrimitives.DrawDivider(scale);

                var minimumLevel = settings.MinimumEnabledLevel;
                if (LosComponents.SliderInt(
                        "最低启用等级##system_min_level",
                        ref minimumLevel,
                        1,
                        100,
                        "Lv.%d",
                        "低于该等级时，智能决策引擎保持禁用。",
                        scale: scale))
                {
                    store.Update(item => item.MinimumEnabledLevel = minimumLevel);
                }
            },
            "诊断",
            "面板与文件日志独立控制",
            height: 235f,
            scale: scale);
    }

    private static void DrawMaintenance(
        BlackMageSettingsStore store,
        float scale,
        Action resetInterface)
    {
        LosCard.Draw(
            "system_maintenance_card",
            () =>
            {
                if (LosComponents.SecondaryButton(
                        "system_save_now",
                        "立即保存",
                        size: new Vector2(132f * scale, 34f * scale),
                        scale: scale,
                        tooltip: "立即将当前控制台设置写入 UTF-8 JSON。"))
                {
                    store.SaveNow();
                }

                ImGui.SameLine();
                if (LosComponents.DangerButton(
                        "system_reset_ui",
                        "重置界面",
                        size: new Vector2(132f * scale, 34f * scale),
                        scale: scale,
                        tooltip: "恢复默认外观、页签、窗口位置与尺寸；不会更改任何 QT。"))
                {
                    resetInterface();
                }

                ImGui.Dummy(new Vector2(0f, 10f * scale));
                LosComponents.KeyValueRow("配置文件", store.FilePath, scale);
            },
            "配置",
            "重置界面不会触碰作战开关",
            height: 175f,
            scale: scale);
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
                    debug.WriterHealthy ? "Writer 正常" : "Writer 异常",
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
                    "返回候选",
                    dispatch is null
                        ? "暂无"
                        : $"{FormatAction(dispatch)} / {dispatch.PActionType}",
                    scale,
                    LosPalette.Info);
                LosComponents.KeyValueRow(
                    "服务器确认",
                    ack is null
                        ? "暂无"
                        : $"{FormatAction(ack)} / Seq {ack.GlobalSequence}",
                    scale,
                    LosPalette.Success);
                LosComponents.KeyValueRow(
                    "Ack 前资源",
                    ack is null ? "暂无" : FormatResources(ack.Resources),
                    scale);
                LosComponents.KeyValueRow(
                    "Gauge 后资源",
                    gauge is null ? "暂无" : FormatResources(gauge.Resources),
                    scale);
                LosComponents.KeyValueRow(
                    "Ack 时序",
                    ack is null
                        ? "暂无"
                        : $"GCD {ack.Resources.GcdRemainSeconds:F2}s"
                            + $" / Cast {ack.Resources.CastRemainSeconds:F2}s"
                            + $" / Lock {ack.Resources.AnimationLockSeconds:F3}s",
                    scale);
            },
            "最近动作与资源",
            "候选返回不等于实际释放；实际释放只看 Ack",
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
                    "Resolver 生产模式",
                    LosStatusTone.Success,
                    scale);
                ImGui.SameLine();
                LosComponents.StatusPill(
                    snapshot.HasPendingIssuedAction ? "等待 Ack" : "无 Pending",
                    snapshot.HasPendingIssuedAction
                        ? LosStatusTone.Warning
                        : LosStatusTone.Neutral,
                    scale);
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                LosComponents.KeyValueRow(
                    "决策核心",
                    "Level 100 Resolver",
                    scale);
                LosComponents.KeyValueRow(
                    "通用 Pending",
                    snapshot.HasPendingIssuedAction
                        ? $"{BlmActionNames.Get(snapshot.PendingIssuedActionId)}"
                            + $" ({snapshot.PendingIssuedActionId})"
                        : "暂无",
                    scale);
                LosComponents.KeyValueRow(
                    "Pending 期限",
                    FormatDeadline(
                        snapshot.PendingIssuedActionDeadlineAtMs,
                        snapshot.CapturedAtMs),
                    scale);
                LosComponents.KeyValueRow(
                    "Gauge 对账",
                    snapshot.PendingGaugeReconcile ? "等待下一 Tick" : "已同步",
                    scale);
                LosComponents.KeyValueRow(
                    "Generation",
                    snapshot.StateGeneration.ToString(),
                    scale);

                BlmPanelPrimitives.DrawDivider(scale);
                LosComponents.KeyValueRow(
                    "历史可信",
                    BoolLabel(snapshot.HistoryReliable),
                    scale);
                LosComponents.KeyValueRow(
                    "Manafont Serial",
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
            "Resolver 与回执",
            "标准循环没有 Route/Step 状态图",
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
                    debug.FileLoggingEnabled ? "JSONL 开启" : "JSONL 关闭",
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
                LosComponents.KeyValueRow("日志文件", EmptyAs(debug.CurrentFilePath, "尚未创建"), scale);
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
                        tooltip: "打开 Los 配置目录下的 DebugLogs。"))
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
            "事件序列：ResolverFrame -> Dispatch -> ActionEffect Ack -> Gauge",
            height: 540f,
            scale: scale);
    }

    private static void DrawEventTable(BlmDebugSnapshot debug, float scale)
    {
        if (debug.RecentEvents.Count == 0)
        {
            ImGui.TextDisabled("暂无 Debug 事件");
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
        var detail = string.IsNullOrEmpty(debugEvent.Reason)
            ? debugEvent.Detail
            : string.IsNullOrEmpty(debugEvent.Detail)
                ? debugEvent.Reason
                : $"{debugEvent.Reason} | {debugEvent.Detail}";
        var result = string.IsNullOrEmpty(debugEvent.RuleId)
            ? EmptyAs(detail, "-")
            : $"{debugEvent.RuleId} | {EmptyAs(detail, "-")}";
        return debugEvent.Resources.MaxMp > 0
            && debugEvent.Kind is BlmDebugEventKind.Decision
            or BlmDebugEventKind.ResolverFrame
            or BlmDebugEventKind.DispatchReturned
            or BlmDebugEventKind.ActionEffectObserved
            or BlmDebugEventKind.AckAccepted
            or BlmDebugEventKind.AckRejected
            or BlmDebugEventKind.GaugeReconciled
            ? $"{result} | MP {debugEvent.Resources.Mp}"
                + $" AF/UI {debugEvent.Resources.AfStacks}/{debugEvent.Resources.IceStacks}"
                + $" H {debugEvent.Resources.UmbralHearts} S {debugEvent.Resources.AstralSoul}"
            : result;
    }

    private static string FormatKind(BlmDebugEventKind kind) => kind switch
    {
        BlmDebugEventKind.Decision => "Decision",
        BlmDebugEventKind.ResolverFrame => "Resolver",
        BlmDebugEventKind.DispatchReturned => "Dispatch",
        BlmDebugEventKind.ActionEffectObserved => "Ack 观察",
        BlmDebugEventKind.AckAccepted => "Ack 接受",
        BlmDebugEventKind.AckRejected => "Ack 拒绝",
        BlmDebugEventKind.GaugeReconciled => "Gauge",
        BlmDebugEventKind.Lifecycle => "生命周期",
        BlmDebugEventKind.AckQueueDropped => "Ack 丢失",
        BlmDebugEventKind.LoggerDropped => "Logger 丢失",
        BlmDebugEventKind.LoggerError => "Logger 错误",
        _ => kind.ToString(),
    };

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
        => $"MP {resources.Mp:N0}/{resources.MaxMp:N0}"
            + $" | {resources.Phase} AF{resources.AfStacks} UI{resources.IceStacks}"
            + $" H{resources.UmbralHearts} S{resources.AstralSoul}"
            + $" P{resources.PolyglotStacks}/{resources.MaxPolyglot}"
            + $" | 悖论 {BoolLabel(resources.HasParadox)}"
            + $" 火苗 {BoolLabel(resources.HasFirestarter)}";

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
                    snapshot.PendingGaugeReconcile ? "等待 Gauge" : "对账完成",
                    snapshot.PendingGaugeReconcile ? LosStatusTone.Warning : LosStatusTone.Info,
                    scale);

                ImGui.Dummy(new Vector2(0f, 8f * scale));
                LosComponents.KeyValueRow(
                    "生命周期",
                    $"Combat {snapshot.CombatSerial} / Generation {snapshot.StateGeneration}",
                    scale);
                LosComponents.KeyValueRow(
                    "Phase Serial",
                    $"Fire {snapshot.FirePhaseSerial} / Ice {snapshot.IcePhaseSerial}",
                    scale);
                LosComponents.KeyValueRow(
                    "Fire IV",
                    $"本火段 {snapshot.Fire4Count} / 魔泉后 {snapshot.Fire4CountSinceManafont}",
                    scale);
                LosComponents.KeyValueRow("Manafont Serial", snapshot.ManafontUseSerial.ToString(), scale);
                LosComponents.KeyValueRow(
                    "最近 Ack",
                    snapshot.LastAckActionId == 0
                        ? "暂无"
                        : $"Action {snapshot.LastAckActionId} / Seq {snapshot.LastAckSequence}",
                    scale);
                LosComponents.KeyValueRow(
                    "Ack 时间",
                    FormatTimestamp(snapshot.LastAckAtMs, snapshot.LastAckAgeMs),
                    scale);
                LosComponents.KeyValueRow(
                    "Gauge 对账",
                    snapshot.PendingGaugeReconcile
                        ? "等待下一 Framework Tick"
                        : FormatGaugeReconcile(snapshot),
                    scale,
                    snapshot.PendingGaugeReconcile ? LosPalette.Warning : LosPalette.TextSecondary);
                LosComponents.KeyValueRow("最近重置", snapshot.LastResetReason, scale);
            },
            "Tracker / Ack",
            "同一 BlmContext 中冻结的诊断字段",
            height: 355f,
            scale: scale);
    }

    private static string FormatGaugeReconcile(BlmUiSnapshot snapshot)
    {
        if (snapshot.LastGaugeReconciledActionId == 0)
            return "暂无动作后对账";

        return $"Action {snapshot.LastGaugeReconciledActionId} / {FormatAge(snapshot.LastGaugeReconcileAgeMs)}";
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
