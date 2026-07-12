namespace LosPr.BLM.Engine;

using LosPr.BLM.Strategies;

public sealed class BlmActionDispatcher
{
    private const long OgcdAckTimeoutMs = 2000;
    private const long InstantGcdAckTimeoutMs = 2000;
    private const long CastGcdAckTimeoutMs = 5000;
    private const long Blizzard3AckTimeoutMs = 6000;
    private const long RequestedStepHandoffMarginMs = 1500;

    private readonly BlmCoordinator _coordinator;
    private readonly BlmFollowUpCoordinator _followUp;
    private readonly BlmDecisionEngine _decisionEngine;
    private readonly StandardOffGcdStrategy _offGcdStrategy;
    private readonly IBlmActionIdNormalizer _normalizer;
    private readonly IBlmDebugSink _debug;
    private GcdInstantPending? _gcdInstantPending;
    private UtilityOgcdPending? _utilityOgcdPending;
    private TransitionOgcdPending? _transitionOgcdPending;
    private long _confirmedInstantGcdAtMs = long.MinValue;
    private long _utilityUsedAfterGcdAtMs = long.MinValue;
    private uint _utilityUsedAfterGcdActionId;
    private int _utilityUsedAfterGcdCount;
    private long _transitionOgcdUsedAfterGcdAtMs = long.MinValue;
    private uint _transitionOgcdUsedAfterGcdActionId;
    private int _transitionOgcdUsedAfterGcdCount;

