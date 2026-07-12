using LosPr.BLM.Core;
using LosPr.BLM.Engine;
using PromeRotation.Data;

namespace Los.Tests;

internal sealed class Step3Harness
{
    private uint _nextSequence = 10;

    public Step3Harness(BlmContext desiredContext)
    {
        Clock = new FakeClock();
        Normalizer = new MappingActionIdNormalizer();
        Coordinator = new BlmCoordinator(Clock, Normalizer);
        FollowUp = new BlmFollowUpCoordinator(Clock);

        var neutral = Stamp(desiredContext with
        {
            Phase = BlmPhase.Neutral,
            AfStacks = 0,
            IceStacks = 0,
            UmbralHearts = 0,
            AstralSoul = 0,
            HasParadox = false,
            HasFirestarter = false,
        });
        Tracker = new BlmStateTracker(
            Coordinator,
            neutral,
            Clock,
            Normalizer,
            FollowUp);
        Dispatcher = new BlmActionDispatcher(
            Coordinator,
            FollowUp,
            normalizer: Normalizer);
        RawContext = Stamp(desiredContext);
        Reconcile(RawContext);
    }

    public FakeClock Clock { get; }
    public MappingActionIdNormalizer Normalizer { get; }
    public BlmCoordinator Coordinator { get; }
    public BlmFollowUpCoordinator FollowUp { get; }
    public BlmStateTracker Tracker { get; }
    public BlmActionDispatcher Dispatcher { get; }
    public BlmContext RawContext { get; private set; }
    public BlmContext Context => Tracker.GetContextSnapshot();

    public PAction? ResolveGcd(BlmDecisionPolicy? policy = null)
        => Dispatcher.ResolveGcd(Context, policy);

    public PAction? ResolveOffGcd(BlmDecisionPolicy? policy = null)
        => Dispatcher.ResolveOffGcd(Context, policy);

    public PAction? ResolveAlways(BlmDecisionPolicy? policy = null)
        => Dispatcher.ResolveAlways(Context, policy);

    public BlmActionEffectAck CaptureAck(
        uint actionId,
        BlmPhase? phaseBefore = null,
        uint? sequence = null)
        => Tracker.CreateAckEnvelope(
            Context.PlayerEntityId,
            actionId,
            sequence ?? _nextSequence++,
            phaseBefore ?? Context.Phase,
            Clock.NowMs);

    public bool ApplyAck(BlmActionEffectAck ack)
        => Tracker.ApplyActionEffect(ack);

    public BlmActionEffectAck ApplyAck(
        PAction action,
        BlmPhase? phaseBefore = null)
    {
        var ack = CaptureAck(action.ActionId, phaseBefore);
        AssertEx.True(ApplyAck(ack), $"动作 {action.ActionId} Ack 应被接收");
        return ack;
    }

    public void Reconcile(BlmContext context)
    {
        RawContext = Stamp(context);
        Dispatcher.PrepareForReconcile(RawContext);
        Tracker.Reconcile(RawContext);
        Dispatcher.Reconcile(Tracker.GetContextSnapshot());
    }

    public void AdvanceAndReconcile(
        long milliseconds,
        Func<BlmContext, BlmContext> transform)
    {
        Clock.Advance(milliseconds);
        Reconcile(transform(RawContext));
    }

    public void SetWindow(
        float gcdRemainSeconds,
        bool? moving = null,
        float? animationLockSeconds = null)
        => Reconcile(RawContext with
        {
            GcdRemainSeconds = gcdRemainSeconds,
            IsMoving = moving ?? RawContext.IsMoving,
            AnimationLockSeconds = animationLockSeconds
                ?? RawContext.AnimationLockSeconds,
        });

    public void AckAndApplyPostGauge(PAction action)
    {
        var phaseBefore = Context.Phase;
        ApplyAck(action, phaseBefore);
        Clock.Advance(16);
        Reconcile(ApplyAction(RawContext, action.ActionId, phaseBefore));
    }

    private BlmContext Stamp(BlmContext context)
        => context with
        {
            CapturedAtMs = Clock.NowMs,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Tracker = BlmTrackerSnapshot.Empty,
        };

