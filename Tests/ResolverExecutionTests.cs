using System.Collections.Immutable;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
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
        TargetSwitchCancelsOldPending();
        HighPriorityAndFrameDriftFailClosed();
        ManualRecoveryAlwaysWorksWithoutPreviousGcd();
        IceParadoxTriplecastBlizzard3SequenceIsDeliverable();
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
            fixture.Execution.Resolve(BlmResolverChannel.OffGcd, fixture.Context) is null,
            "读条中不得提前向 PR 交付 OffGCD");
        AssertEx.False(
            fixture.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "尚未进入真实 weave 窗口时不得提前注册 Pending");
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
                IsSingleTargetMode = true,
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
