using LosPr.BLM.BossFlight;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using PromeRotation.Rotation;
using PromeRotation.Data;

namespace Los.Tests;

internal static class BossFlightTests
{
    public static void RunAll()
    {
        TargetLossWaitsForCombatThreshold();
        FireStateReturnsSelfTargetedTranspose();
        IceStateStopsAfterResourcesOrThreeSoulCasts();
        TargetRecoveryLeavesPreparationState();
        NoTargetCancellationPreservesSelfPending();
    }

    private static void TargetLossWaitsForCombatThreshold()
    {
        var clock = new FakeClock(1);
        var context = CombatContext(clock.NowMs) with
        {
            Phase = BlmPhase.Fire,
            Transpose = TestContext.ReadyAction(BLMSkill.星灵移位),
        };
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        var service = new BlmBossFlightService(tracker, clock);
        service.OnBattleStarted(context);

        var earlyNoTarget = NoTarget(context, 9_999);
        service.ObserveContext(earlyNoTarget);
        service.MarkNoTarget(earlyNoTarget);

        AssertEx.Equal(BlmBossFlightState.Armed, service.State, "10秒前目标丢失不得进入准备");
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Always, earlyNoTarget) is null,
            "10秒前不得返回 Boss上天动作");
    }

    private static void FireStateReturnsSelfTargetedTranspose()
    {
        var clock = new FakeClock(1);
        var context = CombatContext(clock.NowMs) with
        {
            Phase = BlmPhase.Fire,
            IceStacks = 0,
            Transpose = TestContext.ReadyAction(BLMSkill.星灵移位),
        };
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        var service = new BlmBossFlightService(tracker, clock);
        service.OnBattleStarted(context);

        var noTarget = NoTarget(context, 10_001);
        service.ObserveContext(noTarget);
        service.MarkNoTarget(noTarget);
        var action = service.Resolve(BlmResolverChannel.Always, noTarget);

        AssertEx.True(action is not null, "火状态目标丢失应返回星灵移位");
        AssertEx.Equal(BLMSkill.星灵移位, action!.ActionId, "Boss上天星灵移位动作错误");
        AssertEx.Equal(ActionType.Always, action.Type, "星灵移位必须走 Always 通道");
        AssertEx.Equal(0u, action.NetworkTid, "星灵移位必须以自身为目标");
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Always, noTarget) is null,
            "未收到 Ack 前不得重复返回星灵移位");
    }

    private static void IceStateStopsAfterResourcesOrThreeSoulCasts()
    {
        var clock = new FakeClock(1);
        var context = CombatContext(clock.NowMs) with
        {
            Phase = BlmPhase.Ice,
            IceStacks = 1,
            UmbralHearts = 1,
            Mp = 5_000,
            UmbralSoul = TestContext.ReadyAction(BLMSkill.灵极魂),
        };
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        var service = new BlmBossFlightService(tracker, clock);
        service.OnBattleStarted(context);
        var noTarget = NoTarget(context, 10_001) with { GcdRemainSeconds = 0f };
        service.ObserveContext(noTarget);
        service.MarkNoTarget(noTarget);

        for (var sequence = 1u; sequence <= 3; sequence++)
        {
            var action = service.Resolve(BlmResolverChannel.Gcd, noTarget);
            AssertEx.True(
                action is not null,
                $"第 {sequence} 次应返回灵极魂，State={service.State}，Pending={tracker.GetTrackerSnapshot().HasPendingIssuedAction}，Soul={service.SoulCastCount}");
            AssertEx.Equal(BLMSkill.灵极魂, action!.ActionId, "灵极魂动作错误");
            AssertEx.Equal(ActionType.Gcd, action.Type, "灵极魂必须走 GCD 通道");
            AssertEx.Equal(0u, action.NetworkTid, "灵极魂必须以自身为目标");

            var ack = tracker.CreateAckEnvelope(
                context.PlayerEntityId,
                action.ActionId,
                sequence,
                BlmPhase.Ice,
                clock.NowMs);
            var accepted = tracker.ApplyActionEffect(ack);
            service.OnActionEffect(ack, accepted);
            clock.Advance(1);
            noTarget = noTarget with { CapturedAtMs = clock.NowMs };
        }

        AssertEx.Equal(3, service.SoulCastCount, "灵极魂次数应保留 AE 的三次上限");
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Gcd, noTarget) is null,
            "灵极魂达到三次后必须停止");
        AssertEx.Equal(BlmBossFlightState.Completed, service.State, "灵极魂完成后状态错误");

        var full = noTarget with
        {
            IceStacks = 3,
            UmbralHearts = 3,
            Mp = 10_000,
        };
        service.ObserveContext(full);
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Gcd, full) is null,
            "冰层、冰针和 MP 全满后不得继续灵极魂");
    }

    private static void TargetRecoveryLeavesPreparationState()
    {
        var clock = new FakeClock(1);
        var context = CombatContext(clock.NowMs) with
        {
            Phase = BlmPhase.Fire,
            Transpose = TestContext.ReadyAction(BLMSkill.星灵移位),
        };
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        var service = new BlmBossFlightService(tracker, clock);
        service.OnBattleStarted(context);

        var noTarget = NoTarget(context, 10_001);
        service.ObserveContext(noTarget);
        service.MarkNoTarget(noTarget);
        AssertEx.Equal(BlmBossFlightState.Preparing, service.State, "目标丢失后应进入准备");

        var recovered = context with
        {
            CapturedAtMs = 10_002,
            TargetEntityId = 300,
            HasTarget = true,
            HasValidTarget = true,
            EnemyCount = 1,
        };
        service.ObserveContext(recovered);

        AssertEx.Equal(BlmBossFlightState.Armed, service.State, "目标恢复后应退出准备");
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Always, recovered) is null,
            "目标恢复后不得继续返回自身准备动作");
    }

    private static void NoTargetCancellationPreservesSelfPending()
    {
        var clock = new FakeClock(1);
        var context = CombatContext(clock.NowMs);
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        AssertEx.True(
            tracker.TryRegisterIssuedAction(new BlmIssuedActionMetadata(
                tracker.StateGeneration,
                BLMSkill.灵极魂,
                BLMSkill.灵极魂,
                clock.NowMs,
                tracker.GetTrackerSnapshot().LastAckGlobalSequence,
                clock.NowMs + 2_000,
                WasInstant: true,
                IsGcd: true)),
            "测试应能注册灵极魂 Pending");

        tracker.CancelTargetDependentIssuedAction();

        AssertEx.True(
            tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "目标丢失时不得取消自身目标的灵极魂 Pending");
        tracker.CancelIssuedAction();
    }

    private static BlmContext CombatContext(long capturedAtMs)
        => TestContext.Base() with
        {
            CapturedAtMs = capturedAtMs,
            BossFlightEnabled = true,
            InCombat = true,
            IsAlive = true,
            CanAct = true,
            HasTarget = true,
            HasValidTarget = true,
            TargetEntityId = 200,
            EnemyCount = 1,
            GcdRemainSeconds = 0f,
        };

    private static BlmContext NoTarget(BlmContext context, long capturedAtMs)
        => context with
        {
            CapturedAtMs = capturedAtMs,
            HasTarget = false,
            HasValidTarget = false,
            TargetEntityId = 0,
            EnemyCount = 0,
            GcdRemainSeconds = 0f,
        };
}
