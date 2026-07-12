using LosPr.BLM.Core;
using LosPr.BLM.Engine;
using PromeRotation.Data;

namespace Los.Tests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("技能 ID 与等级解锁表", SkillFacts),
            ("Action Change 归一化", ActionChangeNormalization),
            ("Manafont 精确满 MP 谓词", ManafontResourcePredicate),
            ("Transpose Ack 后等待下一 Tick Gauge", TransposeUsesPostGaugeConfirmation),
            ("ActionEffect 来源过滤与幂等", ActionEffectFilteringAndDedupe),
            ("Coordinator generation/token/sequence 隔离", CoordinatorTokenIsolation),
            ("Coordinator deadline 边界", CoordinatorDeadlineBoundaries),
            ("火冰阶段事实与 Context 投影", PhaseFactsAndContextProjection),
            ("死亡复活与战斗生命周期", LifecycleIsolation),
            ("ACR Off 边沿硬重置", AcrOffResetIsIdempotent),
            ("有效 Manafont Transition 原子确认", ManafontTransitionConfirmation),
            ("无关事务中的手动 Manafont", ManualManafontCancelsConflictingTransition),
            ("Manafont 对账超时", ManafontReconcileTimeout),
            ("同场第二次 Manafont", SecondManafontUse),
            ("阶段一决策原语与 Ack 历史", DecisionPrimitiveTests.RunAll),
            ("阶段二100级单体Resolver行为闭包", Level100ResolverParityTests.Run),
            ("Step2 决策 Gate 与纯度", DecisionTests.GatesAndPurity),
            ("Step2 冰段与 AF1 赤字恢复", DecisionTests.IceAndAf1Recovery),
            ("Step2 火段预算与资源边界", DecisionTests.FireBudgetBoundaries),
            ("Step2 移动安全与耀星后续承诺", DecisionTests.MovementAndFollowUp),
            ("Step2 雷法与通晓 Override", DecisionTests.ThunderAndPolyglotOverrides),
            ("Step2 标准单体连续三轮", DecisionTests.ThreeStandardRounds),
            ("Step3 Existing Firestarter Transition", DispatcherTests.ExistingFirestarterPath),
            ("Step3 AF1 Firestarter 赤字恢复", DispatcherTests.Af1DebtPath),
            ("Step3 Always 边界与一次性入队", DispatcherTests.AlwaysBoundariesAndOnceOnly),
            ("Step3 FireToIce 成功与回退", DispatcherTests.FireToIceSuccessAndFallback),
            ("Step3 冰段 MP 延迟后星灵恢复", DispatcherTests.DelayedIceMpKeepsTransposeTransition),
            ("Step3 常规 oGCD 主动选择与防重复", DispatcherTests.UtilityOffGcdStrategies),
            ("Step3 实验 B4 星灵绝望完整路线", DispatcherTests.ExperimentalB4TransposeDespair),
            ("Step3 取消、超时与丢 Ack", DispatcherTests.CancellationTimeoutAndLostAck),
            ("Step3 移动绝望耀星 Follow-up", FollowUpTests.MovementDespairFlareStarLifecycle),
            ("Step3 Follow-up Ack 隔离与幂等", FollowUpTests.AckTokenIsolationAndIdempotence),
            ("Step3 Follow-up 生命周期隔离", FollowUpTests.GenerationCombatPhaseAndTargetInvalidation),
            ("Step3 Follow-up deadline 与资源失效", FollowUpTests.DeadlineAndResourceInvalidation),
            ("Step3 Decision 承诺互斥", Step3ContractTests.DecisionRejectsConflictingCommitments),
            ("Step3 Follow-up 当前阶段 deadline", Step3ContractTests.FollowUpUsesCurrentStageDeadline),
            ("Step3 高优接管保持承诺", Step3ContractTests.HighPriorityReconcilePreservesCommittedWork),
            ("Step3 取消已交付动作时清理 PR 普通队列", Step3ContractTests.QueuedCancellationClearsNormalPrWork),
            ("Step3 接线调度标准单体连续三轮", WiredSimulationTests.ThreeStandardRoundsThroughWiredScheduler),
            ("Debug trace 核心、JSONL、隐私与滚动", DebugTraceTests.RunAll),
        };

        var failed = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"[PASS] {name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"[FAIL] {name}: {exception}");
            }
        }

        Console.WriteLine($"共 {tests.Length} 项，失败 {failed} 项。");
        return failed == 0 ? 0 : 1;
    }

    private static void SkillFacts()
    {
        AssertEx.Equal(149u, BLMSkill.星灵移位, "Transpose ID 错误");
        AssertEx.Equal(152u, BLMSkill.爆炎, "Fire III ID 错误");
        AssertEx.Equal(154u, BLMSkill.冰封, "Blizzard III ID 错误");
        AssertEx.Equal(157u, BLMSkill.魔罩, "Manaward ID 错误");
        AssertEx.Equal(158u, BLMSkill.魔泉, "Manafont ID 错误");
        AssertEx.Equal(159u, BLMSkill.玄冰, "Freeze ID 错误");
        AssertEx.Equal(162u, BLMSkill.核爆, "Flare ID 错误");
        AssertEx.Equal(25793u, BLMSkill.冰冻, "Blizzard II 系 ID 错误");
        AssertEx.Equal(3576u, BLMSkill.冰澈, "Blizzard IV ID 错误");
        AssertEx.Equal(3577u, BLMSkill.炽炎, "Fire IV ID 错误");
        AssertEx.Equal(16505u, BLMSkill.绝望, "Despair ID 错误");
        AssertEx.Equal(25797u, BLMSkill.悖论, "Paradox ID 错误");
        AssertEx.Equal(36986u, BLMSkill.高闪雷, "High Thunder ID 错误");
        AssertEx.Equal(36987u, BLMSkill.高震雷, "High Thunder II ID 错误");
        AssertEx.Equal(36989u, BLMSkill.耀星, "Flare Star ID 错误");
        AssertEx.True(BLMSkill.玄冰 != BLMSkill.冰冻, "Freeze 不得与 Blizzard II 混用");

        AssertEx.False(BlmSkillBook.IsUnlocked(BLMSkill.冰澈, 57), "Lv57 不应解锁 B4");
        AssertEx.True(BlmSkillBook.IsUnlocked(BLMSkill.冰澈, 58), "Lv58 应解锁 B4");
        AssertEx.False(BlmSkillBook.IsUnlocked(BLMSkill.炽炎, 59), "Lv59 不应解锁 F4");
        AssertEx.True(BlmSkillBook.IsUnlocked(BLMSkill.炽炎, 60), "Lv60 应解锁 F4");
        AssertEx.False(BlmSkillBook.IsUnlocked(BLMSkill.绝望, 71), "Lv71 不应解锁 Despair");
        AssertEx.True(BlmSkillBook.IsUnlocked(BLMSkill.绝望, 72), "Lv72 应解锁 Despair");
        AssertEx.False(BlmSkillBook.IsUnlocked(BLMSkill.悖论, 89), "Lv89 不应解锁 Paradox");
        AssertEx.True(BlmSkillBook.IsUnlocked(BLMSkill.悖论, 90), "Lv90 应解锁 Paradox");
        AssertEx.False(BlmSkillBook.IsUnlocked(BLMSkill.耀星, 99), "Lv99 不应解锁 Flare Star");
        AssertEx.True(BlmSkillBook.IsUnlocked(BLMSkill.耀星, 100), "Lv100 应解锁 Flare Star");

        AssertDotRule(BlmContext.SingleTargetDotRuleForLevel(6), BlmBuff.雷一Dot, 24000f, "Thunder");
        AssertDotRule(BlmContext.SingleTargetDotRuleForLevel(45), BlmBuff.暴雷Dot, 27000f, "Thunder III");
        AssertDotRule(BlmContext.SingleTargetDotRuleForLevel(92), BlmBuff.高雷Dot, 30000f, "High Thunder");
        AssertDotRule(BlmContext.AoeDotRuleForLevel(26), BlmBuff.雷二Dot, 18000f, "Thunder II");
        AssertDotRule(BlmContext.AoeDotRuleForLevel(64), BlmBuff.霹雷Dot, 21000f, "Thunder IV");
        AssertDotRule(BlmContext.AoeDotRuleForLevel(92), BlmBuff.高雷二Dot, 24000f, "High Thunder II");
    }

    private static void ActionChangeNormalization()
    {
        var mappings = new Dictionary<uint, uint>
        {
            [BLMSkill.冰冻] = BLMSkill.高冰冻,
            [BLMSkill.烈炎] = BLMSkill.高烈炎,
        };
        var normalizer = new MappingActionIdNormalizer(mappings);

        AssertEx.True(
            BlmSkillBook.ActionIdsMatch(BLMSkill.冰冻, BLMSkill.高冰冻, normalizer),
            "Blizzard II 的升级形态应匹配");
        AssertEx.True(
            BlmSkillBook.IsKnownGcdAction(BLMSkill.高烈炎, normalizer),
            "High Fire II 应识别为已知 GCD");
        AssertEx.False(
            BlmSkillBook.ActionIdsMatch(BLMSkill.冰冻, BLMSkill.玄冰, normalizer),
            "Blizzard II 不得归一化为 Freeze");

        var clock = new FakeClock();
        var coordinator = new BlmCoordinator(clock, normalizer);
        AssertEx.True(
            coordinator.TryBegin(
                1,
                TransitionKind.IceToFire,
                TransitionStep.CommitIceGcd,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.冰冻,
                TransitionExpectation.IceReadyWithFirestarter,
                RotationMode.SingleTarget,
                1000,
                "冻结 adjusted ID",
                IceToFireRoute.ExistingFirestarter),
            "应冻结 Blizzard II 当前升级形态");
        mappings[BLMSkill.冰冻] = BLMSkill.玄冰;
        var intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                1,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.高冰冻,
                BlmQueueChannel.Gcd,
                0f,
                0,
                1000,
                "升级形态 queued"),
            "入队时必须沿用请求时冻结的 adjusted ID");
        var token = coordinator.CaptureAckToken(1);
        AssertEx.True(
            coordinator.TryAcknowledge(token, BLMSkill.高冰冻, 1, clock.NowMs),
            "Ack 不得按动作后的动态 Action Change 重新计算 expected ID");
    }

    private static void ManafontResourcePredicate()
    {
        var restored = Context(
            new FakeClock(),
            BlmPhase.Fire,
            mp: 10000,
            maxMp: 10000,
            afStacks: 3,
            hearts: 3,
            paradox: true,
            thunderhead: true);
        AssertEx.True(restored.HasManafontResourcesRestored, "完整资源应确认 Manafont");
        AssertEx.False(
            (restored with { Mp = 10001 }).HasManafontResourcesRestored,
            "MP 高于 MaxMP 也不得通过精确满 MP 谓词");
        AssertEx.False(
            (restored with { Mp = 9999 }).HasManafontResourcesRestored,
            "MP 未满不得确认 Manafont");
        AssertEx.False(
            (restored with { HasThunderhead = false }).HasManafontResourcesRestored,
            "Manafont 在可用等级都必须确认 Thunderhead");
        AssertEx.False(
            (restored with { UmbralHearts = 2 }).HasManafontResourcesRestored,
            "Lv58+ 必须确认三枚 Umbral Hearts");
        AssertEx.False(
            (restored with { HasParadox = false }).HasManafontResourcesRestored,
            "Lv90+ 必须确认 Paradox");
        AssertEx.True(
            (restored with { Level = 57, UmbralHearts = 0, HasParadox = false })
                .HasManafontResourcesRestored,
            "低等级不得等待尚未解锁的 Hearts/Paradox");
    }

    private static void TransposeUsesPostGaugeConfirmation()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var ice = Context(
            clock,
            BlmPhase.Ice,
            iceStacks: 3,
            hearts: 3,
            firestarter: true);
        var tracker = new BlmStateTracker(coordinator, ice, clock, normalizer);
        var generation = tracker.StateGeneration;

        AssertEx.True(
            coordinator.TryBegin(
                generation,
                TransitionKind.IceToFire,
                TransitionStep.CommitIceGcd,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.冰澈,
                TransitionExpectation.IceReadyWithFirestarter,
                RotationMode.SingleTarget,
                3000,
                "测试冰 GCD",
                IceToFireRoute.ExistingFirestarter),
            "应建立冰转火 Transition");
        var intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.冰澈,
                BlmQueueChannel.Gcd,
                0f,
                0,
                3000,
                "B4 queued"),
            "B4 应进入 Queued");

        var b4Ack = tracker.CreateAckEnvelope(
            100,
            BLMSkill.冰澈,
            10,
            BlmPhase.Ice,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(b4Ack), "B4 Ack 应被接收");
        AssertEx.Equal(
            TransitionStage.Queued,
            coordinator.Peek().Stage,
            "Ack 不能直接 Confirmed");
        clock.Advance(16);
        tracker.Reconcile(At(ice, clock));
        AssertEx.Equal(
            TransitionStage.Confirmed,
            coordinator.Peek().Stage,
            "下一 Tick 的冰资源事实应确认 B4");

        intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryAdvance(
                generation,
                intent.Serial,
                intent.StepIndex,
                TransitionStep.UseTranspose,
                TransitionDeliveryChannel.OffGcd,
                BLMSkill.星灵移位,
                TransitionExpectation.AstralFireOne,
                2000,
                "使用 Transpose"),
            "应推进到 Transpose");
        intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.星灵移位,
                BlmQueueChannel.OffGcd,
                1.2f,
                10,
                2000,
                "Transpose queued"),
            "Transpose 应进入 Queued");
        var transposeAck = tracker.CreateAckEnvelope(
            100,
            BLMSkill.星灵移位,
            11,
            BlmPhase.Ice,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(transposeAck), "Transpose Ack 应被接收");
        AssertEx.Equal(
            TransitionStage.Queued,
            coordinator.Peek().Stage,
            "Transpose Ack 后旧 Gauge 不能确认换相");

        clock.Advance(16);
        tracker.Reconcile(At(ice, clock));
        AssertEx.Equal(
            TransitionStage.Queued,
            coordinator.Peek().Stage,
            "旧 UI3 Gauge 必须保持 Queued");

        var fireOne = Context(
            clock,
            BlmPhase.Fire,
            afStacks: 1,
            firestarter: true);
        clock.Advance(16);
        tracker.Reconcile(At(fireOne, clock));
        AssertEx.Equal(
            TransitionStage.Confirmed,
            coordinator.Peek().Stage,
            "下一 Tick AF1 才能确认 Transpose");
    }

    private static void ActionEffectFilteringAndDedupe()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var fire = Context(clock, BlmPhase.Fire, afStacks: 3);
        var tracker = new BlmStateTracker(coordinator, fire, clock, normalizer);

        var wrongSource = tracker.CreateAckEnvelope(
            999,
            BLMSkill.炽炎,
            100,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.False(tracker.ApplyActionEffect(wrongSource), "非本人 ActionEffect 必须拒绝");

        var first = tracker.CreateAckEnvelope(
            100,
            BLMSkill.炽炎,
            100,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(first), "本人 F4 Ack 应接收");
        AssertEx.False(tracker.ApplyActionEffect(first), "重复 sequence 必须幂等拒绝");
        AssertEx.Equal(1, tracker.GetTrackerSnapshot().Fire4Count, "重复 F4 不得重复计数");

        clock.Advance(200);
        var zeroF4 = tracker.CreateAckEnvelope(
            100,
            BLMSkill.炽炎,
            0,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(zeroF4), "首个零 sequence F4 应接收");
        clock.Advance(10);
        var interleaved = tracker.CreateAckEnvelope(
            100,
            BLMSkill.黑魔纹,
            0,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(interleaved), "交错 oGCD 应接收");
        clock.Advance(10);
        var repeatedZeroF4 = tracker.CreateAckEnvelope(
            100,
            BLMSkill.炽炎,
            0,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.False(
            tracker.ApplyActionEffect(repeatedZeroF4),
            "交错动作不得绕过零 sequence 的每技能去重");
        AssertEx.Equal(2, tracker.GetTrackerSnapshot().Fire4Count, "零 sequence 重放不得加 F4");
    }

    private static void CoordinatorTokenIsolation()
    {
        var clock = new FakeClock();
        var coordinator = new BlmCoordinator(clock, new MappingActionIdNormalizer());
        const long generation = 7;
        AssertEx.True(
            coordinator.TryBegin(
                generation,
                TransitionKind.FireToIce,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                3000,
                "测试 token"),
            "应建立 FireToIce");
        var intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.绝望,
                BlmQueueChannel.Gcd,
                0f,
                50,
                3000,
                "queued"),
            "应标记 Queued");
        var oldToken = coordinator.CaptureAckToken(generation);
        AssertEx.False(
            coordinator.TryAcknowledge(oldToken, BLMSkill.绝望, 49, clock.NowMs),
            "早于 queue baseline 的乱序事件必须拒绝");
        AssertEx.True(
            coordinator.TryAcknowledge(oldToken, BLMSkill.绝望, 51, clock.NowMs),
            "新 sequence 应接受");
        AssertEx.True(
            coordinator.TryAcknowledge(oldToken, BLMSkill.绝望, 51, clock.NowMs),
            "相同 token 与 sequence 的重放应幂等成功");
        AssertEx.False(
            coordinator.TryAcknowledge(oldToken, BLMSkill.绝望, 52, clock.NowMs),
            "不同事件不得覆盖已附着的 Ack");
        AssertEx.False(
            coordinator.TryAcknowledge(
                oldToken with { StateGeneration = generation - 1 },
                BLMSkill.绝望,
                51,
                clock.NowMs),
            "旧 generation token 必须拒绝");

        coordinator.CancelActive("generation reset");
        AssertEx.True(
            coordinator.TryBegin(
                generation + 1,
                TransitionKind.FireToIce,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                3000,
                "新 generation"),
            "新 generation 应可建立事务");
        intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation + 1,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.绝望,
                BlmQueueChannel.Gcd,
                0f,
                51,
                3000,
                "new queued"),
            "新事务应入队");
        AssertEx.False(
            coordinator.TryAcknowledge(oldToken, BLMSkill.绝望, 52, clock.NowMs),
            "旧事务同动作 Ack 不得确认新事务");
        coordinator.CancelActive("校验 expectation");
        AssertEx.False(
            coordinator.TryBegin(
                generation + 2,
                TransitionKind.ManafontExtension,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.AckOnly,
                RotationMode.SingleTarget,
                3000,
                "非法 AckOnly"),
            "关键 Manafont Step 不得用 AckOnly");
    }

    private static void LifecycleIsolation()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var neutral = Context(clock, BlmPhase.Neutral, inCombat: false);
        var tracker = new BlmStateTracker(coordinator, neutral, clock, normalizer);
        AssertEx.True(tracker.GetTrackerSnapshot().HistoryReliable, "非战斗 Neutral 基线应可信");

        var initialGeneration = tracker.StateGeneration;
        tracker.BeginCombat();
        AssertEx.Equal(1L, tracker.CombatSerial, "新战斗 CombatSerial 应递增");
        AssertEx.Equal(initialGeneration + 1, tracker.StateGeneration, "新战斗应硬重置 generation");
        tracker.BeginCombat();
        AssertEx.Equal(initialGeneration + 1, tracker.StateGeneration, "重复 BeginCombat 必须幂等");

        var fire = Context(clock, BlmPhase.Fire, afStacks: 3);
        tracker.Reconcile(fire);
        AssertEx.True(tracker.GetTrackerSnapshot().HistoryReliable, "真实进入 Fire 边沿后历史应可信");
        var f4Ack = tracker.CreateAckEnvelope(
            100,
            BLMSkill.炽炎,
            1,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(f4Ack), "F4 应计入当前 generation");
        AssertEx.Equal(1, tracker.GetTrackerSnapshot().Fire4Count, "F4 计数错误");

        var beforeDeathGeneration = tracker.StateGeneration;
        tracker.OnPlayerDied();
        AssertEx.Equal(beforeDeathGeneration + 1, tracker.StateGeneration, "死亡应增加 generation");
        AssertEx.Equal(0, tracker.GetTrackerSnapshot().Fire4Count, "死亡应清空 F4");
        AssertEx.False(tracker.GetTrackerSnapshot().HistoryReliable, "死亡后历史必须不可信");
        tracker.OnPlayerDied();
        AssertEx.Equal(beforeDeathGeneration + 1, tracker.StateGeneration, "重复死亡通知必须幂等");
        AssertEx.False(tracker.ApplyActionEffect(f4Ack), "死亡前延迟 Ack 必须隔离");

        tracker.OnPlayerRevived();
        AssertEx.Equal(beforeDeathGeneration + 2, tracker.StateGeneration, "复活应再次增加 generation");
        tracker.OnPlayerRevived();
        AssertEx.Equal(beforeDeathGeneration + 2, tracker.StateGeneration, "重复复活通知必须幂等");

        tracker.EndCombat();
        var afterEndGeneration = tracker.StateGeneration;
        tracker.EndCombat();
        AssertEx.Equal(afterEndGeneration, tracker.StateGeneration, "重复 EndCombat 必须幂等");
        tracker.BeginCombat();
        AssertEx.Equal(2L, tracker.CombatSerial, "第二场战斗 CombatSerial 应为 2");
    }

    private static void CoordinatorDeadlineBoundaries()
    {
        var clock = new FakeClock();
        var coordinator = new BlmCoordinator(clock, new MappingActionIdNormalizer());
        AssertEx.True(
            coordinator.TryBegin(
                1,
                TransitionKind.FireToIce,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                100,
                "deadline",
                totalExpireMs: 5000),
            "应建立 deadline 测试事务");
        var intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                1,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.绝望,
                BlmQueueChannel.Gcd,
                0f,
                0,
                100,
                "queued"),
            "应进入 Queued");
        var token = coordinator.CaptureAckToken(1);
        clock.Advance(100);
        AssertEx.True(
            coordinator.TryAcknowledge(token, BLMSkill.绝望, 1, clock.NowMs),
            "deadline 精确边界上的 Ack 应接受");
        AssertEx.Equal(
            TransitionStage.Queued,
            coordinator.Peek().Stage,
            "Ack 仍只能附着在 Queued");

        coordinator.CancelActive("next case");
        AssertEx.True(
            coordinator.TryBegin(
                2,
                TransitionKind.FireToIce,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                100,
                "late deadline",
                totalExpireMs: 5000),
            "应建立第二个 deadline 事务");
        intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                2,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.绝望,
                BlmQueueChannel.Gcd,
                0f,
                1,
                100,
                "queued 2"),
            "第二个事务应入队");
        token = coordinator.CaptureAckToken(2);
        clock.Advance(101);
        AssertEx.False(
            coordinator.TryAcknowledge(token, BLMSkill.绝望, 2, clock.NowMs),
            "deadline 后 1ms 的 Ack 必须拒绝");
        AssertEx.Equal(
            TransitionStage.Cancelled,
            coordinator.Peek().Stage,
            "超时事务必须收敛到 Cancelled");
    }

    private static void PhaseFactsAndContextProjection()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var fire = Context(clock, BlmPhase.Fire, afStacks: 3, paradox: true);
        var tracker = new BlmStateTracker(coordinator, fire, clock, normalizer);

        ApplyOwnAck(tracker, clock, BLMSkill.炽炎, 1, BlmPhase.Fire);
        AssertEx.Equal(1, tracker.GetTrackerSnapshot().Fire4Count, "F4 应计数");
        ApplyOwnAck(tracker, clock, BLMSkill.耀星, 2, BlmPhase.Fire);
        AssertEx.Equal(1, tracker.GetTrackerSnapshot().Fire4Count, "Flare Star 不得清 F4");
        ApplyOwnAck(tracker, clock, BLMSkill.悖论, 3, BlmPhase.Fire);
        AssertEx.True(
            tracker.GetTrackerSnapshot().ParadoxUsedThisFire,
            "火悖论必须记录动作前火阶段 serial");

        clock.Advance(16);
        var ice = At(fire with
        {
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 3,
        }, clock);
        tracker.Reconcile(ice);
        AssertEx.Equal(0, tracker.GetTrackerSnapshot().Fire4Count, "离开火阶段应清 F4");
        ApplyOwnAck(tracker, clock, BLMSkill.悖论, 4, BlmPhase.Ice);
        var facts = tracker.GetTrackerSnapshot();
        AssertEx.True(facts.ParadoxUsedThisIce, "冰悖论必须记录动作前冰阶段 serial");

        clock.Advance(16);
        tracker.Reconcile(At(ice, clock));
        var context = tracker.GetContextSnapshot();
        AssertEx.Equal(
            tracker.StateGeneration,
            context.Tracker.StateGeneration,
            "决策 Context 必须携带同一 Tracker generation");
        AssertEx.Equal(
            facts.IcePhaseSerial,
            context.Tracker.IcePhaseSerial,
            "Context 不得维护第二套 phase serial");
        AssertEx.Equal(BlmBuff.高雷Dot, context.SingleTargetDot.StatusId, "单体雷 DoT ID 错误");
        AssertEx.Equal(BlmBuff.高雷二Dot, context.AoeDot.StatusId, "AOE 雷 DoT ID 错误");
    }

    private static void AcrOffResetIsIdempotent()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var fire = Context(clock, BlmPhase.Fire, afStacks: 3, acrState: AcrState.On);
        var tracker = new BlmStateTracker(coordinator, fire, clock, normalizer);
        ApplyOwnAck(tracker, clock, BLMSkill.炽炎, 1, BlmPhase.Fire);
        AssertEx.Equal(1, tracker.GetTrackerSnapshot().Fire4Count, "前置 F4 应存在");

        var generation = tracker.StateGeneration;
        clock.Advance(16);
        var off = At(fire with { AcrState = AcrState.Off }, clock);
        tracker.Reconcile(off);
        AssertEx.Equal(generation + 1, tracker.StateGeneration, "ACR Off 边沿应硬重置");
        AssertEx.Equal(0, tracker.GetTrackerSnapshot().Fire4Count, "ACR Off 应清临时阶段计数");
        AssertEx.False(
            tracker.GetTrackerSnapshot().HistoryReliable,
            "非 Neutral 状态关闭 ACR 后历史应不可信");

        clock.Advance(16);
        tracker.Reconcile(At(off, clock));
        AssertEx.Equal(generation + 1, tracker.StateGeneration, "持续 Off 不得每帧重置");
    }

    private static void ManafontReconcileTimeout()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var fire = Context(
            clock,
            BlmPhase.Fire,
            mp: 0,
            maxMp: 10000,
            afStacks: 3,
            hearts: 0,
            paradox: false,
            thunderhead: false);
        var tracker = new BlmStateTracker(coordinator, fire, clock, normalizer);
        ApplyOwnAck(tracker, clock, BLMSkill.魔泉, 1, BlmPhase.Fire);

        clock.Advance(BlmCoordinator.PostAckReconcileTimeoutMs - 1);
        tracker.Reconcile(At(fire, clock));
        AssertEx.Equal(0L, tracker.GetTrackerSnapshot().ManafontUseSerial, "旧 Gauge 不得确认 Manafont");
        AssertEx.True(
            tracker.GetTrackerSnapshot().PendingGaugeReconcile,
            "deadline 前应继续等待资源事实");

        clock.Advance(2);
        var lateRestored = At(fire with
        {
            Mp = 10000,
            UmbralHearts = 3,
            HasParadox = true,
            HasThunderhead = true,
        }, clock);
        tracker.Reconcile(lateRestored);
        AssertEx.Equal(0L, tracker.GetTrackerSnapshot().ManafontUseSerial, "超时后不得晚确认 Manafont");
        AssertEx.False(
            tracker.GetTrackerSnapshot().PendingGaugeReconcile,
            "超时后 Pending 必须收敛");
    }

    private static void ManafontTransitionConfirmation()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var afterFinisher = Context(
            clock,
            BlmPhase.Fire,
            mp: 0,
            maxMp: 10000,
            afStacks: 3);
        var tracker = new BlmStateTracker(coordinator, afterFinisher, clock, normalizer);
        var generation = tracker.StateGeneration;

        AssertEx.True(
            coordinator.TryBegin(
                generation,
                TransitionKind.ManafontExtension,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                3000,
                "Manafont 前置收尾"),
            "应建立 ManafontExtension");
        var intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.绝望,
                BlmQueueChannel.Gcd,
                0f,
                0,
                3000,
                "Despair queued"),
            "Despair 应入队");
        ApplyOwnAck(tracker, clock, BLMSkill.绝望, 1, BlmPhase.Fire);
        clock.Advance(16);
        tracker.Reconcile(At(afterFinisher, clock));
        AssertEx.Equal(
            TransitionStage.Confirmed,
            coordinator.Peek().Stage,
            "Despair 应在下一 Tick 确认");

        intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryAdvance(
                generation,
                intent.Serial,
                intent.StepIndex,
                TransitionStep.UseManafont,
                TransitionDeliveryChannel.OffGcd,
                BLMSkill.魔泉,
                TransitionExpectation.ManafontResourcesRestored,
                2000,
                "使用 Manafont"),
            "应推进到 Manafont Step");
        intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.魔泉,
                BlmQueueChannel.OffGcd,
                1.2f,
                1,
                2000,
                "Manafont queued"),
            "Manafont 应入队");
        ApplyOwnAck(tracker, clock, BLMSkill.魔泉, 2, BlmPhase.Fire);
        AssertEx.Equal(0L, tracker.GetTrackerSnapshot().ManafontUseSerial, "Ack 时不得提前提交 Tracker");
        AssertEx.Equal(TransitionStage.Queued, coordinator.Peek().Stage, "Ack 时 Coordinator 仍应 Queued");

        var restored = At(afterFinisher with
        {
            Mp = 10000,
            UmbralHearts = 3,
            HasParadox = true,
            HasThunderhead = true,
        }, clock);
        clock.Advance(16);
        tracker.Reconcile(At(restored, clock));
        AssertEx.Equal(TransitionStage.Confirmed, coordinator.Peek().Stage, "资源谓词应确认 Coordinator");
        AssertEx.Equal(1L, tracker.GetTrackerSnapshot().ManafontUseSerial, "同一 Tick 应原子提交 Tracker");
    }

    private static void ManualManafontCancelsConflictingTransition()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var restored = Context(
            clock,
            BlmPhase.Fire,
            mp: 10000,
            maxMp: 10000,
            afStacks: 3,
            hearts: 3,
            paradox: true,
            thunderhead: true);
        var tracker = new BlmStateTracker(coordinator, restored, clock, normalizer);
        var generation = tracker.StateGeneration;

        AssertEx.True(
            coordinator.TryBegin(
                generation,
                TransitionKind.FireToIce,
                TransitionStep.CommitFireFinisher,
                TransitionDeliveryChannel.Gcd,
                BLMSkill.绝望,
                TransitionExpectation.FireFinisherReady,
                RotationMode.SingleTarget,
                3000,
                "等待 Despair"),
            "应建立冲突事务");
        var intent = coordinator.Peek();
        AssertEx.True(
            coordinator.TryMarkQueued(
                generation,
                intent.Serial,
                intent.StepIndex,
                BLMSkill.绝望,
                BlmQueueChannel.Gcd,
                0f,
                0,
                3000,
                "Despair queued"),
            "冲突事务应入队");

        ApplyOwnAck(tracker, clock, BLMSkill.魔泉, 1, BlmPhase.Fire);
        AssertEx.Equal(
            TransitionStage.Cancelled,
            coordinator.Peek().Stage,
            "手动 Manafont 应取消无关的 Despair 事务");
        AssertEx.Equal(0L, tracker.GetTrackerSnapshot().ManafontUseSerial, "Ack 时仍不得提前记 Manafont");

        clock.Advance(16);
        tracker.Reconcile(At(restored, clock));
        AssertEx.Equal(1L, tracker.GetTrackerSnapshot().ManafontUseSerial, "手动 Manafont 仍应按资源事实记账");
    }

    private static void SecondManafontUse()
    {
        var clock = new FakeClock();
        var normalizer = new MappingActionIdNormalizer();
        var coordinator = new BlmCoordinator(clock, normalizer);
        var fire = Context(
            clock,
            BlmPhase.Fire,
            mp: 10000,
            maxMp: 10000,
            afStacks: 3,
            hearts: 3,
            paradox: true,
            thunderhead: true);
        var tracker = new BlmStateTracker(coordinator, fire, clock, normalizer);

        ConfirmManafont(tracker, clock, fire, 1);
        var first = tracker.GetTrackerSnapshot();
        AssertEx.Equal(1L, first.ManafontUseSerial, "首次 Manafont serial 应为 1");
        AssertEx.True(first.ManafontActiveThisFire, "首次 Manafont 应激活当前火段扩展");

        clock.Advance(16);
        var ice = At(fire with
        {
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 3,
            HasParadox = true,
        }, clock);
        tracker.Reconcile(ice);
        AssertEx.False(
            tracker.GetTrackerSnapshot().ManafontActiveThisFire,
            "进入冰态后应结束本次 Manafont 扩展");

        clock.Advance(16);
        var secondFire = At(fire, clock);
        tracker.Reconcile(secondFire);
        ConfirmManafont(tracker, clock, secondFire, 2);
        var second = tracker.GetTrackerSnapshot();
        AssertEx.Equal(2L, second.ManafontUseSerial, "同场第二次 Manafont 必须使用新 serial");
        AssertEx.True(second.ManafontActiveThisFire, "第二次 Manafont 应正常激活");
    }

    private static void ConfirmManafont(
        BlmStateTracker tracker,
        FakeClock clock,
        BlmContext restoredContext,
        uint sequence)
    {
        var ack = tracker.CreateAckEnvelope(
            100,
            BLMSkill.魔泉,
            sequence,
            BlmPhase.Fire,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(ack), "Manafont Ack 应接收");
        AssertEx.Equal(
            sequence - 1,
            (uint)tracker.GetTrackerSnapshot().ManafontUseSerial,
            "Ack 时不得提前增加 Manafont serial");
        clock.Advance(16);
        tracker.Reconcile(At(restoredContext, clock));
    }

    private static BlmContext Context(
        FakeClock clock,
        BlmPhase phase,
        bool inCombat = true,
        long mp = 10000,
        long maxMp = 10000,
        int afStacks = 0,
        int iceStacks = 0,
        int hearts = 0,
        bool paradox = false,
        bool firestarter = false,
        bool thunderhead = false,
        AcrState acrState = AcrState.On) => new()
        {
            CapturedAtMs = clock.NowMs,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            IsAvailable = true,
            AvailabilityText = "测试状态",
            AcrState = acrState,
            PlayerEntityId = 100,
            JobId = 25,
            Level = 100,
            Mp = mp,
            MaxMp = maxMp,
            InCombat = inCombat,
            IsAlive = true,
            CanAct = true,
            Phase = phase,
            AfStacks = afStacks,
            IceStacks = iceStacks,
            UmbralHearts = hearts,
            HasParadox = paradox,
            HasFirestarter = firestarter,
            HasThunderhead = thunderhead,
            SingleTargetDot = new BlmDotSnapshot
            {
                StatusId = BlmBuff.高雷Dot,
                ExpectedDurationMs = 30000,
            },
            AoeDot = new BlmDotSnapshot
            {
                StatusId = BlmBuff.高雷二Dot,
                ExpectedDurationMs = 24000,
            },
        };

    private static BlmContext At(BlmContext context, FakeClock clock)
        => context with
        {
            CapturedAtMs = clock.NowMs,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Tracker = BlmTrackerSnapshot.Empty,
        };

    private static void ApplyOwnAck(
        BlmStateTracker tracker,
        FakeClock clock,
        uint actionId,
        uint sequence,
        BlmPhase phaseBefore)
    {
        var ack = tracker.CreateAckEnvelope(
            100,
            actionId,
            sequence,
            phaseBefore,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(ack), $"Action {actionId} Ack 应接收");
    }

    private static void AssertDotRule(
        BlmDotRule rule,
        uint expectedStatusId,
        float expectedDurationMs,
        string name)
    {
        AssertEx.Equal(expectedStatusId, rule.StatusId, $"{name} 状态 ID 错误");
        AssertEx.Equal(expectedDurationMs, rule.DurationMs, $"{name} 持续时间错误");
    }
}
