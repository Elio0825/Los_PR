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
            AssertEx.False(line.Contains("C:\\Users", StringComparison.Ordinal), "绝对路径必须脱敏");

            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            AssertEx.Equal(2, root.GetProperty("schemaVersion").GetInt32(), "Debug Schema 版本错误");
            AssertEx.Equal("ResolverFrame", root.GetProperty("kind").GetString()!, "事件类型错误");
            AssertEx.True(root.TryGetProperty("resolver", out var resolver), "Schema 2 必须包含 Resolver");
            AssertEx.Equal("Gcd", resolver.GetProperty("channel").GetString()!, "Resolver 通道错误");
            AssertEx.Equal(BLMSkill.炽炎, resolver.GetProperty("candidateActionId").GetUInt32(), "候选动作错误");
            AssertEx.Equal("T001", resolver.GetProperty("targetKey").GetString()!, "目标匿名键错误");
            AssertEx.False(root.TryGetProperty("transition", out _), "Schema 2 不得保留 Transition");
            AssertEx.False(root.TryGetProperty("followUp", out _), "Schema 2 不得保留 Follow-up");
            AssertEx.False(root.TryGetProperty("shadow", out _), "Schema 2 不得保留 Shadow");
        }
        finally
        {
            DeleteTempDirectory(directory);
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
            IsSingleTargetMode = true,
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
