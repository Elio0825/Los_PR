using System.Collections.Immutable;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;
using LosPr.BLM.Resolvers.Production;
using PromeRotation.Data;

namespace Los.Tests;

internal static class ResolverExecutionTests
{
    public static void RunAll()
    {
        SameFrameAndPendingPreventDuplicateDelivery();
        AckAndGaugeReleasePendingInOrder();
        FireEndHoldPrioritizesTranspose();
        ManafontIsDeliveredAsOffGcd();
        OffGcdWaitsForActualWeaveWindow();
        ManafontBridgeDeliversAfterHardcast();
        TargetSwitchCancelsOldPending();
        TargetSwitchPreservesSelfAbilityPending();
        HighPriorityAndFrameDriftFailClosed();
        ManualRecoveryAlwaysWorksWithoutPreviousGcd();
        IceParadoxTriplecastBlizzard3SequenceIsDeliverable();
        Level90CandidateIsDeliveredInProduction();
        Level1To89CandidatesAreDeliveredInProduction();
        Level60IceAckAheadOfGaugeDeliversBlizzardFour();
        AoeSharedAbilitiesAreDeliveredWithoutGcdLeak();
        AoePendingCancellationFollowsDeliveryTargetContract();
        AoeManafontAlwaysBridgeIsSingleDelivery();
    }

    private static void SameFrameAndPendingPreventDuplicateDelivery()
    {
        var fixture = CreateFixture(FireContext());
        var input = Input(fixture.Context);
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "生产帧应建立");

