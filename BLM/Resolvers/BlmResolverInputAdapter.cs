using System.Collections.Immutable;
using LosPr.BLM.Resolvers.Level100;

namespace LosPr.BLM.Resolvers;

internal sealed class BlmResolverInputAdapter
{
    public static ImmutableArray<uint> RequiredActionIds { get; } =
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

    private readonly BlmResolverRuntimeMemory _runtimeMemory;
    private readonly Func<BlackMageSettings>? _settingsProvider;

    public BlmResolverInputAdapter(
        BlmResolverRuntimeMemory? runtimeMemory = null,
        Func<BlackMageSettings>? settingsProvider = null)
    {
        _runtimeMemory = runtimeMemory ?? new BlmResolverRuntimeMemory();
        _settingsProvider = settingsProvider;
    }

    public bool TryCapture(
        BlmContext context,
        BlmTrackerDecisionSnapshot decision,
        bool highPriority,
        out BlmResolverInput input,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(decision);
        var consoleSettings = ReadConsoleSettings();

        if (context.Level < (consoleSettings?.MinimumEnabledLevel ?? 1))
        {
            input = Build(
                context,
                decision,
                _runtimeMemory.GetSnapshot(),
                [],
                highPriority,
                consoleSettings: consoleSettings);
            reason = "BelowMinimumEnabledLevel";
            return false;
        }

        if (!GenerationsMatch(context, decision))
        {
            input = Build(
                context,
                decision,
                _runtimeMemory.GetSnapshot(),
                [],
                highPriority,
                consoleSettings: consoleSettings);
            reason = "GenerationMismatch";
            return false;
        }

        if (!TryCaptureActions(context, out var actions, out reason))
        {
            input = Build(
                context,
                decision,
                BlmResolverRuntimeState.Empty,
                [],
                highPriority,
                consoleSettings: consoleSettings);
            return false;
        }

        var previousGcd = decision.PreviousGcd;
        var swiftcast = FindAction(actions, MageUniversalSkill.即刻咏唱);
        var previewRuntime = _runtimeMemory.GetSnapshot();
        if (previewRuntime.StateGeneration != decision.Snapshot.StateGeneration)
        {
            previewRuntime = new BlmResolverRuntimeState
            {
                StateGeneration = decision.Snapshot.StateGeneration,
                PreviousGcdSerial = previousGcd?.Serial ?? 0,
            };
        }

        var preliminaryInput = Build(
            context,
            decision,
            previewRuntime,
            actions,
            highPriority,
            ResolveProductionActionChannel,
            consoleSettings);
        var hasAvailableInstantGcd =
            Level100ResolverEngine.HasAvailableInstantGcd(preliminaryInput);
        var runtime = _runtimeMemory.Advance(new BlmResolverRuntimeObservation(
            decision.Snapshot.StateGeneration,
            context.CapturedAtMs,
            context.IsAvailable,
            context.AcrState == AcrState.On,
            context.InCombat,
            context.IsAlive,
            context.IsCasting,
            context.GcdTotalSeconds,
            previousGcd?.OccurredAtMs ?? 0,
            previousGcd?.Serial ?? 0,
            hasAvailableInstantGcd,
            IsReadyNow(swiftcast)));

        input = Build(
            context,
            decision,
            runtime,
            actions,
            highPriority,
            ResolveProductionActionChannel,
            consoleSettings);
        reason = input.FactCoverage.UnsupportedSummary;
        return true;
    }