    public BlmActionDispatcher(
        BlmCoordinator coordinator,
        BlmFollowUpCoordinator followUp,
        BlmDecisionEngine? decisionEngine = null,
        IBlmActionIdNormalizer? normalizer = null,
        IBlmDebugSink? debugSink = null,
        StandardOffGcdStrategy? offGcdStrategy = null)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _followUp = followUp ?? throw new ArgumentNullException(nameof(followUp));
        _decisionEngine = decisionEngine ?? new BlmDecisionEngine();
        _offGcdStrategy = offGcdStrategy ?? new StandardOffGcdStrategy();
        _normalizer = normalizer ?? IdentityBlmActionIdNormalizer.Instance;
        _debug = debugSink ?? NullBlmDebugSink.Instance;
    }

    public PAction? ResolveGcd(
        BlmContext context,
        BlmDecisionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        policy ??= BlmDecisionPolicy.Default;
        ReconcileGcdInstant(context);
        ReconcileUtilityOgcd(context);
        ReconcileTransitionOgcd(context);
        var transitionBefore = _coordinator.Peek();
        var followUpBefore = _followUp.Peek();
        Reconcile(context, policy.HighPriorityQueueActive);
        TraceStateChanges(
            transitionBefore,
            followUpBefore,
            context,
            "Gcd.Reconcile");
        transitionBefore = _coordinator.Peek();
        followUpBefore = _followUp.Peek();

        PAction? Finish(
            PAction? result,
            uint actionId = 0,
            string source = "")
        {
            TraceStateChanges(
                transitionBefore,
                followUpBefore,
                context,
                string.IsNullOrEmpty(source) ? "Gcd" : $"Gcd.{source}");
            if (result is not null)
            {
                QueueGcdInstant(
                    context,
                    actionId,
                    BlmCastSafety.CanCastWhileMoving(context, actionId, policy));
                TraceDispatch(
                    context,
                    "Gcd",
                    actionId,
                    ActionType.Gcd,
                    source);
            }

            return result;
        }

        if (policy.HighPriorityQueueActive)
        {
            return Finish(null);
        }

        if (TryResolveFollowUpGcd(context, policy, out var followUpAction, out var followUpOwnsGcd))
        {
            return Finish(
                followUpAction,
                _followUp.Peek().RequiredActionId,
                "FollowUp");
        }

        if (followUpOwnsGcd)
        {
            return Finish(null, source: "FollowUpOwnsGcd");
        }

        if (TryResolveTransitionGcd(context, policy, out var transitionAction, out var transitionOwnsGcd))
        {
            return Finish(
                transitionAction,
                _coordinator.Peek().ExpectedActionId,
                "Transition");
        }

        if (transitionOwnsGcd)
        {
            return Finish(null, source: "TransitionOwnsGcd");
        }

        var decision = _decisionEngine.Resolve(new BlmDecisionInput
        {
            Context = context,
            Policy = policy,
        });
        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.Decision,
            Context = context,
            MonotonicMs = context.CapturedAtMs,
            EntryPoint = "Gcd",
            ActionId = decision.ActionId,
            NormalizedActionId = NormalizeOrOriginal(decision.ActionId),
            RuleId = decision.RuleId,
            Reason = decision.Reason,
            Detail = decision.HasAction
                ? $"{decision.Layer} / {decision.Target}"
                : $"NoAction / {decision.NoActionReason}",
            TargetEntityId = decision.TargetEntityId,
            Transition = _coordinator.Peek(),
            FollowUp = _followUp.Peek(),
        });
        if (!decision.HasAction)
        {
            return Finish(null, source: "DecisionNoAction");
        }

        if (decision.TransitionRequest is not null
            && decision.FollowUpRequest is not null)
        {
            throw new InvalidOperationException(
                "同一个 GCD Decision 不能同时创建 Transition 与 Follow-up。");
        }

        var action = ToTargetGcd(
            decision.ActionId,
            decision.TargetEntityId);
        CommitInitialTransition(decision, context);
        CommitFollowUp(decision, context);
        return Finish(action, decision.ActionId, $"Decision.{decision.Layer}");
    }

    public PAction? ResolveOffGcd(
        BlmContext context,
        BlmDecisionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        policy ??= BlmDecisionPolicy.Default;
        ReconcileGcdInstant(context);
        ReconcileUtilityOgcd(context);
        ReconcileTransitionOgcd(context);
        var transitionBefore = _coordinator.Peek();
        var followUpBefore = _followUp.Peek();
        Reconcile(context, policy.HighPriorityQueueActive);
        TraceStateChanges(
            transitionBefore,
            followUpBefore,
            context,
            "OffGcd.Reconcile");
        transitionBefore = _coordinator.Peek();
        followUpBefore = _followUp.Peek();

        PAction? Finish(PAction? result, uint actionId = 0, string source = "")
        {
            TraceStateChanges(
                transitionBefore,
                followUpBefore,
                context,
                string.IsNullOrEmpty(source) ? "OffGcd" : $"OffGcd.{source}");
            if (result is not null)
            {
                TraceDispatch(
                    context,
                    "OffGcd",
                    actionId,
                    ActionType.OffGcd,
                    source);
            }

            return result;
        }

        if (policy.HighPriorityQueueActive || !CanWeave(context))
        {
            return Finish(null);
        }

        var intent = _coordinator.Peek();
        if (intent.IsActive
            && intent.Stage == TransitionStage.Requested
            && intent.DeliveryChannel is TransitionDeliveryChannel.OffGcd
                or TransitionDeliveryChannel.OffGcdOrAlways)
        {
            if (!CanUseExpectedActionNow(intent, context, policy))
            {
                Cancel(intent, "oGCD 前置事实失效", context);
                return Finish(null);
            }
            else if (!_coordinator.TryMarkQueued(
                    context.Tracker.StateGeneration,
                    intent.Serial,
                    intent.StepIndex,
                    intent.ExpectedActionId,
                    BlmQueueChannel.OffGcd,
                    context.GcdRemainSeconds,
                    context.Tracker.LastAckGlobalSequence,
                    OgcdAckTimeoutMs,
                    $"oGCD queue: {intent.Kind}/{intent.Step}"))
            {
                return Finish(null);
            }
            else
            {
                QueueTransitionOgcdUsage(context, intent.ExpectedActionId);
                return Finish(
                    ToSelfAction(intent.ExpectedActionId, ActionType.OffGcd),
                    intent.ExpectedActionId,
                    "Transition");
            }
        }

        if (intent.IsActive && !CanWeaveUtilityAlongside(intent))
        {
            return Finish(null);
        }

        if (_utilityOgcdPending is not null
            || !CanResolveUtilityOgcd(context))
        {
            return Finish(null);
        }

        var candidate = _offGcdStrategy.Resolve(context, policy);
        if (!candidate.HasAction
            || IsUtilityBlockedForCurrentGcd(context, candidate.ActionId))
        {
            return Finish(null);
        }

        _utilityOgcdPending = new UtilityOgcdPending(
            context.Tracker.StateGeneration,
            candidate.ActionId,
            context.CapturedAtMs,
            context.CapturedAtMs + OgcdAckTimeoutMs,
            context.Tracker.LastGcdAtMs);
        return Finish(
            ToSelfAction(candidate.ActionId, ActionType.OffGcd),
            candidate.ActionId,
            $"Utility.{candidate.RuleId}");
    }

    public PAction? ResolveAlways(
        BlmContext context,
        BlmDecisionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        policy ??= BlmDecisionPolicy.Default;
        ReconcileGcdInstant(context);
        ReconcileUtilityOgcd(context);
        ReconcileTransitionOgcd(context);
        var transitionBefore = _coordinator.Peek();
        var followUpBefore = _followUp.Peek();
        Reconcile(context, policy.HighPriorityQueueActive);
        TraceStateChanges(
            transitionBefore,
            followUpBefore,
            context,
            "Always.Reconcile");
        transitionBefore = _coordinator.Peek();
        followUpBefore = _followUp.Peek();

        PAction? Finish(PAction? result, uint actionId = 0, string source = "")
        {
            TraceStateChanges(
                transitionBefore,
                followUpBefore,
                context,
                string.IsNullOrEmpty(source) ? "Always" : $"Always.{source}");
            if (result is not null)
            {
                TraceDispatch(
                    context,
                    "Always",
                    actionId,
                    ActionType.Always,
                    source);
            }

            return result;
        }

        if (policy.HighPriorityQueueActive
            || context.IsCasting
            || context.AnimationLockSeconds > 0f
            || context.GcdRemainSeconds > 0.6f)
        {
            return Finish(null);
        }

        var intent = _coordinator.Peek();
        if (intent.IsActive)
        {
            if (intent.Stage == TransitionStage.Requested
                && intent.DeliveryChannel == TransitionDeliveryChannel.OffGcdOrAlways
                && (!IsAllowedAlwaysStep(intent)
                    || !CanUseExpectedActionNow(intent, context, policy)))
            {
                Cancel(intent, "Always 白名单或前置事实失效", context);
            }

            if (intent.Stage != TransitionStage.Requested
                || intent.DeliveryChannel != TransitionDeliveryChannel.OffGcdOrAlways
                || !IsAllowedAlwaysStep(intent)
                || !CanUseExpectedActionNow(intent, context, policy)
                || !_coordinator.TryMarkQueued(
                    context.Tracker.StateGeneration,
                    intent.Serial,
                    intent.StepIndex,
                    intent.ExpectedActionId,
                    BlmQueueChannel.Always,
                    context.GcdRemainSeconds,
                    context.Tracker.LastAckGlobalSequence,
                    OgcdAckTimeoutMs,
                    $"Always queue: {intent.Kind}/{intent.Step}"))
            {
                return Finish(null);
            }

            QueueTransitionOgcdUsage(context, intent.ExpectedActionId);
            return Finish(
                ToSelfAction(intent.ExpectedActionId, ActionType.Always),
                intent.ExpectedActionId,
                "Transition");
        }

        if (_utilityOgcdPending is not null
            || !CanResolveUtilityOgcd(context))
        {
            return Finish(null);
        }

        var candidate = _offGcdStrategy.ResolveRecoveryTranspose(context);
        if (!candidate.HasAction
            || IsUtilityBlockedForCurrentGcd(context, candidate.ActionId))
        {
            return Finish(null);
        }

        _utilityOgcdPending = new UtilityOgcdPending(
            context.Tracker.StateGeneration,
            candidate.ActionId,
            context.CapturedAtMs,
            context.CapturedAtMs + OgcdAckTimeoutMs,
            context.Tracker.LastGcdAtMs);
        return Finish(
            ToSelfAction(candidate.ActionId, ActionType.Always),
            candidate.ActionId,
            $"Utility.{candidate.RuleId}");
    }

    public void Reconcile(BlmContext context, bool highPriorityQueueActive = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        _followUp.Reconcile(
            context.Tracker.StateGeneration,
            context.Tracker.CombatSerial,
            context.Tracker.FirePhaseSerial,
            context);

        if (highPriorityQueueActive)
        {
            return;
        }

        if (!context.IsAvailable
            || context.AcrState != AcrState.On
            || !context.IsAlive
            || !context.HasValidTarget
            || !context.InRange)
        {
            CancelAll("运行事实失效", context);
            return;
        }

        AdvanceConfirmed(context);
    }

    public void PrepareForReconcile(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!HasUnacknowledgedDeliveredWork())
        {
            return;
        }

        var transition = _coordinator.Peek();
        var followUp = _followUp.Peek();
        var runtimeFactsInvalid = !context.IsAvailable
            || context.AcrState != AcrState.On
            || !context.IsAlive
            || !context.HasValidTarget
            || !context.InRange;
        var frozenTargetChanged = (transition.IsActive
                && transition.TargetEntityIdAtRequest != 0
                && transition.TargetEntityIdAtRequest != context.TargetEntityId)
            || (followUp.IsPending
                && followUp.TargetEntityId != 0
                && followUp.TargetEntityId != context.TargetEntityId);
        var queuedFollowUpFactsInvalid = followUp.Stage
                is BlmFollowUpStage.AwaitingTriggerAck
                    or BlmFollowUpStage.RequiredQueued
            && (!context.InFire
                || context.AfStacks != 3
                || !context.AstralSoulFull);
        var transitionDeadlineExpired = IsUnacknowledgedDeliveredWork(transition)
            && (context.CapturedAtMs > transition.ExpireAtMs
                || context.CapturedAtMs > transition.TransitionExpireAtMs);
        var followUpDeadlineExpired = followUp.Stage
                is BlmFollowUpStage.AwaitingTriggerAck
                    or BlmFollowUpStage.RequiredQueued
            && (context.CapturedAtMs > followUp.StageDeadlineAtMs
                || context.CapturedAtMs > followUp.TotalExpireAtMs);

        if (runtimeFactsInvalid
            || frozenTargetChanged
            || queuedFollowUpFactsInvalid
            || transitionDeadlineExpired
            || followUpDeadlineExpired)
        {
            ClearNormalPrQueues(context, "对账前发现已交付动作失效");
        }
    }

    public void CancelAll(string reason, BlmContext? context = null)
    {
        var snapshot = context ?? BlmContext.Unavailable;
        var transitionBefore = _coordinator.Peek();
        var followUpBefore = _followUp.Peek();
        if (HasUnacknowledgedDeliveredWork())
        {
            ClearNormalPrQueues(snapshot, reason);
        }

        _coordinator.CancelActive(reason);
        _followUp.CancelActive(reason);
        _gcdInstantPending = null;
        _utilityOgcdPending = null;
        _transitionOgcdPending = null;
        _utilityUsedAfterGcdAtMs = long.MinValue;
        _utilityUsedAfterGcdActionId = 0;
        _utilityUsedAfterGcdCount = 0;
        _transitionOgcdUsedAfterGcdAtMs = long.MinValue;
        _transitionOgcdUsedAfterGcdActionId = 0;
        _transitionOgcdUsedAfterGcdCount = 0;
        _confirmedInstantGcdAtMs = long.MinValue;
        TraceStateChanges(
            transitionBefore,
            followUpBefore,
            snapshot,
            $"CancelAll.{reason}");
    }

    private bool TryResolveFollowUpGcd(
        BlmContext context,
        BlmDecisionPolicy policy,
        out PAction? action,
        out bool ownsGcd)
    {
        action = null;
        ownsGcd = false;
        var followUp = _followUp.Peek();
        if (!followUp.IsPending)
        {
            return false;
        }

        if (followUp.Stage is BlmFollowUpStage.AwaitingTriggerAck
            or BlmFollowUpStage.TriggerAcknowledged)
        {
            _followUp.TryCancel(
                followUp.StateGeneration,
                followUp.Serial,
                "下一 GCD 窗口到达前未完成绝望 Ack/Gauge 确认");
            return false;
        }

        ownsGcd = true;
        if (followUp.Stage == BlmFollowUpStage.RequiredQueued
            && HasReachedNextGcdWindow(
                followUp.QueuedAtMs,
                context))
        {
            _followUp.TryCancel(
                followUp.StateGeneration,
                followUp.Serial,
                "耀星已入队但未在下一 GCD 窗口前收到 Ack");
            ownsGcd = false;
            return false;
        }

        if (followUp.Stage != BlmFollowUpStage.Active)
        {
            return false;
        }

        if (followUp.StateGeneration != context.Tracker.StateGeneration
            || followUp.CombatSerial != context.Tracker.CombatSerial
            || followUp.FirePhaseSerial != context.Tracker.FirePhaseSerial
            || followUp.TargetEntityId != context.TargetEntityId
            || followUp.RequiredActionId != BLMSkill.耀星
            || !context.InFire
            || context.AfStacks != 3
            || !context.AstralSoulFull)
        {
            _followUp.TryCancel(
                followUp.StateGeneration,
                followUp.Serial,
                "耀星 Follow-up 前置事实失效");
            ownsGcd = false;
            return false;
        }

        if (!BlmCastSafety.CanCastNow(context, followUp.RequiredActionId, policy))
        {
            return false;
        }

        if (!_followUp.TryMarkRequiredQueued(
                followUp.StateGeneration,
                followUp.Serial,
                followUp.RequiredActionId,
                context.Tracker.LastAckGlobalSequence,
                CastGcdAckTimeoutMs,
                "耀星 Follow-up 已交给 GCD 队列"))
        {
            return false;
        }

        action = ToTargetGcd(
            followUp.RequiredActionId,
            followUp.TargetEntityId);
        return true;
    }

    private bool TryResolveTransitionGcd(
        BlmContext context,
        BlmDecisionPolicy policy,
        out PAction? action,
        out bool ownsGcd)
    {
        action = null;
        ownsGcd = false;
        var intent = _coordinator.Peek();
        if (!intent.IsActive)
        {
            return false;
        }

        if (intent.Stage == TransitionStage.Queued)
        {
            var missedAbilityWindow = intent.DeliveryChannel
                    is TransitionDeliveryChannel.OffGcd
                        or TransitionDeliveryChannel.OffGcdOrAlways
                && context.GcdRemainSeconds <= 0.3f;
            var missedGcdAck = intent.DeliveryChannel == TransitionDeliveryChannel.Gcd
                && HasReachedNextGcdWindow(intent.QueuedAtMs, context);
            if (missedAbilityWindow || missedGcdAck)
            {
                Cancel(intent, "动作已入队但未在下一 GCD 窗口前收到 Ack", context);
                return false;
            }

            ownsGcd = true;
            return false;
        }

        if (intent.Stage == TransitionStage.Confirmed)
        {
            ownsGcd = true;
            return false;
        }

        if (intent.Stage != TransitionStage.Requested)
        {
            return false;
        }

        if (intent.DeliveryChannel != TransitionDeliveryChannel.Gcd)
        {
            Cancel(intent, "GCD ready 时能力步骤仍未入队，取消后安全回退", context);
            return false;
        }

        if (!CanUseExpectedActionNow(intent, context, policy))
        {
            Cancel(intent, "已承诺 GCD 的阶段、资源或移动事实失效", context);
            return false;
        }

        if (!_coordinator.TryMarkQueued(
                context.Tracker.StateGeneration,
                intent.Serial,
                intent.StepIndex,
                intent.ExpectedActionId,
                BlmQueueChannel.Gcd,
                context.GcdRemainSeconds,
                context.Tracker.LastAckGlobalSequence,
                QueueAckTimeoutFor(intent.ExpectedActionId),
                $"GCD queue: {intent.Kind}/{intent.Step}"))
        {
            return false;
        }

        ownsGcd = true;
        action = ToTargetGcd(
            intent.ExpectedActionId,
            FrozenTargetOrCurrent(intent, context));
        return true;
    }

    private void CommitInitialTransition(
        BlmDecision decision,
        BlmContext context)
    {
        var request = decision.TransitionRequest;
        if (request is null)
        {
            return;
        }

        if (!_coordinator.TryBegin(
                decision.StateGeneration,
                request.Kind,
                request.Step,
                request.DeliveryChannel,
                request.ExpectedActionId,
                request.Expectation,
                request.ModeAtRequest,
                request.StepExpireMs,
                decision.Reason,
                request.IceToFireRoute,
                request.TotalExpireMs,
                decision.TargetEntityId))
        {
            return;
        }

        var intent = _coordinator.Peek();
        if (!_coordinator.TryMarkQueued(
                context.Tracker.StateGeneration,
                intent.Serial,
                intent.StepIndex,
                decision.ActionId,
                BlmQueueChannel.Gcd,
                context.GcdRemainSeconds,
                context.Tracker.LastAckGlobalSequence,
                QueueAckTimeoutFor(decision.ActionId),
                $"Initial GCD queue: {request.Kind}/{request.Step}"))
        {
            Cancel(intent, "初始 Transition 建立后无法原子标记 Queued", context);
        }
    }

    private void CommitFollowUp(
        BlmDecision decision,
        BlmContext context)
    {
        if (decision.FollowUpRequest is { } request)
        {
            _followUp.TryBegin(request, context);
        }
    }

    private void AdvanceConfirmed(BlmContext context)
    {
        var intent = _coordinator.Peek();
        if (!intent.IsActive || intent.Stage != TransitionStage.Confirmed)
        {
            return;
        }

        if (intent.ModeAtRequest != RotationMode.SingleTarget)
        {
            Cancel(intent, "Step 3 尚未实现 AOE Transition 派生", context);
            return;
        }

        if (IsTerminalStep(intent))
        {
            if (!_coordinator.TryComplete(
                    context.Tracker.StateGeneration,
                    intent.Serial,
                    intent.StepIndex,
                    $"Transition completed: {intent.Kind}/{intent.Step}"))
            {
                Cancel(intent, "Transition 终态提交失败", context);
            }

            return;
        }

        if (!TryBuildNextStep(intent, context, out var next))
        {
            if (ShouldWaitForNextStep(intent, context))
            {
                return;
            }

            Cancel(intent, "无法从 Confirmed 状态派生合法下一步", context);
            return;
        }

        var candidate = intent with
        {
            Step = next.Step,
            DeliveryChannel = next.DeliveryChannel,
            ExpectedActionId = next.ActionId,
            ExpectedAdjustedActionId = next.ActionId,
            Expectation = next.Expectation,
        };
        if (!CanUseExpectedActionNow(candidate, context, BlmDecisionPolicy.Default))
        {
            if (ShouldWaitForNextStep(intent, context))
            {
                return;
            }

            Cancel(intent, "下一步动作前置事实已失效", context);
            return;
        }

        if (!_coordinator.TryAdvance(
                context.Tracker.StateGeneration,
                intent.Serial,
                intent.StepIndex,
                next.Step,
                next.DeliveryChannel,
                next.ActionId,
                next.Expectation,
                RequestedStepTimeout(context, next.ActionId),
                next.Reason))
        {
            Cancel(intent, "Coordinator 拒绝下一步状态边", context);
        }
    }

    private bool TryBuildNextStep(
        BlmIntent intent,
        BlmContext context,
        out NextStep next)
    {
        next = default;
        switch (intent.Kind, intent.IceToFireRoute, intent.Step)
        {
            case (TransitionKind.IceToFire, _, TransitionStep.CommitIceGcd):
                next = new NextStep(
                    TransitionStep.UseTranspose,
                    TransitionDeliveryChannel.OffGcdOrAlways,
                    BLMSkill.星灵移位,
                    intent.IceToFireRoute == IceToFireRoute.Af1ParadoxRecovery
                        ? TransitionExpectation.AstralFireOneWithParadox
                        : TransitionExpectation.AstralFireOne,
                    "冰资源 GCD 已确认，进入星灵移位步骤");
                return true;
            case (TransitionKind.IceToFire,
                IceToFireRoute.ExistingFirestarter,
                TransitionStep.UseTranspose):
                next = new NextStep(
                    TransitionStep.UseFirestarterF3,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.爆炎,
                    TransitionExpectation.AstralFireThree,
                    "星灵已确认，消费 Firestarter 爆炎");
                return true;
            case (TransitionKind.IceToFire,
                IceToFireRoute.Af1ParadoxRecovery,
                TransitionStep.UseTranspose):
                next = new NextStep(
                    TransitionStep.UseAfParadox,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.悖论,
                    TransitionExpectation.FirestarterPresent,
                    "星灵已确认，使用 AF1 悖论修复 Firestarter 赤字");
                return true;
            case (TransitionKind.IceToFire,
                IceToFireRoute.Af1ParadoxRecovery,
                TransitionStep.UseAfParadox):
                next = new NextStep(
                    TransitionStep.UseFirestarterF3,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.爆炎,
                    TransitionExpectation.AstralFireThree,
                    "AF1 悖论已确认，使用 Firestarter 爆炎升至 AF3");
                return true;
            case (TransitionKind.IceToFire,
                IceToFireRoute.B4TransposeDespair,
                TransitionStep.UseTranspose):
                next = new NextStep(
                    TransitionStep.UseTransposeDespair,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.绝望,
                    TransitionExpectation.AstralFireThree,
                    "实验路线星灵已确认，使用绝望直接建立火阶段");
                return true;
            case (TransitionKind.FireToIce, _, TransitionStep.CommitFireFinisher):
                next = new NextStep(
                    TransitionStep.UseTranspose,
                    TransitionDeliveryChannel.OffGcd,
                    BLMSkill.星灵移位,
                    TransitionExpectation.UmbralIceOne,
                    "火末 GCD 已确认，进入星灵移位步骤");
                return true;
            case (TransitionKind.FireToIce, _, TransitionStep.UseTranspose):
                if (!context.HasUsableSwiftcast
                    && !context.HasUsableTriplecast
                    && TryGetReadyInstantBuff(context, out var instantAction))
                {
                    next = new NextStep(
                        TransitionStep.UseInstantBuff,
                        TransitionDeliveryChannel.OffGcd,
                        instantAction,
                        instantAction == MageUniversalSkill.即刻咏唱
                            ? TransitionExpectation.SwiftcastPresent
                            : TransitionExpectation.TriplecastPresent,
                        "星灵已确认，释放已就绪的瞬发能力保障冰封");
                    return true;
                }

                if (!context.HasUsableSwiftcast
                    && !context.HasUsableTriplecast
                    && context.HasParadox
                    && CanInstantBuffBecomeReadyWithin(context, context.GcdTotalSeconds))
                {
                    next = new NextStep(
                        TransitionStep.UseIceParadoxWait,
                        TransitionDeliveryChannel.Gcd,
                        BLMSkill.悖论,
                        TransitionExpectation.RemainInIce,
                        "瞬发能力即将转好，使用冰悖论等待一个 GCD");
                    return true;
                }

                next = new NextStep(
                    TransitionStep.UseBlizzard3,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.冰封,
                    TransitionExpectation.UmbralIceThree,
                    "星灵已确认，使用冰封升至 UI3");
                return true;
            case (TransitionKind.FireToIce, _, TransitionStep.UseIceParadoxWait):
                if (!context.HasUsableSwiftcast
                    && !context.HasUsableTriplecast
                    && TryGetReadyInstantBuff(context, out var delayedInstantAction))
                {
                    next = new NextStep(
                        TransitionStep.UseInstantBuff,
                        TransitionDeliveryChannel.OffGcd,
                        delayedInstantAction,
                        delayedInstantAction == MageUniversalSkill.即刻咏唱
                            ? TransitionExpectation.SwiftcastPresent
                            : TransitionExpectation.TriplecastPresent,
                        "冰悖论已确认，释放刚转好的瞬发能力");
                    return true;
                }

                next = new NextStep(
                    TransitionStep.UseBlizzard3,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.冰封,
                    TransitionExpectation.UmbralIceThree,
                    "等待窗口结束，使用当前可用资源或安全硬读冰封");
                return true;
            case (TransitionKind.FireToIce, _, TransitionStep.UseInstantBuff):
                next = new NextStep(
                    TransitionStep.UseBlizzard3,
                    TransitionDeliveryChannel.Gcd,
                    BLMSkill.冰封,
                    TransitionExpectation.UmbralIceThree,
                    "瞬发能力已确认，使用冰封升至 UI3");
                return true;
            default:
                return false;
        }
    }

    private bool CanUseExpectedActionNow(
        BlmIntent intent,
        BlmContext context,
        BlmDecisionPolicy policy)
    {
        if (intent.StateGeneration != context.Tracker.StateGeneration
            || intent.ModeAtRequest != RotationMode.SingleTarget
            || (intent.TargetEntityIdAtRequest != 0
                && intent.TargetEntityIdAtRequest != context.TargetEntityId)
            || !context.IsAvailable
            || !context.IsAlive
            || !context.CanAct
            || !context.HasValidTarget
            || !context.InRange
            || !BlmSkillBook.IsUnlocked(intent.ExpectedActionId, context.Level))
        {
            return false;
        }

        return intent.Step switch
        {
            TransitionStep.CommitIceGcd => CanCommitIceGcd(intent, context, policy),
            TransitionStep.UseTranspose => CanUseTranspose(intent, context),
            TransitionStep.UseAfParadox => context.InFire
                && context.AfStacks == 1
                && context.HasParadox
                && context.Mp >= BlmFireBudget.FireParadoxCost
                && BlmCastSafety.CanCastNow(context, intent.ExpectedActionId, policy),
            TransitionStep.UseFirestarterF3 => context.InFire
                && context.AfStacks == 1
                && context.HasFirestarter
                && BlmCastSafety.CanCastNow(context, intent.ExpectedActionId, policy),
            TransitionStep.CommitFireFinisher => context.InFire
                && context.AfStacks == 3
                && intent.ExpectedActionId == BLMSkill.绝望
                && context.Mp >= BlmFireBudget.DespairMinimumMp
                && context.CanPlanInstantB3AfterDespair
                && BlmCastSafety.CanCastNow(context, intent.ExpectedActionId, policy),
            TransitionStep.UseInstantBuff => context.InIce
                && context.IceStacks >= 1
                && IsInstantBuffReady(intent.ExpectedActionId, context),
            TransitionStep.UseIceParadoxWait => context.InIce
                && context.IceStacks == 1
                && context.HasParadox
                && intent.ExpectedActionId == BLMSkill.悖论,
            TransitionStep.UseTransposeDespair => context.InFire
                && context.AfStacks == 1
                && context.Level >= 100
                && context.Mp >= BlmFireBudget.DespairMinimumMp
                && intent.ExpectedActionId == BLMSkill.绝望,
            TransitionStep.UseBlizzard3 => context.InIce
                && context.IceStacks >= 1
                && intent.ExpectedActionId == BLMSkill.冰封
                && BlmCastSafety.CanCastNow(context, intent.ExpectedActionId, policy),
            _ => false,
        };
    }

    private bool CanCommitIceGcd(
        BlmIntent intent,
        BlmContext context,
        BlmDecisionPolicy policy)
    {
        if (!context.InIce || context.IceStacks != 3)
        {
            return false;
        }

        if (BlmSkillBook.ActionIdsMatch(
                BLMSkill.悖论,
                intent.ExpectedActionId,
                _normalizer))
        {
            return context.HasParadox
                && BlmCastSafety.CanCastNow(context, intent.ExpectedActionId, policy);
        }

        return BlmSkillBook.ActionIdsMatch(
                BLMSkill.冰澈,
                intent.ExpectedActionId,
                _normalizer)
            && context.UmbralHearts < 3
            && BlmCastSafety.CanCastNow(context, intent.ExpectedActionId, policy);
    }

    private static bool CanUseTranspose(
        BlmIntent intent,
        BlmContext context)
    {
        if (intent.ExpectedActionId != BLMSkill.星灵移位
            || !context.Transpose.IsReady)
        {
            return false;
        }

        return intent.Kind switch
        {
            TransitionKind.IceToFire => context.InIce
                && context.IceStacks == 3
                && context.IsMpFull
                && (intent.IceToFireRoute == IceToFireRoute.ExistingFirestarter
                    ? context.HasFirestarter
                    : context.UmbralHearts == 3),
            TransitionKind.FireToIce => context.InFire
                && context.AfStacks == 3
                && context.Transpose.IsReady,
            _ => false,
        };
    }

    private static bool IsTerminalStep(BlmIntent intent)
        => (intent.Kind, intent.IceToFireRoute, intent.Step) switch
        {
            (TransitionKind.IceToFire, _, TransitionStep.UseFirestarterF3) => true,
            (TransitionKind.IceToFire,
                IceToFireRoute.B4TransposeDespair,
                TransitionStep.UseTransposeDespair) => true,
            (TransitionKind.FireToIce, _, TransitionStep.UseBlizzard3) => true,
            _ => false,
        };

    private static bool IsAllowedAlwaysStep(BlmIntent intent)
        => (intent.Kind, intent.Step, intent.ModeAtRequest, intent.ExpectedActionId) switch
        {
            (TransitionKind.IceToFire,
                TransitionStep.UseTranspose,
                RotationMode.SingleTarget,
                BLMSkill.星灵移位) => true,
            _ => false,
        };

    private static bool CanWeave(BlmContext context)
        => !context.IsCasting
            && context.GcdRemainSeconds
                > Math.Max(0.6f, context.AnimationLockSeconds + 0.05f);

    private static bool CanResolveUtilityOgcd(BlmContext context)
        => context.IsAvailable
            && context.AcrState == AcrState.On
            && context.InCombat
            && context.IsAlive
            && context.CanAct
            && context.HasValidTarget
            && context.InRange;

    private void ReconcileUtilityOgcd(BlmContext context)
    {
        if (_utilityOgcdPending is not { } pending)
        {
            return;
        }

        if (pending.StateGeneration != context.Tracker.StateGeneration
            || context.CapturedAtMs > pending.DeadlineAtMs)
        {
            _utilityOgcdPending = null;
            if (pending.StateGeneration != context.Tracker.StateGeneration)
            {
                _utilityUsedAfterGcdAtMs = long.MinValue;
                _utilityUsedAfterGcdActionId = 0;
                _utilityUsedAfterGcdCount = 0;
                _transitionOgcdUsedAfterGcdAtMs = long.MinValue;
                _transitionOgcdUsedAfterGcdActionId = 0;
                _transitionOgcdUsedAfterGcdCount = 0;
            }
            return;
        }

        if (context.Tracker.LastAckGeneration == pending.StateGeneration
            && context.Tracker.LastAckAtMs >= pending.QueuedAtMs
            && BlmSkillBook.ActionIdsMatch(
                pending.ActionId,
                context.Tracker.LastAckActionId,
                _normalizer))
        {
            if (_utilityUsedAfterGcdAtMs != pending.LastGcdAtMs)
            {
                _utilityUsedAfterGcdCount = 0;
            }

            _utilityUsedAfterGcdAtMs = pending.LastGcdAtMs;
            _utilityUsedAfterGcdActionId = pending.ActionId;
            _utilityUsedAfterGcdCount++;
            _utilityOgcdPending = null;
        }
    }

    private bool IsUtilityBlockedForCurrentGcd(
        BlmContext context,
        uint candidateActionId)
    {
        if (candidateActionId == BLMSkill.星灵移位)
        {
            return WasOgcdActionUsedAfterCurrentGcd(
                context,
                BLMSkill.星灵移位);
        }

        if (_utilityUsedAfterGcdAtMs == context.Tracker.LastGcdAtMs
            && _utilityUsedAfterGcdActionId == candidateActionId)
        {
            return true;
        }

        var usedCount = (_utilityUsedAfterGcdAtMs == context.Tracker.LastGcdAtMs
                ? _utilityUsedAfterGcdCount
                : 0)
            + (_transitionOgcdUsedAfterGcdAtMs == context.Tracker.LastGcdAtMs
                ? _transitionOgcdUsedAfterGcdCount
                : 0);
        var capacity = AllowsDoubleWeaveAfterLastGcd(context) ? 2 : 1;
        return usedCount >= capacity;
    }

    private void RecordTransitionOgcd(long lastGcdAtMs, uint actionId)
    {
        if (_transitionOgcdUsedAfterGcdAtMs != lastGcdAtMs)
        {
            _transitionOgcdUsedAfterGcdCount = 0;
        }

        _transitionOgcdUsedAfterGcdAtMs = lastGcdAtMs;
        _transitionOgcdUsedAfterGcdActionId = actionId;
        _transitionOgcdUsedAfterGcdCount++;
    }

    private void QueueTransitionOgcdUsage(BlmContext context, uint actionId)
        => _transitionOgcdPending = new TransitionOgcdPending(
            context.Tracker.StateGeneration,
            actionId,
            context.CapturedAtMs,
            context.CapturedAtMs + OgcdAckTimeoutMs,
            context.Tracker.LastGcdAtMs);

    private void ReconcileTransitionOgcd(BlmContext context)
    {
        if (_transitionOgcdPending is not { } pending)
        {
            return;
        }

        if (pending.StateGeneration != context.Tracker.StateGeneration
            || context.CapturedAtMs > pending.DeadlineAtMs)
        {
            _transitionOgcdPending = null;
            return;
        }

        if (context.Tracker.LastAckGeneration == pending.StateGeneration
            && context.Tracker.LastAckAtMs >= pending.QueuedAtMs
            && BlmSkillBook.ActionIdsMatch(
                pending.ActionId,
                context.Tracker.LastAckActionId,
                _normalizer))
        {
            RecordTransitionOgcd(pending.LastGcdAtMs, pending.ActionId);
            _transitionOgcdPending = null;
        }
    }

    private bool WasOgcdActionUsedAfterCurrentGcd(
        BlmContext context,
        uint actionId)
        => (_utilityUsedAfterGcdAtMs == context.Tracker.LastGcdAtMs
                && _utilityUsedAfterGcdActionId == actionId)
            || (_transitionOgcdUsedAfterGcdAtMs == context.Tracker.LastGcdAtMs
                && _transitionOgcdUsedAfterGcdActionId == actionId);

    private bool AllowsDoubleWeaveAfterLastGcd(BlmContext context)
    {
        if (_confirmedInstantGcdAtMs == context.Tracker.LastGcdAtMs)
        {
            return true;
        }

        var actionId = NormalizeOrOriginal(context.Tracker.LastGcdId);
        return actionId is BLMSkill.悖论
            or BLMSkill.秽浊
            or BLMSkill.异言
            or BLMSkill.高闪雷
            or BLMSkill.高震雷
            || (actionId == BLMSkill.绝望 && context.Level >= 100);
    }

    private void QueueGcdInstant(
        BlmContext context,
        uint actionId,
        bool isInstant)
        => _gcdInstantPending = new GcdInstantPending(
            context.Tracker.StateGeneration,
            actionId,
            context.CapturedAtMs,
            context.CapturedAtMs + QueueAckTimeoutFor(actionId),
            isInstant);

    private void ReconcileGcdInstant(BlmContext context)
    {
        if (_gcdInstantPending is not { } pending)
        {
            return;
        }

        if (pending.StateGeneration != context.Tracker.StateGeneration
            || context.CapturedAtMs > pending.DeadlineAtMs)
        {
            _gcdInstantPending = null;
            if (pending.StateGeneration != context.Tracker.StateGeneration)
            {
                _confirmedInstantGcdAtMs = long.MinValue;
            }
            return;
        }

        if (context.Tracker.LastGcdAtMs >= pending.QueuedAtMs
            && BlmSkillBook.ActionIdsMatch(
                pending.ActionId,
                context.Tracker.LastGcdId,
                _normalizer))
        {
            _confirmedInstantGcdAtMs = pending.IsInstant
                ? context.Tracker.LastGcdAtMs
                : long.MinValue;
            _gcdInstantPending = null;
        }
    }

    private static bool CanWeaveUtilityAlongside(BlmIntent intent)
        => intent.Stage == TransitionStage.Requested
            && intent.DeliveryChannel == TransitionDeliveryChannel.Gcd;

    private static bool ShouldWaitForNextStep(
        BlmIntent intent,
        BlmContext context)
        => intent.Kind == TransitionKind.IceToFire
            && intent.Step == TransitionStep.CommitIceGcd
            && context.InIce
            && !context.IsMpFull;

    private static bool TryGetReadyInstantBuff(
        BlmContext context,
        out uint actionId)
    {
        if (context.SwiftcastEnabled && context.SwiftcastReady)
        {
            actionId = MageUniversalSkill.即刻咏唱;
            return true;
        }

        if (context.TriplecastEnabled && context.TriplecastReady)
        {
            actionId = BLMSkill.三连咏唱;
            return true;
        }

        actionId = 0;
        return false;
    }

    private static bool CanInstantBuffBecomeReadyWithin(
        BlmContext context,
        float seconds)
        => (context.SwiftcastEnabled
                && IsActionReadyWithin(context.Swiftcast, seconds))
            || (context.TriplecastEnabled
                && IsActionReadyWithin(context.Triplecast, seconds));

    private static bool IsActionReadyWithin(
        BlmActionAvailability action,
        float seconds)
    {
        if (!action.IsUnlocked || !action.IsAvailable)
        {
            return false;
        }

        if (action.IsReady)
        {
            return true;
        }

        var remain = action.MaxCharges > 1
            ? action.NextChargeRemainSeconds
            : action.CooldownRemainSeconds;
        return remain <= Math.Max(0f, seconds);
    }

    private static bool IsInstantBuffReady(uint actionId, BlmContext context)
        => actionId switch
        {
            MageUniversalSkill.即刻咏唱 => context.SwiftcastEnabled
                && context.SwiftcastReady,
            BLMSkill.三连咏唱 => context.TriplecastEnabled
                && context.TriplecastReady,
            _ => false,
        };

    private static bool HasReachedNextGcdWindow(
        long queuedAtMs,
        BlmContext context)
    {
        if (queuedAtMs <= 0 || context.GcdRemainSeconds > 0.3f)
        {
            return false;
        }

        var minimumElapsedMs = (long)Math.Ceiling(
            Math.Max(0.5f, context.GcdTotalSeconds - 0.3f) * 1000f);
        return context.CapturedAtMs - queuedAtMs >= minimumElapsedMs;
    }

    private static long RequestedStepTimeout(
        BlmContext context,
        uint actionId)
    {
        var waitForGcdMs = (long)Math.Ceiling(
            Math.Max(0f, context.GcdRemainSeconds) * 1000f)
            + RequestedStepHandoffMarginMs;
        var baseTimeout = actionId switch
        {
            BLMSkill.星灵移位 => OgcdAckTimeoutMs,
            BLMSkill.冰封 => Blizzard3AckTimeoutMs,
            _ => InstantGcdAckTimeoutMs,
        };
        return Math.Max(baseTimeout, waitForGcdMs);
    }

    private static long QueueAckTimeoutFor(uint actionId) => actionId switch
    {
        BLMSkill.冰封 => Blizzard3AckTimeoutMs,
        BLMSkill.冰澈 or BLMSkill.炽炎 or BLMSkill.耀星 => CastGcdAckTimeoutMs,
        BLMSkill.星灵移位 or BLMSkill.魔泉 => OgcdAckTimeoutMs,
        _ => InstantGcdAckTimeoutMs,
    };

    private static uint FrozenTargetOrCurrent(
        BlmIntent intent,
        BlmContext context)
        => intent.TargetEntityIdAtRequest != 0
            ? intent.TargetEntityIdAtRequest
            : context.TargetEntityId;

    private static PAction ToTargetGcd(uint actionId, uint targetEntityId)
    {
        if (actionId == 0 || targetEntityId == 0)
        {
            throw new InvalidOperationException("目标 GCD 缺少动作或冻结目标。");
        }

        return new PAction(actionId, ActionType.Gcd, ActionTargetType.Self)
        {
            NetworkTid = targetEntityId,
        };
    }

    private static PAction ToSelfAction(uint actionId, ActionType type)
        => new(actionId, type, ActionTargetType.Self);

    private sealed record UtilityOgcdPending(
        long StateGeneration,
        uint ActionId,
        long QueuedAtMs,
        long DeadlineAtMs,
        long LastGcdAtMs);

    private sealed record GcdInstantPending(
        long StateGeneration,
        uint ActionId,
        long QueuedAtMs,
        long DeadlineAtMs,
        bool IsInstant);

    private sealed record TransitionOgcdPending(
        long StateGeneration,
        uint ActionId,
        long QueuedAtMs,
        long DeadlineAtMs,
        long LastGcdAtMs);

    private void Cancel(BlmIntent intent, string reason, BlmContext context)
    {
        var requiresQueueCleanup = IsUnacknowledgedDeliveredWork(intent);
        if (_coordinator.TryCancel(
                intent.StateGeneration,
                intent.Serial,
                intent.StepIndex,
                reason)
            && requiresQueueCleanup)
        {
            ClearNormalPrQueues(context, reason);
        }
    }

    private bool HasUnacknowledgedDeliveredWork()
        => IsUnacknowledgedDeliveredWork(_coordinator.Peek())
            || _followUp.Peek().Stage is BlmFollowUpStage.AwaitingTriggerAck
                or BlmFollowUpStage.RequiredQueued;

    private static bool IsUnacknowledgedDeliveredWork(BlmIntent intent)
        => intent.IsActive
            && intent.Stage == TransitionStage.Queued
            && !intent.HasExpectedAck;

    private void TraceDispatch(
        BlmContext context,
        string entryPoint,
        uint actionId,
        ActionType actionType,
        string source)
    {
        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.DispatchReturned,
            Context = context,
            MonotonicMs = context.CapturedAtMs,
            EntryPoint = entryPoint,
            ActionId = actionId,
            NormalizedActionId = NormalizeOrOriginal(actionId),
            PActionType = actionType.ToString(),
            Reason = source,
            Detail = "已向 PromeRotation 返回候选动作；尚未视为服务器确认释放。",
            TargetEntityId = context.TargetEntityId,
            Transition = _coordinator.Peek(),
            FollowUp = _followUp.Peek(),
        });
    }

    private void TraceStateChanges(
        BlmIntent transitionBefore,
        BlmFollowUpIntent followUpBefore,
        BlmContext context,
        string source)
    {
        var transitionAfter = _coordinator.Peek();
        var followUpAfter = _followUp.Peek();
        if (!Equals(transitionBefore, transitionAfter))
        {
            PublishDebug(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.TransitionChanged,
                Context = context,
                MonotonicMs = context.CapturedAtMs,
                EntryPoint = source,
                ActionId = transitionAfter.ExpectedActionId,
                NormalizedActionId = NormalizeOrOriginal(transitionAfter.ExpectedActionId),
                Reason = transitionAfter.Reason,
                Detail = $"{transitionBefore.Kind}/{transitionBefore.Step}/{transitionBefore.Stage}"
                    + $" -> {transitionAfter.Kind}/{transitionAfter.Step}/{transitionAfter.Stage}",
                TargetEntityId = context.TargetEntityId,
                Transition = transitionAfter,
                FollowUp = followUpAfter,
            });
        }

        if (!Equals(followUpBefore, followUpAfter))
        {
            var actionId = followUpAfter.Stage is BlmFollowUpStage.AwaitingTriggerAck
                or BlmFollowUpStage.TriggerAcknowledged
                ? followUpAfter.TriggerActionId
                : followUpAfter.RequiredActionId;
            PublishDebug(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.FollowUpChanged,
                Context = context,
                MonotonicMs = context.CapturedAtMs,
                EntryPoint = source,
                ActionId = actionId,
                NormalizedActionId = NormalizeOrOriginal(actionId),
                Reason = followUpAfter.Reason,
                Detail = $"{followUpBefore.Kind}/{followUpBefore.Stage}"
                    + $" -> {followUpAfter.Kind}/{followUpAfter.Stage}",
                TargetEntityId = context.TargetEntityId,
                Transition = transitionAfter,
                FollowUp = followUpAfter,
            });
        }
    }

    private uint NormalizeOrOriginal(uint actionId)
    {
        if (actionId == 0)
        {
            return 0;
        }

        try
        {
            var normalized = _normalizer.Normalize(actionId);
            return normalized == 0 ? actionId : normalized;
        }
        catch
        {
            return actionId;
        }
    }

    private void PublishDebug(BlmDebugEventDraft draft)
    {
        try
        {
            _debug.Publish(draft);
        }
        catch
        {
            // Diagnostics must never alter rotation behavior.
        }
    }

    private void ClearNormalPrQueues(BlmContext context, string reason)
    {
        // PR 1.5.2.3 has no owner-aware removal API. Normal queues contain the
        // ACR work; high-priority hotkey/timeline queues remain untouched.
        ActionQueueManager.ClearNormalQueues();
        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.QueueCleared,
            Context = context,
            MonotonicMs = context.CapturedAtMs,
            EntryPoint = "Dispatcher",
            Reason = reason,
            Detail = "仅清理 PR 普通 GCD/oGCD 队列；高优队列保持不变。",
            TargetEntityId = context.TargetEntityId,
            Transition = _coordinator.Peek(),
            FollowUp = _followUp.Peek(),
        });
    }

    private readonly record struct NextStep(
        TransitionStep Step,
        TransitionDeliveryChannel DeliveryChannel,
        uint ActionId,
        TransitionExpectation Expectation,
        string Reason);
}
