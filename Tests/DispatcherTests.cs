using LosPr.BLM.Core;
using PromeRotation.Data;

namespace Los.Tests;

internal static class DispatcherTests
{
    public static void ExistingFirestarterPath()
    {
        var harness = new Step3Harness(Step3Context.Ice(
            firestarter: true,
            paradox: true,
            hearts: 3) with
        {
            LeyLinesEnabled = true,
            LeyLines = Step3Context.ReadyAction(BLMSkill.黑魔纹),
        });

        var iceParadox = RequireAction(
            harness.ResolveGcd(),
            "冰悖论应由 GCD Dispatcher 返回");
        AssertTargetGcd(iceParadox, BLMSkill.悖论, 200);
        var initial = AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.ExistingFirestarter,
            TransitionStep.CommitIceGcd,
            TransitionStage.Queued,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 0);
        AssertEx.Equal(200u, initial.TargetEntityIdAtRequest, "Transition 必须冻结请求期目标");

        harness.ApplyAck(iceParadox);
        AssertEx.Equal(
            TransitionStage.Queued,
            harness.Coordinator.Peek().Stage,
            "冰悖论 Ack 不能直接推进 Transition");
        AssertEx.True(
            harness.ResolveOffGcd() is null,
            "post-gauge 对账前不得提前返回星灵移位");