    public static BlmResolverInput Build(
        BlmContext context,
        BlmTrackerDecisionSnapshot decision,
        BlmResolverRuntimeState runtime,
        ImmutableArray<BlmResolverActionFact> actions,
        bool highPriority,
        Func<uint, BlmResolverChannel?>? resolveActionChannel = null,
        BlackMageSettings? consoleSettings = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(runtime);

        var generationsMatch = GenerationsMatch(context, decision)
            && runtime.StateGeneration == decision.Snapshot.StateGeneration;
        var snapshot = decision.Snapshot;
        var settings = BlmResolverSettings.Default with
        {
            ManafontEnabled = context.ManafontEnabled,
            DumpPolyglotEnabled = context.DumpPolyglotEnabled,
            FastFlareStarEnabled = context.FastFlareStarEnabled,
            TtkEnabled = context.TtkDumpEnabled,
            DotEnabled = context.DotEnabled,
            MoveXenoglossyEnabled = context.MoveXenoEnabled,
            SwiftcastIntoIceEnabled = context.SwiftcastEnabled,
            TriplecastIntoIceEnabled = context.TriplecastEnabled,
            MoveTriplecastEnabled = context.MoveTriplecastEnabled,
            AmplifierEnabled = context.AmplifierEnabled,
            LeyLinesEnabled = context.LeyLinesEnabled,
            CompressFireParadox = consoleSettings?.CompressFireParadoxEnabled
                ?? context.CompressFireParadox,
            DotHpThresholdPercent = consoleSettings?.DotHpThresholdPercent
                ?? BlmResolverSettings.Default.DotHpThresholdPercent,
        };
        var contextFacts = new BlmResolverContextFacts
        {
            CapturedAtMs = context.CapturedAtMs,
            IsAvailable = generationsMatch && context.IsAvailable,
            AcrEnabled = generationsMatch && context.AcrState == AcrState.On,
            PlayerEntityId = context.PlayerEntityId,
            Level = context.Level,
            Mp = context.Mp,
            MaxMp = context.MaxMp,
            InCombat = context.InCombat,
            AutoPullEnabled = context.AutoPullEnabled,
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
            ActionQueueWindowMs = context.ActionQueueWindowMs,
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
            HasLeyLinesStatus737 = context.HasLeyLinesStatus737,
            HasLeyLinesHaste738 = context.HasLeyLinesHaste,
        };
        var history = generationsMatch ? decision.RecentHistory : [];
        var previousGcd = generationsMatch ? decision.PreviousGcd : null;

        return new BlmResolverInput
        {
            StateGeneration = snapshot.StateGeneration,
            Context = contextFacts,
            Settings = settings,
            Actions = actions.IsDefault ? [] : actions,
            RecentHistory = history,
            PreviousGcd = previousGcd,
            UsedWeaves = CountUsedWeaves(
                history,
                previousGcd,
                snapshot.StateGeneration,
                resolveActionChannel),
            Level100Loop = new BlmLevel100LoopFacts
            {
                Fire4Count = generationsMatch
                    ? snapshot.ManafontActiveThisFire
                        ? snapshot.Fire4CountSinceManafont
                        : snapshot.Fire4Count
                    : 0,
                FireParadoxUsed = generationsMatch && snapshot.ParadoxUsedThisFire,
                RecoveringAfterSpecial = false,
            },
            CurrentCastingActionId = context.CurrentCastingActionId,
            SpecialSequenceActive = false,
            HighPriorityQueueActive = highPriority,
            PendingGaugeReconcile = generationsMatch
                ? snapshot.PendingGaugeReconcile
                : true,
            NeedsForcedIceRecovery = generationsMatch
                && runtime.NeedsForcedIceRecovery,
            IsIdle = generationsMatch && runtime.IsIdle,
            DotTargets = [BuildMainTargetDotFact(context, actions)],
            DoubleDotMemory = default,
            CanSpecifyDotTarget = false,
            CasualCombat = new BlmCasualCombatFacts(),
            DefensiveCast = new BlmDefensiveCastFacts(),
            IsPotionAvailable = false,
            FactCoverage = BlmResolverFactCoverage.Phase3A with
            {
                GenerationConsistent = generationsMatch,
                CoreContextSupported = generationsMatch,
                ActionAvailabilitySupported = generationsMatch,
                TrackerHistorySupported = generationsMatch,
                MainTargetDotSupported = generationsMatch,
            },
        };
    }

