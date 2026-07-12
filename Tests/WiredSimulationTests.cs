using LosPr.BLM.Core;
using PromeRotation.Data;

namespace Los.Tests;

internal static class WiredSimulationTests
{
    public static void ThreeStandardRoundsThroughWiredScheduler()
        => new WiredScheduler().RunThreeRounds();

    private enum ResolverLine
    {
        Gcd,
        OffGcd,
        Always,
    }

    private sealed class WiredScheduler
    {
        private const float GcdDecisionThreshold = 0.3f;
        private const float OffGcdDecisionThreshold = 0.6f;
        private const int TargetRounds = 3;
        private const int MaxFrames = 300;

        private readonly Step3Harness _harness;
        private readonly HashSet<long> _transitionSerials = new();
        private readonly List<string> _trace = new();
        private RoundStats _round = new();
        private int _completedRounds;
        private int _completedTransitions;
        private int _emptyGcdCalls;
        private int _alwaysActions;
        private int _offGcdTransposeActions;
        private uint _lastGcdActionId;

        public WiredScheduler()
        {
            _harness = new Step3Harness(Step3Context.Ice(
                firestarter: true,
                paradox: true,
                hearts: 0,
                gcdRemainSeconds: 0.2f));
        }

        public void RunThreeRounds()
        {
            var frame = 0;
            while (_completedRounds < TargetRounds && frame++ < MaxFrames)
            {
                RunSchedulerFrame();
            }

            if (_completedRounds != TargetRounds)
            {
                throw new InvalidOperationException(
                    $"接线仿真未在 {MaxFrames} 帧内完成三轮。Trace={string.Join(" | ", _trace)}");
            }

            AssertEx.Equal(0, _emptyGcdCalls, "真实 GCD 调用窗口不得返回空动作");
            AssertEx.Equal(
                TargetRounds,
                _completedTransitions,
                "每轮 IceToFire Transition 都必须完成一次");
            AssertEx.Equal(
                TargetRounds,
                _transitionSerials.Count,
                "三轮 Transition 必须使用三个不同 serial");
            AssertEx.False(
                _harness.Coordinator.Peek().IsActive,
                "三轮结束后不得残留活跃 Transition");
            AssertEx.False(
                _harness.FollowUp.Peek().IsPending,
                "静止标准循环不得残留移动 Follow-up");

            AssertEx.Equal(0, _alwaysActions, "标准冰悖论路线不得使用 Always");
            AssertEx.Equal(
                TargetRounds,
                _offGcdTransposeActions,
                "每轮冰悖论后都应由宽尾窗 oGCD 提交 Transpose");
        }

        private void RunSchedulerFrame()
        {
            var always = _harness.ResolveAlways();
            if (always is not null)
            {
                AssertEx.True(
                    _harness.Context.GcdRemainSeconds <= OffGcdDecisionThreshold,
                    "Always 非空时必须处于 GcdRemain<=0.6s 的短尾窗");
                Execute(always, ResolverLine.Always);
                return;
            }

            var context = _harness.Context;
            if (context.GcdRemainSeconds <= GcdDecisionThreshold)
            {
                var gcd = _harness.ResolveGcd();
                if (gcd is null)
                {
                    _emptyGcdCalls++;
                    throw new InvalidOperationException(
                        $"GCD 窗口返回 null。Context={Describe(context)}");
                }

                Execute(gcd, ResolverLine.Gcd);
                return;
            }

            if (context.GcdRemainSeconds > OffGcdDecisionThreshold)
            {
                var offGcd = _harness.ResolveOffGcd();
                if (offGcd is not null)
                {
                    AssertEx.True(
                        context.GcdRemainSeconds > OffGcdDecisionThreshold,
                        "oGCD 非空时必须满足 PR 的 GcdRemain>0.6s 门槛");
                    Execute(offGcd, ResolverLine.OffGcd);
                    return;
                }
            }

            AdvanceToGcdDecisionWindow();
        }

