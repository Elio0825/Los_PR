using LosPr.BLM.Resolvers;

namespace LosPr.BLM.Diagnostics;

internal static class BlmDebugHumanFormatter
{
    private const string Separator =
        "--------------------------------------------------------------------------------";

    public static string BuildSummary(BlmDebugEvent debugEvent)
    {
        ArgumentNullException.ThrowIfNull(debugEvent);
        var action = FormatAction(debugEvent.ActionId);
        return debugEvent.Kind switch
        {
            BlmDebugEventKind.ResolverFrame => BuildResolverSummary(debugEvent),
            BlmDebugEventKind.Decision => debugEvent.ActionId == 0
                ? "本次决策没有选择技能。"
                : $"本次决策选择了“{action}”。",
            BlmDebugEventKind.DispatchReturned => debugEvent.ActionId == 0
                ? "本次没有向 PromeRotation 交付技能。"
                : $"已向 PromeRotation 交付“{action}”，正在等待服务器确认。",
            BlmDebugEventKind.ActionEffectObserved => debugEvent.ActionId == 0
                ? "观察到服务器动作回执。"
                : $"服务器报告已使用“{action}”，正在核对回执。",
            BlmDebugEventKind.AckAccepted => debugEvent.ActionId == 0
                ? "服务器回执已被接受。"
                : $"“{action}”的服务器回执已接受，动作视为成功。",
            BlmDebugEventKind.AckRejected => debugEvent.ActionId == 0
                ? "服务器回执被拒绝，未计入成功动作。"
                : $"“{action}”的服务器回执被拒绝，未计入成功动作。",
            BlmDebugEventKind.GaugeReconciled => debugEvent.ActionId == 0
                ? "动作后的资源状态已经完成对账。"
                : $"“{action}”后的资源状态已经完成对账。",
            BlmDebugEventKind.Lifecycle => FirstNonEmpty(
                TranslateText(debugEvent.Reason),
                TranslateText(debugEvent.Detail),
                "循环生命周期发生变化。"),
            BlmDebugEventKind.AckQueueDropped => FirstNonEmpty(
                TranslateText(debugEvent.Reason),
                "服务器回执队列发生丢失。"),
            BlmDebugEventKind.LoggerDropped => FirstNonEmpty(
                TranslateText(debugEvent.Detail),
                "日志队列已满，部分记录未能写入。"),
            BlmDebugEventKind.LoggerError => FirstNonEmpty(
                TranslateText(debugEvent.Detail),
                "日志写入发生错误。"),
            _ => FirstNonEmpty(
                TranslateText(debugEvent.Reason),
                TranslateText(debugEvent.Detail),
                "记录了一条调试事件。"),
        };
    }