    public static bool TryCaptureActions(
        BlmContext context,
        out ImmutableArray<BlmResolverActionFact> actions,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        IBattleChara? me = null;
        IBattleChara? target = null;
        try
        {
            me = PRCore.Me;
            target = PRCore.Target;
        }
        catch
        {
            actions = [];
            reason = "EntityCaptureUnavailable";
            return false;
        }

        if (!EntitySnapshotMatches(
                context,
                me?.EntityId,
                target?.EntityId,
                out reason))
        {
            actions = [];
            return false;
        }

        var facts = ImmutableArray.CreateBuilder<BlmResolverActionFact>(
            RequiredActionIds.Length);
        foreach (var actionId in RequiredActionIds)
        {
            var selfTarget = UsesSelfTarget(actionId);
            var actionTarget = selfTarget ? me : target;
            facts.Add(CaptureAction(context.Level, actionId, actionTarget));
        }

        actions = facts.MoveToImmutable();
        reason = string.Empty;
        return true;
    }

    public static bool EntitySnapshotMatches(
        BlmContext context,
        uint? playerEntityId,
        uint? targetEntityId,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PlayerEntityId == 0
            || playerEntityId != context.PlayerEntityId)
        {
            reason = "PlayerEntityMismatch";
            return false;
        }

        var frozenHasTarget = context.HasTarget || context.TargetEntityId != 0;
        if (context.HasValidTarget && !context.HasTarget)
        {
            reason = "TargetEntityMismatch";
            return false;
        }