        private void Execute(PAction action, ResolverLine line)
        {
            AssertActionContract(action, line);
            var phaseBefore = _harness.Context.Phase;
            var intentBeforeAck = _harness.Coordinator.Peek();

            if (line == ResolverLine.Always)
            {
                _alwaysActions++;
                AssertEx.Equal(BLMSkill.星灵移位, action.ActionId, "基线 Always 只允许 Transpose");
                AssertEx.Equal(
                    BLMSkill.冰澈,
                    _lastGcdActionId,
                    "标准路线不应进入 Always Transpose 分支");
                AssertEx.Equal(
                    TransitionDeliveryChannel.OffGcdOrAlways,
                    intentBeforeAck.DeliveryChannel,
                    "Always 只能消费预先冻结的 OffGcdOrAlways Step");
            }
            else if (line == ResolverLine.OffGcd
                     && action.ActionId == BLMSkill.星灵移位)
            {
                _offGcdTransposeActions++;
            }

            if (line == ResolverLine.Gcd)
            {
                _lastGcdActionId = action.ActionId;
                RecordRoundAction(action.ActionId);
            }

            if (intentBeforeAck.IsActive
                && intentBeforeAck.Stage == TransitionStage.Queued
                && intentBeforeAck.ExpectedActionId == action.ActionId)
            {
                if (intentBeforeAck.StepIndex == 0)
                {
                    AssertEx.True(
                        _transitionSerials.Add(intentBeforeAck.Serial),
                        "同一初始 Transition serial 不得重复入队");
                    AssertEx.Equal(
                        IceToFireRoute.ExistingFirestarter,
                        intentBeforeAck.IceToFireRoute,
                        "连续标准循环应使用保留 Firestarter 路线");
                }
            }

            var ack = _harness.CaptureAck(action.ActionId, phaseBefore);
            AssertEx.True(
                _harness.ApplyAck(ack),
                $"动作 {action.ActionId} 必须通过 Tracker 接收 Ack");

            if (ack.TransitionToken.IsValid)
            {
                AssertEx.Equal(
                    TransitionStage.Queued,
                    _harness.Coordinator.Peek().Stage,
                    "ActionEffect Ack 不能在 pre-gauge 阶段直接推进 Transition");
            }

            if (action.ActionId == BLMSkill.冰封)
            {
                AssertEx.Equal(
                    BlmFireBudget.StandardFire4Limit,
                    _harness.Tracker.GetTrackerSnapshot().Fire4Count,
                    "B3 Gauge 对账前 Tracker 应保留本轮六发 F4");
            }

            _harness.Clock.Advance(16);
            _harness.Reconcile(ApplyPostGauge(
                _harness.RawContext,
                action.ActionId,
                phaseBefore));

            if (intentBeforeAck.Kind == TransitionKind.IceToFire
                && intentBeforeAck.Step == TransitionStep.UseFirestarterF3
                && action.ActionId == BLMSkill.爆炎)
            {
                var completed = _harness.Coordinator.Peek();
                AssertEx.Equal(
                    TransitionStage.Completed,
                    completed.Stage,
                    "Firestarter F3 的下一 Tick AF3 Gauge 必须完成 Transition");
                AssertEx.Equal(
                    intentBeforeAck.Serial,
                    completed.Serial,
                    "完成 Transition 时不得更换 serial");
                _completedTransitions++;
            }

            _trace.Add(
                $"{line}:{action.ActionId}@{phaseBefore}"
                + $" MP={_harness.Context.Mp} Soul={_harness.Context.AstralSoul}"
                + $" F4={_harness.Context.Tracker.Fire4Count}"
                + $" Tr={_harness.Coordinator.Peek().Stage}");

            if (action.ActionId == BLMSkill.冰封)
            {
                CompleteRound();
            }
        }

        private void CompleteRound()
        {
            AssertEx.Equal(
                BlmFireBudget.StandardFire4Limit,
                _round.Fire4Count,
                $"第 {_completedRounds + 1} 轮必须恰好释放六发 F4");
            AssertEx.Equal(
                1,
                _round.FlareStarCount,
                $"第 {_completedRounds + 1} 轮必须恰好释放一发 Flare Star");
            AssertEx.Equal(
                1,
                _round.DespairCount,
                $"第 {_completedRounds + 1} 轮必须恰好释放一发 Despair");
            AssertEx.Equal(
                0,
                _harness.Context.Tracker.Fire4Count,
                "B3 动作后 Gauge 对账必须重置火段 F4 计数");
            AssertEx.False(
                _harness.Coordinator.Peek().IsActive,
                "每轮回到 UI3 后 Transition 必须已经收敛");
            AssertEx.True(_harness.Context.InIce, "每轮必须以真实 B3 Gauge 进入冰阶段");
            AssertEx.Equal(3, _harness.Context.IceStacks, "每轮 B3 后必须为 UI3");
            AssertEx.True(
                _harness.Context.HasFirestarter,
                "火悖论产生的 Firestarter 必须保留给下一轮冰转火");

            _completedRounds++;
            _round = new RoundStats();
        }

        private void RecordRoundAction(uint actionId)
        {
            switch (actionId)
            {
                case BLMSkill.炽炎:
                    _round.Fire4Count++;
                    break;
                case BLMSkill.耀星:
                    _round.FlareStarCount++;
                    break;
                case BLMSkill.绝望:
                    _round.DespairCount++;
                    break;
            }
        }

        private void AssertActionContract(PAction action, ResolverLine line)
        {
            var expectedType = line switch
            {
                ResolverLine.Gcd => ActionType.Gcd,
                ResolverLine.OffGcd => ActionType.OffGcd,
                ResolverLine.Always => ActionType.Always,
                _ => throw new ArgumentOutOfRangeException(nameof(line)),
            };
            AssertEx.Equal(expectedType, action.Type, $"{line} 返回了错误的 PAction.Type");
            AssertEx.True(
                BlmSkillBook.IsUnlocked(action.ActionId, _harness.Context.Level),
                $"Dispatcher 返回了当前等级未解锁动作 {action.ActionId}");

            if (line == ResolverLine.Gcd)
            {
                AssertEx.Equal(
                    _harness.Context.TargetEntityId,
                    action.NetworkTid,
                    "目标 GCD 必须使用当前冻结目标");
            }
        }