    public static string FormatDocument(BlmDebugEvent debugEvent)
    {
        ArgumentNullException.ThrowIfNull(debugEvent);
        var builder = new StringBuilder(1024);
        var localTime = debugEvent.Utc.ToLocalTime();
        builder.Append('[')
            .Append(localTime.ToString("yyyy-MM-dd HH:mm:ss.fff"))
            .Append("]  #")
            .Append(debugEvent.EventSequence)
            .Append("  【")
            .Append(FormatEventKind(debugEvent.Kind))
            .Append('】');
        if (debugEvent.ActionId != 0)
        {
            builder.Append("  ")
                .Append(FormatAction(debugEvent.ActionId))
                .Append(" (")
                .Append(debugEvent.ActionId)
                .Append(')');
        }

        builder.AppendLine();
        AppendLine(
            builder,
            "结论",
            FirstNonEmpty(debugEvent.Summary, BuildSummary(debugEvent)));

        var reason = TranslateText(debugEvent.Reason);
        if (reason.Length > 0 && !string.Equals(reason, debugEvent.Summary, StringComparison.Ordinal))
            AppendLine(builder, "原因", reason);

        var detailIsCoverage = LooksLikeFactCoverage(debugEvent.Detail);
        if (debugEvent.Detail.Length > 0 && !detailIsCoverage)
            AppendLine(builder, "补充", TranslateText(debugEvent.Detail));

        if (debugEvent.RuleId.Length > 0)
            AppendLine(builder, "规则", DescribeRule(debugEvent.RuleId));

        if (debugEvent.Resources.MaxMp > 0)
        {
            AppendLine(builder, "资源", FormatResources(debugEvent.Resources));
            AppendLine(builder, "现场", FormatScene(debugEvent.Resources));
            AppendLine(builder, "时序", FormatTiming(debugEvent.Resources));
        }

        if (debugEvent.Resolver is { } resolver)
        {
            AppendLine(builder, "决策", FormatResolver(resolver));
            if (resolver.BlockReason.Length > 0)
                AppendLine(builder, "阻断", TranslateText(resolver.BlockReason));

            var coverage = FirstNonEmpty(resolver.FactCoverage, detailIsCoverage ? debugEvent.Detail : string.Empty);
            if (coverage.Length > 0)
                AppendLine(builder, "事实缺口", TranslateText(coverage));

            AppendLine(
                builder,
                "底层标识",
                $"入口 {EmptyAs(debugEvent.EntryPoint, "-")}"
                    + $" | Resolver {EmptyAs(resolver.ResolverId, "-")}"
                    + $" | Check {resolver.CheckCode}"
                    + $" | 帧 {resolver.FrameSequence}"
                    + $" | 状态代 {resolver.FrameStateGeneration}");
        }
        else if (debugEvent.EntryPoint.Length > 0 || debugEvent.PActionType.Length > 0)
        {
            AppendLine(
                builder,
                "底层标识",
                $"入口 {EmptyAs(debugEvent.EntryPoint, "-")}"
                    + (debugEvent.PActionType.Length == 0
                        ? string.Empty
                        : $" | 动作类型 {debugEvent.PActionType}"));
        }

        AppendLine(builder, "追踪", FormatTracking(debugEvent));
        builder.AppendLine(Separator);
        builder.AppendLine();
        return builder.ToString();
    }

    public static string FormatEventKind(BlmDebugEventKind kind) => kind switch
    {
        BlmDebugEventKind.Decision => "循环决策",
        BlmDebugEventKind.ResolverFrame => "循环决策帧",
        BlmDebugEventKind.DispatchReturned => "技能已交付",
        BlmDebugEventKind.ActionEffectObserved => "观察到服务器回执",
        BlmDebugEventKind.AckAccepted => "服务器回执已接受",
        BlmDebugEventKind.AckRejected => "服务器回执被拒绝",
        BlmDebugEventKind.GaugeReconciled => "资源状态已对账",
        BlmDebugEventKind.Lifecycle => "循环状态变化",
        BlmDebugEventKind.AckQueueDropped => "服务器回执丢失",
        BlmDebugEventKind.LoggerDropped => "日志记录丢失",
        BlmDebugEventKind.LoggerError => "日志写入错误",
        _ => "调试事件",
    };

    public static string FormatResources(BlmDebugResourceSnapshot resources)
        => $"{FormatPhase(resources.Phase)}"
            + $" | MP {resources.Mp:N0}/{resources.MaxMp:N0}"
            + $" | 火层 {resources.AfStacks} / 冰层 {resources.IceStacks}"
            + $" | 冰针 {resources.UmbralHearts}"
            + $" | 星极魂 {resources.AstralSoul}"
            + $" | 通晓 {resources.PolyglotStacks}/{resources.MaxPolyglot}"
            + $" | 悖论 {YesNo(resources.HasParadox)}"
            + $" | 火苗 {YesNo(resources.HasFirestarter)}"
            + $" | 雷云 {YesNo(resources.HasThunderhead)}";

    public static string FormatPhase(BlmPhase phase) => phase switch
    {
        BlmPhase.Fire => "火阶段",
        BlmPhase.Ice => "冰阶段",
        _ => "无属性阶段",
    };