        harness.AdvanceAndReconcile(16, context => context with
        {
            HasParadox = false,
            GcdRemainSeconds = 2.4f,
        });
        var transposeRequested = AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.ExistingFirestarter,
            TransitionStep.UseTranspose,
            TransitionStage.Requested,
            TransitionDeliveryChannel.OffGcdOrAlways,
            stepIndex: 1);
        AssertEx.Equal(initial.Serial, transposeRequested.Serial, "多步 Transition 必须保持 Serial");

        var transpose = RequireAction(
            harness.ResolveOffGcd(),
            "冰悖论宽尾窗应返回星灵移位");
        AssertSelfAction(transpose, BLMSkill.星灵移位, ActionType.OffGcd);
        AssertEx.True(
            harness.ResolveOffGcd() is null,
            "同一 Transpose Step 不得重复交给 oGCD 队列");
        harness.AckAndApplyPostGauge(transpose);

        var fire3Requested = AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.ExistingFirestarter,
            TransitionStep.UseFirestarterF3,
            TransitionStage.Requested,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 2);
        AssertEx.Equal(initial.Serial, fire3Requested.Serial, "F3 Step 不得更换事务 Serial");

        var leyLines = RequireAction(
            harness.ResolveOffGcd(),
            "冰悖论后的第一插必须是星灵，星灵确认后才允许第二插黑魔纹");
        AssertSelfAction(leyLines, BLMSkill.黑魔纹, ActionType.OffGcd);
        harness.ApplyAck(leyLines);
        harness.AdvanceAndReconcile(16, context => context with
        {
            HasLeyLines = true,
        });
        AssertEx.True(
            harness.ResolveOffGcd() is null,
            "冰悖论双插达到星灵加黑魔纹后不得提交第三个能力");

        harness.SetWindow(0f);
        var fire3 = RequireAction(
            harness.ResolveGcd(),
            "星灵确认后必须由下一 GCD 返回 Firestarter 爆炎");
        AssertTargetGcd(fire3, BLMSkill.爆炎, 200);
        AssertEx.True(
            harness.ResolveGcd() is null,
            "已 Queued 的 Firestarter F3 不得重复返回");
        harness.AckAndApplyPostGauge(fire3);

        var completed = harness.Coordinator.Peek();
        AssertEx.Equal(TransitionStage.Completed, completed.Stage, "Existing Firestarter 路径应完成");
        AssertEx.Equal(initial.Serial, completed.Serial, "Completed 必须保留原事务 Serial");
        AssertEx.Equal(2, completed.StepIndex, "Existing Firestarter 应在第三个 Step 完成");
    }

    public static void Af1DebtPath()
    {
        var harness = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: true,
            hearts: 0));

        var blizzard4 = RequireAction(
            harness.ResolveGcd(),
            "标准冰段应先返回唯一一发冰澈");
        AssertTargetGcd(blizzard4, BLMSkill.冰澈, 200);
        AssertEx.False(harness.Coordinator.Peek().IsActive, "冰澈不得提前创建星灵事务");

        harness.AckAndApplyPostGauge(blizzard4);
        harness.SetWindow(0f);
        var iceParadox = RequireAction(
            harness.ResolveGcd(),
            "冰澈后必须释放冰悖论");
        AssertTargetGcd(iceParadox, BLMSkill.悖论, 200);
        var initial = AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionStep.CommitIceGcd,
            TransitionStage.Queued,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 0);

        harness.AckAndApplyPostGauge(iceParadox);
        var transpose = RequireAction(
            harness.ResolveOffGcd(),
            "冰悖论宽尾窗应由 oGCD 返回星灵移位");
        AssertSelfAction(transpose, BLMSkill.星灵移位, ActionType.OffGcd);
        harness.AckAndApplyPostGauge(transpose);

        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionStep.UseAfParadox,
            TransitionStage.Requested,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 2);
        harness.SetWindow(0f);
        var fireParadox = RequireAction(
            harness.ResolveGcd(),
            "AF1 赤字恢复必须先使用火悖论");
        AssertTargetGcd(fireParadox, BLMSkill.悖论, 200);
        harness.AckAndApplyPostGauge(fireParadox);

        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionStep.UseFirestarterF3,
            TransitionStage.Requested,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 3);
        harness.SetWindow(0f);
        var fire3 = RequireAction(
            harness.ResolveGcd(),
            "火悖论确认 Firestarter 后必须返回爆炎");
        AssertTargetGcd(fire3, BLMSkill.爆炎, 200);
        harness.AckAndApplyPostGauge(fire3);

        var completed = harness.Coordinator.Peek();
        AssertEx.Equal(TransitionStage.Completed, completed.Stage, "AF1 赤字恢复路径应完成");
        AssertEx.Equal(initial.Serial, completed.Serial, "AF1 赤字恢复必须保持同一 Serial");
        AssertEx.Equal(3, completed.StepIndex, "AF1 赤字恢复应在第四个 Step 完成");
    }

    public static void AlwaysBoundariesAndOnceOnly()
    {
        foreach (var gcdRemain in new[] { 1.2f, 0.6001f })
        {
            var harness = PrepareIceParadoxTranspose(gcdRemain);
            AssertEx.True(
                harness.ResolveAlways() is null,
                $"GcdRemain={gcdRemain} 时 Always 必须给 oGCD 让路");
            var transpose = RequireAction(
                harness.ResolveOffGcd(),
                $"GcdRemain={gcdRemain} 时 oGCD 应消费标准冰悖论尾窗");
            AssertSelfAction(transpose, BLMSkill.星灵移位, ActionType.OffGcd);
            AssertEx.True(
                harness.ResolveOffGcd() is null,
                "同一 Serial+StepIndex 的 oGCD 只能返回一次");
        }

        foreach (var gcdRemain in new[] { 0.6f, 0.5f })
        {
            var harness = PrepareIceParadoxTranspose(gcdRemain);
            AssertEx.True(
                harness.ResolveOffGcd() is null,
                $"GcdRemain={gcdRemain} 时 oGCD 不得越过 PR 的 >0.6 门槛");
            var transpose = RequireAction(
                harness.ResolveAlways(),
                "回蓝落在短尾窗时 Always 应提交已冻结的星灵移位");
            AssertSelfAction(transpose, BLMSkill.星灵移位, ActionType.Always);
        }
    }

    public static void FireToIceSuccessAndFallback()
    {
        var success = new Step3Harness(Step3Context.Fire(
            mp: 800,
            soul: 0,
            swift: true,
            swiftRemain: 4f));
        var despair = RequireAction(success.ResolveGcd(), "火末应返回绝望");
        AssertTargetGcd(despair, BLMSkill.绝望, 200);
        var initial = AssertIntent(
            success,
            TransitionKind.FireToIce,
            IceToFireRoute.None,
            TransitionStep.CommitFireFinisher,
            TransitionStage.Queued,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 0);

        success.AckAndApplyPostGauge(despair);
        AssertIntent(
            success,
            TransitionKind.FireToIce,
            IceToFireRoute.None,
            TransitionStep.UseTranspose,
            TransitionStage.Requested,
            TransitionDeliveryChannel.OffGcd,
            stepIndex: 1);
        var transpose = RequireAction(success.ResolveOffGcd(), "绝望宽尾窗应返回星灵移位");
        AssertSelfAction(transpose, BLMSkill.星灵移位, ActionType.OffGcd);
        success.AckAndApplyPostGauge(transpose);

        AssertIntent(
            success,
            TransitionKind.FireToIce,
            IceToFireRoute.None,
            TransitionStep.UseBlizzard3,
            TransitionStage.Requested,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 2);
        success.SetWindow(0f);
        var blizzard3 = RequireAction(success.ResolveGcd(), "星灵确认后必须返回冰封");
        AssertTargetGcd(blizzard3, BLMSkill.冰封, 200);
        success.AckAndApplyPostGauge(blizzard3);
        AssertEx.Equal(
            TransitionStage.Completed,
            success.Coordinator.Peek().Stage,
            "FireToIce 成功路径应完成");
        AssertEx.Equal(initial.Serial, success.Coordinator.Peek().Serial, "FireToIce 必须保持 Serial");

        var delayed = new Step3Harness(Step3Context.Fire(
            mp: 800,
            soul: 0) with
        {
            SwiftcastEnabled = true,
            Swiftcast = CoolingAction(MageUniversalSkill.即刻咏唱, 1f),
        });
        var delayedDespair = RequireAction(delayed.ResolveGcd(), "延迟即刻路线应先返回绝望");
        delayed.AckAndApplyPostGauge(delayedDespair);
        var delayedTranspose = RequireAction(
            delayed.ResolveOffGcd(),
            "即刻尚未转好时仍应先星灵移位");
        delayed.AckAndApplyPostGauge(delayedTranspose);

        delayed.SetWindow(0f);
        var waitParadox = RequireAction(
            delayed.ResolveGcd(),
            "UI1 应使用冰悖论等待即刻转好");
        AssertTargetGcd(waitParadox, BLMSkill.悖论, 200);
        delayed.ApplyAck(waitParadox, BlmPhase.Ice);
        delayed.AdvanceAndReconcile(16, context => context with
        {
            HasParadox = false,
            Swiftcast = Step3Context.ReadyAction(MageUniversalSkill.即刻咏唱),
            GcdRemainSeconds = 2.4f,
        });
        var swiftcast = RequireAction(
            delayed.ResolveOffGcd(),
            "冰悖论后必须释放刚转好的即刻");
        AssertSelfAction(swiftcast, MageUniversalSkill.即刻咏唱, ActionType.OffGcd);
        delayed.AckAndApplyPostGauge(swiftcast);
        delayed.SetWindow(0f);
        var delayedBlizzard3 = RequireAction(
            delayed.ResolveGcd(),
            "即刻确认后必须返回冰封");
        AssertTargetGcd(delayedBlizzard3, BLMSkill.冰封, 200);

        var fallback = new Step3Harness(Step3Context.Fire(mp: 800, soul: 0));
        var fallbackDespair = RequireAction(fallback.ResolveGcd(), "无瞬发路线仍应正常绝望");
        AssertEx.True(
            fallback.Coordinator.Peek().Kind == TransitionKind.None,
            "没有可在一发冰悖论内兑现的瞬发资源时不得创建星灵事务");
        fallback.AckAndApplyPostGauge(fallbackDespair);
        fallback.SetWindow(0f);
        var directBlizzard3 = RequireAction(
            fallback.ResolveGcd(),
            "无瞬发保障时应直接冰封回退");
        AssertTargetGcd(directBlizzard3, BLMSkill.冰封, 200);
    }

    public static void UtilityOffGcdStrategies()
    {
        var manualBlizzard3 = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 0,
            gcdRemainSeconds: 0f) with { Mp = 9200 });
        var blizzard4 = RequireAction(
            manualBlizzard3.ResolveGcd(),
            "手动冰三留下 UI3/空冰针时必须先补冰澈");
        AssertTargetGcd(blizzard4, BLMSkill.冰澈, 200);
        manualBlizzard3.AckAndApplyPostGauge(blizzard4);
        AssertSelfAction(
            RequireAction(
                manualBlizzard3.ResolveAlways(),
                "无冰悖论的冰澈后必须直接星灵移位"),
            BLMSkill.星灵移位,
            ActionType.Always);

        var emptyMpFullHearts = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 3,
            gcdRemainSeconds: 0f) with { Mp = 0 });
        var recoveryBlizzard4 = RequireAction(
            emptyMpFullHearts.ResolveGcd(),
            "空蓝满冰针且无待结算冰法时必须用冰澈主动恢复 MP");
        AssertTargetGcd(recoveryBlizzard4, BLMSkill.冰澈, 200);
        emptyMpFullHearts.AckAndApplyPostGauge(recoveryBlizzard4);
        AssertSelfAction(
            RequireAction(
                emptyMpFullHearts.ResolveAlways(),
                "空蓝满冰针回满后必须恢复星灵移位"),
            BLMSkill.星灵移位,
            ActionType.Always);

        var recoveryTranspose = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 3,
            gcdRemainSeconds: 2.4f));
        AssertSelfAction(
            RequireAction(
                recoveryTranspose.ResolveOffGcd(),
                "满 MP 满冰针且无悖论时应主动星灵移位"),
            BLMSkill.星灵移位,
            ActionType.OffGcd);

        var tailRecoveryTranspose = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 3,
            gcdRemainSeconds: 0.5f));
        AssertSelfAction(
            RequireAction(
                tailRecoveryTranspose.ResolveAlways(),
                "手动或 AOE 冰系 GCD 后的短尾窗应允许恢复星灵移位"),
            BLMSkill.星灵移位,
            ActionType.Always);

        var leyLines = new Step3Harness(Step3Context.Fire(mp: 6000, soul: 3) with
        {
            GcdRemainSeconds = 2.4f,
            LeyLinesEnabled = true,
            LeyLines = Step3Context.ReadyAction(BLMSkill.黑魔纹),
        });
        var leyLinesAction = RequireAction(
            leyLines.ResolveOffGcd(),
            "无 Transition 时应能主动返回黑魔纹");
        AssertSelfAction(leyLinesAction, BLMSkill.黑魔纹, ActionType.OffGcd);
        AssertEx.True(leyLines.ResolveOffGcd() is null, "普通能力 Ack 前不得重复返回");
        leyLines.ApplyAck(leyLinesAction);
        AssertEx.True(leyLines.ResolveOffGcd() is null, "同一 GCD 窗口只允许一个普通能力");

        var amplifier = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 3,
            gcdRemainSeconds: 2.4f) with
        {
            AmplifierEnabled = true,
            Amplifier = Step3Context.ReadyAction(BLMSkill.详述),
            Transpose = Step3Context.UnavailableAction(BLMSkill.星灵移位),
            PolyglotStacks = 1,
            MaxPolyglot = 3,
            PolyglotTimerMs = 10000,
        });
        var amplifierAction = RequireAction(
            amplifier.ResolveOffGcd(),
            "无 Transition 时应能主动返回详述");
        AssertSelfAction(amplifierAction, BLMSkill.详述, ActionType.OffGcd);

        var triplecast = new Step3Harness(Step3Context.Fire(
            mp: 6000,
            soul: 2,
            moving: true,
            gcdRemainSeconds: 2.4f) with
        {
            TriplecastEnabled = true,
            Triplecast = Step3Context.ReadyAction(BLMSkill.三连咏唱),
        });
        AssertSelfAction(
            RequireAction(triplecast.ResolveOffGcd(), "移动时应优先返回三连咏唱"),
            BLMSkill.三连咏唱,
            ActionType.OffGcd);

        var disabledMovementTriplecast = new Step3Harness(Step3Context.Fire(
            mp: 6000,
            soul: 2,
            moving: true,
            gcdRemainSeconds: 2.4f) with
        {
            MoveTriplecastEnabled = false,
            TriplecastEnabled = true,
            Triplecast = Step3Context.ReadyAction(BLMSkill.三连咏唱),
        });
        AssertEx.True(
            disabledMovementTriplecast.ResolveOffGcd() is null,
            "关闭移动三连后不得把三连咏唱作为普通走位能力释放");

        var reservedSwiftcast = new Step3Harness(Step3Context.Fire(
            mp: 6000,
            soul: 2,
            moving: true,
            gcdRemainSeconds: 2.4f) with
        {
            SwiftcastEnabled = true,
            Swiftcast = Step3Context.ReadyAction(MageUniversalSkill.即刻咏唱),
        });
        AssertEx.True(
            reservedSwiftcast.ResolveOffGcd() is null,
            "100 级即刻咏唱必须保留给火转冰，不得作为普通走位能力释放");

        var gated = Step3Context.Fire(mp: 6000, soul: 2) with
        {
            GcdRemainSeconds = 2.4f,
            LeyLinesEnabled = true,
            LeyLines = Step3Context.ReadyAction(BLMSkill.黑魔纹),
        };
        AssertEx.True(
            new Step3Harness(gated with { IsCasting = true }).ResolveOffGcd() is null,
            "读条中不得返回普通能力");
        AssertEx.True(
            new Step3Harness(gated with { AnimationLockSeconds = 2.4f }).ResolveOffGcd() is null,
            "动画锁不足以容纳能力时不得返回普通能力");
        AssertEx.True(
            new Step3Harness(gated with { HasLeyLines = true }).ResolveOffGcd() is null,
            "已有黑魔纹效果时不得重复释放黑魔纹");
        AssertEx.True(
            new Step3Harness(gated with
            {
                LeyLines = Step3Context.UnavailableAction(BLMSkill.黑魔纹),
            }).ResolveOffGcd() is null,
            "黑魔纹冷却未好时不得返回候选");

        var amplifierNearTick = Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 3,
            gcdRemainSeconds: 2.4f) with
        {
            AmplifierEnabled = true,
            Amplifier = Step3Context.ReadyAction(BLMSkill.详述),
            Transpose = Step3Context.UnavailableAction(BLMSkill.星灵移位),
            PolyglotStacks = 2,
            MaxPolyglot = 3,
            PolyglotTimerMs = 3000,
        };
        AssertEx.True(
            new Step3Harness(amplifierNearTick).ResolveOffGcd() is null,
            "自然通晓 Tick 即将到来时详述必须防溢出");

        var instantDoubleWeave = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: true,
            hearts: 3,
            gcdRemainSeconds: 2.4f) with
        {
            Transpose = Step3Context.UnavailableAction(BLMSkill.星灵移位),
            LeyLinesEnabled = true,
            LeyLines = Step3Context.ReadyAction(BLMSkill.黑魔纹),
            AmplifierEnabled = true,
            Amplifier = Step3Context.ReadyAction(BLMSkill.详述),
            PolyglotStacks = 1,
            MaxPolyglot = 3,
            PolyglotTimerMs = 10000,
        });
        AssertEx.True(
            instantDoubleWeave.ApplyAck(
                instantDoubleWeave.CaptureAck(BLMSkill.悖论)),
            "测试前置冰悖论 Ack 应被接收");
        instantDoubleWeave.AdvanceAndReconcile(16, context => context with
        {
            HasParadox = false,
        });
        var firstInstantWeave = RequireAction(
            instantDoubleWeave.ResolveOffGcd(),
            "瞬发悖论后应允许第一插黑魔纹");
        AssertSelfAction(firstInstantWeave, BLMSkill.黑魔纹, ActionType.OffGcd);
        instantDoubleWeave.ApplyAck(firstInstantWeave);
        instantDoubleWeave.AdvanceAndReconcile(16, context => context with
        {
            HasLeyLines = true,
        });
        AssertSelfAction(
            RequireAction(
                instantDoubleWeave.ResolveOffGcd(),
                "瞬发悖论后应允许第二插详述"),
            BLMSkill.详述,
            ActionType.OffGcd);

        var sameWindowRecovery = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: false,
            hearts: 3,
            gcdRemainSeconds: 2.4f) with
        {
            Mp = 3100,
            IsMoving = true,
            TriplecastEnabled = true,
            Triplecast = Step3Context.ReadyAction(BLMSkill.三连咏唱),
        });
        var movementTriplecast = RequireAction(
            sameWindowRecovery.ResolveOffGcd(),
            "低 MP 等待结算期间移动时应返回三连咏唱");
        AssertSelfAction(
            movementTriplecast,
            BLMSkill.三连咏唱,
            ActionType.OffGcd);
        sameWindowRecovery.ApplyAck(movementTriplecast);
        sameWindowRecovery.AdvanceAndReconcile(16, context => context with
        {
            Mp = context.MaxMp,
            IsMoving = false,
        });
        AssertSelfAction(
            RequireAction(
                sameWindowRecovery.ResolveOffGcd(),
                "同一 GCD 已使用其他能力后仍须允许恢复星灵，避免永久等待"),
            BLMSkill.星灵移位,
            ActionType.OffGcd);
    }

    public static void DelayedIceMpKeepsTransposeTransition()
    {
        var harness = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: true,
            hearts: 3,
            gcdRemainSeconds: 0.2f) with
        {
            Mp = 3100,
            TriplecastEnabled = true,
            Triplecast = Step3Context.ReadyAction(BLMSkill.三连咏唱),
        });

        var iceParadox = RequireAction(
            harness.ResolveGcd(),
            "冰段应先释放冰悖论并建立转火事务");
        AssertTargetGcd(iceParadox, BLMSkill.悖论, 200);
        harness.ApplyAck(iceParadox);
        harness.AdvanceAndReconcile(16, context => context with
        {
            HasParadox = false,
            Mp = 3100,
            IsMoving = true,
            GcdRemainSeconds = 2f,
        });

        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionStep.CommitIceGcd,
            TransitionStage.Confirmed,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 0);
        AssertEx.True(
            harness.ResolveOffGcd() is null,
            "MP 结算前必须保留转火事务，不得取消后误用移动三连");

        harness.AdvanceAndReconcile(500, context => context with
        {
            Mp = context.MaxMp,
            IsMoving = false,
            GcdRemainSeconds = 1.5f,
        });
        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.Af1ParadoxRecovery,
            TransitionStep.UseTranspose,
            TransitionStage.Requested,
            TransitionDeliveryChannel.OffGcdOrAlways,
            stepIndex: 1);
        AssertSelfAction(
            RequireAction(
                harness.ResolveOffGcd(),
                "MP 结算完成后必须继续原事务并返回星灵移位"),
            BLMSkill.星灵移位,
            ActionType.OffGcd);
    }

    public static void ExperimentalB4TransposeDespair()
    {
        var harness = new Step3Harness(Step3Context.Ice(
            firestarter: false,
            paradox: true,
            hearts: 0) with
        {
            ExperimentalB4TransposeDespairEnabled = true,
        });

        var iceParadox = RequireAction(
            harness.ResolveGcd(),
            "实验路线必须先释放冰悖论");
        AssertTargetGcd(iceParadox, BLMSkill.悖论, 200);
        harness.AckAndApplyPostGauge(iceParadox);

        harness.SetWindow(0f);
        var blizzard4 = RequireAction(
            harness.ResolveGcd(),
            "实验路线冰悖论后必须释放冰澈");
        AssertTargetGcd(blizzard4, BLMSkill.冰澈, 200);
        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.B4TransposeDespair,
            TransitionStep.CommitIceGcd,
            TransitionStage.Queued,
            TransitionDeliveryChannel.Gcd,
            stepIndex: 0);
        harness.AckAndApplyPostGauge(blizzard4);

        var transpose = RequireAction(
            harness.ResolveAlways(),
            "冰澈短尾窗必须由 Always 返回星灵移位");
        AssertSelfAction(transpose, BLMSkill.星灵移位, ActionType.Always);
        harness.AckAndApplyPostGauge(transpose);

        harness.SetWindow(0f);
        var despair = RequireAction(
            harness.ResolveGcd(),
            "实验路线星灵后必须返回绝望");
        AssertTargetGcd(despair, BLMSkill.绝望, 200);
        harness.AckAndApplyPostGauge(despair);
        AssertEx.Equal(
            TransitionStage.Completed,
            harness.Coordinator.Peek().Stage,
            "B4 星灵绝望事务必须完整收敛");
    }

    public static void CancellationTimeoutAndLostAck()
    {
        var targetLost = PrepareExistingTranspose();
        targetLost.AdvanceAndReconcile(16, context => context with
        {
            HasValidTarget = false,
        });
        AssertCancelled(targetLost, "目标失效必须取消 Transition");
        AssertEx.True(targetLost.ResolveOffGcd() is null, "目标失效后不得返回能力动作");

        var targetChanged = PrepareExistingTranspose();
        targetChanged.AdvanceAndReconcile(16, context => context with
        {
            TargetEntityId = 201,
        });
        AssertEx.True(
            targetChanged.ResolveOffGcd() is null,
            "冻结目标变化后不得向新目标释放星灵步骤");
        AssertCancelled(targetChanged, "冻结目标变化必须取消 Transition");

        var missedWindow = PrepareExistingTranspose();
        missedWindow.SetWindow(0f);
        AssertEx.True(
            missedWindow.ResolveGcd() is null,
            "错过事务尾窗后不得在资源完整时退化为硬读爆炎");
        AssertEx.Equal(
            TransitionStage.Cancelled,
            missedWindow.Coordinator.Peek().Stage,
            "错过能力尾窗的旧事务必须取消");
        AssertSelfAction(
            RequireAction(
                missedWindow.ResolveAlways(),
                "旧事务取消后应由恢复策略重新提交星灵移位"),
            BLMSkill.星灵移位,
            ActionType.Always);

        var lostAck = new Step3Harness(Step3Context.Ice(
            firestarter: true,
            paradox: true,
            hearts: 3));
        var dropped = RequireAction(lostAck.ResolveGcd(), "丢 Ack 场景应先返回冰悖论");
        AssertTargetGcd(dropped, BLMSkill.悖论, 200);
        var firstSerial = lostAck.Coordinator.Peek().Serial;
        lostAck.AdvanceAndReconcile(2001, context => context with
        {
            GcdRemainSeconds = 0f,
        });
        AssertCancelled(
            lostAck,
            "瞬发 GCD 丢 Ack 后必须在下一 GCD ready 前按 deadline 取消");

        var retry = RequireAction(
            lostAck.ResolveGcd(),
            "丢 Ack 事务取消后同一次 GCD ready 必须恢复合法决策");
        AssertTargetGcd(retry, BLMSkill.悖论, 200);
        AssertEx.True(
            lostAck.Coordinator.Peek().Serial > firstSerial,
            "只有超时收敛并创建新 Transition 后才允许重试同一动作");

        var highPriority = PrepareExistingTranspose();
        var takeoverPolicy = BlmDecisionPolicy.Default with
        {
            HighPriorityQueueActive = true,
        };
        var beforeTakeover = highPriority.Coordinator.Peek();
        AssertEx.True(
            highPriority.ResolveOffGcd(takeoverPolicy) is null,
            "高优先级队列接管时 Dispatcher 必须让位");
        var duringTakeover = highPriority.Coordinator.Peek();
        AssertEx.Equal(beforeTakeover.Serial, duringTakeover.Serial, "高优接管不得更换事务 Serial");
        AssertEx.Equal(beforeTakeover.StepIndex, duringTakeover.StepIndex, "高优接管不得推进 StepIndex");
        AssertEx.Equal(beforeTakeover.Stage, duringTakeover.Stage, "高优接管期间事务 Stage 必须保持");

        var queuedDuringTakeover = new Step3Harness(Step3Context.Ice(
            firestarter: true,
            paradox: true,
            hearts: 3));
        RequireAction(queuedDuringTakeover.ResolveGcd(), "高优孤儿保护场景应先 Queued 冰悖论");
        var queuedBefore = queuedDuringTakeover.Coordinator.Peek();
        AssertEx.Equal(TransitionStage.Queued, queuedBefore.Stage, "高优前置场景必须处于 Queued");
        queuedDuringTakeover.Dispatcher.Reconcile(
            queuedDuringTakeover.Context,
            highPriorityQueueActive: true);
        var queuedAfter = queuedDuringTakeover.Coordinator.Peek();
        AssertEx.Equal(queuedBefore.Serial, queuedAfter.Serial, "高优接管不得遗弃已入 PR 队列的事务");
        AssertEx.Equal(queuedBefore.StepIndex, queuedAfter.StepIndex, "高优接管不得改写 Queued StepIndex");
        AssertEx.Equal(TransitionStage.Queued, queuedAfter.Stage, "高优接管期间已 Queued 动作必须继续等待 Ack");
    }

    private static Step3Harness PrepareIceParadoxTranspose(float gcdRemain)
    {
        var harness = new Step3Harness(Step3Context.Ice(
            firestarter: true,
            paradox: true,
            hearts: 3));
        var iceParadox = RequireAction(harness.ResolveGcd(), "边界场景应先返回冰悖论");
        harness.AckAndApplyPostGauge(iceParadox);
        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.ExistingFirestarter,
            TransitionStep.UseTranspose,
            TransitionStage.Requested,
            TransitionDeliveryChannel.OffGcdOrAlways,
            stepIndex: 1);
        harness.SetWindow(gcdRemain);
        return harness;
    }

    private static Step3Harness PrepareExistingTranspose()
    {
        var harness = new Step3Harness(Step3Context.Ice(
            firestarter: true,
            paradox: true,
            hearts: 3));
        var iceParadox = RequireAction(harness.ResolveGcd(), "准备事务时应返回冰悖论");
        harness.AckAndApplyPostGauge(iceParadox);
        AssertIntent(
            harness,
            TransitionKind.IceToFire,
            IceToFireRoute.ExistingFirestarter,
            TransitionStep.UseTranspose,
            TransitionStage.Requested,
            TransitionDeliveryChannel.OffGcdOrAlways,
            stepIndex: 1);
        return harness;
    }

    private static BlmIntent AssertIntent(
        Step3Harness harness,
        TransitionKind kind,
        IceToFireRoute route,
        TransitionStep step,
        TransitionStage stage,
        TransitionDeliveryChannel delivery,
        int stepIndex)
    {
        var intent = harness.Coordinator.Peek();
        AssertEx.Equal(kind, intent.Kind, "Transition Kind 错误");
        AssertEx.Equal(route, intent.IceToFireRoute, "Transition Route 错误");
        AssertEx.Equal(step, intent.Step, "Transition Step 错误");
        AssertEx.Equal(stage, intent.Stage, "Transition Stage 错误");
        AssertEx.Equal(delivery, intent.DeliveryChannel, "Transition DeliveryChannel 错误");
        AssertEx.Equal(stepIndex, intent.StepIndex, "Transition StepIndex 错误");
        return intent;
    }

    private static void AssertCancelled(Step3Harness harness, string message)
        => AssertEx.Equal(
            TransitionStage.Cancelled,
            harness.Coordinator.Peek().Stage,
            message);

    private static PAction RequireAction(PAction? action, string message)
        => action ?? throw new InvalidOperationException(message);

    private static BlmActionAvailability CoolingAction(uint actionId, float remainSeconds)
        => new()
        {
            ActionId = actionId,
            IsUnlocked = true,
            IsAvailable = true,
            Charges = 0,
            MaxCharges = 1,
            CooldownRemainSeconds = remainSeconds,
            RecastTotalSeconds = 40f,
            NextChargeRemainSeconds = remainSeconds,
        };

    private static void AssertTargetGcd(
        PAction action,
        uint expectedActionId,
        uint expectedTargetId)
    {
        AssertEx.Equal(expectedActionId, action.ActionId, "GCD 动作 ID 错误");
        AssertEx.Equal(ActionType.Gcd, action.Type, "GCD 必须使用 ActionType.Gcd");
        AssertEx.Equal(
            ActionTargetType.Self,
            action.Target,
            "冻结 NetworkTid 的动作必须以 Self 作为可解析基准目标");
        AssertEx.Equal(expectedTargetId, action.NetworkTid, "GCD 必须冻结请求期目标实体");
    }

    private static void AssertSelfAction(
        PAction action,
        uint expectedActionId,
        ActionType expectedType)
    {
        AssertEx.Equal(expectedActionId, action.ActionId, "自身能力动作 ID 错误");
        AssertEx.Equal(expectedType, action.Type, "能力动作队列类型错误");
        AssertEx.Equal(ActionTargetType.Self, action.Target, "星灵移位必须以自身为目标");
        AssertEx.Equal(0u, action.NetworkTid, "自身能力不得携带敌方 NetworkTid");
    }
}
