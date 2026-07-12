namespace LosPr.BLM;

public sealed class BlackMageEventHandler : IRotationEventHandler, IDisposable
{
    private const int MaxPendingAcks = 256;

    private readonly BlmStateTracker _tracker;
    private readonly BlmActionDispatcher _dispatcher;
    private readonly IBlmClock _clock;
    private readonly IBlmDebugSink _debug;
    private readonly ConcurrentQueue<BlmActionEffectAck> _pendingAcks = new();
    private int _pendingAckCount;
    private bool _disposed;
    private long _nextErrorLogAtMs;

    public BlackMageEventHandler(
        BlmStateTracker tracker,
        BlmActionDispatcher dispatcher,
        IBlmClock? clock = null,
        IBlmDebugSink? debugSink = null)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _clock = clock ?? SystemBlmClock.Instance;
        _debug = debugSink ?? NullBlmDebugSink.Instance;
        CombatEventManager.OnActionEffect += OnActionEffect;
        EventManager.OnPlayerDied += OnPlayerDied;
        EventManager.OnPlayerRevived += OnPlayerRevived;
    }

    public void OnUpdate()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            while (_pendingAcks.TryDequeue(out var ack))
            {
                Interlocked.Decrement(ref _pendingAckCount);
                var before = _tracker.GetContextSnapshot();
                var accepted = _tracker.ApplyActionEffect(ack);
                var after = _tracker.GetContextSnapshot();
                PublishDebug(new BlmDebugEventDraft
                {
                    Kind = accepted
                        ? BlmDebugEventKind.AckAccepted
                        : BlmDebugEventKind.AckRejected,
                    Context = accepted ? after : before,
                    MonotonicMs = ack.ReceivedAtMs,
                    EntryPoint = "FrameworkTick.ApplyAck",
                    ActionId = ack.ActionId,
                    GlobalSequence = ack.GlobalSequence,
                    Accepted = accepted,
                    Reason = accepted
                        ? "Tracker 已接受服务器 ActionEffect Ack。"
                        : DescribeAckRejection(ack, before),
                    Detail = $"PhaseBefore={ack.PhaseBefore} / PhaseSerial={ack.PhaseSerialBefore}",
                    TargetEntityId = before.TargetEntityId,
                    Transition = after.Tracker.Transition,
                    FollowUp = after.Tracker.FollowUp,
                });
                TraceStateChanges(before, after, "FrameworkTick.ApplyAck", ack.ActionId);
            }

            var beforeGauge = _tracker.GetContextSnapshot();
            var context = BlmContext.Capture(_clock) with
            {
                Tracker = beforeGauge.Tracker,
            };
            _dispatcher.PrepareForReconcile(context);
            _tracker.Reconcile(context);
            var afterGauge = _tracker.GetContextSnapshot();
            if (afterGauge.Tracker.LastGaugeReconciledAtMs > 0
                && (afterGauge.Tracker.LastGaugeReconciledAtMs
                        != beforeGauge.Tracker.LastGaugeReconciledAtMs
                    || afterGauge.Tracker.LastGaugeReconciledActionId
                        != beforeGauge.Tracker.LastGaugeReconciledActionId))
            {
                PublishDebug(new BlmDebugEventDraft
                {
                    Kind = BlmDebugEventKind.GaugeReconciled,
                    Context = afterGauge,
                    MonotonicMs = afterGauge.Tracker.LastGaugeReconciledAtMs,
                    EntryPoint = "FrameworkTick.Gauge",
                    ActionId = afterGauge.Tracker.LastGaugeReconciledActionId,
                    GlobalSequence = afterGauge.Tracker.LastAckGlobalSequence,
                    Accepted = true,
                    Reason = "ActionEffect 后的下一 Framework Tick Gauge 已对账。",
                    Detail = FormatResourceDelta(beforeGauge, afterGauge),
                    TargetEntityId = afterGauge.TargetEntityId,
                    Transition = afterGauge.Tracker.Transition,
                    FollowUp = afterGauge.Tracker.FollowUp,
                });
            }

            TraceStateChanges(
                beforeGauge,
                afterGauge,
                "FrameworkTick.TrackerReconcile",
                afterGauge.Tracker.LastGaugeReconciledActionId);
            _dispatcher.Reconcile(
                afterGauge,
                HasHighPriorityAction());
            var afterDispatcher = afterGauge with
            {
                Tracker = _tracker.GetTrackerSnapshot(),
            };
            TraceStateChanges(
                afterGauge,
                afterDispatcher,
                "FrameworkTick.DispatcherReconcile");
        }
        catch (Exception exception)
        {
            LogErrorThrottled(exception, "Framework Tick 状态对账失败");
        }
    }

    public void OnOutOfBattleUpdate()
    {
    }

    public void OnBattleStarted()
    {
        var before = _tracker.GetContextSnapshot();
        _tracker.BeginCombat();
        var after = _tracker.GetContextSnapshot();
        TraceLifecycle("BattleStarted", "战斗开始", before, after);
    }

    public void OnBattleUpdate()
    {
    }

    public void OnNoTarget()
    {
        var context = _tracker.GetContextSnapshot();
        _dispatcher.CancelAll("目标失效", context);
        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.Lifecycle,
            Context = context,
            MonotonicMs = _clock.NowMs,
            EntryPoint = "NoTarget",
            Reason = "目标失效",
            Detail = "已取消活动承诺；必要时清理 PR 普通队列。",
            Transition = _tracker.GetTrackerSnapshot().Transition,
            FollowUp = _tracker.GetTrackerSnapshot().FollowUp,
        });
    }

    public void OnBattleEnded()
    {
        var before = _tracker.GetContextSnapshot();
        _tracker.EndCombat();
        var after = _tracker.GetContextSnapshot();
        PromeSettings.Instance.OpenerHasBeenExecuted = false;
        TraceLifecycle("BattleEnded", "战斗结束", before, after);
    }

    public void OnTerritoryChanged(ushort territoryId)
    {
        var before = _tracker.GetContextSnapshot();
        _tracker.OnTerritoryChanged(territoryId);
        var after = _tracker.GetContextSnapshot();
        PromeSettings.Instance.OpenerHasBeenExecuted = false;
        TraceLifecycle(
            "TerritoryChanged",
            "区域切换",
            before,
            after,
            $"TerritoryId={territoryId}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.Lifecycle,
            Context = _tracker.GetContextSnapshot(),
            MonotonicMs = _clock.NowMs,
            EntryPoint = "EventHandler.Dispose",
            Reason = "事件处理器停止",
        });
        _disposed = true;
        CombatEventManager.OnActionEffect -= OnActionEffect;
        EventManager.OnPlayerDied -= OnPlayerDied;
        EventManager.OnPlayerRevived -= OnPlayerRevived;
        var discarded = 0;
        while (_pendingAcks.TryDequeue(out _))
        {
            discarded++;
        }

        Interlocked.Exchange(ref _pendingAckCount, 0);
        if (discarded > 0)
        {
            PublishDebug(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.AckQueueDropped,
                Context = _tracker.GetContextSnapshot(),
                MonotonicMs = _clock.NowMs,
                EntryPoint = "EventHandler.Dispose",
                DroppedCount = discarded,
                Reason = "事件处理器停止时丢弃未处理 Ack。",
            });
        }
    }

    private void OnActionEffect(ActionEffectEvent actionEffect)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var context = _tracker.GetContextSnapshot();
            var playerEntityId = context.PlayerEntityId;
            if (playerEntityId == 0 || actionEffect.SourceId != playerEntityId)
            {
                return;
            }

            var receivedAtMs = _clock.NowMs;
            var observedGcd = CaptureObservedGcdFacts(receivedAtMs, context);
            var envelope = _tracker.CreateAckEnvelope(
                actionEffect.SourceId,
                actionEffect.ActionId,
                actionEffect.GlobalSequence,
                ReadPreGaugePhase(),
                receivedAtMs,
                observedGcd.StartedAtMs,
                observedGcd.RemainMs,
                observedGcd.HasHaste);
            PublishDebug(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.ActionEffectObserved,
                Context = context,
                MonotonicMs = envelope.ReceivedAtMs,
                EntryPoint = "OnActionEffect",
                ActionId = envelope.ActionId,
                GlobalSequence = envelope.GlobalSequence,
                Reason = "观察到自身 ActionEffect，已冻结 Ack Token。",
                Detail = $"PhaseBefore={envelope.PhaseBefore} / PhaseSerial={envelope.PhaseSerialBefore}",
                TargetEntityId = context.TargetEntityId,
                Transition = context.Tracker.Transition,
                FollowUp = context.Tracker.FollowUp,
            });
            _pendingAcks.Enqueue(envelope);
            var queued = Interlocked.Increment(ref _pendingAckCount);
            while (queued > MaxPendingAcks && _pendingAcks.TryDequeue(out var dropped))
            {
                queued = Interlocked.Decrement(ref _pendingAckCount);
                PublishDebug(new BlmDebugEventDraft
                {
                    Kind = BlmDebugEventKind.AckQueueDropped,
                    Context = context,
                    MonotonicMs = _clock.NowMs,
                    EntryPoint = "OnActionEffect",
                    ActionId = dropped.ActionId,
                    GlobalSequence = dropped.GlobalSequence,
                    DroppedCount = 1,
                    Reason = "Ack 队列超过 256，丢弃最旧事件并保留最新现场。",
                    TargetEntityId = context.TargetEntityId,
                    Transition = context.Tracker.Transition,
                    FollowUp = context.Tracker.FollowUp,
                });
            }
        }
        catch (Exception exception)
        {
            LogErrorThrottled(exception, "ActionEffect Ack 入队失败");
        }
    }

    private void OnPlayerDied()
    {
        try
        {
            var before = _tracker.GetContextSnapshot();
            _tracker.OnPlayerDied();
            TraceLifecycle(
                "PlayerDied",
                "角色死亡",
                before,
                _tracker.GetContextSnapshot());
        }
        catch (Exception exception)
        {
            LogErrorThrottled(exception, "死亡状态重置失败");
        }
    }

    private void OnPlayerRevived()
    {
        try
        {
            var before = _tracker.GetContextSnapshot();
            _tracker.OnPlayerRevived();
            TraceLifecycle(
                "PlayerRevived",
                "角色复活",
                before,
                _tracker.GetContextSnapshot());
        }
        catch (Exception exception)
        {
            LogErrorThrottled(exception, "复活状态重置失败");
        }
    }

    private BlmPhase ReadPreGaugePhase()
    {
        try
        {
            var gauge = Svc.Gauges.Get<BLMGauge>();
            return gauge.InAstralFire
                ? BlmPhase.Fire
                : gauge.InUmbralIce
                    ? BlmPhase.Ice
                    : BlmPhase.Neutral;
        }
        catch
        {
            return _tracker.GetContextSnapshot().Phase;
        }
    }

    private ObservedGcdFacts CaptureObservedGcdFacts(
        long receivedAtMs,
        BlmContext context)
    {
        var startedAtMs = context.Tracker.LastGcdStartedAtMs;
        var remainMs = 0f;
        try
        {
            var gcdTotalSeconds = Math.Max(0f, ActionHelper.GetGcdTotal());
            var gcdRemainSeconds = Math.Max(0f, ActionHelper.GetGcdRemain());
            remainMs = gcdRemainSeconds * 1000f;
            if (BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(
                    receivedAtMs,
                    gcdTotalSeconds,
                    gcdRemainSeconds,
                    out var observedStartedAtMs))
            {
                startedAtMs = observedStartedAtMs;
            }
        }
        catch
        {
        }

        var hasHaste = context.HasLeyLinesHaste;
        try
        {
            if (PRCore.Me is { } me)
            {
                hasHaste = me.HasStatus(BlmBuff.咏速);
            }
        }
        catch
        {
        }

        return new ObservedGcdFacts(startedAtMs, remainMs, hasHaste);
    }

    private void TraceLifecycle(
        string entryPoint,
        string reason,
        BlmContext before,
        BlmContext after,
        string detail = "")
    {
        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.Lifecycle,
            Context = after,
            MonotonicMs = _clock.NowMs,
            EntryPoint = entryPoint,
            Reason = reason,
            Detail = detail,
            TargetEntityId = after.TargetEntityId,
            Transition = after.Tracker.Transition,
            FollowUp = after.Tracker.FollowUp,
        });
        TraceStateChanges(before, after, entryPoint);
    }

    private void TraceStateChanges(
        BlmContext before,
        BlmContext after,
        string entryPoint,
        uint actionId = 0)
    {
        var transitionBefore = before.Tracker.Transition;
        var transitionAfter = after.Tracker.Transition;
        var followUpBefore = before.Tracker.FollowUp;
        var followUpAfter = after.Tracker.FollowUp;
        if (!Equals(transitionBefore, transitionAfter))
        {
            PublishDebug(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.TransitionChanged,
                Context = after,
                MonotonicMs = _clock.NowMs,
                EntryPoint = entryPoint,
                ActionId = actionId != 0 ? actionId : transitionAfter.ExpectedActionId,
                Reason = transitionAfter.Reason,
                Detail = $"{transitionBefore.Kind}/{transitionBefore.Step}/{transitionBefore.Stage}"
                    + $" -> {transitionAfter.Kind}/{transitionAfter.Step}/{transitionAfter.Stage}",
                TargetEntityId = after.TargetEntityId,
                Transition = transitionAfter,
                FollowUp = followUpAfter,
            });
        }

        if (!Equals(followUpBefore, followUpAfter))
        {
            var expectedActionId = followUpAfter.Stage is BlmFollowUpStage.AwaitingTriggerAck
                or BlmFollowUpStage.TriggerAcknowledged
                ? followUpAfter.TriggerActionId
                : followUpAfter.RequiredActionId;
            PublishDebug(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.FollowUpChanged,
                Context = after,
                MonotonicMs = _clock.NowMs,
                EntryPoint = entryPoint,
                ActionId = actionId != 0 ? actionId : expectedActionId,
                Reason = followUpAfter.Reason,
                Detail = $"{followUpBefore.Kind}/{followUpBefore.Stage}"
                    + $" -> {followUpAfter.Kind}/{followUpAfter.Stage}",
                TargetEntityId = after.TargetEntityId,
                Transition = transitionAfter,
                FollowUp = followUpAfter,
            });
        }
    }

    private static string FormatResourceDelta(BlmContext before, BlmContext after)
        => $"MP {before.Mp}->{after.Mp}; Phase {before.Phase}->{after.Phase}; "
            + $"AF/UI {before.AfStacks}/{before.IceStacks}"
            + $"->{after.AfStacks}/{after.IceStacks}; "
            + $"Hearts {before.UmbralHearts}->{after.UmbralHearts}; "
            + $"Soul {before.AstralSoul}->{after.AstralSoul}; "
            + $"Paradox {before.HasParadox}->{after.HasParadox}; "
            + $"Firestarter {before.HasFirestarter}->{after.HasFirestarter}; "
            + $"Thunderhead {before.HasThunderhead}->{after.HasThunderhead}; "
            + $"Polyglot {before.PolyglotStacks}->{after.PolyglotStacks}";

    private static string DescribeAckRejection(
        BlmActionEffectAck ack,
        BlmContext context)
    {
        if (ack.ActionId == 0)
        {
            return "Tracker 拒绝 ActionId=0 的 Ack。";
        }

        if (ack.StateGeneration != context.Tracker.StateGeneration)
        {
            return $"Tracker 拒绝旧 Generation Ack：{ack.StateGeneration}"
                + $" != {context.Tracker.StateGeneration}。";
        }

        if (ack.CombatSerial != context.Tracker.CombatSerial)
        {
            return $"Tracker 拒绝旧 Combat Ack：{ack.CombatSerial}"
                + $" != {context.Tracker.CombatSerial}。";
        }

        if (ack.GlobalSequence <= context.Tracker.LastAckGlobalSequence)
        {
            return "Tracker 拒绝重复或乱序 Ack。";
        }

        return "Tracker 拒绝 Ack：来源、生命周期或幂等校验未通过。";
    }

    private void PublishDebug(BlmDebugEventDraft draft)
    {
        try
        {
            _debug.Publish(draft);
        }
        catch
        {
            // Diagnostics must never alter event processing.
        }
    }

    private void LogErrorThrottled(Exception exception, string message)
    {
        var now = _clock.NowMs;
        if (now < Interlocked.Read(ref _nextErrorLogAtMs))
        {
            return;
        }

        Interlocked.Exchange(ref _nextErrorLogAtMs, now + 5000);
        PublishDebug(new BlmDebugEventDraft
        {
            Kind = BlmDebugEventKind.LoggerError,
            Context = _tracker.GetContextSnapshot(),
            MonotonicMs = now,
            EntryPoint = "EventHandler",
            Reason = message,
            Detail = exception.GetType().Name,
        });
        try
        {
            Svc.Log.Error(exception, $"[Los] {message}。");
        }
        catch
        {
        }
    }

    private static bool HasHighPriorityAction()
    {
        try
        {
            return ActionQueueManager.HasHighPriorityAction();
        }
        catch
        {
            return false;
        }
    }

    private readonly record struct ObservedGcdFacts(
        long StartedAtMs,
        float RemainMs,
        bool HasHaste);
}