        private void AdvanceToGcdDecisionWindow()
        {
            var remain = Math.Max(0f, _harness.Context.GcdRemainSeconds);
            var milliseconds = Math.Max(
                16L,
                (long)Math.Ceiling(
                    Math.Max(0f, remain - 0.2f) * 1000f));
            _harness.AdvanceAndReconcile(milliseconds, context => context with
            {
                GcdRemainSeconds = 0.2f,
                IsCasting = false,
                AnimationLockSeconds = 0f,
            });
        }

        private static BlmContext ApplyPostGauge(
            BlmContext context,
            uint actionId,
            BlmPhase phaseBefore)
        {
            switch (actionId)
            {
                case BLMSkill.冰澈:
                    return context with
                    {
                        UmbralHearts = 3,
                        Mp = context.MaxMp,
                        GcdRemainSeconds = 0.5f,
                    };
                case BLMSkill.悖论 when phaseBefore == BlmPhase.Ice:
                    return context with
                    {
                        HasParadox = false,
                        GcdRemainSeconds = 2.4f,
                    };
                case BLMSkill.悖论:
                    AssertEx.True(
                        context.Mp >= BlmFireBudget.FireParadoxCost,
                        "火悖论必须满足 1600 MP 成本");
                    return context with
                    {
                        HasParadox = false,
                        HasFirestarter = true,
                        Mp = context.Mp - BlmFireBudget.FireParadoxCost,
                        GcdRemainSeconds = 2.4f,
                    };
                case BLMSkill.星灵移位 when phaseBefore == BlmPhase.Ice:
                    return context with
                    {
                        Phase = BlmPhase.Fire,
                        AfStacks = 1,
                        IceStacks = 0,
                        HasParadox = true,
                        GcdRemainSeconds = Math.Max(0.7f, context.GcdRemainSeconds),
                    };
                case BLMSkill.星灵移位:
                    return context with
                    {
                        Phase = BlmPhase.Ice,
                        AfStacks = 0,
                        IceStacks = 1,
                        GcdRemainSeconds = Math.Max(0.7f, context.GcdRemainSeconds),
                    };
                case BLMSkill.爆炎:
                    AssertEx.True(
                        context.HasFirestarter,
                        "接线标准循环的进火 F3 必须消费已确认 Firestarter");
                    return context with
                    {
                        Phase = BlmPhase.Fire,
                        AfStacks = 3,
                        IceStacks = 0,
                        HasFirestarter = false,
                        AstralSoul = 0,
                        GcdRemainSeconds = 2.4f,
                    };
                case BLMSkill.炽炎:
                    var fire4Cost = context.UmbralHearts > 0
                        ? BlmFireBudget.Fire4HeartCost
                        : BlmFireBudget.Fire4FullCost;
                    AssertEx.True(context.Mp >= fire4Cost, "F4 Gauge 演算不得透支 MP");
                    return context with
                    {
                        Mp = context.Mp - fire4Cost,
                        UmbralHearts = Math.Max(0, context.UmbralHearts - 1),
                        AstralSoul = Math.Min(
                            BlmFireBudget.StandardFire4Limit,
                            context.AstralSoul + 1),
                        GcdRemainSeconds = 0.5f,
                    };
                case BLMSkill.耀星:
                    AssertEx.Equal(
                        BlmFireBudget.StandardFire4Limit,
                        context.AstralSoul,
                        "Flare Star 必须消费满层 Astral Soul");
                    return context with
                    {
                        AstralSoul = 0,
                        GcdRemainSeconds = 0.5f,
                    };
                case BLMSkill.绝望:
                    AssertEx.True(
                        context.Mp >= BlmFireBudget.DespairMinimumMp,
                        "Despair 必须满足 800 MP 门槛");
                    return context with
                    {
                        Mp = 0,
                        GcdRemainSeconds = 2.4f,
                    };
                case BLMSkill.冰封:
                    return context with
                    {
                        Phase = BlmPhase.Ice,
                        AfStacks = 0,
                        IceStacks = 3,
                        UmbralHearts = 0,
                        AstralSoul = 0,
                        Mp = context.MaxMp,
                        HasParadox = true,
                        GcdRemainSeconds = 0.2f,
                    };
                default:
                    throw new InvalidOperationException(
                        $"接线仿真未定义动作 {actionId} 的动作后 Gauge。");
            }
        }

        private static string Describe(BlmContext context)
            => $"Phase={context.Phase},AF={context.AfStacks},UI={context.IceStacks},"
                + $"MP={context.Mp},Hearts={context.UmbralHearts},Soul={context.AstralSoul},"
                + $"F4={context.Tracker.Fire4Count},GcdRemain={context.GcdRemainSeconds:F2},"
                + $"Transition={context.Tracker.Transition.Kind}/{context.Tracker.Transition.Step}/"
                + context.Tracker.Transition.Stage;
    }

    private sealed class RoundStats
    {
        public int Fire4Count { get; set; }
        public int FlareStarCount { get; set; }
        public int DespairCount { get; set; }
    }
}
