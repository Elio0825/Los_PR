using LosPr.BLM.Core;

namespace Los.Tests;

internal static class TrackerProductionTests
{
    public static void RunAll()
    {
        ManafontActivatesOnlyAfterRestoredGauge();
        ManafontTimeoutDoesNotCreateFalseFacts();
        LifecycleResetClearsProductionPending();
        HardcastPendingTracksStartCancelAndCompletion();
    }

    private static void ManafontActivatesOnlyAfterRestoredGauge()
    {
        var fixture = CreateFixture();
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(ManafontAck(fixture, 1)),
            "魔泉 Ack 应被接受");

        var afterAck = fixture.Tracker.GetTrackerSnapshot();
        AssertEx.True(afterAck.PendingGaugeReconcile, "魔泉 Ack 后必须等待 Gauge");
        AssertEx.False(afterAck.ManafontActiveThisFire, "Gauge 前不得推测魔泉已恢复资源");
        AssertEx.Equal(0L, afterAck.ManafontUseSerial, "Gauge 前不得增加魔泉序号");

        fixture.Clock.Advance(16);
        fixture.Tracker.Reconcile(RestoredContext(fixture));
        var reconciled = fixture.Tracker.GetTrackerSnapshot();
        AssertEx.False(reconciled.PendingGaugeReconcile, "资源恢复后应完成魔泉 Gauge 对账");
        AssertEx.True(reconciled.ManafontActiveThisFire, "完整资源事实应确认魔泉已生效");
        AssertEx.Equal(1L, reconciled.ManafontUseSerial, "首次确认魔泉应增加一次序号");
        AssertEx.Equal(BLMSkill.魔泉, reconciled.LastGaugeReconciledActionId, "Gauge 动作应记录为魔泉");

