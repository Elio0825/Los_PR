using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Production;

namespace Los.Tests;

internal static class ResolverDebugTests
{
    public static void RunAll()
    {
        JsonlUsesResolverSchema2();
        FirstWritePrecedesRetentionCleanupWithOccupiedOldLog();
        RotatedRetentionChainCompletesBeforeDisposeReturns();
        ContinuousPublishFlushesBeforeDispose();
        FrameDiagnosticsDedupeAndResetByGeneration();
        NoTargetLifecycleIsEdgeTriggered();
    }

    private static void JsonlUsesResolverSchema2()
    {
        var directory = NewTempDirectory();
        try
        {
            using var trace = new BlmDebugTraceService(directory, () => true);
            var context = TestContext.Base() with
            {
                CapturedAtMs = 12_345,
                TargetEntityId = 424_242,
                TargetName = "不得写入日志的目标名",
                Phase = BlmPhase.Fire,
                AfStacks = 3,
                IceStacks = 0,
                IsAoeMode = true,
                EnemyCount = 2,
                AoeTargetId = 434_343,
                AoeTargetCanUseAttack = true,
                AoeTargetHitCount = 2,
                AoeTargetIsCurrentTarget = false,
                Tracker = new BlmTrackerSnapshot
                {
                    StateGeneration = 7,
                    CombatSerial = 3,
                    FirePhaseSerial = 4,
                },
            };
            trace.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.ResolverFrame,
                Context = context,
                MonotonicMs = context.CapturedAtMs,
                EntryPoint = "Resolver.Frame.Gcd",
                ActionId = BLMSkill.炽炎,
                NormalizedActionId = BLMSkill.炽炎,
                RuleId = "ST100.Fire4",
                Reason = "Changed C:\\Users\\Tester\\secret.txt",
                TargetEntityId = context.TargetEntityId,
                Resolver = new BlmDebugResolverDraft
                {
                    FrameSequence = 11,
                    FrameCapturedAtMs = context.CapturedAtMs,
                    FrameStateGeneration = context.Tracker.StateGeneration,
                    Channel = BlmResolverChannel.Gcd,
                    CandidateActionId = BLMSkill.炽炎,
                    DeliverableActionId = BLMSkill.炽炎,
                    TargetEntityId = context.TargetEntityId,
                    TargetKind = BlmResolverTargetKind.CurrentTarget,
                    ResolverId = "ST100.Fire4",
                    CheckCode = 1,
                    RemainingWeaves = 0,
                    FactCoverage = "ExactWeaveChannel",
                },
            });
            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "Resolver JSONL 应完成写入");
            trace.Dispose();

            var file = Directory.GetFiles(directory, "Los-BLM-*.jsonl").Single();
            var bytes = File.ReadAllBytes(file);
            AssertEx.True(bytes.Length > 3, "Resolver JSONL 不得为空");
            AssertEx.False(
                bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "Resolver JSONL 必须是 UTF-8 无 BOM");
            var line = File.ReadLines(file, new UTF8Encoding(false, true)).Single();
            AssertEx.False(line.Contains("不得写入日志的目标名", StringComparison.Ordinal), "不得记录目标名");
            AssertEx.False(line.Contains("424242", StringComparison.Ordinal), "不得记录原始目标 EntityId");
            AssertEx.False(line.Contains("434343", StringComparison.Ordinal), "不得记录原始AOE中心 EntityId");
            AssertEx.False(line.Contains("C:\\Users", StringComparison.Ordinal), "绝对路径必须脱敏");

            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            AssertEx.Equal(2, root.GetProperty("schemaVersion").GetInt32(), "Debug Schema 版本错误");
            AssertEx.Equal("ResolverFrame", root.GetProperty("kind").GetString()!, "事件类型错误");
            AssertEx.True(
                root.GetProperty("summary").GetString()!.Contains("GCD 技能通道选择了“炽炎”", StringComparison.Ordinal),
                "JSONL 必须附带人类可读的中文摘要");
            AssertEx.True(root.TryGetProperty("resolver", out var resolver), "Schema 2 必须包含 Resolver");
            AssertEx.Equal("Gcd", resolver.GetProperty("channel").GetString()!, "Resolver 通道错误");
            AssertEx.Equal(BLMSkill.炽炎, resolver.GetProperty("candidateActionId").GetUInt32(), "候选动作错误");
            AssertEx.Equal("T001", resolver.GetProperty("targetKey").GetString()!, "目标匿名键错误");
            var resources = root.GetProperty("resources");
            AssertEx.True(resources.GetProperty("isAoeMode").GetBoolean(), "AOE模式事实缺失");
            AssertEx.Equal(2, resources.GetProperty("enemyCount").GetInt32(), "敌人数事实缺失");
            AssertEx.Equal(
                2,
                resources.GetProperty("aoeTargetHitCount").GetInt32(),
                "AOE中心命中数事实缺失");
            AssertEx.False(
                resources.GetProperty("aoeTargetIsCurrentTarget").GetBoolean(),
                "AOE中心类型事实缺失");
            AssertEx.False(root.TryGetProperty("transition", out _), "Schema 2 不得保留 Transition");
            AssertEx.False(root.TryGetProperty("followUp", out _), "Schema 2 不得保留 Follow-up");
            AssertEx.False(root.TryGetProperty("shadow", out _), "Schema 2 不得保留 Shadow");

            var readableFile = Path.ChangeExtension(file, ".log");
            AssertEx.True(File.Exists(readableFile), "每个 JSONL 必须生成同名易读日志");
            var readableBytes = File.ReadAllBytes(readableFile);
            AssertEx.True(readableBytes.Length > 3, "易读日志不得为空");
            AssertEx.False(
                readableBytes[0] == 0xEF && readableBytes[1] == 0xBB && readableBytes[2] == 0xBF,
                "易读日志必须是 UTF-8 无 BOM");
            var readable = File.ReadAllText(readableFile, new UTF8Encoding(false, true));
            AssertEx.True(readable.StartsWith("Los 黑魔职业调试日志（易读版）", StringComparison.Ordinal), "易读日志缺少中文文件头");
            AssertEx.True(readable.Contains("【循环决策帧】", StringComparison.Ordinal), "易读日志缺少中文事件标题");
            AssertEx.True(readable.Contains("结论：GCD 技能通道选择了“炽炎”", StringComparison.Ordinal), "易读日志缺少中文结论");
            AssertEx.True(readable.Contains("资源：火阶段", StringComparison.Ordinal), "易读日志缺少中文资源分组");
            AssertEx.True(readable.Contains("事实缺口：精确插入窗口", StringComparison.Ordinal), "易读日志缺少事实缺口翻译");
            AssertEx.True(readable.Contains("底层标识：入口 Resolver.Frame.Gcd", StringComparison.Ordinal), "易读日志必须保留诊断入口");
            AssertEx.False(readable.Contains("不得写入日志的目标名", StringComparison.Ordinal), "易读日志不得记录目标名");
            AssertEx.False(readable.Contains("424242", StringComparison.Ordinal), "易读日志不得记录原始目标 EntityId");
            AssertEx.False(readable.Contains("C:\\Users", StringComparison.Ordinal), "易读日志绝对路径必须脱敏");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static void FirstWritePrecedesRetentionCleanupWithOccupiedOldLog()
    {
        var directory = NewTempDirectory();
        var occupiedPath = Path.Combine(
            directory,
            "Los-BLM-20000101-000000-p1.jsonl");
        var deletablePath = Path.Combine(
            directory,
            "Los-BLM-20000101-000001-p1.jsonl");
        File.WriteAllText(occupiedPath, "old occupied\n", new UTF8Encoding(false));
        File.WriteAllText(deletablePath, "old deletable\n", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(occupiedPath, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(deletablePath, DateTime.UtcNow.AddDays(-1));

        FileStream? occupied = null;
        BlmDebugTraceService? trace = null;
        try
        {
            occupied = new FileStream(
                occupiedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            trace = new BlmDebugTraceService(
                directory,
                () => true,
                new BlmDebugTraceOptions
                {
                    RetainedFileCount = 1,
                });
            trace.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.Lifecycle,
                Context = TestContext.Base(),
                MonotonicMs = 35_000,
                EntryPoint = "Debug.FirstWriteBeforeRetention",
            });

            var currentPath = string.Empty;
            var firstLineVisible = SpinWait.SpinUntil(
                () =>
                {
                    currentPath = Directory
                        .EnumerateFiles(directory, "Los-BLM-*.jsonl")
                        .FirstOrDefault(path =>
                            !string.Equals(path, occupiedPath, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(path, deletablePath, StringComparison.OrdinalIgnoreCase)
                            && new FileInfo(path).Length > 0)
                        ?? string.Empty;
                    return currentPath.Length > 0;
                },
                TimeSpan.FromSeconds(2));

            AssertEx.True(firstLineVisible, "旧日志被占用时新实例首行必须在2秒内可见");
            trace.Dispose();
            using (var json = JsonDocument.Parse(
                File.ReadLines(currentPath, new UTF8Encoding(false, true)).Single()))
            {
                AssertEx.Equal(
                    "Lifecycle",
                    json.RootElement.GetProperty("kind").GetString()!,
                    "首条日志事件类型错误");
            }

            var snapshot = trace.GetSnapshot();
            AssertEx.True(snapshot.WriterHealthy, $"保留清理不得损坏Writer: {snapshot.LastError}");
            AssertEx.True(File.Exists(occupiedPath), "被其他实例占用的旧日志不得删除");
            AssertEx.False(File.Exists(deletablePath), "未占用的超额旧日志应完成保留清理");
        }
        finally
        {
            trace?.Dispose();
            occupied?.Dispose();
            DeleteTempDirectory(directory);
        }
    }

    private static void RotatedRetentionChainCompletesBeforeDisposeReturns()
    {
        var directory = NewTempDirectory();
        var occupiedPath = Path.Combine(
            directory,
            "Los-BLM-19990101-000000-p1.jsonl");
        var deletablePath = Path.Combine(
            directory,
            "Los-BLM-19990101-000001-p1.jsonl");
        File.WriteAllText(occupiedPath, "old occupied\n", new UTF8Encoding(false));
        File.WriteAllText(deletablePath, "old deletable\n", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(occupiedPath, DateTime.UtcNow.AddDays(-4));
        File.SetLastWriteTimeUtc(deletablePath, DateTime.UtcNow.AddDays(-3));

        FileStream? occupied = null;
        BlmDebugTraceService? trace = null;
        try
        {
            occupied = new FileStream(
                occupiedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            trace = new BlmDebugTraceService(
                directory,
                () => true,
                new BlmDebugTraceOptions
                {
                    MaximumFileBytes = 1,
                    RetainedFileCount = 3,
                });
            var context = TestContext.Base();
            trace.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.Lifecycle,
                Context = context,
                MonotonicMs = 36_000,
                EntryPoint = "Debug.Rotation.0",
            });

            var firstLineVisible = SpinWait.SpinUntil(
                () => Directory
                    .EnumerateFiles(directory, "Los-BLM-*.jsonl")
                    .Any(path =>
                        !string.Equals(path, occupiedPath, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(path, deletablePath, StringComparison.OrdinalIgnoreCase)
                        && new FileInfo(path).Length > 0),
                TimeSpan.FromSeconds(2));
            AssertEx.True(firstLineVisible, "轮转测试首条日志必须在Dispose前实时可见");

            for (var index = 1; index <= 3; index++)
            {
                trace.Publish(new BlmDebugEventDraft
                {
                    Kind = BlmDebugEventKind.Lifecycle,
                    Context = context,
                    MonotonicMs = 36_000 + index,
                    EntryPoint = $"Debug.Rotation.{index}",
                });
            }

            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "轮转事件必须完成消费者写入");
            trace.Dispose();

            var snapshot = trace.GetSnapshot();
            AssertEx.True(snapshot.WriterHealthy, $"轮转清理链失败: {snapshot.LastError}");
            AssertEx.Equal(4L, snapshot.WrittenCount, "轮转测试必须写入四条事件");
            AssertEx.True(File.Exists(snapshot.CurrentFilePath), "当前活动日志段不得被清理");
            AssertEx.True(File.Exists(occupiedPath), "被旧实例占用的日志不得删除");
            AssertEx.False(File.Exists(deletablePath), "未占用的超额旧日志必须删除");

            var retainedSegments = Directory
                .GetFiles(directory, "Los-BLM-*.jsonl")
                .Where(path =>
                    !string.Equals(path, occupiedPath, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            AssertEx.Equal(3, retainedSegments.Length, "轮转后必须保留最新三个当前实例日志段");
            AssertEx.True(
                retainedSegments.Contains(
                    snapshot.CurrentFilePath,
                    StringComparer.OrdinalIgnoreCase),
                "最新活动日志段必须位于保留集合");
            foreach (var path in retainedSegments)
            {
                var line = File.ReadLines(path, new UTF8Encoding(false, true)).Single();
                using var json = JsonDocument.Parse(line);
                AssertEx.Equal(
                    "Lifecycle",
                    json.RootElement.GetProperty("kind").GetString()!,
                    "轮转日志段必须保持完整JSONL");
                AssertEx.True(
                    File.Exists(Path.ChangeExtension(path, ".log")),
                    "每个保留的 JSONL 段都必须保留配对的易读日志");
            }
        }
        finally
        {
            trace?.Dispose();
            occupied?.Dispose();
            DeleteTempDirectory(directory);
        }
    }

    private static void ContinuousPublishFlushesBeforeDispose()
    {
        const int producerCount = 3;
        var directory = NewTempDirectory();
        var trace = new BlmDebugTraceService(
            directory,
            () => true,
            new BlmDebugTraceOptions
            {
                QueueCapacity = 64,
                RecentEventCapacity = 64,
            });
        using var stop = new CancellationTokenSource();
        using var started = new CountdownEvent(producerCount);
        var context = TestContext.Base() with
        {
            CapturedAtMs = 40_000,
            TargetEntityId = 900_001,
        };
        var repeatedDraft = new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.ResolverFrame,
            Context = context,
            MonotonicMs = context.CapturedAtMs,
            EntryPoint = "Resolver.Frame.Continuous",
            TargetEntityId = context.TargetEntityId,
            Resolver = new BlmDebugResolverDraft
            {
                FrameSequence = 1,
                FrameCapturedAtMs = context.CapturedAtMs,
                Channel = BlmResolverChannel.Gcd,
                CandidateActionId = BLMSkill.炽炎,
                DeliverableActionId = BLMSkill.炽炎,
                TargetEntityId = context.TargetEntityId,
                TargetKind = BlmResolverTargetKind.CurrentTarget,
                ResolverId = "Debug.Continuous",
                CheckCode = 1,
            },
        };
        var producers = Enumerable.Range(0, producerCount)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    started.Signal();
                    while (!stop.IsCancellationRequested)
                    {
                        trace.Publish(repeatedDraft);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        try
        {
            var allStarted = started.Wait(TimeSpan.FromSeconds(1));
            var becameNonEmptyWhilePublishing = allStarted
                && SpinWait.SpinUntil(
                    () => Directory
                        .EnumerateFiles(directory, "Los-BLM-*.jsonl")
                        .Any(path => new FileInfo(path).Length > 0),
                    TimeSpan.FromSeconds(2));
            var producerWasStillRunning = producers.Any(task => !task.IsCompleted);

            stop.Cancel();
            var producersStopped = Task.WaitAll(producers, TimeSpan.FromSeconds(5));

            AssertEx.True(allStarted, "持续发布生产者必须在时限内启动");
            AssertEx.True(producerWasStillRunning, "检查落盘时生产者必须仍在持续Publish");
            AssertEx.True(
                becameNonEmptyWhilePublishing,
                "持续Publish期间Debug JSONL必须在2秒内出现且非空，不能等待Dispose");
            AssertEx.True(producersStopped, "持续发布生产者必须可正常停止");
            AssertEx.True(trace.Flush(TimeSpan.FromSeconds(5)), "持续发布停止后队列必须排空");

            trace.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.Lifecycle,
                Context = context,
                MonotonicMs = context.CapturedAtMs + 1,
                EntryPoint = "Debug.DisposeDrain",
                Detail = "Dispose必须排空最后一条事件",
            });
            trace.Dispose();

            var snapshot = trace.GetSnapshot();
            AssertEx.True(snapshot.WriterHealthy, $"消费者错误状态不可见或未恢复: {snapshot.LastError}");
            AssertEx.Equal(0L, snapshot.DroppedCount, "持续发布回归不得产生队列丢弃");
            AssertEx.Equal(snapshot.AcceptedCount, snapshot.WrittenCount, "Dispose必须写完全部已接纳事件");

            var lines = Directory.GetFiles(directory, "Los-BLM-*.jsonl")
                .SelectMany(path => File.ReadLines(path, new UTF8Encoding(false, true)))
                .ToArray();
            AssertEx.Equal((int)snapshot.WrittenCount, lines.Length, "JSONL行数必须等于已写事件数");
            foreach (var line in lines)
            {
                using var json = JsonDocument.Parse(line);
                var kind = json.RootElement.GetProperty("kind").GetString();
                AssertEx.False(kind == nameof(BlmDebugEventKind.LoggerError), "消费者不得产生LoggerError");
                AssertEx.False(kind == nameof(BlmDebugEventKind.LoggerDropped), "消费者不得丢弃事件");
            }
        }
        finally
        {
            stop.Cancel();
            try
            {
                Task.WaitAll(producers, TimeSpan.FromSeconds(5));
            }
            finally
            {
                trace.Dispose();
                DeleteTempDirectory(directory);
            }
        }
    }

    private static void FrameDiagnosticsDedupeAndResetByGeneration()
    {
        var clock = new FakeClock(20_000);
        var initial = TestContext.Base() with
        {
            CapturedAtMs = clock.NowMs,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            IceStacks = 0,
            AstralSoul = 0,
            UmbralHearts = 0,
            HasParadox = false,
            PolyglotStacks = 0,
            GcdRemainSeconds = 0.2f,
        };
        var tracker = new BlmStateTracker(initial, clock, new MappingActionIdNormalizer());
        var sink = new CapturingDebugSink();
        var execution = new BlmResolverExecutionService(tracker, sink);
        var first = tracker.GetContextSnapshot();
        AssertEx.True(execution.BeginFrame(first, Input(first)), "首个 Resolver 帧应建立");
        AssertEx.Equal(3, sink.Events.Count, "首帧应按三个通道记录诊断");
        AssertEx.True(
            sink.Events.All(item => item.Kind == BlmDebugEventKind.ResolverFrame),
            "首帧诊断必须全部为 ResolverFrame");

        clock.Advance(1);
        var unchanged = first with { CapturedAtMs = clock.NowMs };
        AssertEx.True(execution.BeginFrame(unchanged, Input(unchanged)), "同代相同决策帧应建立");
        AssertEx.Equal(3, sink.Events.Count, "心跳前相同决策不得重复刷屏");

        clock.Advance(1);
        tracker.Reconcile(unchanged with
        {
            CapturedAtMs = clock.NowMs,
            TargetEntityId = 201,
        });
        var retargeted = tracker.GetContextSnapshot();
        AssertEx.True(execution.BeginFrame(retargeted, Input(retargeted)), "换目标帧应建立");
        AssertEx.Equal(6, sink.Events.Count, "同代换目标必须重新记录三个通道");

        execution.InvalidateFrame();
        clock.Advance(1);
        var recovered = retargeted with { CapturedAtMs = clock.NowMs };
        AssertEx.True(execution.BeginFrame(recovered, Input(recovered)), "显式失效后的恢复帧应建立");
        AssertEx.Equal(9, sink.Events.Count, "显式失效后的首帧必须重新记录三个通道");

        tracker.OnTerritoryChanged(777);
        clock.Advance(1);
        var reset = tracker.GetContextSnapshot() with { CapturedAtMs = clock.NowMs };
        AssertEx.True(execution.BeginFrame(reset, Input(reset)), "新 generation 帧应建立");
        AssertEx.Equal(12, sink.Events.Count, "generation 改变必须重新记录三个通道");
        AssertEx.True(
            sink.Events.Skip(9).All(item =>
                item.Resolver?.FrameStateGeneration == reset.Tracker.StateGeneration),
            "generation 后的 Resolver 诊断不得复用旧代");
    }

    private static void NoTargetLifecycleIsEdgeTriggered()
    {
        var clock = new FakeClock(30_000);
        var initial = TestContext.Base() with
        {
            CapturedAtMs = clock.NowMs,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            GcdRemainSeconds = 0.2f,
        };
        var tracker = new BlmStateTracker(initial, clock, new MappingActionIdNormalizer());
        var sink = new CapturingDebugSink();
        var execution = new BlmResolverExecutionService(tracker, sink);
        using var handler = new BlackMageEventHandler(
            tracker,
            new BlmResolverInputAdapter(),
            execution,
            new TestLogSystemEventSource(),
            clock,
            sink);
        var context = tracker.GetContextSnapshot();
        var beginProductionFrame = typeof(BlackMageEventHandler).GetMethod(
            "BeginProductionFrame",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到生产帧接纳方法");

        BeginValidFrame();
        ArmPending();
        execution.InvalidateFrame();
        handler.OnNoTarget();
        AssertNoFrameOrPending("首次无目标");
        AssertEx.Equal(1, NoTargetEvents(), "首次无目标应记录一次 Lifecycle");

        AssertEx.True(
            execution.BeginFrame(context, Input(context)),
            "重复无目标测试应能重建生产帧");
        ArmPending();
        handler.OnNoTarget();
        AssertNoFrameOrPending("连续无目标");
        AssertEx.Equal(1, NoTargetEvents(), "连续无目标不得重复记录 Lifecycle");

        BeginValidFrame();
        ArmPending();
        execution.InvalidateFrame();
        handler.OnNoTarget();
        AssertNoFrameOrPending("目标恢复后再次失效");
        AssertEx.Equal(2, NoTargetEvents(), "目标恢复后再次失效应重新记录一次 Lifecycle");
        return;

        void BeginValidFrame()
        {
            var started = beginProductionFrame.Invoke(
                handler,
                new object?[] { context, Input(context) });
            AssertEx.Equal(true, (bool)started!, "有效目标的生产帧应建立");
        }

        void ArmPending()
        {
            AssertEx.True(
                tracker.TryRegisterIssuedAction(new BlmIssuedActionMetadata(
                    context.Tracker.StateGeneration,
                    BLMSkill.炽炎,
                    BLMSkill.炽炎,
                    clock.NowMs,
                    context.Tracker.LastAckGlobalSequence,
                    clock.NowMs + 5000,
                    false,
                    true)),
                "无目标测试应能建立通用 Pending");
            AssertEx.True(
                tracker.GetTrackerSnapshot().HasPendingIssuedAction,
                "无目标前必须存在 Pending");
        }

        void AssertNoFrameOrPending(string scenario)
        {
            AssertEx.True(execution.GetSnapshot() is null, $"{scenario}必须失效 Resolver 帧");
            AssertEx.False(
                tracker.GetTrackerSnapshot().HasPendingIssuedAction,
                $"{scenario}必须清理通用 Pending");
        }

        int NoTargetEvents()
            => sink.Events.Count(item =>
                item.Kind == BlmDebugEventKind.Lifecycle
                && item.EntryPoint == "NoTarget");
    }

    private static BlmResolverInput Input(BlmContext context) => new()
    {
        StateGeneration = context.Tracker.StateGeneration,
        Context = new BlmResolverContextFacts
        {
            CapturedAtMs = context.CapturedAtMs,
            IsAvailable = context.IsAvailable,
            AcrEnabled = context.AcrState == PromeRotation.Data.AcrState.On,
            PlayerEntityId = context.PlayerEntityId,
            Level = context.Level,
            Mp = context.Mp,
            MaxMp = context.MaxMp,
            InCombat = context.InCombat,
            IsAlive = context.IsAlive,
            CanAct = context.CanAct,
            IsSingleTargetMode = !context.IsAoeMode,
            EnemyCount = context.EnemyCount,
            AoeTargetId = context.AoeTargetId,
            AoeTargetCanUseAttack = context.AoeTargetCanUseAttack,
            AoeTargetHitCount = context.AoeTargetHitCount,
            AoeTargetIsCurrentTarget = context.AoeTargetIsCurrentTarget,
            HasTarget = context.HasTarget,
            CanUseAttackActionOnTarget = context.HasValidTarget,
            CurrentTargetId = context.TargetEntityId,
            GcdTotalSeconds = context.GcdTotalSeconds,
            GcdRemainSeconds = context.GcdRemainSeconds,
            Phase = context.Phase,
            AstralFireStacks = context.AfStacks,
            UmbralIceStacks = context.IceStacks,
            UmbralHearts = context.UmbralHearts,
            AstralSoulStacks = context.AstralSoul,
            HasParadox = context.HasParadox,
            HasFirestarter = context.HasFirestarter,
            HasThunderhead = context.HasThunderhead,
            PolyglotStacks = context.PolyglotStacks,
            PolyglotTimerMs = context.PolyglotTimerMs,
        },
        Settings = BlmResolverSettings.Default with
        {
            DotEnabled = false,
            ManafontEnabled = false,
            LeyLinesEnabled = false,
        },
        Actions = ImmutableArray.Create(new BlmResolverActionFact
        {
            RequestedActionId = BLMSkill.炽炎,
            AdjustedActionId = BLMSkill.炽炎,
            IsUnlocked = true,
            CanCast = true,
            Charges = 1f,
        }),
        Level100Loop = new BlmLevel100LoopFacts
        {
            Fire4Count = context.AstralSoul,
        },
        DotTargets = ImmutableArray.Create(new BlmResolverDotTargetFact
        {
            EntityId = context.TargetEntityId,
            IsValid = context.HasValidTarget,
            IsTargetable = context.HasValidTarget,
            IsAlive = true,
            CanUseAttackActionOn = context.HasValidTarget,
            IsInDotRange = true,
            CurrentHp = context.TargetHp,
            MaxHp = context.TargetMaxHp,
        }),
        FactCoverage = BlmResolverFactCoverage.Phase3A,
    };

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"Los-ResolverDebug-{Guid.NewGuid():N}");
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
        public List<BlmDebugEventDraft> Events { get; } = [];

        public void Publish(BlmDebugEventDraft draft) => Events.Add(draft);
    }
}
