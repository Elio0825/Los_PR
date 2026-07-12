using LosPr.BLM.Core;
using LosPr.BLM.Engine;
using PromeRotation.Data;

namespace Los.Tests;

internal static class FollowUpTests
{
    public static void MovementDespairFlareStarLifecycle()
    {
        var (harness, despair) = BeginMovementFollowUp();
        var initial = harness.FollowUp.Peek();
        AssertEx.Equal(
            BlmFollowUpStage.AwaitingTriggerAck,
            initial.Stage,
            "移动绝望返回后只能等待触发 Ack");
        AssertEx.True(initial.Serial > 0, "Follow-up 必须分配独立 serial");
        AssertEx.Equal(
            harness.Context.TargetEntityId,
            initial.TargetEntityId,
            "Follow-up 必须冻结请求目标");

        harness.ApplyAck(despair);
        AssertEx.Equal(
            BlmFollowUpStage.TriggerAcknowledged,
            harness.FollowUp.Peek().Stage,
            "绝望 Ack 只能附着，不能直接激活耀星");

        harness.AdvanceAndReconcile(16, context => context);
        AssertEx.Equal(
            BlmFollowUpStage.TriggerAcknowledged,
            harness.FollowUp.Peek().Stage,
            "绝望 Ack 后旧 MP Gauge 不得推进 Follow-up");

        harness.AdvanceAndReconcile(16, context => context with
        {
            Mp = 0,
            GcdRemainSeconds = 2.4f,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Active,
            harness.FollowUp.Peek().Stage,
            "下一 Tick 确认绝望资源变化后才可激活耀星");

        AssertEx.True(
            harness.ResolveGcd() is null,
            "移动且没有可靠瞬发时 Follow-up 必须占有 GCD，不能插入其他技能");
        AssertEx.Equal(
            BlmFollowUpStage.Active,
            harness.FollowUp.Peek().Stage,
            "暂时无法安全释放耀星时必须保留承诺");

        harness.SetWindow(0.2f, moving: false);
        var flareStar = RequireAction(
            harness.ResolveGcd(),
            "停止移动后应返回已承诺的耀星");
        AssertEx.Equal(BLMSkill.耀星, flareStar.ActionId, "Follow-up 必须锁定耀星");
        AssertEx.Equal(ActionType.Gcd, flareStar.Type, "耀星必须进入 GCD 队列");
        AssertEx.Equal(
            initial.TargetEntityId,
            flareStar.NetworkTid,
            "耀星必须使用 Follow-up 冻结的目标");
        AssertEx.Equal(
            BlmFollowUpStage.RequiredQueued,
            harness.FollowUp.Peek().Stage,
            "耀星返回后必须原子进入 RequiredQueued");

        harness.ApplyAck(flareStar);
        AssertEx.Equal(
            BlmFollowUpStage.RequiredAcknowledged,
            harness.FollowUp.Peek().Stage,
            "耀星 Ack 不能跳过动作后 Gauge 对账");

        harness.AdvanceAndReconcile(16, context => context);
        AssertEx.Equal(
            BlmFollowUpStage.RequiredAcknowledged,
            harness.FollowUp.Peek().Stage,
            "耀星 Ack 后旧 Soul Gauge 不得完成 Follow-up");

        harness.AdvanceAndReconcile(16, context => context with
        {
            AstralSoul = 0,
        });
        var completed = harness.FollowUp.Peek();
        AssertEx.Equal(
            BlmFollowUpStage.Completed,
            completed.Stage,
            "耀星 Ack 与下一 Tick Soul 消耗都确认后才可完成");
        AssertEx.Equal(initial.Serial, completed.Serial, "完整 Follow-up 必须沿用同一 serial");
    }

    public static void AckTokenIsolationAndIdempotence()
    {
        var (wrongHarness, _) = BeginMovementFollowUp();
        var wrongAction = wrongHarness.CaptureAck(
            BLMSkill.炽炎,
            sequence: 40);
        AssertEx.Equal(
            BLMSkill.绝望,
            wrongAction.FollowUpToken.ExpectedActionId,
            "错误动作事件应携带当前冻结的绝望 Token");
        AssertEx.True(
            wrongHarness.ApplyAck(wrongAction),
            "错误技能仍可作为普通 Tracker 事实接收");
        AssertEx.Equal(
            BlmFollowUpStage.AwaitingTriggerAck,
            wrongHarness.FollowUp.Peek().Stage,
            "错误动作不得推进 Follow-up");

        var triggerAck = wrongHarness.CaptureAck(
            BLMSkill.绝望,
            sequence: 41);
        AssertEx.True(wrongHarness.ApplyAck(triggerAck), "正确绝望 Ack 应被接收");
        AssertEx.False(
            wrongHarness.ApplyAck(triggerAck),
            "相同 GlobalSequence 的重复 Ack 必须由 Tracker 幂等拒绝");
        AssertEx.Equal(
            BlmFollowUpStage.TriggerAcknowledged,
            wrongHarness.FollowUp.Peek().Stage,
            "重复 Ack 不得二次推进状态");

        var (oldHarness, _) = BeginMovementFollowUp();
        var oldEnvelope = oldHarness.CaptureAck(
            BLMSkill.绝望,
            sequence: 50);
        var oldSerial = oldHarness.FollowUp.Peek().Serial;
        oldHarness.FollowUp.CancelActive("建立旧 Token 隔离场景");

        var nextDespair = RequireAction(
            oldHarness.ResolveGcd(),
            "终态后应能建立新的移动绝望 Follow-up");
        AssertEx.Equal(BLMSkill.绝望, nextDespair.ActionId, "新事务触发动作必须仍为绝望");
        var newSerial = oldHarness.FollowUp.Peek().Serial;
        AssertEx.True(newSerial > oldSerial, "新 Follow-up 必须使用更大的 serial");

        AssertEx.True(
            oldHarness.ApplyAck(oldEnvelope),
            "旧事件可作为普通 Tracker 事实接收");
        AssertEx.Equal(
            BlmFollowUpStage.AwaitingTriggerAck,
            oldHarness.FollowUp.Peek().Stage,
            "旧 serial Token 不得激活新 Follow-up");
        AssertEx.Equal(
            newSerial,
            oldHarness.FollowUp.Peek().Serial,
            "旧 Token 不得覆盖新事务");

        var newAck = oldHarness.CaptureAck(
            BLMSkill.绝望,
            sequence: 51);
        AssertEx.True(oldHarness.ApplyAck(newAck), "新 Token 的绝望 Ack 应被接收");
        AssertEx.Equal(
            BlmFollowUpStage.TriggerAcknowledged,
            oldHarness.FollowUp.Peek().Stage,
            "只有新 Token 才能推进新事务");
    }

    public static void GenerationCombatPhaseAndTargetInvalidation()
    {
        AssertCoordinatorMismatchCancels(
            (intent, context) => (
                intent.StateGeneration + 1,
                intent.CombatSerial,
                intent.FirePhaseSerial,
                context),
            "generation 变化必须取消 Follow-up");
        AssertCoordinatorMismatchCancels(
            (intent, context) => (
                intent.StateGeneration,
                intent.CombatSerial + 1,
                intent.FirePhaseSerial,
                context),
            "combat serial 变化必须取消 Follow-up");
        AssertCoordinatorMismatchCancels(
            (intent, context) => (
                intent.StateGeneration,
                intent.CombatSerial,
                intent.FirePhaseSerial + 1,
                context),
            "fire phase serial 变化必须取消 Follow-up");

        var (targetHarness, _) = BeginMovementFollowUp();
        targetHarness.Reconcile(targetHarness.RawContext with
        {
            TargetEntityId = targetHarness.RawContext.TargetEntityId + 1,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Cancelled,
            targetHarness.FollowUp.Peek().Stage,
            "目标实体变化必须取消 Follow-up");

        var (invalidTargetHarness, _) = BeginMovementFollowUp();
        invalidTargetHarness.Reconcile(invalidTargetHarness.RawContext with
        {
            HasValidTarget = false,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Cancelled,
            invalidTargetHarness.FollowUp.Peek().Stage,
            "目标失效必须取消 Follow-up");
    }

    public static void DeadlineAndResourceInvalidation()
    {
        var (deadlineHarness, _) = BeginMovementFollowUp();
        deadlineHarness.Clock.Advance(BlmFollowUpCoordinator.TriggerAckTimeoutMs + 1);
        deadlineHarness.Reconcile(deadlineHarness.RawContext);
        AssertEx.Equal(
            BlmFollowUpStage.Cancelled,
            deadlineHarness.FollowUp.Peek().Stage,
            "绝望 Ack 局部期限后必须取消 Follow-up");

        var (beforeAckHarness, _) = BeginMovementFollowUp();
        beforeAckHarness.Reconcile(beforeAckHarness.RawContext with
        {
            AstralSoul = 5,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Cancelled,
            beforeAckHarness.FollowUp.Peek().Stage,
            "绝望 Ack 前 Soul 资源失效必须立即取消");

        var (afterAckHarness, despair) = BeginMovementFollowUp();
        afterAckHarness.ApplyAck(despair);
        afterAckHarness.AdvanceAndReconcile(16, context => context with
        {
            Mp = 0,
            AstralSoul = 5,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Cancelled,
            afterAckHarness.FollowUp.Peek().Stage,
            "绝望 Ack 后下一 Tick Soul 资源失效必须立即取消");

        var (activeHarness, activeDespair) = BeginMovementFollowUp();
        activeHarness.ApplyAck(activeDespair);
        activeHarness.AdvanceAndReconcile(16, context => context with
        {
            Mp = 0,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Active,
            activeHarness.FollowUp.Peek().Stage,
            "前置场景必须先进入 Active");
        activeHarness.AdvanceAndReconcile(16, context => context with
        {
            AstralSoul = 5,
        });
        AssertEx.Equal(
            BlmFollowUpStage.Cancelled,
            activeHarness.FollowUp.Peek().Stage,
            "耀星入队前 Soul 被其他来源消耗必须取消");
    }

    private static (Step3Harness Harness, PAction Despair) BeginMovementFollowUp()
    {
        var harness = new Step3Harness(Step3Context.Fire(
            mp: BlmFireBudget.DespairMinimumMp,
            soul: 6,
            moving: true));
        var despair = RequireAction(
            harness.ResolveGcd(),
            "移动满 Soul 场景应先返回绝望");
        AssertEx.Equal(BLMSkill.绝望, despair.ActionId, "Follow-up 触发动作必须为绝望");
        AssertEx.Equal(ActionType.Gcd, despair.Type, "绝望必须进入 GCD 队列");
        return (harness, despair);
    }

    private static void AssertCoordinatorMismatchCancels(
        Func<BlmFollowUpIntent, BlmContext, (
            long StateGeneration,
            long CombatSerial,
            long FirePhaseSerial,
            BlmContext Context)> mismatch,
        string message)
    {
        var (harness, _) = BeginMovementFollowUp();
        var intent = harness.FollowUp.Peek();
        var values = mismatch(intent, harness.Context);
        harness.FollowUp.Reconcile(
            values.StateGeneration,
            values.CombatSerial,
            values.FirePhaseSerial,
            values.Context);
        AssertEx.Equal(BlmFollowUpStage.Cancelled, harness.FollowUp.Peek().Stage, message);
    }

    private static PAction RequireAction(PAction? action, string message)
        => action ?? throw new InvalidOperationException(message);
}