        fixture.Clock.Advance(100);
        var fire4Ack = fixture.Tracker.CreateAckEnvelope(
            fixture.Context.PlayerEntityId,
            BLMSkill.炽炎,
            2,
            BlmPhase.Fire,
            fixture.Clock.NowMs);
        AssertEx.True(fixture.Tracker.ApplyActionEffect(fire4Ack), "魔泉后的炽炎 Ack 应被接受");
        var afterFire4 = fixture.Tracker.GetTrackerSnapshot();
        AssertEx.Equal(1, afterFire4.Fire4Count, "火段炽炎计数错误");
        AssertEx.Equal(1, afterFire4.Fire4CountSinceManafont, "魔泉后炽炎计数错误");
    }

    private static void ManafontTimeoutDoesNotCreateFalseFacts()
    {
        var fixture = CreateFixture();
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(ManafontAck(fixture, 1)),
            "超时测试的魔泉 Ack 应被接受");

        fixture.Clock.Advance(BlmStateTracker.ManafontReconcileTimeoutMs + 1);
        fixture.Tracker.Reconcile(RestoredContext(fixture));
        var snapshot = fixture.Tracker.GetTrackerSnapshot();
        AssertEx.False(snapshot.PendingGaugeReconcile, "超时后必须清除 Gauge 等待");
        AssertEx.False(snapshot.ManafontActiveThisFire, "超时到达的资源不得倒推魔泉成功");
        AssertEx.Equal(0L, snapshot.ManafontUseSerial, "超时不得增加魔泉序号");
    }

    private static void LifecycleResetClearsProductionPending()
    {
        var fixture = CreateFixture();
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(ManafontAck(fixture, 1)),
            "重置测试的魔泉 Ack 应被接受");
        var generation = fixture.Tracker.StateGeneration;
        AssertEx.True(
            fixture.Tracker.TryRegisterIssuedAction(new BlmIssuedActionMetadata(
                generation,
                BLMSkill.炽炎,
                BLMSkill.炽炎,
                fixture.Clock.NowMs,
                1,
                fixture.Clock.NowMs + 5000,
                false,
                true)),
            "应能建立通用 Pending");

        fixture.Tracker.OnTerritoryChanged(777);
        var snapshot = fixture.Tracker.GetTrackerSnapshot();
        AssertEx.True(snapshot.StateGeneration > generation, "生命周期重置必须推进 generation");
        AssertEx.False(snapshot.HasPendingIssuedAction, "生命周期重置必须清除签发 Pending");
        AssertEx.False(snapshot.PendingGaugeReconcile, "生命周期重置必须清除 Gauge Pending");
        AssertEx.False(snapshot.ManafontActiveThisFire, "生命周期重置不得保留魔泉状态");
        AssertEx.Equal(0, snapshot.AcknowledgedActionHistoryCount, "生命周期重置必须隔离 Ack 历史");
    }

    private static void HardcastPendingTracksStartCancelAndCompletion()
    {
        var cancelled = CreateFixture();
        RegisterPending(cancelled, BLMSkill.炽炎, wasInstant: false, isGcd: true);
        cancelled.Tracker.Reconcile(cancelled.Context with
        {
            CapturedAtMs = cancelled.Clock.NowMs,
            IsCasting = true,
            CurrentCastingActionId = BLMSkill.炽炎,
            CastTotalSeconds = 2.5f,
            CastRemainSeconds = 1.5f,
        });
        AssertEx.True(
            cancelled.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "观察到匹配硬读条后必须保留Pending");
        cancelled.Clock.Advance(16);
        cancelled.Tracker.Reconcile(cancelled.Context with
        {
            CapturedAtMs = cancelled.Clock.NowMs,
            IsCasting = false,
            CurrentCastingActionId = 0,
            CastRemainSeconds = 0f,
        });
        AssertEx.False(
            cancelled.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "硬读条在剩余时间明显大于0时消失必须立即清除Pending");
        RegisterPending(cancelled, BLMSkill.炽炎, wasInstant: false, isGcd: true);

        var completed = CreateFixture();
        RegisterPending(completed, BLMSkill.炽炎, wasInstant: false, isGcd: true);
        completed.Tracker.Reconcile(completed.Context with
        {
            CapturedAtMs = completed.Clock.NowMs,
            IsCasting = true,
            CurrentCastingActionId = BLMSkill.炽炎,
            CastTotalSeconds = 2.5f,
            CastRemainSeconds = 0.1f,
        });
        completed.Clock.Advance(16);
        completed.Tracker.Reconcile(completed.Context with
        {
            CapturedAtMs = completed.Clock.NowMs,
            IsCasting = false,
            CurrentCastingActionId = 0,
            CastRemainSeconds = 0f,
        });
        AssertEx.True(
            completed.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "正常读条接近完成后消失必须保留Ack宽限");
        completed.Clock.Advance(249);
        completed.Tracker.Reconcile(completed.Context with
        {
            CapturedAtMs = completed.Clock.NowMs,
            IsCasting = false,
        });
        AssertEx.True(
            completed.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "Ack宽限250ms内不得误清正常完成Pending");
        completed.Clock.Advance(1);
        completed.Tracker.Reconcile(completed.Context with
        {
            CapturedAtMs = completed.Clock.NowMs,
            IsCasting = false,
        });
        AssertEx.False(
            completed.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "正常完成但Ack缺失超过宽限后必须清除Pending");

        var acknowledged = CreateFixture();
        RegisterPending(acknowledged, BLMSkill.炽炎, wasInstant: false, isGcd: true);
        acknowledged.Tracker.Reconcile(acknowledged.Context with
        {
            CapturedAtMs = acknowledged.Clock.NowMs,
            IsCasting = true,
            CurrentCastingActionId = BLMSkill.炽炎,
            CastRemainSeconds = 0.1f,
        });
        acknowledged.Clock.Advance(16);
        AssertEx.True(
            acknowledged.Tracker.ApplyActionEffect(
                acknowledged.Tracker.CreateAckEnvelope(
                    acknowledged.Context.PlayerEntityId,
                    BLMSkill.炽炎,
                    1,
                    BlmPhase.Fire,
                    acknowledged.Clock.NowMs)),
            "正常完成硬读条Ack必须接收");
        AssertEx.False(
            acknowledged.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "匹配Ack必须按既有路径清除Pending");

        var neverStarted = CreateFixture();
        RegisterPending(neverStarted, BLMSkill.炽炎, wasInstant: false, isGcd: true);
        neverStarted.Clock.Advance(749);
        neverStarted.Tracker.Reconcile(neverStarted.Context with
        {
            CapturedAtMs = neverStarted.Clock.NowMs,
            IsCasting = false,
        });
        AssertEx.True(
            neverStarted.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "硬读条启动等待750ms边界前必须保留Pending");
        neverStarted.Clock.Advance(1);
        neverStarted.Tracker.Reconcile(neverStarted.Context with
        {
            CapturedAtMs = neverStarted.Clock.NowMs,
            IsCasting = false,
        });
        AssertEx.False(
            neverStarted.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "签发750ms仍未开始硬读条必须清除Pending");

        var instant = CreateFixture();
        RegisterPending(instant, BLMSkill.异言, wasInstant: true, isGcd: true);
        instant.Clock.Advance(800);
        instant.Tracker.Reconcile(instant.Context with
        {
            CapturedAtMs = instant.Clock.NowMs,
            IsCasting = false,
        });
        AssertEx.True(
            instant.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "硬读条启动观察不得影响瞬发GCD Pending");
        instant.Tracker.CancelIssuedAction();
        RegisterPending(instant, BLMSkill.星灵移位, wasInstant: false, isGcd: false);
        instant.Clock.Advance(800);
        instant.Tracker.Reconcile(instant.Context with
        {
            CapturedAtMs = instant.Clock.NowMs,
            IsCasting = false,
        });
        AssertEx.True(
            instant.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "硬读条启动观察不得影响oGCD Pending");
    }

    private static void RegisterPending(
        Fixture fixture,
        uint actionId,
        bool wasInstant,
        bool isGcd)
        => AssertEx.True(
            fixture.Tracker.TryRegisterIssuedAction(new BlmIssuedActionMetadata(
                fixture.Tracker.StateGeneration,
                actionId,
                actionId,
                fixture.Clock.NowMs,
                fixture.Tracker.GetTrackerSnapshot().LastAckGlobalSequence,
                fixture.Clock.NowMs + 5000,
                wasInstant,
                isGcd)),
            $"动作{actionId}的Pending必须成功注册");

    private static Fixture CreateFixture()
    {
        var clock = new FakeClock(10_000);
        var context = TestContext.Base() with
        {
            CapturedAtMs = clock.NowMs,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            IceStacks = 0,
            Mp = 0,
            UmbralHearts = 0,
            HasParadox = false,
            HasThunderhead = false,
        };
        return new Fixture(
            clock,
            context,
            new BlmStateTracker(context, clock, new MappingActionIdNormalizer()));
    }

    private static BlmActionEffectAck ManafontAck(Fixture fixture, uint sequence)
        => fixture.Tracker.CreateAckEnvelope(
            fixture.Context.PlayerEntityId,
            BLMSkill.魔泉,
            sequence,
            BlmPhase.Fire,
            fixture.Clock.NowMs);

    private static BlmContext RestoredContext(Fixture fixture)
        => fixture.Context with
        {
            CapturedAtMs = fixture.Clock.NowMs,
            Mp = fixture.Context.MaxMp,
            AfStacks = 3,
            UmbralHearts = 3,
            HasParadox = true,
            HasThunderhead = true,
        };

    private sealed record Fixture(
        FakeClock Clock,
        BlmContext Context,
        BlmStateTracker Tracker);
}