    public static string TranslateText(string? value)
    {
        var text = BlmDebugText.Clean(value);
        if (text.Length == 0)
            return string.Empty;

        var replacements = new (string Source, string Target)[]
        {
            ("LifecycleOrTargetGate", "循环未启用、角色不可行动或目标无效"),
            ("SpecialSequenceActive", "起手或特殊序列正在执行"),
            ("HighPriorityQueueActive", "高优先级技能队列正在执行"),
            ("PendingGaugeReconcile", "正在等待资源状态对账"),
            ("ExactWeaveChannel", "精确插入窗口"),
            ("CasualDutyAverageTtk", "日常副本平均存活时间"),
            ("DefensiveCast", "防护技能读条识别"),
            ("DotBlacklist", "DOT 黑名单"),
            ("MultiTargetDot", "多目标 DOT"),
            ("Potion", "爆发药识别"),
            ("PhaseBefore=Ice", "确认前阶段=冰阶段"),
            ("PhaseBefore=Fire", "确认前阶段=火阶段"),
            ("PhaseBefore=Neutral", "确认前阶段=无属性阶段"),
            ("PhaseSerial=", "阶段序号="),
            ("Phase ", "阶段 "),
            ("AF/UI", "火层/冰层"),
            ("Hearts", "冰针"),
            ("Soul", "星极魂"),
            ("Paradox", "悖论"),
            ("Firestarter", "火苗"),
            ("Thunderhead", "雷云"),
            ("Polyglot", "通晓"),
            ("Resolver#", "规则顺序#"),
            ("Check=", "检查码="),
            ("Changed", "决策内容发生变化"),
            ("Ice", "冰"),
            ("Fire", "火"),
            ("Neutral", "无属性"),
            ("True", "有"),
            ("False", "无"),
        };
        foreach (var (source, target) in replacements)
            text = text.Replace(source, target, StringComparison.Ordinal);

        return text.Replace(",", "、", StringComparison.Ordinal)
            .Replace(";", "；", StringComparison.Ordinal);
    }

    private static string BuildResolverSummary(BlmDebugEvent debugEvent)
    {
        var resolver = debugEvent.Resolver;
        if (resolver is null)
            return "已生成循环决策帧，但没有附带 Resolver 详情。";

        var channel = FormatChannel(resolver.Channel);
        if (resolver.CandidateActionId == 0)
        {
            return resolver.DeliveryBlocked
                ? $"{channel}当前没有可执行技能，原因：{TranslateText(resolver.BlockReason)}。"
                : $"{channel}本帧没有候选技能。";
        }

        var action = FormatAction(resolver.CandidateActionId);
        if (resolver.DeliverableActionId != 0 && !resolver.DeliveryBlocked)
            return $"{channel}选择了“{action}”，当前可以执行。";

        var reason = TranslateText(resolver.BlockReason);
        return reason.Length == 0
            ? $"{channel}选择了“{action}”，但本帧暂未交付。"
            : $"{channel}选择了“{action}”，但被阻断：{reason}。";
    }

    private static string FormatResolver(BlmDebugResolverSnapshot resolver)
    {
        var candidate = resolver.CandidateActionId == 0
            ? "无"
            : $"{FormatAction(resolver.CandidateActionId)} ({resolver.CandidateActionId})";
        var deliverable = resolver.DeliverableActionId == 0
            ? "无"
            : $"{FormatAction(resolver.DeliverableActionId)} ({resolver.DeliverableActionId})";
        return $"{FormatChannel(resolver.Channel)}"
            + $" | 候选 {candidate}"
            + $" | 可交付 {deliverable}"
            + $" | 剩余插入 {resolver.RemainingWeaves}"
            + $" | 目标 {FormatTarget(resolver.TargetKind, resolver.TargetKey)}"
            + (resolver.HighPriorityQueueActive ? " | 高优先队列占用中" : string.Empty);
    }

    private static string FormatScene(BlmDebugResourceSnapshot resources)
        => $"{(resources.InCombat ? "战斗中" : "脱战")}"
            + $" | {(resources.IsAoeMode ? "AOE 模式" : "单体模式")}"
            + $" | {(resources.IsMoving ? "移动中" : "站定")}"
            + $" | {(resources.IsCasting ? "正在读条" : "未读条")}"
            + $" | {(resources.CanAct ? "可以行动" : "暂不可行动")}"
            + $" | 敌人 {resources.EnemyCount}"
            + (resources.IsAoeMode ? $" | AOE 命中 {resources.AoeTargetHitCount}" : string.Empty);

