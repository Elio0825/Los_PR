using LosPr.BLM.Core;
using LosPr.BLM.Engine;
using PromeRotation.Data;

namespace Los.Tests;

internal static class DecisionTests
{
    private static readonly BlmDecisionEngine Engine = new();

    public static void GatesAndPurity()
    {
        var context = DecisionContext();
        var trackerBefore = context.Tracker;
        var first = Engine.Resolve(context);
        var second = Engine.Resolve(context);
        AssertEx.Equal(first, second, "相同 Context 必须返回完全相同的 Decision");
        AssertEx.Equal(trackerBefore, context.Tracker, "纯决策不得修改 Tracker 快照");
        AssertAction(first, BLMSkill.冰封, BlmRuleId.NeutralBlizzard3);

        AssertNoAction(
            Engine.Resolve(context with { HasTarget = false, HasValidTarget = false }),
            BlmNoActionReason.NoTarget);
        AssertNoAction(
            Engine.Resolve(context with { CanAct = false }),
            BlmNoActionReason.CannotAct);
        AssertNoAction(
            Engine.Resolve(context with { InRange = false }),
            BlmNoActionReason.OutOfRange);
        AssertNoAction(
            Engine.Resolve(context with { AcrState = AcrState.Off }),
            BlmNoActionReason.Disabled);
        AssertNoAction(
            Engine.Resolve(context with { Level = 99 }),
            BlmNoActionReason.UnsupportedLevelOrMode);
        AssertNoAction(
            Engine.Resolve(
                context,
                BlmDecisionPolicy.Default with { HighPriorityQueueActive = true }),
            BlmNoActionReason.HighPriorityQueue);

        var unreliable = FireContext(
            mp: 10000,
            hearts: 3,
            soul: 3,
            fire4Count: 0,
            historyReliable: false);
        var fromZero = Engine.Resolve(unreliable);
        var fromSix = Engine.Resolve(unreliable with
        {
            Tracker = unreliable.Tracker with { Fire4Count = 6 },
        });
        AssertEx.Equal(fromZero, fromSix, "历史不可信时不得读取 Fire4Count 分支");
        AssertAction(
            fromZero,
            BLMSkill.悖论,
            BlmRuleId.FireParadox);
        AssertEx.True(
            fromZero.TransitionRequest is null,
            "历史不可信时不得根据旧计数创建额外 Transition");

        var unreliableIce = IceContext(
            iceStacks: 3,
            hearts: 3,
            paradox: true) with
        {
            Tracker = Tracker(BlmPhase.Ice, historyReliable: false),
        };
        AssertAction(
            Engine.Resolve(unreliableIce),
            BLMSkill.悖论,
            BlmRuleId.IceParadox);

        AssertAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 0,
                paradox: false,
                mp: 9200) with
            {
                Tracker = Tracker(BlmPhase.Ice, historyReliable: false),
            }),
            BLMSkill.冰澈,
            BlmRuleId.IceBlizzard4);

    }

    public static void IceAndAf1Recovery()
    {
        AssertAction(
            Engine.Resolve(DecisionContext()),
            BLMSkill.冰封,
            BlmRuleId.NeutralBlizzard3);
        AssertAction(
            Engine.Resolve(IceContext(iceStacks: 2, hearts: 3, paradox: true)),
            BLMSkill.冰封,
            BlmRuleId.UiLowBlizzard3);
        AssertAction(
            Engine.Resolve(IceContext(iceStacks: 1, hearts: 0, paradox: false)),
            BLMSkill.冰封,
            BlmRuleId.UiLowBlizzard3);

        var b4 = Engine.Resolve(IceContext(
            iceStacks: 3,
            hearts: 2,
            paradox: true));
        AssertAction(b4, BLMSkill.冰澈, BlmRuleId.IceBlizzard4);
        AssertEx.True(b4.TransitionRequest is null, "普通 B4 不应提前创建 Transition");

        var firestarterB4 = Engine.Resolve(IceContext(
            iceStacks: 3,
            hearts: 2,
            paradox: true,
            firestarter: true));
        AssertAction(firestarterB4, BLMSkill.冰澈, BlmRuleId.IceBlizzard4);
        AssertEx.True(firestarterB4.TransitionRequest is null, "冰澈本身不预置星灵事务");

        var iceParadox = Engine.Resolve(IceContext(
            iceStacks: 3,
            hearts: 3,
            paradox: true) with
        {
            Tracker = Tracker(BlmPhase.Ice) with { FirestarterDebt = true },
        });
        AssertAction(iceParadox, BLMSkill.悖论, BlmRuleId.IceParadox);
        AssertTransition(
            iceParadox,
            TransitionKind.IceToFire,
            TransitionStep.CommitIceGcd,
            TransitionDeliveryChannel.Gcd,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionExpectation.IceReadyForAf1Paradox);
        var firstRoundIceParadox = Engine.Resolve(IceContext(
            iceStacks: 3,
            hearts: 3,
            paradox: true));
        AssertAction(firstRoundIceParadox, BLMSkill.悖论, BlmRuleId.IceParadox);
        AssertTransition(
            firstRoundIceParadox,
            TransitionKind.IceToFire,
            TransitionStep.CommitIceGcd,
            TransitionDeliveryChannel.Gcd,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionExpectation.IceReadyForAf1Paradox);
        var iceParadoxWithoutTranspose = Engine.Resolve(IceContext(
            iceStacks: 3,
            hearts: 3,
            paradox: true) with
        {
            Transpose = UnavailableAction(BLMSkill.星灵移位),
        });
        AssertAction(
            iceParadoxWithoutTranspose,
            BLMSkill.悖论,
            BlmRuleId.IceParadox);
        AssertEx.True(
            iceParadoxWithoutTranspose.TransitionRequest is null,
            "星灵不可用时仍应打冰悖论，但不得创建无法完成的 Transition");

        var usedTracker = Tracker(BlmPhase.Ice) with
        {
            ParadoxUsedIceSerial = 1,
        };
        AssertAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 3,
                paradox: true) with
            { Tracker = usedTracker }),
            BLMSkill.悖论,
            BlmRuleId.IceParadox);
        AssertAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 3,
                paradox: false,
                mp: 9999) with { Tracker = usedTracker }),
            BLMSkill.冰澈,
            BlmRuleId.IceMpRecovery);
        AssertNoAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 3,
                paradox: false,
                mp: 2900) with
            {
                Tracker = usedTracker with { PendingGaugeReconcile = true },
                HasThunderhead = true,
            }),
            BlmNoActionReason.WaitingForGaugeReconcile);
        AssertAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 3,
                paradox: false,
                mp: 2900) with
            {
                Tracker = usedTracker,
            }),
            BLMSkill.冰澈,
            BlmRuleId.IceMpRecovery);
        AssertNoAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 3,
                paradox: false,
                mp: 3100) with
            {
                Tracker = usedTracker with
                {
                    LastGcdId = BLMSkill.冰澈,
                    LastGcdAtMs = 1000,
                },
            }),
            BlmNoActionReason.WaitingForIceMpGain);
        AssertNoAction(
            Engine.Resolve(IceContext(
                iceStacks: 3,
                hearts: 3,
                paradox: false,
                mp: 10000) with { Tracker = usedTracker }),
            BlmNoActionReason.WaitingForTranspose);

        AssertAction(
            Engine.Resolve(Af1Context(firestarter: true, paradox: true)),
            BLMSkill.爆炎,
            BlmRuleId.Af1FirestarterFire3);

        var af1Paradox = Engine.Resolve(Af1Context(
            firestarter: false,
            paradox: true,
            mp: 1600));
        AssertAction(af1Paradox, BLMSkill.悖论, BlmRuleId.Af1Paradox);
        AssertTransition(
            af1Paradox,
            TransitionKind.IceToFire,
            TransitionStep.UseAfParadox,
            TransitionDeliveryChannel.Gcd,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionExpectation.FirestarterPresent);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: true,
                mp: 1599)),
            BLMSkill.冰封,
            BlmRuleId.FireResourceRecovery);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: false,
                mp: 1999) with
            { UmbralHearts = 3 }),
            BLMSkill.冰封,
            BlmRuleId.FireResourceRecovery);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: false,
                mp: 2000) with
            { UmbralHearts = 3 }),
            BLMSkill.爆炎,
            BlmRuleId.AfLowHardFire3);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: false,
                mp: 3999) with
            { UmbralHearts = 0 }),
            BLMSkill.冰封,
            BlmRuleId.FireResourceRecovery);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: false,
                mp: 4000) with
            { UmbralHearts = 0 }),
            BLMSkill.爆炎,
            BlmRuleId.AfLowHardFire3);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: false,
                mp: 10000)),
            BLMSkill.爆炎,
            BlmRuleId.AfLowHardFire3);
        AssertAction(
            Engine.Resolve(Af1Context(
                firestarter: false,
                paradox: true,
                mp: 10000) with
            { AfStacks = 2 }),
            BLMSkill.爆炎,
            BlmRuleId.AfLowHardFire3);
    }

    public static void FireBudgetBoundaries()
    {
        var reduced = FireContext(
            mp: 10000,
            hearts: 1,
            soul: 2,
            fire4Count: 2);
        var full = reduced with { UmbralHearts = 0 };
        AssertEx.Equal(
            800,
            BlmFireBudget.Build(reduced, reduced.Tracker).NextFire4Cost,
            "有 Umbral Heart 时下一发 F4 应为 800 MP");
        AssertEx.Equal(
            1600,
            BlmFireBudget.Build(full, full.Tracker).NextFire4Cost,
            "无 Umbral Heart 时下一发 F4 应为 1600 MP");

        AssertAction(
            Engine.Resolve(reduced with { Mp = 3200 }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(reduced with { Mp = 3199 }),
            BLMSkill.悖论,
            BlmRuleId.FireParadox);
        AssertAction(
            Engine.Resolve(reduced with
            {
                Mp = 3199,
                CompressFireParadox = true,
            }),
            BLMSkill.悖论,
            BlmRuleId.FireParadox);
        AssertAction(
            Engine.Resolve(reduced with { Mp = 2399 }),
            BLMSkill.绝望,
            BlmRuleId.Despair);

        var paradoxPosition = FireContext(
            mp: 2400,
            hearts: 0,
            soul: 3,
            fire4Count: 3,
            paradox: true);
        AssertAction(
            Engine.Resolve(paradoxPosition),
            BLMSkill.悖论,
            BlmRuleId.FireParadox);

        var compressedBeforeThird = FireContext(
            mp: 6000,
            hearts: 1,
            soul: 2,
            fire4Count: 2,
            paradox: true,
            compressFire: true) with { IsMoving = true };
        AssertAction(
            Engine.Resolve(compressedBeforeThird),
            BLMSkill.悖论,
            BlmRuleId.FireParadox);
        AssertAction(
            Engine.Resolve(compressedBeforeThird with
            {
                TriplecastStacks = 1,
                TriplecastRemainSeconds = 5f,
            }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(paradoxPosition with { Mp = 2399 }),
            BLMSkill.绝望,
            BlmRuleId.Despair);

        var paradoxUsed = paradoxPosition with
        {
            HasParadox = false,
            Tracker = paradoxPosition.Tracker with
            {
                ParadoxUsedFireSerial = paradoxPosition.Tracker.FirePhaseSerial,
            },
        };
        AssertAction(
            Engine.Resolve(paradoxUsed),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(paradoxUsed with { Mp = 2399 }),
            BLMSkill.绝望,
            BlmRuleId.Despair);

        var fifth = FireContext(
            mp: 2400,
            hearts: 0,
            soul: 5,
            fire4Count: 5,
            paradox: false,
            paradoxUsed: true);
        AssertAction(Engine.Resolve(fifth), BLMSkill.炽炎, BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(fifth with { Mp = 2399 }),
            BLMSkill.绝望,
            BlmRuleId.Despair);

        AssertAction(
            Engine.Resolve(FireContext(
                mp: 800,
                hearts: 0,
                soul: 6,
                fire4Count: 6,
                paradox: false,
                paradoxUsed: true)),
            BLMSkill.耀星,
            BlmRuleId.FlareStar);
        AssertAction(
            Engine.Resolve(FireContext(
                mp: 800,
                hearts: 0,
                soul: 6,
                fire4Count: 7,
                paradox: false,
                paradoxUsed: true)),
            BLMSkill.耀星,
            BlmRuleId.FlareStar);

        var afterFlareStar = FireContext(
            mp: 800,
            hearts: 0,
            soul: 0,
            fire4Count: 6,
            paradox: false,
            paradoxUsed: true);
        AssertAction(
            Engine.Resolve(afterFlareStar),
            BLMSkill.绝望,
            BlmRuleId.Despair);
        AssertAction(
            Engine.Resolve(afterFlareStar with { Mp = 10000 }),
            BLMSkill.绝望,
            BlmRuleId.Despair);
        AssertAction(
            Engine.Resolve(afterFlareStar with { Mp = 799 }),
            BLMSkill.冰封,
            BlmRuleId.FireBlizzard3);
        AssertAction(
            Engine.Resolve(FireContext(
                mp: 2900,
                hearts: 3,
                soul: 0,
                fire4Count: 0,
                paradox: false,
                historyReliable: false)),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);

        var instantIceEntry = afterFlareStar with
        {
            HasSwiftcast = true,
            SwiftcastRemainSeconds = 4f,
        };
        var despairWithTransition = Engine.Resolve(instantIceEntry);
        AssertTransition(
            despairWithTransition,
            TransitionKind.FireToIce,
            TransitionStep.CommitFireFinisher,
            TransitionDeliveryChannel.Gcd,
            IceToFireRoute.None,
            TransitionExpectation.FireFinisherReady);
        var despairWithoutTranspose = Engine.Resolve(instantIceEntry with
        {
            Transpose = UnavailableAction(BLMSkill.星灵移位),
        });
        AssertEx.True(
            despairWithoutTranspose.TransitionRequest is null,
            "星灵不可用时绝望不得创建火转冰 Transition");
        AssertEx.True(
            Engine.Resolve(instantIceEntry with
            {
                SwiftcastRemainSeconds = 3.24f,
            }).TransitionRequest is null,
            "Swift 低于 B3 reserve 时不得创建火转冰 Transition");
        AssertTransition(
            Engine.Resolve(instantIceEntry with
            {
                SwiftcastRemainSeconds = 3.25f,
            }),
            TransitionKind.FireToIce,
            TransitionStep.CommitFireFinisher,
            TransitionDeliveryChannel.Gcd,
            IceToFireRoute.None,
            TransitionExpectation.FireFinisherReady);
        AssertEx.True(
            Engine.Resolve(instantIceEntry with
            {
                HasSwiftcast = false,
                TriplecastStacks = 1,
                TriplecastRemainSeconds = 3.24f,
            }).TransitionRequest is null,
            "Triple 低于 B3 reserve 时不得创建火转冰 Transition");
        AssertTransition(
            Engine.Resolve(instantIceEntry with
            {
                HasSwiftcast = false,
                TriplecastStacks = 1,
                TriplecastRemainSeconds = 3.25f,
            }),
            TransitionKind.FireToIce,
            TransitionStep.CommitFireFinisher,
            TransitionDeliveryChannel.Gcd,
            IceToFireRoute.None,
            TransitionExpectation.FireFinisherReady);

        var compressedAfterStar = FireContext(
            mp: 2400,
            hearts: 0,
            soul: 0,
            fire4Count: 6,
            paradox: true,
            compressFire: true);
        AssertAction(
            Engine.Resolve(compressedAfterStar),
            BLMSkill.悖论,
            BlmRuleId.FireParadox);

        var inconsistentSoul = FireContext(
            mp: 2400,
            hearts: 0,
            soul: 5,
            fire4Count: 6,
            paradox: false,
            paradoxUsed: true);
        var recovery = Engine.Resolve(inconsistentSoul);
        AssertAction(recovery, BLMSkill.炽炎, BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(inconsistentSoul with
            {
                Tracker = inconsistentSoul.Tracker with { Fire4Count = 7 },
            }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertEx.Equal(
            5,
            inconsistentSoul.AstralSoul,
            "异常计数下仍必须以 Soul Gauge 作为耀星资源真值");
    }

    public static void MovementAndFollowUp()
    {
        var movingSoul = FireContext(
            mp: 800,
            hearts: 0,
            soul: 6,
            fire4Count: 6,
            paradox: false,
            paradoxUsed: true) with
        {
            IsMoving = true,
            Tracker = Tracker(BlmPhase.Fire) with
            {
                Fire4Count = 6,
                ParadoxUsedFireSerial = 1,
                LastAckGlobalSequence = 41,
            },
        };
        var reordered = Engine.Resolve(movingSoul);
        AssertAction(reordered, BLMSkill.绝望, BlmRuleId.MoveDespairReorder);
        AssertEx.True(reordered.TransitionRequest is null, "移动绝望不得误建火末 Transition");
        AssertEx.Equal(
            BlmFollowUpKind.FlareStarAfterMovementDespair,
            reordered.FollowUpRequest?.Kind ?? BlmFollowUpKind.None,
            "移动绝望必须声明耀星后续承诺");
        AssertEx.Equal(
            BLMSkill.耀星,
            reordered.FollowUpRequest?.RequiredActionId ?? 0,
            "后续承诺必须锁定耀星");
        AssertEx.Equal(
            movingSoul.Tracker.StateGeneration,
            reordered.FollowUpRequest?.StateGeneration ?? 0,
            "Follow-up 必须冻结 generation");
        AssertEx.Equal(
            movingSoul.Tracker.CombatSerial,
            reordered.FollowUpRequest?.CombatSerial ?? 0,
            "Follow-up 必须冻结战斗 serial");
        AssertEx.Equal(
            movingSoul.Tracker.FirePhaseSerial,
            reordered.FollowUpRequest?.FirePhaseSerial ?? 0,
            "Follow-up 必须冻结火阶段 serial");
        AssertEx.True(
            (reordered.FollowUpRequest?.ExpireAfterMs ?? 0) > 0,
            "Follow-up 必须携带有限相对时限");
        AssertEx.Equal(
            41u,
            reordered.FollowUpRequest?.RequestedAfterGlobalSequence ?? 0,
            "Follow-up 必须冻结触发动作前的 Ack sequence 基线");

        AssertNoAction(
            Engine.Resolve(movingSoul with { Mp = 799 }),
            BlmNoActionReason.MovementNoSafeGcd);
        AssertAction(
            Engine.Resolve(movingSoul with
            {
                HasSwiftcast = true,
                SwiftcastRemainSeconds = 0.1f,
            }),
            BLMSkill.绝望,
            BlmRuleId.MoveDespairReorder);
        AssertAction(
            Engine.Resolve(movingSoul with
            {
                HasSwiftcast = true,
                SwiftcastRemainSeconds = 1f,
            }),
            BLMSkill.耀星,
            BlmRuleId.FlareStar);
        AssertAction(
            Engine.Resolve(movingSoul with
            {
                TriplecastStacks = 1,
                TriplecastRemainSeconds = 1f,
            }),
            BLMSkill.耀星,
            BlmRuleId.FlareStar);
        AssertAction(
            Engine.Resolve(movingSoul with
            {
                HasSwiftcast = true,
                SwiftcastRemainSeconds = 1.4f,
                GcdRemainSeconds = 1f,
                AnimationLockSeconds = 0.2f,
            }),
            BLMSkill.绝望,
            BlmRuleId.MoveDespairReorder);
        AssertAction(
            Engine.Resolve(movingSoul with
            {
                HasSwiftcast = true,
                SwiftcastRemainSeconds = 1.5f,
                GcdRemainSeconds = 1f,
                AnimationLockSeconds = 0.2f,
            }),
            BLMSkill.耀星,
            BlmRuleId.FlareStar);

        AssertAction(
            Engine.Resolve(movingSoul with
            {
                PolyglotStacks = 1,
                MaxPolyglot = 3,
                PolyglotTimerMs = 20000,
            }),
            BLMSkill.异言,
            BlmRuleId.PolyglotMoveFill);

        var firestarterHeld = FireContext(
            mp: 10000,
            hearts: 3,
            soul: 0,
            fire4Count: 0,
            firestarter: true) with
        {
            IsMoving = true,
            MoveXenoEnabled = false,
        };
        AssertNoAction(
            Engine.Resolve(firestarterHeld),
            BlmNoActionReason.MovementNoSafeGcd);
        AssertAction(
            Engine.Resolve(
                firestarterHeld,
                BlmDecisionPolicy.Default with
                {
                    AllowEmergencyFirestarter = true,
                }),
            BLMSkill.爆炎,
            BlmRuleId.MoveEmergencyFirestarter);

        var movingAf1Recovery = Af1Context(
            firestarter: true,
            paradox: true) with
        {
            IsMoving = true,
            DotEnabled = true,
            HasThunderhead = true,
            SingleTargetDot = Dot(0),
            PolyglotStacks = 3,
            MaxPolyglot = 3,
            PolyglotTimerMs = 0,
        };
        AssertAction(
            Engine.Resolve(movingAf1Recovery),
            BLMSkill.爆炎,
            BlmRuleId.Af1FirestarterFire3);

    }

    public static void ThunderAndPolyglotOverrides()
    {
        var baseFire = FireContext(
            mp: 10000,
            hearts: 3,
            soul: 0,
            fire4Count: 0);
        var missingDot = baseFire with
        {
            DotEnabled = true,
            HasThunderhead = true,
            SingleTargetDot = Dot(0),
            EnemyCount = 5,
        };
        AssertAction(
            Engine.Resolve(missingDot),
            BLMSkill.高闪雷,
            BlmRuleId.ThunderRefresh);
        AssertAction(
            Engine.Resolve(missingDot with
            {
                SingleTargetDot = Dot(2999),
            }),
            BLMSkill.高闪雷,
            BlmRuleId.ThunderRefresh);
        AssertAction(
            Engine.Resolve(missingDot with
            {
                SingleTargetDot = Dot(3000),
            }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(missingDot with { DotEnabled = false }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(missingDot with { HasThunderhead = false }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);

        AssertAction(
            Engine.Resolve(missingDot with
            {
                AstralSoul = 6,
                Tracker = missingDot.Tracker with { Fire4Count = 6 },
            }),
            BLMSkill.耀星,
            BlmRuleId.FlareStar);

        var capped = baseFire with
        {
            PolyglotStacks = 3,
            MaxPolyglot = 3,
            PolyglotTimerMs = 7000,
            EnemyCount = 5,
        };
        AssertAction(
            Engine.Resolve(capped),
            BLMSkill.异言,
            BlmRuleId.PolyglotOvercap);
        AssertAction(
            Engine.Resolve(capped with { PolyglotTimerMs = 7001 }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(capped with
            {
                PolyglotStacks = 1,
                PolyglotTimerMs = 0,
            }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);
        AssertAction(
            Engine.Resolve(capped with
            {
                PolyglotStacks = 0,
                MaxPolyglot = 0,
                PolyglotTimerMs = 0,
            }),
            BLMSkill.炽炎,
            BlmRuleId.Fire4);

        var both = capped with
        {
            DotEnabled = true,
            HasThunderhead = true,
            SingleTargetDot = Dot(0),
        };
        AssertAction(
            Engine.Resolve(both),
            BLMSkill.异言,
            BlmRuleId.PolyglotOvercap);
    }

    public static void ThreeStandardRounds()
    {
        var context = DecisionContext() with
        {
            CompressFireParadox = false,
            DotEnabled = false,
            MoveXenoEnabled = false,
            Transpose = UnavailableAction(BLMSkill.星灵移位),
        };
        var expected = string.Join(",", new uint[]
        {
            BLMSkill.冰封,
            BLMSkill.冰澈,
            BLMSkill.悖论,
            BLMSkill.爆炎,
            BLMSkill.炽炎,
            BLMSkill.炽炎,
            BLMSkill.炽炎,
            BLMSkill.悖论,
            BLMSkill.炽炎,
            BLMSkill.炽炎,
            BLMSkill.炽炎,
            BLMSkill.耀星,
            BLMSkill.绝望,
        });
        var currentRound = new List<uint>();
        var completedRounds = 0;

        for (var step = 0; step < 100 && completedRounds < 3; step++)
        {
            var decision = Engine.Resolve(context);
            AssertEx.True(decision.HasAction, $"三轮仿真第 {step} 步不得空决策");
            AssertEx.True(
                BlmSkillBook.IsUnlocked(decision.ActionId, context.Level),
                $"三轮仿真返回未解锁动作 {decision.ActionId}");
            if (decision.TransitionRequest is not null)
            {
                AssertEx.Equal(
                    decision.ActionId,
                    decision.TransitionRequest.ExpectedActionId,
                    "Transition 首动作必须等于本次 GCD");
            }

            currentRound.Add(decision.ActionId);
            context = Advance(context, decision.ActionId);
            if (decision.ActionId != BLMSkill.绝望)
            {
                continue;
            }

            AssertEx.Equal(
                expected,
                string.Join(",", currentRound),
                $"第 {completedRounds + 1} 轮标准序列不合法");
            AssertEx.Equal(
                6,
                currentRound.Count(actionId => actionId == BLMSkill.炽炎),
                "每轮必须恰好六发炽炎");
            AssertEx.Equal(
                1,
                currentRound.Count(actionId => actionId == BLMSkill.耀星),
                "每轮必须恰好一发耀星");
            completedRounds++;
            currentRound.Clear();
        }

        AssertEx.Equal(3, completedRounds, "标准单体纯状态仿真必须连续完成三轮");
    }

    private static BlmContext Advance(BlmContext context, uint actionId)
    {
        var tracker = context.Tracker with
        {
            LastGcdId = actionId,
            LastGcdAtMs = context.CapturedAtMs + 2500,
        };
        var next = context with
        {
            CapturedAtMs = context.CapturedAtMs + 2500,
            Tracker = tracker,
        };

        switch (actionId)
        {
            case BLMSkill.冰封:
                {
                    var iceSerial = tracker.IcePhaseSerial + 1;
                    return next with
                    {
                        Phase = BlmPhase.Ice,
                        AfStacks = 0,
                        IceStacks = 3,
                        UmbralHearts = 0,
                        AstralSoul = 0,
                        Mp = next.MaxMp,
                        HasParadox = true,
                        Tracker = tracker with
                        {
                            IcePhaseSerial = iceSerial,
                            Fire4Count = 0,
                            ParadoxUsedIceSerial = 0,
                            LastObservedPhase = BlmPhase.Ice,
                        },
                    };
                }

            case BLMSkill.冰澈:
                return next with
                {
                    UmbralHearts = 3,
                    Mp = next.MaxMp,
                };

            case BLMSkill.爆炎:
                {
                    var fireSerial = tracker.FirePhaseSerial + 1;
                    return next with
                    {
                        Phase = BlmPhase.Fire,
                        AfStacks = 3,
                        IceStacks = 0,
                        AstralSoul = 0,
                        HasParadox = true,
                        HasFirestarter = false,
                        Tracker = tracker with
                        {
                            FirePhaseSerial = fireSerial,
                            Fire4Count = 0,
                            ParadoxUsedFireSerial = 0,
                            LastObservedPhase = BlmPhase.Fire,
                        },
                    };
                }

            case BLMSkill.炽炎:
                {
                    var cost = next.UmbralHearts > 0
                        ? BlmFireBudget.Fire4HeartCost
                        : BlmFireBudget.Fire4FullCost;
                    AssertEx.True(next.Mp >= cost, "仿真不得释放无法支付的炽炎");
                    return next with
                    {
                        Mp = next.Mp - cost,
                        UmbralHearts = Math.Max(0, next.UmbralHearts - 1),
                        AstralSoul = Math.Min(6, next.AstralSoul + 1),
                        Tracker = tracker with
                        {
                            Fire4Count = tracker.Fire4Count + 1,
                        },
                    };
                }

            case BLMSkill.悖论 when next.InIce:
                return next with
                {
                    HasParadox = false,
                    Tracker = tracker with
                    {
                        ParadoxUsedIceSerial = tracker.IcePhaseSerial,
                    },
                };

            case BLMSkill.悖论:
                AssertEx.True(
                    next.Mp >= BlmFireBudget.FireParadoxCost,
                    "仿真不得释放无法支付的火悖论");
                return next with
                {
                    Mp = next.Mp - BlmFireBudget.FireParadoxCost,
                    HasParadox = false,
                    HasFirestarter = true,
                    Tracker = tracker with
                    {
                        ParadoxUsedFireSerial = tracker.FirePhaseSerial,
                    },
                };

            case BLMSkill.耀星:
                AssertEx.True(next.AstralSoul >= 6, "耀星必须由满层 Soul 支付");
                return next with { AstralSoul = 0 };

            case BLMSkill.绝望:
                AssertEx.True(
                    next.Mp >= BlmFireBudget.DespairMinimumMp,
                    "绝望必须满足 800 MP 门槛");
                return next with { Mp = 0 };

            default:
                throw new InvalidOperationException($"仿真未处理动作 {actionId}");
        }
    }

    private static BlmContext DecisionContext() => new()
    {
        CapturedAtMs = 1000,
        CapturedAtUtc = DateTimeOffset.UtcNow,
        IsAvailable = true,
        AvailabilityText = "测试状态",
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
        TargetName = "测试目标",
        TargetHp = 1000000,
        TargetMaxHp = 1000000,
        EnemyCount = 1,
        SingleTargetDot = Dot(10000),
        AoeDot = new BlmDotSnapshot
        {
            StatusId = BlmBuff.高雷二Dot,
            RemainingMs = 10000,
            ExpectedDurationMs = 24000,
        },
        Phase = BlmPhase.Neutral,
        MaxPolyglot = 3,
        PolyglotTimerMs = 20000,
        CompressFireParadox = false,
        DotEnabled = false,
        MoveXenoEnabled = true,
        Transpose = ReadyAction(BLMSkill.星灵移位),
        Tracker = Tracker(BlmPhase.Neutral),
    };

    private static BlmContext IceContext(
        int iceStacks,
        int hearts,
        bool paradox,
        bool firestarter = false,
        long mp = 10000)
        => DecisionContext() with
        {
            Phase = BlmPhase.Ice,
            AfStacks = 0,
            IceStacks = iceStacks,
            UmbralHearts = hearts,
            HasParadox = paradox,
            HasFirestarter = firestarter,
            Mp = mp,
            Tracker = Tracker(BlmPhase.Ice),
        };

    private static BlmContext Af1Context(
        bool firestarter,
        bool paradox,
        long mp = 10000)
        => DecisionContext() with
        {
            Phase = BlmPhase.Fire,
            AfStacks = 1,
            HasFirestarter = firestarter,
            HasParadox = paradox,
            Mp = mp,
            Tracker = Tracker(BlmPhase.Fire),
        };

    private static BlmContext FireContext(
        long mp,
        int hearts,
        int soul,
        int fire4Count,
        bool paradox = true,
        bool paradoxUsed = false,
        bool firestarter = false,
        bool compressFire = false,
        bool historyReliable = true)
    {
        var tracker = Tracker(BlmPhase.Fire, historyReliable) with
        {
            Fire4Count = fire4Count,
            ParadoxUsedFireSerial = paradoxUsed ? 1 : 0,
        };
        return DecisionContext() with
        {
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            UmbralHearts = hearts,
            AstralSoul = soul,
            HasParadox = paradox,
            HasFirestarter = firestarter,
            Mp = mp,
            CompressFireParadox = compressFire,
            Tracker = tracker,
        };
    }

    private static BlmTrackerSnapshot Tracker(
        BlmPhase phase,
        bool historyReliable = true) => new()
        {
            CombatSerial = 1,
            StateGeneration = 1,
            IsCombatActive = true,
            HistoryReliable = historyReliable,
            FirePhaseSerial = phase == BlmPhase.Fire ? 1 : 0,
            IcePhaseSerial = phase == BlmPhase.Ice ? 1 : 0,
            LastObservedPhase = phase,
        };

    private static BlmDotSnapshot Dot(float remainingMs) => new()
    {
        StatusId = BlmBuff.高雷Dot,
        RemainingMs = remainingMs,
        ExpectedDurationMs = 30000,
    };

    private static BlmActionAvailability ReadyAction(uint actionId) => new()
    {
        ActionId = actionId,
        IsUnlocked = true,
        IsAvailable = true,
        Charges = 1,
        MaxCharges = 1,
    };

    private static BlmActionAvailability UnavailableAction(uint actionId) => new()
    {
        ActionId = actionId,
        IsUnlocked = true,
        IsAvailable = false,
        MaxCharges = 1,
        CooldownRemainSeconds = 10,
    };

    private static void AssertAction(
        BlmDecision decision,
        uint expectedActionId,
        string expectedRuleId)
    {
        AssertEx.Equal(expectedActionId, decision.ActionId, "决策动作错误");
        AssertEx.Equal(expectedRuleId, decision.RuleId, "决策规则 ID 错误");
        AssertEx.Equal(BlmNoActionReason.None, decision.NoActionReason, "动作决策不应含停手原因");
        AssertEx.Equal(BlmDecisionTarget.CurrentTarget, decision.Target, "GCD 必须冻结当前目标");
        AssertEx.Equal(200u, decision.TargetEntityId, "GCD 目标实体错误");
    }

    private static void AssertNoAction(
        BlmDecision decision,
        BlmNoActionReason expectedReason)
    {
        AssertEx.Equal(0u, decision.ActionId, "停手决策不得包含动作");
        AssertEx.Equal(expectedReason, decision.NoActionReason, "停手原因错误");
        AssertEx.Equal(BlmDecisionTarget.None, decision.Target, "停手决策不得冻结目标");
    }

    private static void AssertTransition(
        BlmDecision decision,
        TransitionKind kind,
        TransitionStep step,
        TransitionDeliveryChannel delivery,
        IceToFireRoute route,
        TransitionExpectation expectation)
    {
        var transition = decision.TransitionRequest
            ?? throw new InvalidOperationException("预期存在 TransitionRequest");
        AssertEx.Equal(decision.ActionId, transition.ExpectedActionId, "Transition 首动作不匹配");
        AssertEx.Equal(kind, transition.Kind, "Transition Kind 错误");
        AssertEx.Equal(step, transition.Step, "Transition Step 错误");
        AssertEx.Equal(delivery, transition.DeliveryChannel, "Transition Delivery 错误");
        AssertEx.Equal(route, transition.IceToFireRoute, "IceToFireRoute 错误");
        AssertEx.Equal(expectation, transition.Expectation, "Transition Expectation 错误");
        AssertEx.Equal(RotationMode.SingleTarget, transition.ModeAtRequest, "Transition 模式必须冻结单体");

        var coordinator = new BlmCoordinator(
            new FakeClock(),
            new MappingActionIdNormalizer());
        AssertEx.True(
            coordinator.TryBegin(
                decision.StateGeneration,
                transition.Kind,
                transition.Step,
                transition.DeliveryChannel,
                transition.ExpectedActionId,
                transition.Expectation,
                transition.ModeAtRequest,
                transition.StepExpireMs,
                decision.Reason,
                transition.IceToFireRoute,
                transition.TotalExpireMs),
            "Decision 生成的 TransitionRequest 必须可被 Coordinator.TryBegin 接受");
    }
}