        var first = fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context);
        AssertAction(first, BLMSkill.炽炎, ActionType.Gcd, fixture.Context.TargetEntityId);
        AssertEx.True(fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction, "返回后应建立单 Pending");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context) is null,
            "同帧不得重复返回同一 GCD");

        fixture = fixture with { Context = NextFrame(fixture, 1) };
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, Input(fixture.Context)),
            "下一帧应建立");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context) is null,
            "Ack 前跨帧不得重复返回 Pending 动作");

        fixture.Clock.Advance(5001);
        var expiredContext = fixture.Context with { CapturedAtMs = fixture.Clock.NowMs };
        fixture.Tracker.Reconcile(expiredContext);
        expiredContext = fixture.Tracker.GetContextSnapshot();
        AssertEx.True(
            fixture.Execution.BeginFrame(expiredContext, Input(expiredContext)),
            "Pending 超时后的帧应建立");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, expiredContext),
            BLMSkill.炽炎,
            ActionType.Gcd,
            expiredContext.TargetEntityId);
    }

    private static void AckAndGaugeReleasePendingInOrder()
    {
        var fixture = CreateFixture(FireContext());
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, Input(fixture.Context)),
            "Ack 测试帧应建立");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context),
            BLMSkill.炽炎,
            ActionType.Gcd,
            fixture.Context.TargetEntityId);

        fixture.Clock.Advance(100);
        var ack = fixture.Tracker.CreateAckEnvelope(
            fixture.Context.PlayerEntityId,
            BLMSkill.炽炎,
            1,
            BlmPhase.Fire,
            fixture.Clock.NowMs,
            fixture.Clock.NowMs - 50,
            2400f,
            false);
        AssertEx.True(fixture.Tracker.ApplyActionEffect(ack), "匹配 Ack 应被 Tracker 接收");
        var postAck = fixture.Tracker.GetContextSnapshot() with
        {
            CapturedAtMs = fixture.Clock.NowMs,
        };
        AssertEx.False(postAck.Tracker.HasPendingIssuedAction, "匹配 Ack 应清除通用 Pending");
        AssertEx.True(postAck.Tracker.PendingGaugeReconcile, "Ack 后必须等待 Gauge");
        AssertEx.True(
            fixture.Execution.BeginFrame(postAck, Input(postAck)),
            "Ack 后阻断帧仍应可观察");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, postAck) is null,
            "Gauge 对账前不得交付下一动作");

        fixture.Clock.Advance(16);
        fixture.Tracker.Reconcile(postAck with { CapturedAtMs = fixture.Clock.NowMs });
        var reconciled = fixture.Tracker.GetContextSnapshot();
        AssertEx.False(reconciled.Tracker.PendingGaugeReconcile, "下一 Tick 应完成 Gauge 对账");
        AssertEx.True(
            fixture.Execution.BeginFrame(reconciled, Input(reconciled)),
            "Gauge 后帧应恢复");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, reconciled),
            BLMSkill.炽炎,
            ActionType.Gcd,
            reconciled.TargetEntityId);
    }

    private static void FireEndHoldPrioritizesTranspose()
    {
        var context = FireContext() with
        {
            Mp = 0,
            AstralSoul = 0,
            HasSwiftcast = true,
            SwiftcastRemainSeconds = 10f,
            GcdRemainSeconds = 2f,
        };
        var fixture = CreateFixture(context);
        var previous = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.绝望,
            wasInstant: true);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with { ManafontEnabled = false },
            [Ready(BLMSkill.星灵移位), Ready(BLMSkill.黑魔纹)],
            previous);
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "火末帧应建立");
        AssertEx.True(
            fixture.Execution.GetSnapshot()!.Decision.HoldGcdForTranspose,
            "火末应进入星灵 Hold");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context) is null,
            "Hold 只能阻断主循环冰三");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, fixture.Context) is null,
            "星灵存在时黑魔纹不得从 OffGCD 抢先");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context),
            BLMSkill.星灵移位,
            ActionType.Always,
            0);
    }

    private static void ManafontIsDeliveredAsOffGcd()
    {
        var context = FireContext() with
        {
            Mp = 0,
            AstralSoul = 0,
            GcdRemainSeconds = 2f,
        };
        var fixture = CreateFixture(context);
        var previous = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.绝望,
            wasInstant: true);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with { ManafontEnabled = true },
            [Ready(BLMSkill.魔泉), Ready(BLMSkill.星灵移位)],
            previous);
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "魔泉帧应建立");
        AssertEx.True(
            fixture.Execution.GetSnapshot()!.Decision.AlwaysCandidate is null,
            "魔泉成立时星灵必须让路");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, fixture.Context),
            BLMSkill.魔泉,
            ActionType.OffGcd,
            0);
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, fixture.Context) is null,
            "魔泉 Ack 前不得重复返回");
    }

    private static void HighPriorityAndFrameDriftFailClosed()
    {
        var fixture = CreateFixture(FireContext());
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, Input(fixture.Context)),
            "Gate 测试帧应建立");
        AssertEx.True(
            fixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                fixture.Context,
                highPriorityQueueActive: true) is null,
            "实时高优动作必须阻断生产交付");
        AssertEx.True(
            fixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                fixture.Context with { TargetEntityId = 201 }) is null,
            "目标漂移不得复用旧生产帧");
        AssertEx.True(
            fixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                fixture.Context with { CapturedAtMs = fixture.Context.CapturedAtMs + 1 }) is null,
            "时间戳漂移不得复用旧生产帧");
    }

    private static void OffGcdWaitsForActualWeaveWindow()
    {
        var context = FireContext() with
        {
            Mp = 0,
            AstralSoul = 0,
            IsCasting = true,
            CastRemainSeconds = 1.5f,
            GcdRemainSeconds = 2f,
        };
        var fixture = CreateFixture(context);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with { ManafontEnabled = true },
            [Ready(BLMSkill.魔泉)],
            Success(
                fixture.Context.Tracker.StateGeneration,
                BLMSkill.绝望,
                wasInstant: true));
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "读条中魔泉帧应建立");
        AssertEx.True(
            fixture.Execution.GetSnapshot()!.Decision.OffGcdCandidate?.ActionId
                == BLMSkill.魔泉,
            "测试前提要求 Resolver 已选出魔泉");
        AssertEx.True(
            fixture.Execution.GetSnapshot()!.Decision.AlwaysBridgeCandidate?.ActionId
                == BLMSkill.魔泉,
            "读条中火末必须建立魔泉Always桥");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context) is null,
            "魔泉桥成立时不得提前交付回冰GCD");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, fixture.Context) is null,
            "读条中不得提前向 PR 交付 OffGCD");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context) is null,
            "读条结束前Always桥也不得提前交付魔泉");
        AssertEx.False(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "尚未进入真实 weave 窗口时不得提前注册 Pending");
    }

    private static void ManafontBridgeDeliversAfterHardcast()
    {
        var context = FireContext() with
        {
            Level = 99,
            Mp = 0,
            AstralSoul = 0,
            IsCasting = false,
            CastRemainSeconds = 0f,
            GcdRemainSeconds = 0.3f,
        };
        var fixture = CreateFixture(context);
        var settings = BlmResolverSettings.Default with { ManafontEnabled = true };
        var actions = new[] { Ready(BLMSkill.魔泉) };
        var previousGcd = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.绝望,
            wasInstant: false);
        var input = Input(
            fixture.Context,
            settings,
            actions,
            previousGcd);
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, input),
            "硬读条结束后的魔泉桥帧应建立");
        var decision = fixture.Execution.GetSnapshot()!.Decision;
        AssertEx.True(
            decision.GcdCandidate?.ActionId == BLMSkill.冰封,
            "桥接测试前提要求主循环准备回冰");
        AssertEx.True(
            decision.AlwaysBridgeCandidate?.ActionId == BLMSkill.魔泉,
            "桥接测试前提要求Always桥持有魔泉");
        AssertEx.True(
            decision.GcdBlockedByAlwaysBridge,
            "魔泉交付前必须阻止回冰GCD");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context) is null,
            "魔泉桥成立时GCD入口必须fail closed");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, fixture.Context) is null,
            "0.3秒窗口不得从普通OffGCD入口交付");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context),
            BLMSkill.魔泉,
            ActionType.Always,
            0);
        AssertEx.True(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "Always桥交付后必须建立通用Pending");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context) is null,
            "魔泉Ack前Always桥不得重复交付");

        fixture.Clock.Advance(1);
        fixture.Tracker.Reconcile(fixture.Context with
        {
            CapturedAtMs = fixture.Clock.NowMs,
        });
        var waitingAck = fixture.Tracker.GetContextSnapshot();
        AssertEx.True(
            fixture.Execution.BeginFrame(
                waitingAck,
                Input(waitingAck, settings, actions, previousGcd)),
            "等待魔泉Ack时下一生产帧应建立");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, waitingAck) is null,
            "魔泉Ack前跨帧不得让冰封抢先");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, waitingAck) is null,
            "魔泉Ack前跨帧不得重复交付Always桥");

        fixture.Clock.Advance(100);
        var ack = fixture.Tracker.CreateAckEnvelope(
            fixture.Context.PlayerEntityId,
            BLMSkill.魔泉,
            1,
            BlmPhase.Fire,
            fixture.Clock.NowMs);
        AssertEx.True(fixture.Tracker.ApplyActionEffect(ack), "Always桥魔泉Ack应被Tracker接收");
        var postAck = fixture.Tracker.GetContextSnapshot() with
        {
            CapturedAtMs = fixture.Clock.NowMs,
        };
        AssertEx.False(postAck.Tracker.HasPendingIssuedAction, "魔泉Ack必须清除Always桥Pending");
        AssertEx.True(postAck.Tracker.PendingGaugeReconcile, "魔泉Ack后必须等待Gauge恢复");
        AssertEx.True(
            fixture.Execution.BeginFrame(
                postAck,
                Input(postAck, settings, actions, previousGcd)),
            "魔泉Ack后的阻断帧应可观察");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, postAck) is null,
            "Manafont Gauge对账前不得交付冰封");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, postAck) is null,
            "Manafont Gauge对账前不得重复交付Always桥");

        fixture.Clock.Advance(16);
        fixture.Tracker.Reconcile(postAck with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            Mp = postAck.MaxMp,
            AfStacks = 3,
            IceStacks = 0,
            UmbralHearts = 3,
            HasParadox = true,
            HasThunderhead = true,
        });
        var restored = fixture.Tracker.GetContextSnapshot();
        AssertEx.False(restored.Tracker.PendingGaugeReconcile, "资源恢复后必须完成Manafont Gauge对账");
        AssertEx.True(restored.Tracker.ManafontActiveThisFire, "Gauge确认后必须激活Manafont火段事实");
        AssertEx.Equal(0, restored.Tracker.Fire4CountSinceManafont, "Manafont后火四计数必须从零开始");
        AssertEx.True(
            fixture.Execution.BeginFrame(
                restored,
                Input(
                    restored,
                    settings,
                    [Ready(BLMSkill.炽炎)],
                    previousGcd)),
            "Manafont Gauge恢复后的续火帧应建立");
        AssertEx.Equal(
            BLMSkill.炽炎,
            fixture.Execution.GetSnapshot()!.Decision.GcdCandidate!.ActionId,
            "Manafont Gauge恢复后必须产生炽炎候选");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, restored),
            BLMSkill.炽炎,
            ActionType.Gcd,
            restored.TargetEntityId);
    }

    private static void TargetSwitchCancelsOldPending()
    {
        var fixture = CreateFixture(FireContext());
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, Input(fixture.Context)),
            "换目标测试首帧应建立");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context),
            BLMSkill.炽炎,
            ActionType.Gcd,
            fixture.Context.TargetEntityId);

        fixture.Clock.Advance(1);
        fixture.Tracker.Reconcile(fixture.Context with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            TargetEntityId = 201,
        });
        var switched = fixture.Tracker.GetContextSnapshot();
        AssertEx.True(
            fixture.Execution.BeginFrame(switched, Input(switched)),
            "换目标后的新帧应建立");
        AssertEx.False(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "目标切换必须放弃旧目标的 Pending");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, switched),
            BLMSkill.炽炎,
            ActionType.Gcd,
            switched.TargetEntityId);
    }

    private static void TargetSwitchPreservesSelfAbilityPending()
    {
        var context = TestContext.Base() with
        {
            IsAoeMode = true,
            EnemyCount = 3,
            AoeTargetId = 200,
            AoeTargetCanUseAttack = true,
            AoeTargetHitCount = 3,
            AoeTargetIsCurrentTarget = true,
            GcdRemainSeconds = 0.7f,
        };
        var fixture = CreateFixture(context);
        var previous = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.高冰冻,
            wasInstant: true);
        var settings = BlmResolverSettings.Default with
        {
            TtkEnabled = true,
            ManafontEnabled = false,
            DotEnabled = false,
            AmplifierEnabled = false,
            LeyLinesEnabled = false,
            AutoMitigationEnabled = false,
        };
        var input = Input(
            fixture.Context,
            settings,
            [Ready(MageUniversalSkill.即刻咏唱)],
            previous);
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, input),
            "即刻Self Pending首帧应建立");
        AssertAction(
            fixture.Execution.Resolve(
                BlmResolverChannel.OffGcd,
                fixture.Context),
            MageUniversalSkill.即刻咏唱,
            ActionType.OffGcd,
            0);

        fixture.Clock.Advance(1);
        var switched = fixture.Context with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            TargetEntityId = 300,
        };
        var switchedInput = Input(
            switched,
            settings,
            [Ready(MageUniversalSkill.即刻咏唱)],
            previous);
        AssertEx.True(
            fixture.Execution.BeginFrame(switched, switchedInput),
            "主目标切换后即刻帧必须重建");
        AssertEx.True(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "主目标切换不得取消Self即刻Pending");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, switched) is null,
            "主目标切换后即刻Pending必须阻止Ack前重复交付");
    }

    private static void ManualRecoveryAlwaysWorksWithoutPreviousGcd()
    {
        var context = TestContext.Base() with
        {
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 3,
            UmbralHearts = 3,
            HasParadox = false,
            GcdRemainSeconds = 0f,
        };
        var fixture = CreateFixture(context);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default,
            [Ready(BLMSkill.星灵移位)]);
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "异常恢复帧应建立");
        AssertEx.Equal(
            0,
            fixture.Execution.GetSnapshot()!.Decision.RemainingWeaves,
            "无已确认 GCD 时容量应保守为0");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context),
            BLMSkill.星灵移位,
            ActionType.Always,
            0);
    }

    private static void IceParadoxTriplecastBlizzard3SequenceIsDeliverable()
    {
        var paradoxContext = TestContext.Base() with
        {
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 1,
            UmbralHearts = 3,
            Mp = 0,
            HasParadox = true,
            GcdRemainSeconds = 0.2f,
        };
        var paradoxFixture = CreateFixture(paradoxContext);
        var beforeParadox = Success(
            paradoxFixture.Context.Tracker.StateGeneration,
            BLMSkill.绝望,
            wasInstant: true);
        var paradoxInput = Input(
            paradoxFixture.Context,
            BlmResolverSettings.Default,
            [Ready(BLMSkill.三连咏唱)],
            beforeParadox);
        AssertEx.True(
            paradoxFixture.Execution.BeginFrame(paradoxFixture.Context, paradoxInput),
            "UI1 悖论帧应建立");
        AssertAction(
            paradoxFixture.Execution.Resolve(BlmResolverChannel.Gcd, paradoxFixture.Context),
            BLMSkill.悖论,
            ActionType.Gcd,
            paradoxFixture.Context.TargetEntityId);

        var tripleContext = paradoxContext with { HasParadox = false, GcdRemainSeconds = 2f };
        var tripleFixture = CreateFixture(tripleContext);
        var afterParadox = Success(
            tripleFixture.Context.Tracker.StateGeneration,
            BLMSkill.悖论,
            wasInstant: true);
        var tripleInput = Input(
            tripleFixture.Context,
            BlmResolverSettings.Default,
            [Ready(BLMSkill.三连咏唱)],
            afterParadox);
        AssertEx.True(tripleFixture.Execution.BeginFrame(tripleFixture.Context, tripleInput), "三连帧应建立");
        AssertAction(
            tripleFixture.Execution.Resolve(BlmResolverChannel.OffGcd, tripleFixture.Context),
            BLMSkill.三连咏唱,
            ActionType.OffGcd,
            0);

        var blizzardContext = tripleContext with
        {
            TriplecastStacks = 3,
            TriplecastRemainSeconds = 15f,
            GcdRemainSeconds = 0.2f,
        };
        var blizzardFixture = CreateFixture(blizzardContext);
        var blizzardInput = Input(
            blizzardFixture.Context,
            BlmResolverSettings.Default,
            [Unavailable(BLMSkill.三连咏唱)],
            Success(
                blizzardFixture.Context.Tracker.StateGeneration,
                BLMSkill.悖论,
                wasInstant: true));
        AssertEx.True(
            blizzardFixture.Execution.BeginFrame(blizzardFixture.Context, blizzardInput),
            "三连 Buff 后帧应建立");
        AssertAction(
            blizzardFixture.Execution.Resolve(BlmResolverChannel.Gcd, blizzardFixture.Context),
            BLMSkill.冰封,
            ActionType.Gcd,
            blizzardFixture.Context.TargetEntityId);
    }

    private static void Level90CandidateIsDeliveredInProduction()
    {
        var fixture = CreateFixture(FireContext() with { Level = 90 });
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with
            {
                ManafontEnabled = false,
                DotEnabled = false,
                MoveXenoglossyEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(BLMSkill.炽炎)]);
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, input),
            "90级生产帧应建立");
        var candidate = fixture.Execution.GetSnapshot()!.Decision.GcdCandidate;
        AssertEx.True(candidate is not null, "90级生产帧必须产生GCD候选");
        AssertEx.Equal(BLMSkill.炽炎, candidate!.ActionId, "90级生产候选动作错误");
        AssertEx.Equal("GCD.单体90_99", candidate.ResolverId, "90级生产候选Resolver错误");
        AssertEx.Equal(16, candidate.ManifestOrder, "90级生产候选manifest顺序错误");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context),
            BLMSkill.炽炎,
            ActionType.Gcd,
            fixture.Context.TargetEntityId);
    }

    private static void Level1To89CandidatesAreDeliveredInProduction()
    {
        var boundaries = new[]
        {
            (Level: 1, Action: BLMSkill.火炎, Resolver: "GCD.单体1_34", Order: 20),
            (Level: 34, Action: BLMSkill.火炎, Resolver: "GCD.单体1_34", Order: 20),
            (Level: 35, Action: BLMSkill.火炎, Resolver: "GCD.单体35_59", Order: 19),
            (Level: 59, Action: BLMSkill.火炎, Resolver: "GCD.单体35_59", Order: 19),
            (Level: 60, Action: BLMSkill.炽炎, Resolver: "GCD.单体60_71", Order: 18),
            (Level: 71, Action: BLMSkill.炽炎, Resolver: "GCD.单体60_71", Order: 18),
            (Level: 72, Action: BLMSkill.炽炎, Resolver: "GCD.单体72_89", Order: 17),
            (Level: 89, Action: BLMSkill.炽炎, Resolver: "GCD.单体72_89", Order: 17),
        };
        foreach (var boundary in boundaries)
        {
            var fixture = CreateFixture(FireContext() with { Level = boundary.Level });
            var input = Input(
                fixture.Context,
                BlmResolverSettings.Default with
                {
                    ManafontEnabled = false,
                    DotEnabled = false,
                    MoveXenoglossyEnabled = false,
                    AmplifierEnabled = false,
                    LeyLinesEnabled = false,
                    AutoMitigationEnabled = false,
                },
                [Ready(boundary.Action)]);
            AssertEx.True(
                fixture.Execution.BeginFrame(fixture.Context, input),
                $"{boundary.Level}级生产帧应建立");
            var candidate = fixture.Execution.GetSnapshot()!.Decision.GcdCandidate;
            AssertEx.True(candidate is not null, $"{boundary.Level}级生产帧必须产生GCD候选");
            AssertEx.Equal(boundary.Action, candidate!.ActionId, $"{boundary.Level}级生产候选动作错误");
            AssertEx.Equal(boundary.Resolver, candidate.ResolverId, $"{boundary.Level}级生产候选Resolver错误");
            AssertEx.Equal(boundary.Order, candidate.ManifestOrder, $"{boundary.Level}级生产候选manifest顺序错误");
            AssertAction(
                fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context),
                boundary.Action,
                ActionType.Gcd,
                fixture.Context.TargetEntityId);
        }
    }

    private static void Level60IceAckAheadOfGaugeDeliversBlizzardFour()
    {
        var context = TestContext.Base() with
        {
            Level = 60,
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 1,
            UmbralHearts = 2,
            GcdRemainSeconds = 0.2f,
        };
        var fixture = CreateFixture(context);
        var previous = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.冰封,
            wasInstant: false);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with
            {
                ManafontEnabled = false,
                DotEnabled = false,
                MoveXenoglossyEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(BLMSkill.冰澈)],
            previous);
        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, input),
            "60级冰封Ack领先Gauge生产帧应建立");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context),
            BLMSkill.冰澈,
            ActionType.Gcd,
            fixture.Context.TargetEntityId);
    }

    private static void AoeSharedAbilitiesAreDeliveredWithoutGcdLeak()
    {
        var transposeContext = TestContext.Base() with
        {
            IsAoeMode = true,
            EnemyCount = 3,
            AoeTargetId = 200,
            AoeTargetCanUseAttack = true,
            AoeTargetHitCount = 3,
            AoeTargetIsCurrentTarget = true,
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 1,
            UmbralHearts = 3,
            GcdRemainSeconds = 0.2f,
        };
        var transposeFixture = CreateFixture(transposeContext);
        var transposeInput = Input(
            transposeFixture.Context,
            BlmResolverSettings.Default with
            {
                ManafontEnabled = false,
                DotEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(BLMSkill.星灵移位), Ready(BLMSkill.玄冰)]);
        AssertEx.True(
            transposeFixture.Execution.BeginFrame(
                transposeFixture.Context,
                transposeInput),
            "AOE星灵生产帧应建立");
        AssertEx.True(
            transposeFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                transposeFixture.Context) is null,
            "UI1加三冰针不得重复玄冰");
        AssertAction(
            transposeFixture.Execution.Resolve(
                BlmResolverChannel.Always,
                transposeFixture.Context),
            BLMSkill.星灵移位,
            ActionType.Always,
            0);

        var completeIceContext = transposeContext with { IceStacks = 3 };
        var completeIceFixture = CreateFixture(completeIceContext);
        var completeIceInput = Input(
            completeIceFixture.Context,
            transposeInput.Settings,
            [Ready(BLMSkill.星灵移位)]);
        AssertEx.True(
            completeIceFixture.Execution.BeginFrame(
                completeIceFixture.Context,
                completeIceInput),
            "AOE完整冰相星灵生产帧应建立");
        AssertAction(
            completeIceFixture.Execution.Resolve(
                BlmResolverChannel.Always,
                completeIceFixture.Context),
            BLMSkill.星灵移位,
            ActionType.Always,
            0);

        var swiftContext = TestContext.Base() with
        {
            IsAoeMode = true,
            EnemyCount = 3,
            Level = 100,
            Phase = BlmPhase.Fire,
            GcdRemainSeconds = 1.5f,
        };
        var swiftFixture = CreateFixture(swiftContext);
        var swiftPrevious = Success(
            swiftFixture.Context.Tracker.StateGeneration,
            BLMSkill.爆炎,
            wasInstant: true);
        var swiftInput = Input(
            swiftFixture.Context,
            BlmResolverSettings.Default with
            {
                TtkEnabled = true,
                ManafontEnabled = false,
                DotEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(MageUniversalSkill.即刻咏唱)],
            swiftPrevious);
        AssertEx.True(
            swiftFixture.Execution.BeginFrame(swiftFixture.Context, swiftInput),
            "AOE即刻生产帧应建立");
        AssertAction(
            swiftFixture.Execution.Resolve(
                BlmResolverChannel.OffGcd,
                swiftFixture.Context),
            MageUniversalSkill.即刻咏唱,
            ActionType.OffGcd,
            0);

        var tripleContext = swiftContext with { Level = 100 };
        var tripleFixture = CreateFixture(tripleContext);
        var triplePrevious = Success(
            tripleFixture.Context.Tracker.StateGeneration,
            BLMSkill.爆炎,
            wasInstant: true);
        var tripleInput = Input(
            tripleFixture.Context,
            BlmResolverSettings.Default with
            {
                TtkEnabled = true,
                ManafontEnabled = false,
                DotEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(BLMSkill.三连咏唱)],
            triplePrevious);
        AssertEx.True(
            tripleFixture.Execution.BeginFrame(tripleFixture.Context, tripleInput),
            "AOE三连生产帧应建立");
        AssertEx.True(
            tripleFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                tripleFixture.Context) is null,
            "AOE三连生产帧不得泄漏GCD");
        AssertAction(
            tripleFixture.Execution.Resolve(
                BlmResolverChannel.OffGcd,
                tripleFixture.Context),
            BLMSkill.三连咏唱,
            ActionType.OffGcd,
            0);
        AssertEx.True(
            tripleFixture.Execution.Resolve(
                BlmResolverChannel.OffGcd,
                tripleFixture.Context) is null,
            "AOE三连Pending期间不得重复交付");

        var manafontContext = swiftContext with
        {
            Mp = 799,
            AfStacks = 3,
            UmbralHearts = 0,
            AstralSoul = 0,
            GcdRemainSeconds = 1.5f,
        };
        var manafontFixture = CreateFixture(manafontContext);
        var manafontPrevious = Success(
            manafontFixture.Context.Tracker.StateGeneration,
            BLMSkill.耀星,
            wasInstant: true);
        var manafontInput = Input(
            manafontFixture.Context,
            BlmResolverSettings.Default with
            {
                ManafontEnabled = true,
                DotEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(BLMSkill.魔泉)],
            manafontPrevious);
        AssertEx.True(
            manafontFixture.Execution.BeginFrame(
                manafontFixture.Context,
                manafontInput),
            "AOE Manafont生产帧应建立");
        AssertEx.True(
            manafontFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                manafontFixture.Context) is null,
            "AOE Manafont生产帧不得泄漏GCD");
        AssertAction(
            manafontFixture.Execution.Resolve(
                BlmResolverChannel.OffGcd,
                manafontFixture.Context),
            BLMSkill.魔泉,
            ActionType.OffGcd,
            0);
        AssertEx.True(
            manafontFixture.Execution.Resolve(
                BlmResolverChannel.OffGcd,
                manafontFixture.Context) is null,
            "AOE Manafont Pending期间不得重复交付");
    }

    private static void AoePendingCancellationFollowsDeliveryTargetContract()
    {
        var context = TestContext.Base() with
        {
            IsAoeMode = true,
            EnemyCount = 3,
            AoeTargetId = 201,
            AoeTargetCanUseAttack = true,
            AoeTargetHitCount = 3,
            AoeTargetIsCurrentTarget = false,
            Level = 100,
            Phase = BlmPhase.Neutral,
            GcdRemainSeconds = 0.2f,
        };
        var fixture = CreateFixture(context);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with
            {
                ManafontEnabled = false,
                DotEnabled = false,
                MoveXenoglossyEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(BLMSkill.冰冻)]);
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "AOE指定中心生产帧应建立");
        var initialFrameSequence = fixture.Execution.GetSnapshot()!.FrameSequence;
        var candidate = fixture.Execution.GetSnapshot()!.Decision.GcdCandidate;
        AssertEx.True(candidate is not null, "AOE指定中心必须产生GCD候选");
        AssertEx.Equal(BlmResolverTargetKind.SpecifiedTarget, candidate!.TargetKind, "AOE指定中心TargetKind错误");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context),
            BLMSkill.冰冻,
            ActionType.Gcd,
            201);
        AssertEx.True(fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction, "AOE指定中心交付后必须建立Pending");

        fixture.Clock.Advance(1);
        var drifted = fixture.Context with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            AoeTargetId = 202,
        };
        var driftedInput = Input(
            drifted,
            input.Settings,
            [Ready(BLMSkill.冰冻)]);
        AssertEx.True(fixture.Execution.BeginFrame(drifted, driftedInput), "AOE中心漂移后必须建立新帧");
        AssertEx.True(
            fixture.Execution.GetSnapshot()!.FrameSequence > initialFrameSequence,
            "AOE中心漂移必须重建DecisionFrame");
        AssertEx.False(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "AOE中心漂移必须取消已签发SpecifiedTarget GCD Pending");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, drifted),
            BLMSkill.冰冻,
            ActionType.Gcd,
            202);

        fixture.Clock.Advance(1);
        var singleTarget = drifted with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            IsAoeMode = false,
            EnemyCount = 1,
            AoeTargetId = 0,
            AoeTargetCanUseAttack = false,
            AoeTargetHitCount = 0,
            AoeTargetIsCurrentTarget = false,
        };
        var singleInput = Input(
            singleTarget,
            input.Settings,
            [Ready(BLMSkill.冰封)]);
        AssertEx.True(
            fixture.Execution.BeginFrame(singleTarget, singleInput),
            "AOE切回单体必须建立无残留新帧");
        AssertEx.False(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "AOE切回单体必须取消已签发GCD Pending");

        var geometryFixture = CreateFixture(context);
        var geometryInput = Input(
            geometryFixture.Context,
            input.Settings,
            [Ready(BLMSkill.冰冻)]);
        AssertEx.True(
            geometryFixture.Execution.BeginFrame(
                geometryFixture.Context,
                geometryInput),
            "AOE几何漂移首帧应建立");
        AssertAction(
            geometryFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                geometryFixture.Context),
            BLMSkill.冰冻,
            ActionType.Gcd,
            201);
        geometryFixture.Clock.Advance(1);
        var geometryDrifted = geometryFixture.Context with
        {
            CapturedAtMs = geometryFixture.Clock.NowMs,
            EnemyCount = 2,
            AoeTargetHitCount = 2,
        };
        var geometryDriftedInput = Input(
            geometryDrifted,
            geometryInput.Settings,
            [Ready(BLMSkill.冰冻)]);
        AssertEx.True(
            geometryFixture.Execution.BeginFrame(
                geometryDrifted,
                geometryDriftedInput),
            "EnemyCount/HitCount漂移必须重建DecisionFrame");
        AssertEx.True(
            geometryFixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "纯AOE几何漂移不得取消GCD Pending");
        AssertEx.True(
            geometryFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                geometryDrifted) is null,
            "纯AOE几何漂移后旧Pending必须继续阻止重复签发");

        var currentTargetGcdContext = context with
        {
            EnemyCount = 2,
            AoeTargetHitCount = 2,
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = 3,
            UmbralHearts = 2,
        };
        var currentTargetGcdFixture = CreateFixture(currentTargetGcdContext);
        var currentTargetGcdInput = Input(
            currentTargetGcdFixture.Context,
            input.Settings,
            [Ready(BLMSkill.冰澈)]);
        AssertEx.True(
            currentTargetGcdFixture.Execution.BeginFrame(
                currentTargetGcdFixture.Context,
                currentTargetGcdInput),
            "AOE冰澈首帧应建立");
        AssertAction(
            currentTargetGcdFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                currentTargetGcdFixture.Context),
            BLMSkill.冰澈,
            ActionType.Gcd,
            currentTargetGcdFixture.Context.TargetEntityId);
        currentTargetGcdFixture.Clock.Advance(1);
        var currentTargetGcdDrifted = currentTargetGcdFixture.Context with
        {
            CapturedAtMs = currentTargetGcdFixture.Clock.NowMs,
            AoeTargetId = 202,
        };
        var currentTargetGcdDriftedInput = Input(
            currentTargetGcdDrifted,
            currentTargetGcdInput.Settings,
            [Ready(BLMSkill.冰澈)]);
        AssertEx.True(
            currentTargetGcdFixture.Execution.BeginFrame(
                currentTargetGcdDrifted,
                currentTargetGcdDriftedInput),
            "AOE中心漂移后冰澈帧必须重建");
        AssertEx.True(
            currentTargetGcdFixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "AOE中心漂移不得取消当前主目标冰澈Pending");
        AssertEx.True(
            currentTargetGcdFixture.Execution.Resolve(
                BlmResolverChannel.Gcd,
                currentTargetGcdDrifted) is null,
            "AOE中心漂移后冰澈Pending必须继续阻止重复签发");
    }

    private static void AoeManafontAlwaysBridgeIsSingleDelivery()
    {
        var context = TestContext.Base() with
        {
            IsAoeMode = true,
            EnemyCount = 3,
            AoeTargetId = 200,
            AoeTargetCanUseAttack = true,
            AoeTargetHitCount = 3,
            AoeTargetIsCurrentTarget = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            IceStacks = 0,
            UmbralHearts = 0,
            AstralSoul = 0,
            Mp = 799,
            IsCasting = false,
            AnimationLockSeconds = 0f,
            GcdRemainSeconds = 0.499f,
        };
        var fixture = CreateFixture(context);
        var previous = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.耀星,
            wasInstant: true);
        var input = Input(
            fixture.Context,
            BlmResolverSettings.Default with
            {
                ManafontEnabled = true,
                TtkEnabled = true,
                DotEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
            },
            [Ready(MageUniversalSkill.即刻咏唱), Ready(BLMSkill.魔泉)],
            previous);
        AssertEx.Equal(
            MageUniversalSkill.即刻咏唱,
            Level100ResolverEngine.Evaluate(input with
            {
                Actions =
                [
                    Ready(MageUniversalSkill.即刻咏唱),
                    Unavailable(BLMSkill.魔泉),
                ],
            }).OffGcdCandidate!.ActionId,
            "Manafont不可用时0.499秒竞争前提必须由即刻获胜");
        AssertEx.True(fixture.Execution.BeginFrame(fixture.Context, input), "AOE Manafont桥生产帧应建立");
        var decision = fixture.Execution.GetSnapshot()!.Decision;
        AssertEx.True(
            decision.AlwaysBridgeCandidate?.ActionId == BLMSkill.魔泉,
            "更早即刻候选存在时AOE Manafont仍必须独立建立Always桥");
        AssertEx.True(
            decision.OffGcdCandidate?.ActionId == BLMSkill.魔泉,
            "AOE Manafont Always桥成立时必须覆盖更早OffGCD候选");
        AssertEx.True(decision.GcdCandidate is null, "AOE Manafont Ack/Gauge前不得交付换相GCD");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context),
            BLMSkill.魔泉,
            ActionType.Always,
            0);
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context) is null,
            "AOE Manafont Always桥不得重复交付");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Gcd, fixture.Context) is null,
            "AOE Manafont Ack/Gauge前不得转冰");

        fixture.Clock.Advance(1);
        var drifted = fixture.Context with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            AoeTargetId = 201,
            AoeTargetIsCurrentTarget = false,
        };
        var driftedInput = Input(
            drifted,
            input.Settings,
            [Ready(MageUniversalSkill.即刻咏唱), Ready(BLMSkill.魔泉)],
            previous);
        AssertEx.True(
            fixture.Execution.BeginFrame(drifted, driftedInput),
            "AOE中心漂移后Manafont帧必须重建");
        AssertEx.True(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "AOE中心漂移不得取消Self Manafont Pending");
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, drifted) is null,
            "AOE中心漂移后Manafont Pending必须阻止重复Always交付");

        var driftedFrameSequence = fixture.Execution.GetSnapshot()!.FrameSequence;
        fixture.Clock.Advance(1);
        var switchedPrimaryTarget = drifted with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            TargetEntityId = 300,
        };
        var switchedPrimaryTargetInput = Input(
            switchedPrimaryTarget,
            input.Settings,
            [Ready(MageUniversalSkill.即刻咏唱), Ready(BLMSkill.魔泉)],
            previous);
        AssertEx.True(
            fixture.Execution.BeginFrame(
                switchedPrimaryTarget,
                switchedPrimaryTargetInput),
            "主目标切换后Manafont帧必须重建");
        AssertEx.True(
            fixture.Execution.GetSnapshot()!.FrameSequence > driftedFrameSequence,
            "主目标切换必须产生新DecisionFrame");
        AssertEx.True(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "主目标切换不得取消Self Manafont Pending");
        AssertEx.True(
            fixture.Execution.Resolve(
                BlmResolverChannel.Always,
                switchedPrimaryTarget) is null,
            "主目标切换后Manafont Pending必须阻止Ack前重复Always交付");

        foreach (var competitor in new[]
        {
            (ActionId: BLMSkill.三连咏唱, GcdRemain: 0.5f, Name: "三连"),
            (ActionId: MageUniversalSkill.醒梦, GcdRemain: 0.6f, Name: "醒梦"),
            (ActionId: BLMSkill.详述, GcdRemain: 0.5f, Name: "详述"),
        })
        {
            AssertAoeManafontBridgeOverridesCompetitor(
                competitor.ActionId,
                competitor.GcdRemain,
                competitor.Name);
        }
    }

    private static void AssertAoeManafontBridgeOverridesCompetitor(
        uint competitorActionId,
        float gcdRemainSeconds,
        string scenario)
    {
        var context = TestContext.Base() with
        {
            IsAoeMode = true,
            EnemyCount = 3,
            AoeTargetId = 200,
            AoeTargetCanUseAttack = true,
            AoeTargetHitCount = 3,
            AoeTargetIsCurrentTarget = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            IceStacks = 0,
            UmbralHearts = 0,
            AstralSoul = 0,
            Mp = 799,
            IsCasting = false,
            AnimationLockSeconds = 0f,
            GcdRemainSeconds = gcdRemainSeconds,
        };
        var fixture = CreateFixture(context);
        var previous = Success(
            fixture.Context.Tracker.StateGeneration,
            BLMSkill.核爆,
            wasInstant: false);
        var isAmplifier = competitorActionId == BLMSkill.详述;
        var settings = BlmResolverSettings.Default with
        {
            ManafontEnabled = true,
            TtkEnabled = !isAmplifier,
            DotEnabled = false,
            AmplifierEnabled = isAmplifier,
            LeyLinesEnabled = false,
            AutoMitigationEnabled = false,
        };
        var input = Input(
            fixture.Context,
            settings,
            [Ready(competitorActionId), Ready(BLMSkill.魔泉)],
            previous);
        var competitorOnly = Level100ResolverEngine.Evaluate(input with
        {
            Actions =
            [
                Ready(competitorActionId),
                Unavailable(BLMSkill.魔泉),
            ],
        });
        AssertEx.Equal(
            competitorActionId,
            competitorOnly.OffGcdCandidate!.ActionId,
            $"Manafont不可用时{scenario}竞争前提错误");

        AssertEx.True(
            fixture.Execution.BeginFrame(fixture.Context, input),
            $"{scenario}竞争Manafont帧应建立");
        var decision = fixture.Execution.GetSnapshot()!.Decision;
        AssertEx.Equal(
            BLMSkill.魔泉,
            decision.AlwaysBridgeCandidate!.ActionId,
            $"{scenario}竞争时Manafont Always桥错误");
        AssertEx.Equal(
            BLMSkill.魔泉,
            decision.OffGcdCandidate!.ActionId,
            $"{scenario}竞争时Manafont必须覆盖普通OffGCD");
        AssertAction(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context),
            BLMSkill.魔泉,
            ActionType.Always,
            0);
        AssertEx.True(
            fixture.Execution.Resolve(BlmResolverChannel.Always, fixture.Context) is null,
            $"{scenario}竞争时Manafont Always只能交付一次");
    }

    private static Fixture CreateFixture(BlmContext context)
    {
        var clock = new FakeClock(context.CapturedAtMs);
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        var captured = tracker.GetContextSnapshot();
        return new Fixture(
            clock,
            tracker,
            new BlmResolverExecutionService(tracker),
            captured);
    }

    private static BlmContext FireContext() => TestContext.Base() with
    {
        Phase = BlmPhase.Fire,
        AfStacks = 3,
        IceStacks = 0,
        UmbralHearts = 0,
        AstralSoul = 0,
        HasParadox = false,
        HasFirestarter = false,
        PolyglotStacks = 0,
        GcdRemainSeconds = 0.2f,
    };

    private static BlmContext NextFrame(Fixture fixture, long advanceMs)
    {
        fixture.Clock.Advance(advanceMs);
        return fixture.Tracker.GetContextSnapshot() with
        {
            CapturedAtMs = fixture.Clock.NowMs,
        };
    }

    private static BlmResolverInput Input(
        BlmContext context,
        BlmResolverSettings? settings = null,
        IEnumerable<BlmResolverActionFact>? actions = null,
        BlmActionSuccess? previousGcd = null)
    {
        var generation = context.Tracker.StateGeneration;
        var history = previousGcd is { } previous
            ? ImmutableArray.Create(previous)
            : ImmutableArray<BlmActionSuccess>.Empty;
        return new BlmResolverInput
        {
            StateGeneration = generation,
            Context = new BlmResolverContextFacts
            {
                CapturedAtMs = context.CapturedAtMs,
                IsAvailable = context.IsAvailable,
                AcrEnabled = context.AcrState == AcrState.On,
                PlayerEntityId = context.PlayerEntityId,
                Level = context.Level,
                Mp = context.Mp,
                MaxMp = context.MaxMp,
                InCombat = context.InCombat,
                IsAlive = context.IsAlive,
                CanAct = context.CanAct,
                IsMoving = context.IsMoving,
                IsCasting = context.IsCasting,
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
                AnimationLockSeconds = context.AnimationLockSeconds,
                Phase = context.Phase,
                AstralFireStacks = context.AfStacks,
                UmbralIceStacks = context.IceStacks,
                UmbralHearts = context.UmbralHearts,
                AstralSoulStacks = context.AstralSoul,
                HasParadox = context.HasParadox,
                HasFirestarter = context.HasFirestarter,
                HasThunderhead = context.HasThunderhead,
                PolyglotStacks = context.PolyglotStacks,
                MaxPolyglotStacks = context.MaxPolyglot,
                PolyglotTimerMs = context.PolyglotTimerMs,
                HasSwiftcast = context.HasSwiftcast,
                TriplecastStacks = context.TriplecastStacks,
            },
            Settings = settings ?? BlmResolverSettings.Default,
            Actions = actions?.ToImmutableArray() ?? [],
            RecentHistory = history,
            PreviousGcd = previousGcd,
            UsedWeaves = 0,
            Level100Loop = new BlmLevel100LoopFacts
            {
                Fire4Count = context.AstralSoul,
                FireParadoxUsed = false,
            },
            PendingGaugeReconcile = context.Tracker.PendingGaugeReconcile,
            DotTargets =
            [
                new BlmResolverDotTargetFact
                {
                    EntityId = context.TargetEntityId,
                    IsValid = context.HasValidTarget,
                    IsTargetable = context.HasValidTarget,
                    IsAlive = true,
                    CanUseAttackActionOn = context.HasValidTarget,
                    IsInDotRange = true,
                    CurrentHp = context.TargetHp,
                    MaxHp = context.TargetMaxHp,
                    SingleTargetDotRemainingMs = context.SingleTargetDot.RemainingMs,
                    AoeDotRemainingMs = context.AoeDot.RemainingMs,
                },
            ],
            FactCoverage = BlmResolverFactCoverage.Phase3A,
        };
    }

    private static BlmResolverActionFact Ready(uint actionId) => new()
    {
        RequestedActionId = actionId,
        AdjustedActionId = actionId,
        IsUnlocked = true,
        CanCast = true,
        Charges = 1f,
        MaxCharges = 1,
        CooldownRemainMs = 0d,
    };

    private static BlmResolverActionFact Unavailable(uint actionId) => new()
    {
        RequestedActionId = actionId,
        AdjustedActionId = actionId,
        IsUnlocked = true,
        CanCast = false,
        Charges = 0f,
        MaxCharges = 1,
        CooldownRemainMs = 10_000d,
    };

    private static BlmActionSuccess Success(
        long generation,
        uint actionId,
        bool wasInstant)
        => new(
            generation,
            1,
            actionId,
            actionId,
            actionId,
            1,
            900,
            950,
            wasInstant,
            true);

    private static void AssertAction(
        PAction? action,
        uint expectedActionId,
        ActionType expectedType,
        uint expectedTargetId)
    {
        AssertEx.True(action is not null, $"应返回动作 {expectedActionId}");
        AssertEx.Equal(expectedActionId, action!.ActionId, "PAction ActionId 错误");
        AssertEx.Equal(expectedType, action.Type, "PAction Type 错误");
        AssertEx.Equal(expectedTargetId, action.NetworkTid, "PAction 目标错误");
    }

    private sealed record Fixture(
        FakeClock Clock,
        BlmStateTracker Tracker,
        BlmResolverExecutionService Execution,
        BlmContext Context);
}