    private static string FormatTiming(BlmDebugResourceSnapshot resources)
        => $"GCD 剩余 {resources.GcdRemainSeconds:0.000} 秒"
            + $" / 总计 {resources.GcdTotalSeconds:0.000} 秒"
            + $" | 读条剩余 {resources.CastRemainSeconds:0.000} 秒"
            + $" / 总计 {resources.CastTotalSeconds:0.000} 秒"
            + $" | 动画锁 {resources.AnimationLockSeconds:0.000} 秒"
            + $" | 即刻 {resources.SwiftcastRemainSeconds:0.0} 秒"
            + $" | 三连 {resources.TriplecastStacks} 层"
            + $" / {resources.TriplecastRemainSeconds:0.0} 秒";

    private static string FormatTracking(BlmDebugEvent debugEvent)
    {
        var builder = new StringBuilder(160);
        builder.Append("会话 ")
            .Append(EmptyAs(debugEvent.SessionId, "-"))
            .Append(" | 单调时间 ")
            .Append(debugEvent.MonotonicMs)
            .Append(" ms")
            .Append(" | 战斗序号 ")
            .Append(debugEvent.CombatSerial)
            .Append(" | 状态代 ")
            .Append(debugEvent.StateGeneration)
            .Append(" | 火阶段序号 ")
            .Append(debugEvent.FirePhaseSerial)
            .Append(" | 冰阶段序号 ")
            .Append(debugEvent.IcePhaseSerial);
        if (debugEvent.TargetKey.Length > 0)
            builder.Append(" | 目标 ").Append(debugEvent.TargetKey);
        if (debugEvent.GlobalSequence > 0)
            builder.Append(" | 服务器序列 ").Append(debugEvent.GlobalSequence);
        return builder.ToString();
    }

    private static string DescribeRule(string ruleId)
    {
        if (ruleId.StartsWith("Opener57.", StringComparison.Ordinal))
            return $"5+7 起手步骤（{ruleId}）";
        if (ruleId.StartsWith("GCD.单体100", StringComparison.Ordinal))
            return $"100 级单体 GCD 循环（{ruleId}）";
        if (ruleId.StartsWith("GCD.单体90", StringComparison.Ordinal))
            return $"90–99 级单体 GCD 循环（{ruleId}）";
        if (ruleId.StartsWith("Ability.", StringComparison.Ordinal))
            return $"能力技规则（{ruleId}）";
        return ruleId;
    }

    private static string FormatChannel(BlmResolverChannel channel) => channel switch
    {
        BlmResolverChannel.Gcd => "GCD 技能通道",
        BlmResolverChannel.Always => "高优先能力技通道",
        BlmResolverChannel.OffGcd => "普通能力技通道",
        _ => "未知通道",
    };

    private static string FormatTarget(BlmResolverTargetKind kind, string targetKey)
    {
        var label = kind switch
        {
            BlmResolverTargetKind.CurrentTarget => "当前目标",
            BlmResolverTargetKind.Self => "自身",
            BlmResolverTargetKind.SpecifiedTarget => "指定目标",
            BlmResolverTargetKind.Potion => "爆发药",
            _ => "未知目标",
        };
        return targetKey.Length == 0 ? label : $"{label} ({targetKey})";
    }

    private static string FormatAction(uint actionId)
        => actionId == 0 ? "无动作" : BlmActionNames.Get(actionId);

    private static bool LooksLikeFactCoverage(string value)
        => value.Contains("ExactWeaveChannel", StringComparison.Ordinal)
            || value.Contains("CasualDutyAverageTtk", StringComparison.Ordinal);

    private static void AppendLine(StringBuilder builder, string label, string value)
        => builder.Append("  ").Append(label).Append('：').AppendLine(value);

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string EmptyAs(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string YesNo(bool value) => value ? "有" : "无";
}