    private static BlmContext ApplyAction(
        BlmContext context,
        uint actionId,
        BlmPhase phaseBefore)
        => actionId switch
        {
            BLMSkill.冰澈 => context with
            {
                UmbralHearts = 3,
                Mp = context.MaxMp,
                GcdRemainSeconds = 0.5f,
            },
            BLMSkill.悖论 when phaseBefore == BlmPhase.Ice => context with
            {
                HasParadox = false,
                GcdRemainSeconds = 2.4f,
            },
            BLMSkill.悖论 => context with
            {
                HasParadox = false,
                HasFirestarter = true,
                Mp = Math.Max(0, context.Mp - BlmFireBudget.FireParadoxCost),
                GcdRemainSeconds = 2.4f,
            },
            BLMSkill.星灵移位 when phaseBefore == BlmPhase.Ice => context with
            {
                Phase = BlmPhase.Fire,
                AfStacks = 1,
                IceStacks = 0,
                GcdRemainSeconds = Math.Max(0.7f, context.GcdRemainSeconds),
                HasParadox = true,
            },
            BLMSkill.星灵移位 => context with
            {
                Phase = BlmPhase.Ice,
                AfStacks = 0,
                IceStacks = 1,
                HasParadox = true,
                GcdRemainSeconds = Math.Max(0.7f, context.GcdRemainSeconds),
            },
            MageUniversalSkill.即刻咏唱 => context with
            {
                HasSwiftcast = true,
                SwiftcastRemainSeconds = 10f,
                GcdRemainSeconds = Math.Max(0.7f, context.GcdRemainSeconds),
            },
            BLMSkill.三连咏唱 => context with
            {
                TriplecastStacks = 3,
                TriplecastRemainSeconds = 15f,
                GcdRemainSeconds = Math.Max(0.7f, context.GcdRemainSeconds),
            },
            BLMSkill.爆炎 => context with
            {
                Phase = BlmPhase.Fire,
                AfStacks = 3,
                IceStacks = 0,
                HasFirestarter = false,
                GcdRemainSeconds = 2.4f,
            },
            BLMSkill.绝望 => context with
            {
                Mp = 0,
                AfStacks = context.InFire ? 3 : context.AfStacks,
                GcdRemainSeconds = 2.4f,
            },
            BLMSkill.耀星 => context with
            {
                AstralSoul = 0,
                GcdRemainSeconds = 0.5f,
            },
            BLMSkill.冰封 => context with
            {
                Phase = BlmPhase.Ice,
                AfStacks = 0,
                IceStacks = 3,
                UmbralHearts = 0,
                AstralSoul = 0,
                Mp = context.MaxMp,
                GcdRemainSeconds = 2.4f,
                HasParadox = true,
            },
            _ => context,
        };
}

internal static class Step3Context
{
    public static BlmContext Ice(
        bool firestarter,
        bool paradox = true,
        int hearts = 3,
        float gcdRemainSeconds = 0.2f)
        => Base() with
        {
            Phase = BlmPhase.Ice,
            IceStacks = 3,
            UmbralHearts = hearts,
            HasParadox = paradox,
            HasFirestarter = firestarter,
            GcdRemainSeconds = gcdRemainSeconds,
        };

    public static BlmContext Fire(
        long mp,
        int soul,
        bool moving = false,
        bool swift = false,
        float swiftRemain = 0,
        float gcdRemainSeconds = 0.2f)
        => Base() with
        {
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            Mp = mp,
            AstralSoul = soul,
            IsMoving = moving,
            HasSwiftcast = swift,
            SwiftcastRemainSeconds = swiftRemain,
            HasParadox = false,
            GcdRemainSeconds = gcdRemainSeconds,
        };

    public static BlmContext Base() => new()
    {
        CapturedAtMs = 1000,
        CapturedAtUtc = DateTimeOffset.UtcNow,
        IsAvailable = true,
        AvailabilityText = "Step3 测试状态",
        AcrState = AcrState.On,
        PlayerEntityId = 100,
        JobId = 25,
        Level = 100,
        Mp = 10000,
        MaxMp = 10000,
        InCombat = true,
        IsAlive = true,
        CanAct = true,
        GcdTotalSeconds = 2.5f,
        HasTarget = true,
        HasValidTarget = true,
        InRange = true,
        TargetEntityId = 200,
        TargetName = "Step3 测试目标",
        TargetHp = 1000000,
        TargetMaxHp = 1000000,
        EnemyCount = 1,
        SingleTargetDot = new BlmDotSnapshot
        {
            StatusId = BlmBuff.高雷Dot,
            RemainingMs = 10000,
            ExpectedDurationMs = 30000,
        },
        AoeDot = new BlmDotSnapshot
        {
            StatusId = BlmBuff.高雷二Dot,
            RemainingMs = 10000,
            ExpectedDurationMs = 24000,
        },
        MaxPolyglot = 3,
        PolyglotTimerMs = 20000,
        DotEnabled = false,
        MoveXenoEnabled = true,
        MoveTriplecastEnabled = true,
        Transpose = ReadyAction(BLMSkill.星灵移位),
    };

    public static BlmActionAvailability ReadyAction(uint actionId) => new()
    {
        ActionId = actionId,
        IsUnlocked = true,
        IsAvailable = true,
        Charges = 1,
        MaxCharges = 1,
    };

    public static BlmActionAvailability UnavailableAction(uint actionId) => new()
    {
        ActionId = actionId,
        IsUnlocked = true,
        IsAvailable = false,
        MaxCharges = 1,
        CooldownRemainSeconds = 10,
    };
}
