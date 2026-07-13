using System.Collections.Immutable;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;
using PromeRotation.Data;

namespace Los.Tests;

internal static class Phase3FactAdapterTests
{
    public static void RunAll()
    {
        TrackerDecisionSnapshotIsAtomicAndGenerationBound();
        PureBuildMapsFrozenFactsAndExistingSettings();
        ManafontProjectsPostRestoreFire4Count();
        UsedWeavesAreDerivedAfterPreviousGcd();
        RuntimeMemoryUsesPreviousGcdAndResets();
        AvailableInstantGcdControlsForcedRecovery();
        AcrOffDoesNotAccumulateIdle();
        EntitySnapshotValidationFailsClosed();
        GenerationMismatchFailsClosed();
        RequiredActionTableMatchesLevel100Closure();
    }

    private static void TrackerDecisionSnapshotIsAtomicAndGenerationBound()
    {
        var clock = new FakeClock(10_000);
        var context = BaseContext(0) with
        {
            CapturedAtMs = clock.NowMs,
            Tracker = BlmTrackerSnapshot.Empty,
        };
        var tracker = new BlmStateTracker(
            context,
            clock,
            new MappingActionIdNormalizer());
        var generation = tracker.StateGeneration;

        var gcd = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.炽炎,
            1,
            BlmPhase.Fire,
            clock.NowMs,
            clock.NowMs - 100,
            2400f,
            false);
        AssertEx.True(tracker.ApplyActionEffect(gcd), "GCD Ack 应进入历史");
        clock.Advance(25);
        var ability = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            MageUniversalSkill.醒梦,
            2,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(ability), "能力 Ack 应进入历史");

        var decision = tracker.CaptureDecisionSnapshot();
        AssertEx.Equal(generation, decision.Snapshot.StateGeneration, "快照 generation 错误");
        AssertEx.Equal(2, decision.RecentHistory.Length, "原子历史数量错误");
        AssertEx.True(
            decision.RecentHistory.All(item => item.StateGeneration == generation),
            "原子历史不得混入其他 generation");
        AssertEx.Equal(BLMSkill.炽炎, decision.PreviousGcd!.Value.ActualAckId, "PreviousGcd 错误");
        AssertEx.False(decision.RecentHistory[1].IsGcd, "能力不得标记为 GCD");

        tracker.EndCombat();
        var reset = tracker.CaptureDecisionSnapshot();
        AssertEx.True(reset.Snapshot.StateGeneration > generation, "离战应推进 generation");
        AssertEx.Equal(0, reset.RecentHistory.Length, "新 generation 不得保留旧历史");
        AssertEx.True(reset.PreviousGcd is null, "新 generation 不得保留 PreviousGcd");
    }

    private static void PureBuildMapsFrozenFactsAndExistingSettings()
    {
        const long generation = 7;
        var context = BaseContext(generation) with
        {
            IsCasting = true,
            CurrentCastingActionId = BLMSkill.冰封,
            EnemyCount = 2,
            IsAoeMode = true,
            HasLeyLines = true,
            HasLeyLinesStatus737 = true,
            HasLeyLinesHaste = false,
            DotEnabled = false,
            TtkDumpEnabled = true,
            MoveXenoEnabled = false,
            SwiftcastEnabled = false,
            TriplecastEnabled = false,
            MoveTriplecastEnabled = false,
            AmplifierEnabled = false,
            LeyLinesEnabled = false,
            CompressFireParadox = false,
        };
        var decision = Decision(
            generation,
            fire4Count: 4,
            fireParadoxUsed: true);
        var actions = ImmutableArray.Create(ReadyAction(BLMSkill.闪雷, BLMSkill.高闪雷));
        var input = BlmResolverInputAdapter.Build(
            context,
            decision,
            new BlmResolverRuntimeState { StateGeneration = generation },
            actions,
            highPriority: true);

        AssertEx.Equal(BLMSkill.冰封, input.CurrentCastingActionId, "当前读条动作未投影");
        AssertEx.Equal(generation, input.StateGeneration, "输入必须显式携带 Tracker generation");
        AssertEx.False(input.Context.IsSingleTargetMode, "最终AOE模式未映射");
        AssertEx.Equal(2, input.Context.EnemyCount, "敌人数事实未映射");
        AssertEx.True(input.Context.IsTwoTargetAoe, "双目标AOE派生事实错误");
        AssertEx.True(input.Context.HasLeyLinesStatus737, "737 应独立投影");
        AssertEx.False(input.Context.HasLeyLinesHaste738, "738 不得由合并 HasLeyLines 推断");
        AssertEx.Equal(3, input.Context.MaxPolyglotStacks, "通晓等级上限事实未投影");
        AssertEx.Equal(4, input.Level100Loop.Fire4Count, "火四计数必须来自 Tracker");
        AssertEx.True(input.Level100Loop.FireParadoxUsed, "火悖论事实必须来自 Tracker");
        AssertEx.True(input.HighPriorityQueueActive, "高优队列事实未投影");
        AssertEx.False(input.Settings.DotEnabled, "Dot QT 映射错误");
        AssertEx.True(input.Settings.TtkEnabled, "TTK QT 映射错误");
        AssertEx.False(input.Settings.MoveXenoglossyEnabled, "移动通晓 QT 映射错误");
        AssertEx.False(input.Settings.SwiftcastIntoIceEnabled, "即刻 QT 映射错误");
        AssertEx.False(input.Settings.TriplecastIntoIceEnabled, "三连 QT 映射错误");
        AssertEx.False(input.Settings.MoveTriplecastEnabled, "移动三连 QT 映射错误");
        AssertEx.False(input.Settings.AmplifierEnabled, "详述 QT 映射错误");
        AssertEx.False(input.Settings.LeyLinesEnabled, "黑魔纹 QT 映射错误");
        AssertEx.False(input.Settings.CompressFireParadox, "压缩火悖论映射错误");
        AssertEx.Equal(
            BlmResolverSettings.Default.ManafontEnabled,
            input.Settings.ManafontEnabled,
            "未注册 Manafont QT 必须保持默认");
        AssertEx.Equal(
            BlmResolverSettings.Default.DoubleDotEnabled,
            input.Settings.DoubleDotEnabled,
            "未注册双 DOT QT 必须保持默认");
        AssertEx.Equal(
            BlmResolverSettings.Default.PotionEnabled,
            input.Settings.PotionEnabled,
            "未注册药物 QT 必须保持默认");
        AssertEx.Equal(1, input.DotTargets.Length, "3A 必须提供主目标 DOT 事实");
        AssertEx.Equal(context.TargetEntityId, input.DotTargets[0].EntityId, "DOT 目标错误");
        AssertEx.Equal(
            context.SingleTargetDot.RemainingMs,
            input.DotTargets[0].SingleTargetDotRemainingMs,
            "DOT 剩余时间必须来自冻结 Context");
        AssertEx.False(input.CanSpecifyDotTarget, "3A 不得宣称支持指定 DOT 目标");
        AssertEx.True(input.FactCoverage.UsedWeavesApproximate, "weave 覆盖应标记近似");
        AssertEx.False(input.FactCoverage.PotionSupported, "3A 不得宣称支持药物事实");
    }

    private static void UsedWeavesAreDerivedAfterPreviousGcd()
    {
        const long generation = 11;
        var previous = Success(generation, 10, BLMSkill.炽炎, isGcd: true);
        var history = ImmutableArray.Create(
            Success(generation, 9, MageUniversalSkill.醒梦, isGcd: false),
            previous,
            Success(generation, 11, MageUniversalSkill.即刻咏唱, isGcd: false),
            Success(generation, 12, 999_001, isGcd: false),
            Success(generation, 13, BLMSkill.火炎, isGcd: false),
            Success(generation + 1, 14, MageUniversalSkill.醒梦, isGcd: false));
        var decision = Decision(generation) with
        {
            RecentHistory = history,
            PreviousGcd = previous,
        };
        var runtime = new BlmResolverRuntimeState { StateGeneration = generation };
        var exactKnown = BlmResolverInputAdapter.Build(
            BaseContext(generation),
            decision,
            runtime,
            [],
            highPriority: false,
            actionId => actionId switch
            {
                MageUniversalSkill.即刻咏唱 => BlmResolverChannel.OffGcd,
                BLMSkill.火炎 => BlmResolverChannel.Gcd,
                _ => null,
            });
        AssertEx.Equal(1, exactKnown.UsedWeaves, "生产分类只应计入已识别 weave");

        var approximate = BlmResolverInputAdapter.Build(
            BaseContext(generation),
            decision,
            runtime,
            [],
            highPriority: false);
        AssertEx.Equal(3, approximate.UsedWeaves, "无分类器时应按非 GCD Ack 近似计数");
    }

    private static void ManafontProjectsPostRestoreFire4Count()
    {
        const long generation = 9;
        var context = BaseContext(generation) with
        {
            Mp = 10_000,
            AstralSoul = 0,
            HasParadox = false,
            DotEnabled = false,
        };
        var decision = new BlmTrackerDecisionSnapshot
        {
            Snapshot = new BlmTrackerSnapshot
            {
                StateGeneration = generation,
                IsCombatActive = true,
                HistoryReliable = true,
                FirePhaseSerial = 3,
                Fire4Count = 6,
                Fire4CountSinceManafont = 0,
                ManafontActiveThisFire = true,
                ManafontUseSerial = 1,
            },
        };
        var input = BlmResolverInputAdapter.Build(
            context,
            decision,
            new BlmResolverRuntimeState { StateGeneration = generation },
            [ReadyAction(BLMSkill.炽炎)],
            highPriority: false);

        AssertEx.Equal(
            0,
            input.Level100Loop.Fire4Count,
            "魔泉对账后必须投影魔泉后的火四计数，而不是整个火段总数");
        AssertEx.Equal(
            BLMSkill.炽炎,
            Level100ResolverEngine.Evaluate(input).GcdCandidate!.ActionId,
            "六火四后的魔泉扩展火段必须重新进入炽炎分支");
        var level90Frame = Level100ResolverEngine.Evaluate(input with
        {
            Context = input.Context with { Level = 90 },
        });
        AssertEx.Equal(
            BLMSkill.炽炎,
            level90Frame.GcdCandidate!.ActionId,
            "90级Manafont后必须使用Fire4CountSinceManafont续火");
        AssertEx.Equal(
            "GCD.单体90_99",
            level90Frame.GcdCandidate.ResolverId,
            "90级Manafont后必须进入90级主循环");
    }

    private static void RuntimeMemoryUsesPreviousGcdAndResets()
    {
        const long generation = 5;
        var state = new BlmResolverRuntimeState { StateGeneration = generation };
        var beforeIdle = BlmResolverRuntimeMemory.Reduce(
            state,
            Observation(generation, 5999, previousAtMs: 1000, previousSerial: 10));
        AssertEx.False(beforeIdle.IsIdle, "2 GCD 阈值前不得 Idle");

        var idle = BlmResolverRuntimeMemory.Reduce(
            beforeIdle,
            Observation(generation, 6000, previousAtMs: 1000, previousSerial: 10));
        AssertEx.True(idle.IsIdle, "2 GCD 边界应进入 Idle");
        AssertEx.Equal(6000L, idle.IdleSinceMs, "首次检测到 Idle 时应记录当前 tick");
        AssertEx.False(idle.NeedsForcedIceRecovery, "刚进入 Idle 不得强制回冰");

        var beforeRecovery = BlmResolverRuntimeMemory.Reduce(
            idle,
            Observation(generation, 13_499, previousAtMs: 1000, previousSerial: 10));
        AssertEx.Equal(6000L, beforeRecovery.IdleSinceMs, "持续 Idle 必须保留首次时间");
        AssertEx.False(
            beforeRecovery.NeedsForcedIceRecovery,
            "Idle 后恢复阈值前 1ms 不得强制回冰");
        var recovery = BlmResolverRuntimeMemory.Reduce(
            beforeRecovery,
            Observation(generation, 13_500, previousAtMs: 1000, previousSerial: 10));
        AssertEx.True(recovery.NeedsForcedIceRecovery, "Idle 后恢复阈值边界应强制回冰");
        var instant = BlmResolverRuntimeMemory.Reduce(
            recovery,
            Observation(
                generation,
                13_500,
                previousAtMs: 1000,
                previousSerial: 10,
                hasAvailableInstantGcd: true));
        AssertEx.False(instant.NeedsForcedIceRecovery, "已有瞬发不得强制恢复");
        var swiftReady = BlmResolverRuntimeMemory.Reduce(
            recovery,
            Observation(
                generation,
                13_500,
                previousAtMs: 1000,
                previousSerial: 10,
                swiftcastReady: true));
        AssertEx.False(swiftReady.NeedsForcedIceRecovery, "即刻当前可用不得强制恢复");

        var newGcd = BlmResolverRuntimeMemory.Reduce(
            recovery,
            Observation(generation, 13_500, previousAtMs: 13_500, previousSerial: 11));
        AssertEx.False(newGcd.IsIdle, "新 GCD 必须清 Idle");
        AssertEx.Equal(0L, newGcd.IdleSinceMs, "新 GCD 必须清 IdleSince");
        var casting = BlmResolverRuntimeMemory.Reduce(
            recovery,
            Observation(
                generation,
                13_500,
                previousAtMs: 1000,
                previousSerial: 10,
                isCasting: true));
        AssertEx.False(casting.IsIdle, "读条中必须清 Idle");
        var nextGeneration = BlmResolverRuntimeMemory.Reduce(
            recovery,
            Observation(generation + 1, 13_500, previousAtMs: 1000, previousSerial: 10));
        AssertEx.False(nextGeneration.IsIdle, "generation 变化必须清 Idle");
    }

    private static void AvailableInstantGcdControlsForcedRecovery()
    {
        const long generation = 31;
        var decision = Decision(generation);
        var runtime = new BlmResolverRuntimeState { StateGeneration = generation };
        var baseContext = BaseContext(generation) with
        {
            Mp = 10_000,
            HasParadox = false,
            HasFirestarter = false,
            HasThunderhead = false,
            PolyglotStacks = 0,
            HasSwiftcast = true,
            TriplecastStacks = 0,
            DotEnabled = true,
            MoveXenoEnabled = true,
        };
        var actions = ImmutableArray.Create(
            UnavailableAction(7561),
            UnavailableAction(7421));
        var buffOnly = BlmResolverInputAdapter.Build(
            baseContext,
            decision,
            runtime,
            actions,
            highPriority: false);
        AssertEx.False(
            Level100ResolverEngine.HasAvailableInstantGcd(buffOnly),
            "仅有即刻 Buff、没有可选瞬发 GCD 时 selector 必须返回 false");

        var idleState = new BlmResolverRuntimeState
        {
            StateGeneration = generation,
            PreviousGcdSerial = 10,
            IdleSinceMs = 6000,
            IsIdle = true,
        };
        var forced = BlmResolverRuntimeMemory.Reduce(
            idleState,
            Observation(
                generation,
                13_500,
                previousAtMs: 1000,
                previousSerial: 10,
                hasAvailableInstantGcd: false,
                swiftcastReady: false));
        AssertEx.True(
            forced.NeedsForcedIceRecovery,
            "即刻 Buff 不得代替 selector 或即刻 ability ready 阻止强制回冰");

        var polyglot = BlmResolverInputAdapter.Build(
            baseContext with
            {
                HasSwiftcast = false,
                PolyglotStacks = 1,
            },
            decision,
            runtime,
            actions,
            highPriority: false);
        AssertEx.True(
            Level100ResolverEngine.HasAvailableInstantGcd(polyglot),
            "有通晓且移动异言开启时应存在可用瞬发 GCD");
        var polyglotBlocked = BlmResolverRuntimeMemory.Reduce(
            idleState,
            Observation(
                generation,
                13_500,
                previousAtMs: 1000,
                previousSerial: 10,
                hasAvailableInstantGcd:
                    Level100ResolverEngine.HasAvailableInstantGcd(polyglot)));
        AssertEx.False(
            polyglotBlocked.NeedsForcedIceRecovery,
            "通晓候选应阻止强制回冰");

        var thunder = BlmResolverInputAdapter.Build(
            baseContext with
            {
                HasSwiftcast = false,
                HasThunderhead = true,
                SingleTargetDot = new BlmDotSnapshot
                {
                    StatusId = 3871,
                    RemainingMs = 0,
                    ExpectedDurationMs = 30_000,
                },
            },
            decision,
            runtime,
            actions.Add(ReadyAction(144, 36986)),
            highPriority: false);
        AssertEx.True(
            Level100ResolverEngine.HasAvailableInstantGcd(thunder),
            "雷云且 DOT 缺失时应存在可用瞬发 GCD");
        var thunderBlocked = BlmResolverRuntimeMemory.Reduce(
            idleState,
            Observation(
                generation,
                13_500,
                previousAtMs: 1000,
                previousSerial: 10,
                hasAvailableInstantGcd:
                    Level100ResolverEngine.HasAvailableInstantGcd(thunder)));
        AssertEx.False(
            thunderBlocked.NeedsForcedIceRecovery,
            "雷云候选应阻止强制回冰");
    }

    private static void AcrOffDoesNotAccumulateIdle()
    {
        const long generation = 41;
        var initial = new BlmResolverRuntimeState { StateGeneration = generation };
        var firstOff = BlmResolverRuntimeMemory.Reduce(
            initial,
            Observation(
                generation,
                6000,
                previousAtMs: 1000,
                previousSerial: 10,
                acrEnabled: false));
        var sustainedOff = BlmResolverRuntimeMemory.Reduce(
            firstOff,
            Observation(
                generation,
                30_000,
                previousAtMs: 1000,
                previousSerial: 10,
                acrEnabled: false));
        AssertEx.False(sustainedOff.IsIdle, "ACR Off 持续期间不得累计 Idle");
        AssertEx.Equal(0L, sustainedOff.IdleSinceMs, "ACR Off 持续期间必须清空 IdleSince");
        AssertEx.False(
            sustainedOff.NeedsForcedIceRecovery,
            "ACR Off 持续期间不得形成强制恢复状态");

        var firstOn = BlmResolverRuntimeMemory.Reduce(
            sustainedOff,
            Observation(
                generation,
                30_000,
                previousAtMs: 1000,
                previousSerial: 10,
                acrEnabled: true));
        AssertEx.True(firstOn.IsIdle, "ACR 重新开启首 Tick 可重新识别 Idle");
        AssertEx.Equal(30_000L, firstOn.IdleSinceMs, "重新开启首 Tick 必须从当前时刻开始 Idle");
        AssertEx.False(
            firstOn.NeedsForcedIceRecovery,
            "ACR 重新开启首 Tick 不得继承 Off 期间时长立即强制回冰");
    }

    private static void EntitySnapshotValidationFailsClosed()
    {
        var context = BaseContext(51);
        AssertEx.True(
            BlmResolverInputAdapter.EntitySnapshotMatches(
                context,
                context.PlayerEntityId,
                context.TargetEntityId,
                out var reason),
            "玩家与目标实体均匹配时应接受生产快照");
        AssertEx.Equal(string.Empty, reason, "匹配快照不应返回失败原因");

        AssertEx.False(
            BlmResolverInputAdapter.EntitySnapshotMatches(
                context,
                context.PlayerEntityId + 1,
                context.TargetEntityId,
                out reason),
            "玩家实体漂移必须整帧失败");
        AssertEx.Equal("PlayerEntityMismatch", reason, "玩家漂移原因错误");
        AssertEx.False(
            BlmResolverInputAdapter.EntitySnapshotMatches(
                context,
                context.PlayerEntityId,
                context.TargetEntityId + 1,
                out reason),
            "目标实体漂移必须整帧失败");
        AssertEx.Equal("TargetEntityMismatch", reason, "目标漂移原因错误");

        var noTarget = context with
        {
            HasTarget = false,
            HasValidTarget = false,
            TargetEntityId = 0,
        };
        AssertEx.True(
            BlmResolverInputAdapter.EntitySnapshotMatches(
                noTarget,
                noTarget.PlayerEntityId,
                null,
                out reason),
            "冻结 Context 与生产侧均无目标时应接受");
        AssertEx.False(
            BlmResolverInputAdapter.EntitySnapshotMatches(
                noTarget,
                noTarget.PlayerEntityId,
                context.TargetEntityId,
                out reason),
            "冻结后新出现目标也属于实体漂移");
        AssertEx.False(
            BlmResolverInputAdapter.EntitySnapshotMatches(
                noTarget with { HasValidTarget = true },
                noTarget.PlayerEntityId,
                null,
                out reason),
            "无目标 Context 不得携带有效攻击目标事实");

        var conservative = BlmResolverInputAdapter.Build(
            context,
            Decision(51),
            BlmResolverRuntimeState.Empty,
            [],
            highPriority: false);
        AssertEx.False(conservative.Context.IsAvailable, "实体采集失败路径必须使用保守不可用输入");
        AssertEx.False(
            conservative.FactCoverage.GenerationConsistent,
            "实体采集失败路径不得宣称事实覆盖完整");
    }

    private static void GenerationMismatchFailsClosed()
    {
        var input = BlmResolverInputAdapter.Build(
            BaseContext(20),
            Decision(21, fire4Count: 6, fireParadoxUsed: true),
            new BlmResolverRuntimeState
            {
                StateGeneration = 21,
                IsIdle = true,
                NeedsForcedIceRecovery = true,
            },
            [ReadyAction(BLMSkill.炽炎)],
            highPriority: false);

        AssertEx.False(input.Context.IsAvailable, "混代输入必须 fail closed");
        AssertEx.Equal(21L, input.StateGeneration, "混代输入仍应保留 decision 来源代");
        AssertEx.False(input.Context.AcrEnabled, "混代输入不得 eligible");
        AssertEx.True(input.PendingGaugeReconcile, "混代输入必须标记保守阻断");
        AssertEx.Equal(0, input.RecentHistory.Length, "混代输入不得携带历史");
        AssertEx.True(input.PreviousGcd is null, "混代输入不得携带 PreviousGcd");
        AssertEx.Equal(0, input.Level100Loop.Fire4Count, "混代输入不得携带火段计数");
        AssertEx.False(input.IsIdle, "混代输入不得携带运行时状态");
        AssertEx.False(input.FactCoverage.GenerationConsistent, "覆盖信息必须暴露混代");
        AssertEx.False(
            input.FactCoverage.ActionAvailabilitySupported,
            "混代保守输入不得宣称动作事实完整");
        AssertEx.False(
            input.FactCoverage.MainTargetDotSupported,
            "混代保守输入不得宣称 DOT 事实完整");
    }

    private static void RequiredActionTableMatchesLevel100Closure()
    {
        uint[] expected =
        [
            BLMSkill.火炎,
            BLMSkill.冰结,
            BLMSkill.闪雷,
            BLMSkill.震雷,
            BLMSkill.烈炎,
            BLMSkill.星灵移位,
            BLMSkill.爆炎,
            BLMSkill.冰封,
            BLMSkill.玄冰,
            BLMSkill.核爆,
            BLMSkill.冰澈,
            BLMSkill.炽炎,
            BLMSkill.秽浊,
            BLMSkill.绝望,
            BLMSkill.异言,
            BLMSkill.冰冻,
            BLMSkill.悖论,
            BLMSkill.高闪雷,
            BLMSkill.高震雷,
            BLMSkill.耀星,
            MageUniversalSkill.即刻咏唱,
            BLMSkill.三连咏唱,
            MageUniversalSkill.醒梦,
            BLMSkill.详述,
            BLMSkill.魔泉,
            BLMSkill.黑魔纹,
            MageUniversalSkill.昏乱,
            BLMSkill.魔罩,
        ];
        AssertEx.True(
            expected.SequenceEqual(BlmResolverInputAdapter.RequiredActionIds),
            "生产动作表必须覆盖 Phase 2 全部动作事实");
    }

    private static BlmContext BaseContext(long generation) => TestContext.Base() with
    {
        CapturedAtMs = 10_000,
        Phase = BlmPhase.Fire,
        AfStacks = 3,
        HasTarget = true,
        HasValidTarget = true,
        InRange = true,
        TargetDistance = 12f,
        Tracker = new BlmTrackerSnapshot
        {
            StateGeneration = generation,
            IsCombatActive = true,
            HistoryReliable = true,
        },
    };

    private static BlmTrackerDecisionSnapshot Decision(
        long generation,
        int fire4Count = 0,
        bool fireParadoxUsed = false)
        => new()
        {
            Snapshot = new BlmTrackerSnapshot
            {
                StateGeneration = generation,
                IsCombatActive = true,
                HistoryReliable = true,
                FirePhaseSerial = 3,
                ParadoxUsedFireSerial = fireParadoxUsed ? 3 : 0,
                Fire4Count = fire4Count,
            },
        };

    private static BlmResolverRuntimeObservation Observation(
        long generation,
        long nowMs,
        long previousAtMs,
        long previousSerial,
        bool hasAvailableInstantGcd = false,
        bool swiftcastReady = false,
        bool isCasting = false,
        bool acrEnabled = true)
        => new(
            generation,
            nowMs,
            IsAvailable: true,
            AcrEnabled: acrEnabled,
            InCombat: true,
            IsAlive: true,
            IsCasting: isCasting,
            GcdTotalSeconds: 2.5f,
            PreviousGcdOccurredAtMs: previousAtMs,
            PreviousGcdSerial: previousSerial,
            HasAvailableInstantGcd: hasAvailableInstantGcd,
            SwiftcastCurrentlyAvailable: swiftcastReady);

    private static BlmActionSuccess Success(
        long generation,
        long serial,
        uint actionId,
        bool isGcd)
        => new(
            generation,
            serial,
            actionId,
            actionId,
            actionId,
            (uint)serial,
            1000 + serial,
            1000 + serial,
            false,
            isGcd);

    private static BlmResolverActionFact ReadyAction(
        uint requestedActionId,
        uint adjustedActionId = 0)
        => new()
        {
            RequestedActionId = requestedActionId,
            AdjustedActionId = adjustedActionId == 0
                ? requestedActionId
                : adjustedActionId,
            IsUnlocked = true,
            CanCast = true,
            Charges = 1f,
            MaxCharges = 1,
        };

    private static BlmResolverActionFact UnavailableAction(uint actionId)
        => new()
        {
            RequestedActionId = actionId,
            AdjustedActionId = actionId,
            IsUnlocked = true,
            CanCast = false,
            Charges = 0f,
            MaxCharges = 1,
            CooldownRemainMs = 10_000d,
            RecastTotalMs = 60_000d,
        };
}
