using System.Linq;
using LosPr.BLM.Resolvers;
using LosPr.BLM.UI;
using PromeRotation.Updaters;

namespace LosPr.BLM.Openers;

internal sealed class BlmOpenerExecutionService
{
    private const long GcdAckTimeoutMs = 5500;
    private const long Fire3AckTimeoutMs = 6500;
    private const long OffGcdAckTimeoutMs = 2500;
    private const long GaugeConfirmationTimeoutMs = 2000;
    private const long StepReadyTimeoutMs = 5000;
    private const long PotionAttemptConfirmationMs = 250;
    private const long PotionRetryDelayMs = 100;
    private const long PotionAttemptWindowMs = 1500;

    private readonly object _gate = new();
    private readonly BlmStateTracker _tracker;
    private readonly IBlmActionIdNormalizer _normalizer;
    private readonly IBlmClock _clock;
    private readonly IBlmDebugSink _debug;
    private readonly Func<BlmOpenerPolicy> _policyProvider;
    private readonly Func<float> _countdownProvider;
    private readonly Func<uint> _potionProvider;
    private readonly Func<uint, uint> _adjustActionId;
    private readonly Action<PAction> _preCombatDispatcher;
    private readonly Action<string> _countdownCanceller;
    private readonly Action _normalQueueClearer;
    private readonly Func<bool> _hasActiveCommandProvider;
    private readonly Action _actionUpdaterResetter;
    private readonly Func<uint, bool> _potionDispatcher;
    private readonly Func<uint, float> _potionCooldownProvider;

    private BlmOpenerPlan? _plan;
    private BlmOpenerStatus _status;
    private int _stepIndex;
    private uint _targetEntityId;
    private long _stateGeneration;
    private long _combatSerial;
    private long _lastAttemptedCombatSerial = -1;
    private bool _combatRebased;
    private long _stepReadyAtMs;
    private long _countdownEndedAtMs;
    private int _fire4BeforeManafont;
    private int _fire4AfterManafont;
    private long _potionRetryAtMs;
    private int _potionAttemptCount;

    private bool _hasPending;
    private bool _pendingTracked;
    private uint _pendingRequestedActionId;
    private uint _pendingAdjustedActionId;
    private long _pendingIssuedAtMs;
    private long _pendingDeadlineAtMs;
    private bool _pendingWasInstant;
    private bool _pendingIsGcd;
    private bool _pendingHardcastObserved;
    private float _pendingHardcastLastRemainSeconds;
    private BlmActionEffectAck? _acceptedAck;
    private string? _cancelRequested;
    private string _lastReason = "尚未启动";