        if (frozenHasTarget)
        {
            if (!context.HasTarget
                || context.TargetEntityId == 0
                || targetEntityId != context.TargetEntityId)
            {
                reason = "TargetEntityMismatch";
                return false;
            }
        }
        else if (targetEntityId is > 0)
        {
            reason = "TargetEntityMismatch";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static BlmResolverActionFact CaptureAction(
        int level,
        uint requestedActionId,
        IBattleChara? target)
    {
        var unlockedByLevel = BlmSkillBook.IsUnlocked(requestedActionId, level);
        if (!unlockedByLevel)
        {
            return UnavailableAction(requestedActionId);
        }

        try
        {
            var adjustedActionId = ActionHelper.GetAdjustedActionId(requestedActionId);
            if (adjustedActionId == 0)
            {
                adjustedActionId = requestedActionId;
            }

            var isUnlocked = ActionHelper.IsActionAvailableByLevelAndQuest(
                adjustedActionId);
            var maxCharges = Math.Max(1, ActionHelper.GetMaxCharges(adjustedActionId));
            var charges = FiniteNonNegative(ActionHelper.GetActionCharges(adjustedActionId));
            var cooldown = FiniteNonNegative(
                ActionHelper.GetActionCooldown(adjustedActionId));
            var recastTotal = FiniteNonNegative(
                ActionHelper.GetActionRecastTime(adjustedActionId));
            return new BlmResolverActionFact
            {
                RequestedActionId = requestedActionId,
                AdjustedActionId = adjustedActionId,
                IsUnlocked = isUnlocked,
                CanCast = isUnlocked
                    && target is not null
                    && ActionHelper.CanCast(adjustedActionId, target),
                Charges = charges,
                MaxCharges = maxCharges,
                CooldownRemainMs = cooldown * 1000d,
                RecastTotalMs = recastTotal * 1000d,
            };
        }
        catch
        {
            return UnavailableAction(requestedActionId);
        }
    }

    private static BlmResolverActionFact UnavailableAction(uint actionId)
        => new()
        {
            RequestedActionId = actionId,
            AdjustedActionId = actionId,
            IsUnlocked = false,
            CanCast = false,
            Charges = 0f,
            MaxCharges = 1,
            CooldownRemainMs = double.MaxValue,
            RecastTotalMs = 0d,
        };

    private static BlmResolverDotTargetFact BuildMainTargetDotFact(
        BlmContext context,
        ImmutableArray<BlmResolverActionFact> actions)
    {
        var thunder = FindAction(actions, BLMSkill.闪雷);
        return new BlmResolverDotTargetFact
        {
            EntityId = context.TargetEntityId,
            IsValid = context.HasValidTarget,
            IsTargetable = context.HasValidTarget,
            IsAlive = context.HasValidTarget && context.TargetHp > 0,
            CanUseAttackActionOn = context.HasValidTarget,
            IsInDotRange = context.InRange,
            IsBlacklisted = false,
            CanCastDot = context.HasValidTarget
                && context.InRange
                && thunder is { IsUnlocked: true, CanCast: true },
            CurrentHp = context.TargetHp,
            MaxHp = context.TargetMaxHp,
            Distance = context.TargetDistance,
            SingleTargetDotRemainingMs = context.SingleTargetDot.RemainingMs,
            AoeDotRemainingMs = context.AoeDot.RemainingMs,
        };
    }

    private static int CountUsedWeaves(
        ImmutableArray<BlmActionSuccess> history,
        BlmActionSuccess? previousGcd,
        long stateGeneration,
        Func<uint, BlmResolverChannel?>? resolveActionChannel)
    {
        if (previousGcd is not { IsGcd: true } gcd)
        {
            return 0;
        }

        var count = 0;
        foreach (var success in history)
        {
            if (success.StateGeneration != stateGeneration
                || success.Serial <= gcd.Serial
                || success.IsGcd)
            {
                continue;
            }

            if (resolveActionChannel is not null
                && resolveActionChannel(EffectiveActionId(success))
                    is not (BlmResolverChannel.OffGcd or BlmResolverChannel.Always))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static BlmResolverActionFact? FindAction(
        ImmutableArray<BlmResolverActionFact> actions,
        uint requestedActionId)
    {
        if (actions.IsDefaultOrEmpty)
        {
            return null;
        }

        foreach (var action in actions)
        {
            if (action.RequestedActionId == requestedActionId)
            {
                return action;
            }
        }

        return null;
    }

    private static bool IsReadyNow(BlmResolverActionFact? action)
        => action is { IsUnlocked: true, CanCast: true }
            && (action.Charges >= 1f
                || action.CooldownRemainMs
                    <= BlmDecisionPrimitives.AeAssistAbilityQueueToleranceMs);

    private static BlmResolverChannel? ResolveProductionActionChannel(uint actionId)
    {
        try
        {
            if (!ActionHelper.TryResolveActionType(actionId, out var actionType))
            {
                return null;
            }

            return actionType switch
            {
                ActionType.Gcd => BlmResolverChannel.Gcd,
                ActionType.OffGcd => BlmResolverChannel.OffGcd,
                ActionType.Always => BlmResolverChannel.Always,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static uint EffectiveActionId(BlmActionSuccess success)
        => success.ActualAckId != 0
            ? success.ActualAckId
            : success.AdjustedAtIssue != 0
                ? success.AdjustedAtIssue
                : success.RequestedId;

    private static bool GenerationsMatch(
        BlmContext context,
        BlmTrackerDecisionSnapshot decision)
        => context.Tracker.StateGeneration > 0
            && context.Tracker.StateGeneration == decision.Snapshot.StateGeneration;

    private static bool UsesSelfTarget(uint actionId)
        => actionId is BLMSkill.星灵移位
            or MageUniversalSkill.即刻咏唱
            or BLMSkill.三连咏唱
            or MageUniversalSkill.醒梦
            or BLMSkill.详述
            or BLMSkill.魔泉
            or BLMSkill.黑魔纹
            or BLMSkill.魔罩;

    private static float FiniteNonNegative(float value)
        => float.IsFinite(value) ? Math.Max(0f, value) : 0f;

    private BlackMageSettings? ReadConsoleSettings()
    {
        try
        {
            return _settingsProvider?.Invoke();
        }
        catch
        {
            return null;
        }
    }
}
