using System.Text;
using System.Text.Json;
using LosPr.BLM.Core;
using LosPr.BLM.Data;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.Engine;
using LosPr.BLM.UI;
using PromeRotation.Data;

namespace Los.Tests;

public static class DebugTraceTests
{
    public static void RunAll()
    {
        Utf8JsonlAndPrivacy();
        FileSwitchDoesNotAffectMemory();
        DecisionDedupeAndMemoryCapacity();
        DisposeDrainsAcceptedEvents();
        IoFailureStaysInsideLogger();
        RollingAndRetentionAreScoped();
        SettingsMigrationAndUiProjection();
        DispatcherSeparatesCandidateFromAck();
    }

    private static void Utf8JsonlAndPrivacy()
    {
        var directory = NewTempDirectory();
        try
        {
            using var trace = new BlmDebugTraceService(directory, () => true);
            var context = CreateContext("绝不能写进日志的木桩名字", 424242);
            trace.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.Decision,
                Context = context,
                MonotonicMs = 123456,
                EntryPoint = "Gcd",
                ActionId = BLMSkill.炽炎,
                RuleId = BlmRuleId.Fire4,
                Reason = "中文、引号 \"、路径 C:\\Users\\Tester\\secret.txt 与换行\n都必须形成合法 JSONL。",
                TargetEntityId = context.TargetEntityId,
            });
            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "Debug 日志应在超时前写完");

            var snapshot = trace.GetSnapshot();
            AssertEx.Equal(1, snapshot.RecentEvents.Count, "内存面板应收到同一事件");
            AssertEx.Equal("T001", snapshot.RecentEvents[0].TargetKey, "目标应使用会话内匿名键");
            AssertEx.Equal(8123L, snapshot.RecentEvents[0].Resources.Mp, "应冻结决策时 MP");
            AssertEx.Equal(
                TransitionStage.Queued,
                snapshot.RecentEvents[0].Transition.Stage,
                "应冻结 Transition 状态");

            trace.Dispose();
            var files = Directory.GetFiles(directory, "Los-BLM-*.jsonl");
            AssertEx.Equal(1, files.Length, "启用文件日志后应生成一个 JSONL");
            var bytes = File.ReadAllBytes(files[0]);
            AssertEx.True(bytes.Length > 3, "JSONL 不应为空");
            AssertEx.False(
                bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "JSONL 必须是 UTF-8 无 BOM");

            var text = File.ReadAllText(files[0], new UTF8Encoding(false, true));
            AssertEx.True(text.Contains("中文、引号", StringComparison.Ordinal), "中文应按 UTF-8 保留");
            AssertEx.False(
                text.Contains("绝不能写进日志的木桩名字", StringComparison.Ordinal),
                "不得记录目标名称");
            AssertEx.False(text.Contains("424242", StringComparison.Ordinal), "不得记录原始目标 EntityId");
            AssertEx.False(text.Contains("C:\\Users", StringComparison.Ordinal), "不得记录绝对用户路径");
            AssertEx.True(text.Contains("<path>", StringComparison.Ordinal), "绝对路径应被明确脱敏");
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            AssertEx.Equal(1, lines.Length, "单事件应只占一行 JSONL");
            using var json = JsonDocument.Parse(lines[0]);
            AssertEx.Equal(
                "Decision",
                json.RootElement.GetProperty("kind").GetString()!,
                "事件类型应序列化为稳定字符串");
            AssertEx.Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32(), "Schema 版本错误");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static void FileSwitchDoesNotAffectMemory()
    {
        var directory = NewTempDirectory();
        try
        {
            var enabled = false;
            using var trace = new BlmDebugTraceService(directory, () => enabled);
            trace.Publish(Draft(11, 1000));
            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "关闭日志时 Flush 不应阻塞");
            AssertEx.Equal(0, Directory.GetFiles(directory, "*.jsonl").Length, "关闭时不得创建文件");

            enabled = true;
            trace.Publish(Draft(22, 2000));
            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "开启日志后应完成写入");

            enabled = false;
            trace.Publish(Draft(33, 3000));
            AssertEx.Equal(3, trace.GetSnapshot().RecentEvents.Count, "文件开关不得影响内存面板");
            trace.Dispose();

            var file = Directory.GetFiles(directory, "Los-BLM-*.jsonl").Single();
            var text = File.ReadAllText(file, Encoding.UTF8);
            AssertEx.True(text.Contains("\"actionId\":22", StringComparison.Ordinal), "开启期间事件应写盘");
            AssertEx.False(text.Contains("\"actionId\":11", StringComparison.Ordinal), "开启前事件不得补写");
            AssertEx.False(text.Contains("\"actionId\":33", StringComparison.Ordinal), "关闭后事件不得写盘");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static void DecisionDedupeAndMemoryCapacity()
    {
        var directory = NewTempDirectory();
        try
        {
            using var trace = new BlmDebugTraceService(
                directory,
                () => false,
                new BlmDebugTraceOptions { RecentEventCapacity = 3 });

            var decision = Draft(10, 1000) with
            {
                Kind = BlmDebugEventKind.Decision,
                RuleId = "NA.TEST",
                Reason = "相同无动作决策",
            };
            trace.Publish(decision);
            trace.Publish(decision with { MonotonicMs = 1100 });
            trace.Publish(decision with { MonotonicMs = 1150, EntryPoint = "Always" });
            trace.Publish(decision with { MonotonicMs = 1300 });
            trace.Publish(Draft(20, 1400));
            trace.Publish(Draft(30, 1500));
            trace.Publish(Draft(40, 1600));

            var snapshot = trace.GetSnapshot();
            AssertEx.Equal(3, snapshot.RecentEvents.Count, "内存视图必须保持固定容量");
            AssertEx.Equal(20u, snapshot.RecentEvents[0].ActionId, "应淘汰最旧事件");
            AssertEx.Equal(40u, snapshot.RecentEvents[^1].ActionId, "应保留最新事件");
            AssertEx.Equal(
                5L,
                snapshot.AcceptedCount,
                "2 秒内同入口 Decision 应合并，不同入口应独立保留");

            trace.ClearView();
            AssertEx.Equal(0, trace.GetSnapshot().RecentEvents.Count, "清空面板只应清除内存事件");
            AssertEx.Equal(5L, trace.GetSnapshot().AcceptedCount, "清空面板不得重置累计指标");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static void DisposeDrainsAcceptedEvents()
    {
        var directory = NewTempDirectory();
        try
        {
            var trace = new BlmDebugTraceService(
                directory,
                () => true,
                new BlmDebugTraceOptions { QueueCapacity = 128 });
            for (uint actionId = 1; actionId <= 40; actionId++)
            {
                trace.Publish(Draft(actionId, actionId));
            }

            trace.Dispose();
            trace.Dispose();
            trace.Publish(Draft(99, 99));

            var file = Directory.GetFiles(directory, "Los-BLM-*.jsonl").Single();
            AssertEx.Equal(
                40,
                File.ReadLines(file).Count(line => !string.IsNullOrWhiteSpace(line)),
                "Dispose 必须排空已接受的文件事件");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static void IoFailureStaysInsideLogger()
    {
        var root = NewTempDirectory();
        try
        {
            var invalidDirectory = Path.Combine(root, "occupied");
            File.WriteAllText(invalidDirectory, "这是文件，不是目录。", Encoding.UTF8);
            using var trace = new BlmDebugTraceService(invalidDirectory, () => true);
            trace.Publish(Draft(1, 1));
            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "I/O 失败后队列仍应收敛");

            var snapshot = trace.GetSnapshot();
            AssertEx.False(snapshot.WriterHealthy, "I/O 失败应反映到健康状态");
            AssertEx.True(!string.IsNullOrWhiteSpace(snapshot.LastError), "I/O 失败应保留简化错误");
            AssertEx.True(
                snapshot.RecentEvents.Any(item => item.Kind == BlmDebugEventKind.LoggerError),
                "内存面板应保留 LoggerError");
            AssertEx.False(trace.TryOpenLogDirectory(), "无效目录应返回 false 而不是向 UI 抛异常");
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static void RollingAndRetentionAreScoped()
    {
        var directory = NewTempDirectory();
        try
        {
            for (var index = 0; index < 6; index++)
            {
                var old = Path.Combine(directory, $"Los-BLM-old-{index:D2}.jsonl");
                File.WriteAllText(old, "{}\n", new UTF8Encoding(false));
                File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddMinutes(-20 - index));
            }

            var unrelated = Path.Combine(directory, "keep-me.jsonl");
            File.WriteAllText(unrelated, "不可删除", Encoding.UTF8);
            using var trace = new BlmDebugTraceService(
                directory,
                () => true,
                new BlmDebugTraceOptions
                {
                    QueueCapacity = 256,
                    MaximumFileBytes = 2048,
                    RetainedFileCount = 5,
                });
            for (var index = 0; index < 30; index++)
            {
                trace.Publish(Draft((uint)(index + 1), index + 1) with
                {
                    Reason = new string('测', 250),
                });
            }

            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(10)), "滚动日志应完成写入");
            trace.Dispose();
            var files = Directory.GetFiles(directory, "Los-BLM-*.jsonl");
            AssertEx.True(files.Length is >= 2 and <= 5, "滚动文件应在保留上限内");
            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file).Where(line => !string.IsNullOrWhiteSpace(line)))
                {
                    using var _ = JsonDocument.Parse(line);
                }
            }

            AssertEx.True(File.Exists(unrelated), "保留清理不得删除不匹配前缀的文件");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static void SettingsMigrationAndUiProjection()
    {
        var settings = new BlackMageSettings();
        settings.Normalize();
        AssertEx.Equal(1, settings.DebugTraceSetupVersion, "Debug 配置迁移版本错误");
        AssertEx.True(settings.ShowAdvancedDebug, "旧配置首次迁移应显示 Debug 面板");
        AssertEx.True(settings.DecisionLogging, "旧配置首次迁移应开启木桩文件日志");

        settings.ShowAdvancedDebug = false;
        settings.DecisionLogging = false;
        settings.Normalize();
        AssertEx.False(settings.ShowAdvancedDebug, "迁移后不得覆盖用户关闭面板的选择");
        AssertEx.False(settings.DecisionLogging, "迁移后不得覆盖用户关闭日志的选择");

        var context = CreateContext("仅 UI 使用的目标名", 200);
        var snapshot = BlmUiSnapshot.FromContext(context);
        AssertEx.Equal(context.Tracker.Transition, snapshot.Transition, "UI 必须投影同一 Transition");
        AssertEx.Equal(context.Tracker.FollowUp, snapshot.FollowUp, "UI 必须投影同一 Follow-up");
    }

    private static void DispatcherSeparatesCandidateFromAck()
    {
        var clock = new FakeClock(123456);
        var coordinator = new BlmCoordinator(clock);
        var followUp = new BlmFollowUpCoordinator(clock);
        var sink = new CapturingDebugSink();
        var dispatcher = new BlmActionDispatcher(
            coordinator,
            followUp,
            debugSink: sink);
        var source = CreateContext("不会进入 Debug 的目标名", 200);
        var context = source with
        {
            IsMoving = false,
            Tracker = source.Tracker with
            {
                HistoryReliable = true,
                IsCombatActive = true,
                Transition = BlmIntent.Empty,
                FollowUp = BlmFollowUpIntent.Empty,
            },
        };

        var action = dispatcher.ResolveGcd(context);
        AssertEx.True(action is not null, "测试 Context 应返回候选 GCD");
        AssertEx.True(
            sink.Events.Any(item => item.Kind == BlmDebugEventKind.Decision),
            "Dispatcher 应记录 Decision");
        AssertEx.True(
            sink.Events.Any(item => item.Kind == BlmDebugEventKind.DispatchReturned),
            "返回 PAction 时应记录 DispatchReturned");
        AssertEx.False(
            sink.Events.Any(item => item.Kind is BlmDebugEventKind.AckAccepted
                or BlmDebugEventKind.ActionEffectObserved),
            "Dispatcher 不得把候选 PAction 伪装成服务器 Ack");
    }

    private static BlmDebugEventDraft Draft(uint actionId, long atMs) => new()
    {
        Kind = BlmDebugEventKind.DispatchReturned,
        MonotonicMs = atMs,
        EntryPoint = "Test",
        ActionId = actionId,
        PActionType = "Gcd",
    };

    private static BlmContext CreateContext(string targetName, uint targetEntityId)
    {
        var transition = new BlmIntent
        {
            StateGeneration = 9,
            Kind = TransitionKind.IceToFire,
            Step = TransitionStep.UseTranspose,
            Stage = TransitionStage.Queued,
            DeliveryChannel = TransitionDeliveryChannel.OffGcd,
            Serial = 17,
            StepIndex = 1,
            ExpectedActionId = BLMSkill.星灵移位,
            Reason = "等待星灵 Ack",
        };
        var followUp = new BlmFollowUpIntent
        {
            Kind = BlmFollowUpKind.FlareStarAfterMovementDespair,
            Stage = BlmFollowUpStage.Active,
            StateGeneration = 9,
            CombatSerial = 3,
            FirePhaseSerial = 4,
            Serial = 21,
            TriggerActionId = BLMSkill.绝望,
            RequiredActionId = BLMSkill.耀星,
            Reason = "移动绝望后耀星",
        };
        return new BlmContext
        {
            CapturedAtMs = 123456,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            IsAvailable = true,
            AcrState = AcrState.On,
            PlayerEntityId = 100,
            JobId = 25,
            Level = 100,
            Mp = 8123,
            MaxMp = 10000,
            IsMoving = true,
            InCombat = true,
            IsAlive = true,
            CanAct = true,
            HasTarget = true,
            HasValidTarget = true,
            InRange = true,
            TargetEntityId = targetEntityId,
            TargetName = targetName,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            UmbralHearts = 2,
            AstralSoul = 6,
            HasParadox = true,
            HasFirestarter = true,
            HasThunderhead = true,
            PolyglotStacks = 2,
            MaxPolyglot = 3,
            PolyglotTimerMs = 12000,
            HasSwiftcast = true,
            SwiftcastRemainSeconds = 8f,
            TriplecastStacks = 2,
            TriplecastRemainSeconds = 10f,
            Tracker = new BlmTrackerSnapshot
            {
                CombatSerial = 3,
                StateGeneration = 9,
                IsCombatActive = true,
                HistoryReliable = true,
                FirePhaseSerial = 4,
                Transition = transition,
                FollowUp = followUp,
            },
        };
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Los-DebugTrace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed class CapturingDebugSink : IBlmDebugSink
    {
        public List<BlmDebugEventDraft> Events { get; } = new();

        public void Publish(BlmDebugEventDraft draft)
            => Events.Add(draft);
    }
}