    public BlmOpenerExecutionService(
        BlmStateTracker tracker,
        IBlmActionIdNormalizer normalizer,
        IBlmClock? clock = null,
        IBlmDebugSink? debugSink = null,
        Func<BlmOpenerPolicy>? policyProvider = null,
        Func<float>? countdownProvider = null,
        Func<uint>? potionProvider = null,
        Func<uint, uint>? adjustActionId = null,
        Action<PAction>? preCombatDispatcher = null,
        Action<string>? countdownCanceller = null,
        Action? normalQueueClearer = null,
        Func<bool>? hasActiveCommandProvider = null,
        Action? actionUpdaterResetter = null,
        Func<uint, bool>? potionDispatcher = null,
        Func<uint, float>? potionCooldownProvider = null)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _normalizer = normalizer ?? throw new ArgumentNullException(nameof(normalizer));
        _clock = clock ?? SystemBlmClock.Instance;
        _debug = debugSink ?? NullBlmDebugSink.Instance;
        _policyProvider = policyProvider ?? (() => default);
        _countdownProvider = countdownProvider ?? ReadCountdown;
        _potionProvider = potionProvider ?? ReadPotion;
        _adjustActionId = adjustActionId ?? AdjustActionId;
        _preCombatDispatcher = preCombatDispatcher ?? ActionUpdater.UseAction;
        _countdownCanceller = countdownCanceller ?? CountdownManager.ResetForRefresh;
        _normalQueueClearer = normalQueueClearer ?? ActionQueueManager.ClearNormalQueues;
        _hasActiveCommandProvider = hasActiveCommandProvider ?? ActionUpdater.HasActiveCommand;
        _actionUpdaterResetter = actionUpdaterResetter ?? ActionUpdater.Reset;
        _potionDispatcher = potionDispatcher ?? DispatchPotion;
        _potionCooldownProvider = potionCooldownProvider ?? ReadPotionCooldown;
    }

    public bool OwnsExecution
    {
        get
        {
            lock (_gate)
            {
                return OwnsExecutionNoLock();
            }
        }
    }

    public BlmOpenerSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            var step = CurrentStepNoLock();
            return new BlmOpenerSnapshot
            {
                Variant = _plan?.Variant ?? BlmOpenerVariant.None,
                Mode = _plan?.Mode ?? BlmOpenerMode.None,
                Status = _status,
                StepIndex = _stepIndex,
                StepCount = _plan?.Steps.Length ?? 0,
                StepId = step?.Id ?? string.Empty,
                ExpectedActionId = step?.ActionId ?? 0,
                HasPendingAction = _hasPending,
                OwnsExecution = OwnsExecutionNoLock(),
                Fire4BeforeManafont = _fire4BeforeManafont,
                Fire4AfterManafont = _fire4AfterManafont,
                LastReason = _lastReason,
            };
        }
    }

    public bool TryArmCountdown(BlmContext context)
        => TryArmCountdown(
            context,
            BlmOpenerVariant.Standard57,
            SafeReadPolicy(),
            SafeReadPotion());

    public bool TryArmCountdown(BlmContext context, BlmOpenerVariant variant)
        => TryArmCountdown(context, variant, SafeReadPolicy(), SafeReadPotion());

    internal bool TryArmCountdown(
        BlmContext context,
        BlmOpenerPolicy policy,
        uint potionId)
        => TryArmCountdown(
            context,
            BlmOpenerVariant.Standard57,
            policy,
            potionId);

    internal bool TryArmCountdown(
        BlmContext context,
        BlmOpenerVariant variant,
        BlmOpenerPolicy policy,
        uint potionId)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (OwnsExecutionNoLock())
            {
                return _plan?.Mode == BlmOpenerMode.HighEndCountdown
                    && _plan.Variant == variant;
            }

            if (!policy.HighEndCountdownEnabled
                || !IsVariantEnabled(policy, variant)
                || context.InCombat
                || !BlmAdditionalOpenerDefinitions.SupportsLevel(variant, context.Level))
            {
                return false;
            }

            var plan = BlmAdditionalOpenerDefinitions.BuildCountdown(
                variant,
                context.Level,
                context.DotEnabled,
                policy.HighEndPotionEnabled ? potionId : 0,
                policy.NoTriplecast);
            if (!CanStart(context, plan))
            {
                return false;
            }

            StartSessionNoLock(
                plan,
                context,
                BlmOpenerStatus.Armed,
                $"检测到倒计时，已武装{plan.DisplayName}");
            ArmExternalFire3NoLock();
            PublishNoLock(
                context,
                BlmDebugEventKind.Lifecycle,
                "Opener.Arm.HighEnd",
                0,
                _lastReason,
                detail: $"PotionEnabled={policy.HighEndPotionEnabled}; "
                    + $"ResolvedPotionId={potionId}; PlannedPotionId={plan.PotionId}");
            return true;
        }
    }

    public void OnAcceptedAction(BlmActionEffectAck ack)
    {
        lock (_gate)
        {
            if (!OwnsExecutionNoLock() || !_hasPending)
            {
                return;
            }

            if (ActionMatchesNoLock(
                    _pendingRequestedActionId,
                    _pendingAdjustedActionId,
                    ack.ActionId))
            {
                _acceptedAck = ack;
                _pendingDeadlineAtMs = Math.Max(
                    _pendingDeadlineAtMs,
                    ack.ReceivedAtMs + GaugeConfirmationTimeoutMs);
                return;
            }

            if (BlmHotkeyCatalog.ConsumeManualAbilityObservation(ack.ActionId))
            {
                PublishNoLock(
                    _tracker.GetContextSnapshot(),
                    BlmDebugEventKind.AckAccepted,
                    "Opener.ManualHotkeyIgnored",
                    ack.ActionId,
                    "已确认安全 Hotkey 插入，保持当前起手步骤不变",
                    ack.GlobalSequence);
                return;
            }

            if (BlmSkillBook.IsKnownGcdAction(ack.ActionId, _normalizer)
                || BlmSkillBook.IsKnownSelfAbilityId(ack.ActionId))
            {
                _cancelRequested = $"观察到非预期动作 {ack.ActionId}，按手动覆盖取消";
            }
        }
    }

    internal PAction? TryCreateCountdownPrecastAction(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (_plan?.Mode != BlmOpenerMode.HighEndCountdown
                || _status != BlmOpenerStatus.Armed
                || _stepIndex != 0
                || !_hasPending
                || _acceptedAck is not null
                || !IsVariantEnabled(SafeReadPolicy(), _plan.Variant)
                || !context.HasValidTarget
                || !context.InRange
                || context.TargetEntityId != _targetEntityId)
            {
                return null;
            }

            return BlmCountdownOpenerBase.CreateFireThreePrecastAction(context);
        }
    }

    public void Update(BlmContext context, bool highPriorityQueueActive)
        => Update(
            context,
            highPriorityQueueActive,
            SafeReadPolicy(),
            SafeReadCountdown());

    internal void Update(
        BlmContext context,
        bool highPriorityQueueActive,
        BlmOpenerPolicy policy,
        float countdownSeconds)
    {
        ArgumentNullException.ThrowIfNull(context);
        PAction? preCombatAction = null;
        BlmOpenerStep? preCombatStep = null;
        lock (_gate)
        {
            if (_status == BlmOpenerStatus.Draining)
            {
                if (!SafeHasActiveCommand() && !highPriorityQueueActive)
                {
                    FinishCancellationNoLock(context);
                }

                return;
            }

            if (!OwnsExecutionNoLock())
            {
                TryStartDailyNoLock(context, highPriorityQueueActive, policy);
            }

            if (!OwnsExecutionNoLock())
            {
                return;
            }

            if (_plan is null || !IsVariantEnabled(policy, _plan.Variant))
            {
                CancelNoLock(context, "起手 QT 已关闭");
                return;
            }

            if (_cancelRequested is { } cancelRequested)
            {
                CancelNoLock(context, cancelRequested);
                return;
            }

            if (!TryReconcileGenerationNoLock(context))
            {
                return;
            }

            if (!ValidateActiveContextNoLock(context, highPriorityQueueActive))
            {
                return;
            }

            TrackCountdownNoLock(context, countdownSeconds);
            if (!OwnsExecutionNoLock())
            {
                return;
            }

            ObservePendingHardcastNoLock(context);
            if (CurrentStepNoLock() is { Kind: BlmOpenerStepKind.Item } pendingPotion
                && SafeReadPotionCooldown(pendingPotion.ActionId) > 0.1f)
            {
                CompleteOptionalPotionNoLock(context, pendingPotion);
            }

            if (_acceptedAck is { } accepted)
            {
                if (IsCheckpointConfirmedNoLock(context))
                {
                    CompleteCurrentStepNoLock(context, accepted);
                }
                else if (_clock.NowMs > _pendingDeadlineAtMs)
                {
                    CancelNoLock(context, $"{CurrentStepNoLock()?.Id} Ack 后 Gauge 未在时限内确认");
                }
            }
            else if (_hasPending
                     && CurrentStepNoLock() is { Kind: BlmOpenerStepKind.Item }
                     && _clock.NowMs - _pendingIssuedAtMs >= PotionAttemptConfirmationMs)
            {
                RetryOrSkipOptionalPotionNoLock(context, "本次药水提交未获得 Ack 或公共冷却确认");
            }
            else if (_hasPending
                     && _pendingTracked
                     && !_tracker.GetTrackerSnapshot().HasPendingIssuedAction)
            {
                var reason = $"{CurrentStepNoLock()?.Id} 未收到 Ack，Tracker Pending 已失效";
                if (WasPendingHardcastInterruptedNoLock() && !highPriorityQueueActive)
                {
                    RetryInterruptedHardcastNoLock(context, reason);
                }
                else
                {
                    CancelNoLock(context, reason);
                }
            }
            else if (_hasPending && _clock.NowMs > _pendingDeadlineAtMs)
            {
                var reason = $"{CurrentStepNoLock()?.Id} 等待 Ack 超时";
                CancelNoLock(context, reason);
            }

            if (!OwnsExecutionNoLock() || _hasPending)
            {
                return;
            }

            if (CurrentStepNoLock() is { Kind: BlmOpenerStepKind.Item }
                && _clock.NowMs - _stepReadyAtMs >= PotionAttemptWindowMs)
            {
                SkipOptionalPotionNoLock(context, "药水短重试窗口内始终不满足投递或确认条件");
                return;
            }

            if (CurrentStepNoLock() is { Kind: BlmOpenerStepKind.Gcd } waitingGcd
                && context.IsMoving
                && !PredictInstantNoLock(waitingGcd.ActionId, context))
            {
                _stepReadyAtMs = _clock.NowMs;
                return;
            }

            if (_clock.NowMs - _stepReadyAtMs > StepReadyTimeoutMs)
            {
                CancelNoLock(context, $"{CurrentStepNoLock()?.Id} 长时间不满足投递条件");
                return;
            }

            if (_plan?.Mode == BlmOpenerMode.HighEndCountdown && !context.InCombat)
            {
                preCombatStep = CurrentStepNoLock();
                if (preCombatStep is { } bridgeStep)
                {
                    preCombatAction = PrepareDispatchNoLock(
                        bridgeStep.Channel,
                        context,
                        isPreCombatBridge: true);
                }
            }
        }

        if (preCombatAction is not null && preCombatStep is { } dispatchedStep)
        {
            try
            {
                _preCombatDispatcher(preCombatAction);
                PublishDispatch(context, dispatchedStep, preCombatAction, "Opener.PreCombatBridge");
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    CancelNoLock(context, $"战前桥投递失败：{exception.GetType().Name}");
                }
            }
        }
    }

    public PAction? Resolve(BlmResolverChannel channel, BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BlmOpenerStep? dispatchedStep = null;
        PAction? result;
        lock (_gate)
        {
            if (_status != BlmOpenerStatus.Executing
                || _hasPending
                || !context.InCombat)
            {
                return null;
            }

            dispatchedStep = CurrentStepNoLock();
            if (dispatchedStep is null
                || !CanDeliverOnChannelNoLock(
                    dispatchedStep.Value,
                    channel,
                    context,
                    isPreCombatBridge: false))
            {
                return null;
            }

            result = PrepareDispatchNoLock(channel, context, isPreCombatBridge: false);
        }

        if (result is not null && dispatchedStep is { } step)
        {
            if (step.Kind == BlmOpenerStepKind.Item)
            {
                bool submitted;
                try
                {
                    submitted = _potionDispatcher(step.ActionId);
                }
                catch
                {
                    submitted = false;
                }

                if (!submitted)
                {
                    lock (_gate)
                    {
                        RetryOrSkipOptionalPotionNoLock(context, "药水直接提交被游戏接口拒绝");
                    }
                }
                else
                {
                    PublishDispatch(context, step, result, "Opener.ItemDirect");
                }

                return null;
            }

            PublishDispatch(context, step, result, $"Opener.{channel}");
        }

        return result;
    }

    public void Cancel(BlmContext context, string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_gate)
        {
            if (OwnsExecutionNoLock())
            {
                CancelNoLock(context, reason);
            }
        }
    }

    private void TryStartDailyNoLock(
        BlmContext context,
        bool highPriorityQueueActive,
        BlmOpenerPolicy policy)
    {
        var plan = BlmOpener57Definition.Build(
            BlmOpenerMode.DailyInCombat,
            context.DotEnabled,
            potionId: 0,
            policy.NoTriplecast);
        if (!policy.Enabled
            || !policy.DailyInCombatEnabled
            || policy.Level100FlareEnabled
            || !context.InCombat
            || !context.DutyComposition.IsSinglePartyEightPlayer
            || highPriorityQueueActive
            || context.Tracker.CombatSerial <= 0
            || context.Tracker.CombatSerial == _lastAttemptedCombatSerial
            || !CanStart(context, plan))
        {
            return;
        }

        StartSessionNoLock(
            plan,
            context,
            BlmOpenerStatus.Executing,
            "T 已开怪，启动日常无药 5+7 起手");
        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.Arm.Daily",
            0,
            _lastReason);
    }

    private void StartSessionNoLock(
        BlmOpenerPlan plan,
        BlmContext context,
        BlmOpenerStatus status,
        string reason)
    {
        _tracker.CancelIssuedAction();
        _plan = plan;
        _status = status;
        _stepIndex = 0;
        _targetEntityId = context.TargetEntityId;
        _stateGeneration = context.Tracker.StateGeneration;
        _combatSerial = context.Tracker.CombatSerial;
        _lastAttemptedCombatSerial = context.Tracker.CombatSerial;
        _combatRebased = plan.Mode == BlmOpenerMode.DailyInCombat;
        _stepReadyAtMs = _clock.NowMs;
        _countdownEndedAtMs = 0;
        _fire4BeforeManafont = 0;
        _fire4AfterManafont = 0;
        ResetPotionAttemptsNoLock();
        ClearPendingNoLock();
        _cancelRequested = null;
        _lastReason = reason;
    }

    private void ArmExternalFire3NoLock()
    {
        var fire3 = CurrentStepNoLock()
            ?? throw new InvalidOperationException("高难起手缺少爆炎步骤。");
        _hasPending = true;
        _pendingTracked = false;
        _pendingRequestedActionId = fire3.ActionId;
        _pendingAdjustedActionId = fire3.ActionId;
        _pendingIssuedAtMs = _clock.NowMs;
        _pendingDeadlineAtMs = long.MaxValue;
        _pendingWasInstant = false;
        _pendingIsGcd = true;
    }

    private bool TryReconcileGenerationNoLock(BlmContext context)
    {
        if (context.Tracker.StateGeneration == _stateGeneration)
        {
            return true;
        }

        if (_plan?.Mode != BlmOpenerMode.HighEndCountdown
            || _combatRebased
            || !context.InCombat
            || context.Tracker.CombatSerial <= _combatSerial)
        {
            CancelNoLock(context, "StateGeneration 在非倒计时进战边界发生变化");
            return false;
        }

        _stateGeneration = context.Tracker.StateGeneration;
        _combatSerial = context.Tracker.CombatSerial;
        _lastAttemptedCombatSerial = context.Tracker.CombatSerial;
        _combatRebased = true;

        if (_hasPending && _pendingTracked && _acceptedAck is null)
        {
            var rebound = new BlmIssuedActionMetadata(
                _stateGeneration,
                _pendingRequestedActionId,
                _pendingAdjustedActionId,
                Math.Min(_pendingIssuedAtMs, _clock.NowMs),
                context.Tracker.LastAckGlobalSequence,
                Math.Max(_pendingDeadlineAtMs, _clock.NowMs + OffGcdAckTimeoutMs),
                _pendingWasInstant,
                _pendingIsGcd);
            _pendingTracked = _tracker.TryRegisterIssuedAction(rebound);
            if (!_pendingTracked)
            {
                CancelNoLock(context, "倒计时进战后无法重绑定在途动作");
                return false;
            }
        }

        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.RebaseCombat",
            _pendingRequestedActionId,
            "倒计时会话已绑定新的 CombatSerial/StateGeneration");
        return true;
    }

    private bool ValidateActiveContextNoLock(
        BlmContext context,
        bool highPriorityQueueActive)
    {
        if (!context.IsAvailable || !context.IsAlive || context.AcrState != AcrState.On)
        {
            CancelNoLock(context, "角色、ACR 或状态上下文不可用");
            return false;
        }

        if (_plan is null
            || context.Level < _plan.MinimumLevel
            || context.Level > _plan.MaximumLevel)
        {
            CancelNoLock(context, "当前等级不再满足起手计划资格");
            return false;
        }

        if (!context.HasValidTarget
            || !context.InRange
            || context.TargetEntityId == 0
            || context.TargetEntityId != _targetEntityId)
        {
            CancelNoLock(context, "起手目标失效、越界或发生切换");
            return false;
        }

        if (context.IsAoeMode)
        {
            CancelNoLock(context, "战斗路线切换为 AOE");
            return false;
        }

        if (highPriorityQueueActive && !BlmHotkeyCatalog.HasPendingManualAbility)
        {
            CancelNoLock(context, "检测到高优先级手动动作");
            return false;
        }

        if (_plan?.Mode == BlmOpenerMode.DailyInCombat && !context.InCombat)
        {
            CancelNoLock(context, "日常起手期间离开战斗");
            return false;
        }

        if (_plan?.Mode == BlmOpenerMode.HighEndCountdown
            && _combatRebased
            && !context.InCombat)
        {
            CancelNoLock(context, "高难起手进战后异常脱战");
            return false;
        }

        return true;
    }

    private void TrackCountdownNoLock(BlmContext context, float countdownSeconds)
    {
        if (_plan?.Mode != BlmOpenerMode.HighEndCountdown || _stepIndex != 0)
        {
            return;
        }

        if (countdownSeconds > 0f)
        {
            _countdownEndedAtMs = 0;
            return;
        }

        if (_acceptedAck is not null)
        {
            return;
        }

        if (context.InCombat)
        {
            _countdownEndedAtMs = _countdownEndedAtMs == 0
                ? _clock.NowMs
                : _countdownEndedAtMs;
            if (_clock.NowMs - _countdownEndedAtMs > Fire3AckTimeoutMs)
            {
                CancelNoLock(context, "进战后仍未收到倒计时爆炎 Ack");
            }
            return;
        }

        if (context.IsCasting && ActionMatchesNoLock(BLMSkill.爆炎, BLMSkill.爆炎, context.CurrentCastingActionId))
        {
            return;
        }

        _countdownEndedAtMs = _countdownEndedAtMs == 0
            ? _clock.NowMs
            : _countdownEndedAtMs;
        if (_clock.NowMs - _countdownEndedAtMs > Fire3AckTimeoutMs)
        {
            CancelNoLock(context, "倒计时结束后未收到爆炎 Ack");
        }
    }

    private PAction? PrepareDispatchNoLock(
        BlmResolverChannel channel,
        BlmContext context,
        bool isPreCombatBridge)
    {
        var step = CurrentStepNoLock();
        if (step is null
            || !CanDeliverOnChannelNoLock(
                step.Value,
                channel,
                context,
                isPreCombatBridge)
            || !CanDispatchNoLock(
                step.Value,
                channel,
                context,
                isPreCombatBridge))
        {
            return null;
        }

        if (step.Value.Kind == BlmOpenerStepKind.Item
            && SafeReadPotionCooldown(step.Value.ActionId) > 0.1f)
        {
            CompleteOptionalPotionNoLock(context, step.Value);
            return null;
        }

        var adjustedActionId = step.Value.Kind == BlmOpenerStepKind.Item
            ? step.Value.ActionId
            : SafeAdjustActionId(step.Value.ActionId);
        if (adjustedActionId == 0)
        {
            adjustedActionId = step.Value.ActionId;
        }

        var actionType = channel == BlmResolverChannel.Always
            ? ActionType.Always
            : step.Value.Kind switch
            {
                BlmOpenerStepKind.Gcd => ActionType.Gcd,
                BlmOpenerStepKind.OffGcd => ActionType.OffGcd,
                BlmOpenerStepKind.Item => ActionType.Item,
                _ => throw new ArgumentOutOfRangeException(),
            };
        var targetType = step.Value.TargetsEnemy
            ? ActionTargetType.Target
            : ActionTargetType.Self;
        var action = new PAction(adjustedActionId, actionType, targetType)
        {
            RequiresVerification = !isPreCombatBridge
                && step.Value.Kind != BlmOpenerStepKind.Item,
        };
        if (step.Value.TargetsEnemy)
        {
            action.NetworkTid = _targetEntityId;
        }

        var now = _clock.NowMs;
        if (step.Value.Kind == BlmOpenerStepKind.Item)
        {
            _potionAttemptCount++;
        }

        var isGcd = step.Value.Kind == BlmOpenerStepKind.Gcd;
        var wasInstant = isGcd && PredictInstantNoLock(step.Value.ActionId, context);
        var timeout = step.Value.ActionId == BLMSkill.爆炎
            ? Fire3AckTimeoutMs
            : isGcd
                ? GcdAckTimeoutMs
                : OffGcdAckTimeoutMs;
        var metadata = new BlmIssuedActionMetadata(
            _stateGeneration,
            step.Value.ActionId,
            adjustedActionId,
            now,
            context.Tracker.LastAckGlobalSequence,
            now + timeout,
            wasInstant,
            isGcd);
        if (!_tracker.TryRegisterIssuedAction(metadata))
        {
            _cancelRequested = $"{step.Value.Id} 无法登记唯一 Pending";
            return null;
        }

        _status = BlmOpenerStatus.Executing;
        _hasPending = true;
        _pendingTracked = true;
        _pendingRequestedActionId = step.Value.ActionId;
        _pendingAdjustedActionId = adjustedActionId;
        _pendingIssuedAtMs = now;
        _pendingDeadlineAtMs = now + timeout;
        _pendingWasInstant = wasInstant;
        _pendingIsGcd = isGcd;
        _acceptedAck = null;
        return action;
    }

    private static bool CanDeliverOnChannelNoLock(
        BlmOpenerStep step,
        BlmResolverChannel channel,
        BlmContext context,
        bool isPreCombatBridge)
    {
        if (isPreCombatBridge)
        {
            return channel == step.Channel;
        }

        if (step.Kind == BlmOpenerStepKind.Gcd)
        {
            return channel == BlmResolverChannel.Gcd;
        }

        if (step.Kind == BlmOpenerStepKind.Item)
        {
            var itemHasWeaveWindow = BlmDecisionPrimitives.CanWeaveNow(
                context.IsCasting,
                context.GcdRemainSeconds,
                context.AnimationLockSeconds);
            return channel == BlmResolverChannel.OffGcd && itemHasWeaveWindow
                || channel == BlmResolverChannel.Always
                    && !itemHasWeaveWindow
                    && context.GcdRemainSeconds <= 0.6f;
        }

        var hasWeaveWindow = BlmDecisionPrimitives.CanWeaveNow(
            context.IsCasting,
            context.GcdRemainSeconds,
            context.AnimationLockSeconds);
        return channel == BlmResolverChannel.OffGcd && hasWeaveWindow
            || channel == BlmResolverChannel.Always
                && !hasWeaveWindow
                && context.GcdRemainSeconds <= 0.6f;
    }

    private bool CanDispatchNoLock(
        BlmOpenerStep step,
        BlmResolverChannel channel,
        BlmContext context,
        bool isPreCombatBridge)
    {
        if (step.Kind == BlmOpenerStepKind.Item && _clock.NowMs < _potionRetryAtMs)
        {
            return false;
        }

        if (!context.CanAct || !context.IsAlive || context.IsCasting || context.AnimationLockSeconds > 0f)
        {
            return false;
        }

        if (step.TargetsEnemy
            && (!context.HasValidTarget
                || !context.InRange
                || context.TargetEntityId != _targetEntityId))
        {
            return false;
        }

        if (step.Kind == BlmOpenerStepKind.Gcd
            && context.GcdRemainSeconds * 1000f > context.ActionQueueWindowMs)
        {
            return false;
        }

        if (step.Kind != BlmOpenerStepKind.Gcd
            && !isPreCombatBridge
            && channel == BlmResolverChannel.OffGcd
            && !BlmDecisionPrimitives.CanWeaveNow(
                context.IsCasting,
                context.GcdRemainSeconds,
                context.AnimationLockSeconds))
        {
            return false;
        }

        if (step.Kind == BlmOpenerStepKind.Gcd
            && context.IsMoving
            && !PredictInstantNoLock(step.ActionId, context))
        {
            return false;
        }

        return step.ActionId switch
        {
            BLMSkill.爆炎 => context.Phase == BlmPhase.Neutral && context.IsMpFull,
            BLMSkill.冰封 => context.InFire,
            BLMSkill.闪雷 or BLMSkill.高闪雷 => context.HasThunderhead,
            MageUniversalSkill.即刻咏唱 => context.SwiftcastReady,
            BLMSkill.详述 => context.AmplifierReady,
            BLMSkill.炽炎 => context.InFire,
            BLMSkill.黑魔纹 => context.LeyLinesReady,
            BLMSkill.异言 => context.PolyglotStacks > 0,
            BLMSkill.魔泉 => context.InFire && context.ManafontReady,
            BLMSkill.耀星 => context.InFire && context.AstralSoulFull,
            BLMSkill.悖论 => context.InFire && context.HasParadox,
            BLMSkill.核爆 => context.InFire,
            BLMSkill.三连咏唱 => context.TriplecastReady,
            BLMSkill.绝望 => context.InFire,
            BLMSkill.星灵移位 => context.InFire && context.Transpose.IsReady,
            _ => true,
        };
    }

    private bool IsCheckpointConfirmedNoLock(BlmContext context)
    {
        var checkpoint = CurrentStepNoLock()?.Checkpoint ?? BlmOpenerCheckpoint.None;
        return checkpoint switch
        {
            BlmOpenerCheckpoint.None => true,
            BlmOpenerCheckpoint.FireEntry => context.InFire && context.AfStacks == 3,
            BlmOpenerCheckpoint.Swiftcast => context.HasSwiftcast,
            BlmOpenerCheckpoint.LeyLines => context.HasLeyLines,
            BlmOpenerCheckpoint.Manafont => context.Tracker.ManafontActiveThisFire
                && context.HasManafontResourcesRestored,
            BlmOpenerCheckpoint.Triplecast => context.TriplecastStacks > 0,
            BlmOpenerCheckpoint.Transpose => context.InIce,
            BlmOpenerCheckpoint.IceEntry => context.InIce,
            _ => false,
        };
    }

    private void CompleteCurrentStepNoLock(
        BlmContext context,
        BlmActionEffectAck accepted)
    {
        var step = CurrentStepNoLock();
        if (step is null)
        {
            CancelNoLock(context, "收到 Ack 时当前步骤不存在");
            return;
        }

        if (step.Value.Id.StartsWith("Fire4.Pre.", StringComparison.Ordinal))
        {
            _fire4BeforeManafont++;
        }
        else if (step.Value.Id.StartsWith("Fire4.Post.", StringComparison.Ordinal))
        {
            _fire4AfterManafont++;
        }

        PublishNoLock(
            context,
            BlmDebugEventKind.AckAccepted,
            "Opener.StepConfirmed",
            accepted.ActionId,
            $"{step.Value.Id} 已确认，Step={_stepIndex + 1}/{_plan!.Steps.Length}",
            accepted.GlobalSequence);

        _tracker.CancelIssuedAction();
        ClearPendingNoLock();
        _stepIndex++;
        _stepReadyAtMs = _clock.NowMs;
        ResetPotionAttemptsNoLock();
        if (_stepIndex < _plan!.Steps.Length)
        {
            _status = BlmOpenerStatus.Executing;
            return;
        }

        if (_fire4BeforeManafont != _plan.ExpectedFire4BeforeManafont
            || _fire4AfterManafont != _plan.ExpectedFire4AfterManafont)
        {
            CancelNoLock(
                context,
                $"{_plan.DisplayName}炽炎计数不变量失败："
                + $"{_fire4BeforeManafont}+{_fire4AfterManafont}");
            return;
        }

        _status = BlmOpenerStatus.Completed;
        _lastReason = $"{_plan.DisplayName}全部 ActionEffect/Gauge 已确认";
        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.Completed",
            0,
            _lastReason);
    }

    private void CancelNoLock(BlmContext context, string reason)
    {
        if (_status == BlmOpenerStatus.Draining)
        {
            return;
        }

        if (_plan?.Mode == BlmOpenerMode.HighEndCountdown && _stepIndex == 0)
        {
            try
            {
                _countdownCanceller($"Los起手取消：{reason}");
            }
            catch
            {
            }
        }

        try
        {
            _normalQueueClearer();
        }
        catch
        {
        }

        _tracker.CancelIssuedAction();
        ClearPendingNoLock();
        ResetPotionAttemptsNoLock();
        _status = BlmOpenerStatus.Draining;
        _cancelRequested = null;
        _lastReason = reason;
        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.Draining",
            0,
            $"{reason}；等待PR ActiveCommand排空");
    }

    private void RetryInterruptedHardcastNoLock(BlmContext context, string reason)
    {
        try
        {
            _actionUpdaterResetter();
        }
        catch
        {
            CancelNoLock(context, $"{reason}；PR ActionUpdater.Reset 失败，降级排空");
            return;
        }

        _tracker.CancelIssuedAction();
        ClearPendingNoLock();
        _status = BlmOpenerStatus.Executing;
        _stepReadyAtMs = _clock.NowMs;
        _cancelRequested = null;
        _lastReason = reason;
        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.HardcastRetryArmed",
            0,
            $"{reason}；已重置PR旧指令，保留当前步骤等待重新投递");
    }

    private void SkipOptionalPotionNoLock(BlmContext context, string reason)
    {
        if (CurrentStepNoLock() is not { Kind: BlmOpenerStepKind.Item } potionStep)
        {
            CancelNoLock(context, reason);
            return;
        }

        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.OptionalPotionSkipped",
            potionStep.ActionId,
            $"{reason}；药水为可选步骤，继续当前冻结起手");
        _tracker.CancelIssuedAction();
        ClearPendingNoLock();
        _stepIndex++;
        _stepReadyAtMs = _clock.NowMs;
        ResetPotionAttemptsNoLock();
        _status = BlmOpenerStatus.Executing;
        _cancelRequested = null;
        _lastReason = reason;
    }

    private void CompleteOptionalPotionNoLock(BlmContext context, BlmOpenerStep potionStep)
    {
        PublishNoLock(
            context,
            BlmDebugEventKind.AckAccepted,
            "Opener.PotionCooldownConfirmed",
            potionStep.ActionId,
            "药水公共冷却已启动，确认可选药水步骤成功");
        _tracker.CancelIssuedAction();
        ClearPendingNoLock();
        _stepIndex++;
        _stepReadyAtMs = _clock.NowMs;
        ResetPotionAttemptsNoLock();
        _status = BlmOpenerStatus.Executing;
        _lastReason = "药水公共冷却确认成功";
    }

    private void RetryOrSkipOptionalPotionNoLock(BlmContext context, string reason)
    {
        if (CurrentStepNoLock() is not { Kind: BlmOpenerStepKind.Item } potionStep)
        {
            CancelNoLock(context, reason);
            return;
        }

        if (_clock.NowMs - _stepReadyAtMs >= PotionAttemptWindowMs)
        {
            SkipOptionalPotionNoLock(context, reason);
            return;
        }

        _tracker.CancelIssuedAction();
        ClearPendingNoLock();
        _status = BlmOpenerStatus.Executing;
        _potionRetryAtMs = _clock.NowMs + PotionRetryDelayMs;
        _cancelRequested = null;
        _lastReason = reason;
        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.PotionRetryArmed",
            potionStep.ActionId,
            $"{reason}；第 {_potionAttemptCount} 次提交未确认，短延迟后重试");
    }

    private void ResetPotionAttemptsNoLock()
    {
        _potionRetryAtMs = 0;
        _potionAttemptCount = 0;
    }

    private void FinishCancellationNoLock(BlmContext context)
    {
        _status = BlmOpenerStatus.Cancelled;
        PublishNoLock(
            context,
            BlmDebugEventKind.Lifecycle,
            "Opener.Cancelled",
            0,
            _lastReason);
    }

    private void ClearPendingNoLock()
    {
        _hasPending = false;
        _pendingTracked = false;
        _pendingRequestedActionId = 0;
        _pendingAdjustedActionId = 0;
        _pendingIssuedAtMs = 0;
        _pendingDeadlineAtMs = 0;
        _pendingWasInstant = false;
        _pendingIsGcd = false;
        _pendingHardcastObserved = false;
        _pendingHardcastLastRemainSeconds = 0f;
        _acceptedAck = null;
    }

    private void ObservePendingHardcastNoLock(BlmContext context)
    {
        if (!_hasPending
            || !_pendingTracked
            || !_pendingIsGcd
            || _pendingWasInstant
            || !context.IsCasting
            || context.CurrentCastingActionId == 0
            || !ActionMatchesNoLock(
                _pendingRequestedActionId,
                _pendingAdjustedActionId,
                context.CurrentCastingActionId))
        {
            return;
        }

        _pendingHardcastObserved = true;
        _pendingHardcastLastRemainSeconds = Math.Max(0f, context.CastRemainSeconds);
    }

    private bool WasPendingHardcastInterruptedNoLock()
        => _pendingIsGcd
            && !_pendingWasInstant
            && _pendingHardcastObserved
            && _pendingHardcastLastRemainSeconds > 0.15f;

    private static bool CanStart(BlmContext context, BlmOpenerPlan plan)
        => context.IsAvailable
            && context.AcrState == AcrState.On
            && context.Level >= plan.MinimumLevel
            && context.Level <= plan.MaximumLevel
            && context.IsAlive
            && context.CanAct
            && !context.IsCasting
            && !context.IsMoving
            && !context.IsAoeMode
            && context.HasValidTarget
            && context.InRange
            && context.TargetEntityId != 0
            && context.Phase == BlmPhase.Neutral
            && context.IsMpFull
            && plan.Steps.All(step => IsStepUnlockedAtLevel(step, context.Level))
            && plan.Steps.All(step => IsRequiredAbilityReady(step, context));

    private static bool IsStepUnlockedAtLevel(BlmOpenerStep step, int level)
        => step.Kind == BlmOpenerStepKind.Item
            || BlmSkillBook.IsUnlocked(step.ActionId, level);

    private static bool IsRequiredAbilityReady(BlmOpenerStep step, BlmContext context)
        => step.ActionId switch
        {
            MageUniversalSkill.即刻咏唱 => context.SwiftcastReady,
            BLMSkill.三连咏唱 => context.TriplecastReady,
            BLMSkill.黑魔纹 => context.LeyLinesReady,
            BLMSkill.详述 => context.AmplifierReady,
            BLMSkill.魔泉 => context.ManafontReady,
            BLMSkill.星灵移位 => context.Transpose.IsReady,
            _ => true,
        };

    private bool PredictInstantNoLock(uint actionId, BlmContext context)
        => actionId is BLMSkill.闪雷 or BLMSkill.高闪雷
            or BLMSkill.异言 or BLMSkill.耀星 or BLMSkill.悖论
            || actionId == BLMSkill.绝望 && context.Level >= 100
            || actionId == BLMSkill.爆炎 && context.HasFirestarter
            || context.HasSwiftcast
            || context.TriplecastStacks > 0;

    private bool ActionMatchesNoLock(
        uint requestedActionId,
        uint adjustedActionId,
        uint actualActionId)
    {
        if (actualActionId == 0)
        {
            return false;
        }

        if (CurrentStepNoLock()?.Kind == BlmOpenerStepKind.Item
            && NormalizeItemId(requestedActionId) == NormalizeItemId(actualActionId))
        {
            return true;
        }

        return actualActionId == requestedActionId
                || actualActionId == adjustedActionId
                || BlmSkillBook.ActionIdsMatch(
                    requestedActionId,
                    actualActionId,
                    _normalizer)
                || BlmSkillBook.ActionIdsMatch(
                    adjustedActionId,
                    actualActionId,
                    _normalizer);
    }

    private static uint NormalizeItemId(uint itemId)
        => itemId >= 1_000_000 ? itemId - 1_000_000 : itemId;

    private static unsafe bool DispatchPotion(uint itemId)
    {
        var actionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if (actionManager == null || PRCore.Me is not { } me)
        {
            return false;
        }

        var isHighQuality = itemId >= 1_000_000;
        return actionManager->UseAction(
            FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item,
            NormalizeItemId(itemId),
            me.GameObjectId,
            isHighQuality ? 1u : 0u,
            FFXIVClientStructs.FFXIV.Client.Game.ActionManager.UseActionMode.Queue,
            0u,
            null);
    }

    private static float ReadPotionCooldown(uint itemId)
        => ActionHelper.GetItemCooldown(NormalizeItemId(itemId));

    private bool OwnsExecutionNoLock()
        => _status is BlmOpenerStatus.Armed
            or BlmOpenerStatus.Executing
            or BlmOpenerStatus.Draining;

    private BlmOpenerStep? CurrentStepNoLock()
        => _plan is not null && _stepIndex >= 0 && _stepIndex < _plan.Steps.Length
            ? _plan.Steps[_stepIndex]
            : null;

    private BlmOpenerPolicy SafeReadPolicy()
    {
        try
        {
            return _policyProvider();
        }
        catch
        {
            return default;
        }
    }

    private float SafeReadCountdown()
    {
        try
        {
            return Math.Max(0f, _countdownProvider());
        }
        catch
        {
            return 0f;
        }
    }

    private uint SafeReadPotion()
    {
        try
        {
            return _potionProvider();
        }
        catch
        {
            return 0;
        }
    }

    private float SafeReadPotionCooldown(uint itemId)
    {
        try
        {
            return Math.Max(0f, _potionCooldownProvider(itemId));
        }
        catch
        {
            return 0f;
        }
    }

    private uint SafeAdjustActionId(uint actionId)
    {
        try
        {
            return _adjustActionId(actionId);
        }
        catch
        {
            return actionId;
        }
    }

    private bool SafeHasActiveCommand()
    {
        try
        {
            return _hasActiveCommandProvider();
        }
        catch
        {
            return true;
        }
    }

    private static bool IsVariantEnabled(
        BlmOpenerPolicy policy,
        BlmOpenerVariant variant)
        => variant switch
        {
            BlmOpenerVariant.Level70 or BlmOpenerVariant.Level80
                => policy.Level70To89Enabled,
            BlmOpenerVariant.Level90 => policy.Level90To99Enabled,
            BlmOpenerVariant.Standard57 => policy.Enabled,
            BlmOpenerVariant.Flare => policy.Level100FlareEnabled,
            _ => false,
        };

    private static float ReadCountdown()
    {
        try
        {
            return PRGameData.GetCountdown();
        }
        catch
        {
            return 0f;
        }
    }

    private static uint ReadPotion()
    {
        try
        {
            return PRGameData.GetBestPotionId();
        }
        catch
        {
            return 0;
        }
    }

    private static uint AdjustActionId(uint actionId)
    {
        try
        {
            var adjusted = ActionHelper.GetAdjustedActionId(actionId);
            return adjusted == 0 ? actionId : adjusted;
        }
        catch
        {
            return actionId;
        }
    }

    private void PublishDispatch(
        BlmContext context,
        BlmOpenerStep step,
        PAction action,
        string entryPoint)
    {
        try
        {
            _debug.Publish(new BlmDebugEventDraft
            {
                Kind = BlmDebugEventKind.DispatchReturned,
                Context = context,
                MonotonicMs = _clock.NowMs,
                EntryPoint = entryPoint,
                ActionId = action.ActionId,
                NormalizedActionId = step.ActionId,
                PActionType = action.Type.ToString(),
                RuleId = $"Opener57.{step.Id}",
                Reason = "起手执行器仅投递当前步骤，等待 Tracker Ack。",
                TargetEntityId = action.NetworkTid,
            });
        }
        catch
        {
        }
    }

    private void PublishNoLock(
        BlmContext context,
        BlmDebugEventKind kind,
        string entryPoint,
        uint actionId,
        string reason,
        uint globalSequence = 0,
        string? detail = null)
    {
        try
        {
            _debug.Publish(new BlmDebugEventDraft
            {
                Kind = kind,
                Context = context,
                MonotonicMs = _clock.NowMs,
                EntryPoint = entryPoint,
                ActionId = actionId,
                GlobalSequence = globalSequence,
                RuleId = CurrentStepNoLock() is { } step
                    ? $"Opener57.{step.Id}"
                    : "Opener57",
                Reason = reason,
                Detail = detail ?? string.Empty,
                TargetEntityId = _targetEntityId,
            });
        }
        catch
        {
        }
    }
}
