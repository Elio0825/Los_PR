using LosPr.BLM.Core;
using LosPr.BLM.Engine;
using PromeRotation.Data;
using PromeRotation.Managers;

namespace Los.Tests;

internal static class Step3ContractTests
{
    public static void DecisionRejectsConflictingCommitments()
    {
        var context = Step3Context.Fire(
            mp: BlmFireBudget.DespairMinimumMp,
            soul: 6) with
        {
            Tracker = new BlmTrackerSnapshot
            {
                StateGeneration = 1,
                CombatSerial = 1,
                FirePhaseSerial = 1,
            },
        };
        var transition = new BlmTransitionRequest(
            TransitionKind.FireToIce,
            TransitionStep.CommitFireFinisher,
            TransitionDeliveryChannel.Gcd,
            BLMSkill.绝望,
            TransitionExpectation.FireFinisherReady,
            RotationMode.SingleTarget,
            IceToFireRoute.None,
            2000,
            10000);
        var followUp = new BlmFollowUpRequest(
            BlmFollowUpKind.FlareStarAfterMovementDespair,
            context.Tracker.StateGeneration,
            context.Tracker.CombatSerial,
            context.Tracker.FirePhaseSerial,
            context.TargetEntityId,
            BLMSkill.绝望,
            BLMSkill.耀星,
            10,
            8000,
            "测试互斥承诺");

        var threw = false;
        try
        {
            _ = BlmDecision.Gcd(
                context,
                BLMSkill.绝望,
                "TEST.CONFLICTING_COMMITMENTS",
                "同一 GCD 不得携带两种承诺。",
                BlmDecisionLayer.Override,
                transition,
                followUp);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        AssertEx.True(threw, "BlmDecision.Gcd 必须拒绝同时存在的 Transition 与 Follow-up");
    }

    public static void FollowUpUsesCurrentStageDeadline()
    {
        var clock = new FakeClock();
        var coordinator = new BlmFollowUpCoordinator(clock);
        var context = Step3Context.Fire(
            mp: BlmFireBudget.DespairMinimumMp,
            soul: 6,
            moving: true) with
        {
            Tracker = new BlmTrackerSnapshot
            {
                StateGeneration = 1,
                CombatSerial = 1,
                FirePhaseSerial = 1,
            },
        };
        var request = new BlmFollowUpRequest(
            BlmFollowUpKind.FlareStarAfterMovementDespair,
            context.Tracker.StateGeneration,
            context.Tracker.CombatSerial,
            context.Tracker.FirePhaseSerial,
            context.TargetEntityId,
            BLMSkill.绝望,
            BLMSkill.耀星,
            10,
            8000,
            "测试当前阶段 deadline");

        AssertEx.True(coordinator.TryBegin(request, context), "Follow-up 前置事务应建立成功");
        var triggerToken = coordinator.CaptureAckToken(1, 1);
        AssertEx.True(
            coordinator.TryAcknowledge(
                triggerToken,
                BLMSkill.绝望,
                11,
                clock.NowMs),
            "绝望触发 Ack 应被接受");
        coordinator.Reconcile(
            1,
            1,
            1,
            context with { Mp = 0 });
        AssertEx.Equal(
            BlmFollowUpStage.Active,
            coordinator.Peek().Stage,
            "绝望后置事实应激活耀星承诺");

        var activeToken = coordinator.CaptureAckToken(1, 1);
        AssertEx.Equal(
            BlmFollowUpStage.Active,
            activeToken.StageAtCapture,
            "测试必须冻结 Active 阶段旧 Token");
        AssertEx.True(
            coordinator.TryMarkRequiredQueued(
                1,
                coordinator.Peek().Serial,
                BLMSkill.耀星,
                11,
                500,
                "缩短 RequiredQueued deadline"),
            "耀星应进入 RequiredQueued");

        var queued = coordinator.Peek();
        AssertEx.True(
            queued.StageDeadlineAtMs < activeToken.DeadlineAtMs,
            "RequiredQueued 必须把当前阶段 deadline 缩短到旧 Token 之前");
        clock.Advance(queued.StageDeadlineAtMs - clock.NowMs + 1);
        AssertEx.False(
            coordinator.TryAcknowledge(
                activeToken,
                BLMSkill.耀星,
                12,
                clock.NowMs),
            "旧 Active Token 不得绕过 RequiredQueued 的当前阶段 deadline");
        AssertEx.Equal(
            BlmFollowUpStage.RequiredQueued,
            coordinator.Peek().Stage,
            "拒绝迟到 Ack 不得伪造 RequiredAcknowledged");
    }

    public static void HighPriorityReconcilePreservesCommittedWork()
    {
        var transitionHarness = new Step3Harness(Step3Context.Ice(
            firestarter: true,
            paradox: true,
            hearts: 3));
        AssertEx.True(
            transitionHarness.ResolveGcd() is not null,
            "测试必须先建立已入队 Transition");
        var transitionBefore = transitionHarness.Coordinator.Peek();
        transitionHarness.Dispatcher.Reconcile(
            transitionHarness.Context,
            highPriorityQueueActive: true);
        var transitionAfter = transitionHarness.Coordinator.Peek();
        AssertEx.Equal(
            transitionBefore.Serial,
            transitionAfter.Serial,
            "高优接管不得替换 Transition serial");
        AssertEx.Equal(
            transitionBefore.StepIndex,
            transitionAfter.StepIndex,
            "高优接管不得推进 Transition step");
        AssertEx.Equal(
            transitionBefore.Stage,
            transitionAfter.Stage,
            "高优接管不得取消已交给 PR 的 Transition");

        var followUpHarness = new Step3Harness(Step3Context.Fire(
            mp: BlmFireBudget.DespairMinimumMp,
            soul: 6,
            moving: true));
        AssertEx.True(
            followUpHarness.ResolveGcd() is not null,
            "测试必须先建立移动绝望 Follow-up");
        var followUpBefore = followUpHarness.FollowUp.Peek();
        followUpHarness.Dispatcher.Reconcile(
            followUpHarness.Context,
            highPriorityQueueActive: true);
        var followUpAfter = followUpHarness.FollowUp.Peek();
        AssertEx.Equal(
            followUpBefore.Serial,
            followUpAfter.Serial,
            "高优接管不得替换 Follow-up serial");
        AssertEx.Equal(
            followUpBefore.Stage,
            followUpAfter.Stage,
            "高优接管不得取消已承诺的 Follow-up");
    }

    public static void QueuedCancellationClearsNormalPrWork()
    {
        ActionQueueManager.ClearNormalQueues();
        try
        {
            var missedWindow = new Step3Harness(Step3Context.Ice(
                firestarter: true,
                paradox: true,
                hearts: 3));
            var iceParadox = missedWindow.ResolveGcd()
                ?? throw new InvalidOperationException("测试必须先返回冰悖论");
            missedWindow.AckAndApplyPostGauge(iceParadox);
            AssertEx.Equal(
                TransitionStep.UseTranspose,
                missedWindow.Coordinator.Peek().Step,
                "冰悖论确认后必须请求 Transpose");

            missedWindow.SetWindow(1.2f);
            var transpose = missedWindow.ResolveOffGcd()
                ?? throw new InvalidOperationException("宽尾窗必须返回 Transpose");
            AssertEx.Equal(ActionType.OffGcd, transpose.Type, "Transpose 必须进入 oGCD 队列");
            EnqueueNormalWithoutGame(transpose);
            AssertEx.True(
                ActionQueueManager.HasActionsInOffGcdQueue(),
                "测试必须建立普通 oGCD 队列工作");

            missedWindow.SetWindow(0f);
            AssertEx.True(
                missedWindow.ResolveGcd() is null,
                "错过旧 Transpose 后应保留完整冰资源给恢复星灵，而不是硬读 F3");
            AssertEx.Equal(
                TransitionStage.Cancelled,
                missedWindow.Coordinator.Peek().Stage,
                "错过窗口的旧 Transition 必须取消");
            AssertEx.False(
                ActionQueueManager.HasActionsInOffGcdQueue(),
                "Transition 取消时必须清除已失效的普通 oGCD 工作");
            var recoveryTranspose = missedWindow.ResolveAlways()
                ?? throw new InvalidOperationException("旧事务取消后必须重新返回恢复 Transpose");
            AssertEx.Equal(BLMSkill.星灵移位, recoveryTranspose.ActionId, "恢复动作必须是 Transpose");
            AssertEx.Equal(ActionType.Always, recoveryTranspose.Type, "GCD ready 时恢复 Transpose 应走 Always");

            ActionQueueManager.ClearNormalQueues();
            var acrOff = new Step3Harness(Step3Context.Ice(
                firestarter: true,
                paradox: true,
                hearts: 3));
            var queuedGcd = acrOff.ResolveGcd()
                ?? throw new InvalidOperationException("测试必须先返回待入队 GCD");
            EnqueueNormalWithoutGame(queuedGcd);
            AssertEx.True(
                ActionQueueManager.HasActionsInGcdQueue(),
                "测试必须建立普通 GCD 队列工作");

            acrOff.AdvanceAndReconcile(16, context => context with
            {
                AcrState = AcrState.Off,
            });
            AssertEx.Equal(
                TransitionStage.Cancelled,
                acrOff.Coordinator.Peek().Stage,
                "ACR Off 必须取消已入队的 Transition");
            AssertEx.False(
                ActionQueueManager.HasActionsInGcdQueue(),
                "ACR Off 必须清除普通 GCD 队列，不得留下孤儿动作");

            ActionQueueManager.ClearNormalQueues();
            var transitionDeadline = new Step3Harness(Step3Context.Ice(
                firestarter: true,
                paradox: true,
                hearts: 3));
            var deadlineGcd = transitionDeadline.ResolveGcd()
                ?? throw new InvalidOperationException("Transition deadline 测试必须先返回 GCD");
            EnqueueNormalWithoutGame(deadlineGcd);
            transitionDeadline.AdvanceAndReconcile(2001, context => context);
            AssertEx.Equal(
                TransitionStage.Cancelled,
                transitionDeadline.Coordinator.Peek().Stage,
                "Transition deadline 必须取消未 Ack 的已交付动作");
            AssertEx.False(
                ActionQueueManager.HasActionsInGcdQueue(),
                "Transition deadline 自取消前必须清理普通 GCD 队列");

            ActionQueueManager.ClearNormalQueues();
            var followUpDeadline = new Step3Harness(Step3Context.Fire(
                mp: BlmFireBudget.DespairMinimumMp,
                soul: 6,
                moving: true));
            var deadlineDespair = followUpDeadline.ResolveGcd()
                ?? throw new InvalidOperationException("Follow-up deadline 测试必须先返回绝望");
            EnqueueNormalWithoutGame(deadlineDespair);
            followUpDeadline.AdvanceAndReconcile(
                BlmFollowUpCoordinator.TriggerAckTimeoutMs + 1,
                context => context);
            AssertEx.Equal(
                BlmFollowUpStage.Cancelled,
                followUpDeadline.FollowUp.Peek().Stage,
                "Follow-up deadline 必须取消未 Ack 的绝望承诺");
            AssertEx.False(
                ActionQueueManager.HasActionsInGcdQueue(),
                "Follow-up deadline 自取消前必须清理普通 GCD 队列");
        }
        finally
        {
            ActionQueueManager.ClearNormalQueues();
        }
    }

    private static void EnqueueNormalWithoutGame(PAction action)
    {
        var fieldName = action.Type switch
        {
            ActionType.Gcd => "_normalPriorityGcdQueue",
            ActionType.OffGcd => "_normalPriorityOffGcdQueue",
            ActionType.Always => "_normalPriorityAlwaysQueue",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var field = typeof(ActionQueueManager).GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException($"PR 1.5.2.3 缺少队列字段 {fieldName}");
        var queue = field.GetValue(null) as Queue<IActionCommand>
            ?? throw new InvalidOperationException($"PR 队列字段 {fieldName} 类型不兼容");
        queue.Enqueue(new SingleActionCommand(action));
    }

}
