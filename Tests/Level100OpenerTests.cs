using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Data;
using LosPr.BLM.Openers;
using LosPr.BLM.Resolvers;
using PromeRotation.Data;

namespace Los.Tests;

internal static class Level100OpenerTests
{
    private static readonly BlmOpenerPolicy EnabledWithoutPotion = new(
        Enabled: true,
        HighEndPotionEnabled: false,
        DailyInCombatEnabled: true);

    public static void RunAll()
    {
        FormalOpenerIsRegisteredAndDisabledByDefault();
        HighEndAndDailyShareTheFrozenFivePlusSevenContract();
        DailyWaitsForCombatAndCompletesFivePlusSevenWithoutPotion();
        DailyRequiresDailyPresetAndSinglePartyEightPlayerDuty();
        CountdownFireThreeBridgesBeforeInCombatAndRebindsGeneration();
        MissingAckCancelsInsteadOfSkippingTheStep();
        TargetModeAndManualOverrideCancelTheWholeSequence();
        DotDisabledUsesAlwaysBridgeAfterFireThree();
        PotionUsesAlwaysAndRetriesUntilCooldownConfirmation();
        MissingPotionAckSkipsOptionalStepAndCompletesFivePlusSeven();
        InCombatMayArriveBeforeCountdownFireThreeAck();
        CancelledCountdownFactoryCannotEmitFireThree();
        CancellationDrainsActiveCommandBeforeResolverHandoff();
        InterruptedFireFourRetriesSameStepAndCompletesFivePlusSeven();
    }

    private static void FormalOpenerIsRegisteredAndDisabledByDefault()
    {
        var settings = new BlackMageSettings();
        AssertEx.Equal(BlmOpenerSelection.None, settings.OpenerSelection, "起手必须默认关闭");
        AssertEx.True(settings.OpenerPotionEnabled, "高难起手药水策略默认开启");

        BlackMageRotation.ApplyModeDefaults(settings, BlmConsoleMode.Daily);
        AssertEx.False(settings.OpenerPotionEnabled, "日常预设必须关闭起手药水");
        BlackMageRotation.ApplyModeDefaults(settings, BlmConsoleMode.HighEnd);
        AssertEx.True(settings.OpenerPotionEnabled, "高难预设必须恢复起手药水");

        var migrated = new BlackMageSettings
        {
            CombatMode = BlmConsoleMode.HighEnd,
            OpenerPotionEnabled = false,
            OpenerPolicyVersion = 0,
        };
        migrated.Normalize();
        AssertEx.True(migrated.OpenerPotionEnabled, "旧版高难配置必须一次性恢复药水默认");
        migrated.OpenerPotionEnabled = false;
        migrated.Normalize();
        AssertEx.False(migrated.OpenerPotionEnabled, "迁移后必须保留用户手动关闭药水的选择");
        AssertEx.False(
            BlackMageRotation.QtList.ContainsKey("100级5+7起手"),
            "正式起手不得继续占用QT");
        AssertEx.False(
            BlackMageRotation.QtList.ContainsKey("高难起手爆发药"),
            "起手药水不得继续占用QT");

        settings.OpenerSelection = BlmOpenerSelection.Standard57;
        BlackMageRotation.ApplyModeDefaults(settings, BlmConsoleMode.Daily);
        var dailyPolicy = BlackMageRotation.CreateOpenerPolicy(settings);
        AssertEx.True(dailyPolicy.DailyInCombatEnabled, "日常预设必须允许八人本无倒计时起手");
        AssertEx.False(dailyPolicy.HighEndCountdownEnabled, "日常预设不得武装高难倒计时起手");

        BlackMageRotation.ApplyModeDefaults(settings, BlmConsoleMode.HighEnd);
        var highEndPolicy = BlackMageRotation.CreateOpenerPolicy(settings);
        AssertEx.False(highEndPolicy.DailyInCombatEnabled, "高难预设不得回落到无倒计时日常起手");
        AssertEx.True(highEndPolicy.HighEndCountdownEnabled, "高难预设必须允许倒计时起手");
        AssertEx.True(
            BlackMageRotation.Openers.TryGetValue(BlmLevel100Opener.Name, out var openerType),
            "PR 起手注册表必须包含正式5+7适配器");
        AssertEx.Equal(typeof(BlmLevel100Opener), openerType!, "正式起手注册类型错误");

        var opener = new BlmLevel100Opener();
        AssertEx.Equal(0, opener.InCombatSequence.Count, "PR 原生战斗动作组必须保持为空");
        var precast = BlmLevel100Opener.CreatePrecastAction();
        AssertEx.Equal(3500, BlmOpener57Definition.PrecastRemainingMs, "爆炎预读时间错误");
        AssertEx.Equal(BLMSkill.爆炎, precast.ActionId, "倒计时预读必须使用爆炎");
        AssertEx.False(precast.RequiresVerification, "PR 倒计时执行器不处理验证标记");
    }

    private static void HighEndAndDailyShareTheFrozenFivePlusSevenContract()
    {
        const uint potionId = 1_049_237;
        var highEnd = BlmOpener57Definition.Build(
            BlmOpenerMode.HighEndCountdown,
            dotEnabled: true,
            potionId);
        var daily = BlmOpener57Definition.Build(
            BlmOpenerMode.DailyInCombat,
            dotEnabled: true,
            potionId);

        AssertEx.Equal(5, CountPrefix(highEnd, "Fire4.Pre."), "高难魔泉前炽炎数量错误");
        AssertEx.Equal(7, CountPrefix(highEnd, "Fire4.Post."), "高难魔泉后炽炎数量错误");
        AssertEx.Equal(5, CountPrefix(daily, "Fire4.Pre."), "日常魔泉前炽炎数量错误");
        AssertEx.Equal(7, CountPrefix(daily, "Fire4.Post."), "日常魔泉后炽炎数量错误");
        AssertEx.Equal(2, CountAction(highEnd, BLMSkill.耀星), "高难必须包含两发耀星");
        AssertEx.Equal(2, CountAction(daily, BLMSkill.耀星), "日常必须包含两发耀星");
        AssertEx.Equal(1, CountAction(highEnd, potionId), "高难有药时必须包含一个药水节点");
        AssertEx.Equal(0, CountAction(daily, potionId), "日常动作表不得包含药水");
        AssertEx.Equal(0u, daily.PotionId, "日常计划不得保留药水ID");

        var highEndWithoutPotion = BlmOpener57Definition.Build(
            BlmOpenerMode.HighEndCountdown,
            dotEnabled: true,
            potionId: 0);
        AssertEx.False(
            highEndWithoutPotion.Steps.Any(step => step.Kind == BlmOpenerStepKind.Item),
            "无药高难必须删除药水步骤，而不是加入ActionId=0占位");

        var potionIndex = IndexOf(highEnd, "Potion");
        AssertEx.Equal("Fire4.Pre.1", highEnd.Steps[potionIndex - 1].Id, "药水前置位置错误");
        AssertEx.Equal("LeyLines", highEnd.Steps[potionIndex + 1].Id, "药水后黑魔纹位置错误");
        AssertEx.True(
            IndexOf(highEnd, "Triplecast") < IndexOf(highEnd, "Fire4.Post.7"),
            "三连必须位于最后一发魔泉后炽炎之前");
    }

    private static void DailyWaitsForCombatAndCompletesFivePlusSevenWithoutPotion()
    {
        var clock = new FakeClock();
        var outOfCombat = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(outOfCombat, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);

        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        AssertEx.False(service.OwnsExecution, "日常起手不得在T开怪前取得执行权");

        tracker.Reconcile(outOfCombat with { InCombat = true, CapturedAtMs = clock.NowMs });
        var context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        AssertEx.True(service.OwnsExecution, "T开怪后日常起手必须取得执行权");

        var plan = BlmOpener57Definition.Build(
            BlmOpenerMode.DailyInCombat,
            dotEnabled: true,
            potionId: 0);
        uint sequence = 1;
        foreach (var step in plan.Steps)
        {
            context = PrepareForDispatch(tracker.GetContextSnapshot(), step);
            tracker.Reconcile(context);
            context = tracker.GetContextSnapshot();
            service.Update(context, false, EnabledWithoutPotion, 0f);

            var action = service.Resolve(step.Channel, context);
            AssertEx.True(action is not null, $"日常起手未投递 {step.Id}");
            AssertEx.Equal(step.ActionId, action!.ActionId, $"日常起手 {step.Id} 动作错误");
            AssertEx.True(action.RequiresVerification, $"日常起手 {step.Id} 必须启用PR验证");
            AssertEx.False(action.Type == ActionType.Item, "日常起手不得投递Item动作");

            var ack = tracker.CreateAckEnvelope(
                context.PlayerEntityId,
                action.ActionId,
                sequence++,
                context.Phase,
                clock.NowMs);
            AssertEx.True(tracker.ApplyActionEffect(ack), $"Tracker拒绝 {step.Id} Ack");
            service.OnAcceptedAction(ack);

            var after = ApplyActionResult(context, step);
            clock.Advance(20);
            tracker.Reconcile(after with { CapturedAtMs = clock.NowMs });
            service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        }

        var snapshot = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Completed, snapshot.Status, "日常5+7必须完整结束");
        AssertEx.False(snapshot.OwnsExecution, "日常起手完成后必须交还标准Resolver");
        AssertEx.Equal(5, snapshot.Fire4BeforeManafont, "日常前半段炽炎确认数错误");
        AssertEx.Equal(7, snapshot.Fire4AfterManafont, "日常后半段炽炎确认数错误");
    }

    private static void DailyRequiresDailyPresetAndSinglePartyEightPlayerDuty()
    {
        AssertDailyStart(
            new BlmDutyComposition(8, 1),
            EnabledWithoutPotion,
            expected: true,
            "单队八人日常本必须允许无倒计时起手");
        AssertDailyStart(
            new BlmDutyComposition(4, 1),
            EnabledWithoutPotion,
            expected: false,
            "四人迷宫不得启动日常5+7");
        AssertDailyStart(
            new BlmDutyComposition(8, 3),
            EnabledWithoutPotion,
            expected: false,
            "24人本不得启动日常5+7");
        AssertDailyStart(
            default,
            EnabledWithoutPotion,
            expected: false,
            "副本编制未知时不得冒险启动日常5+7");
        AssertDailyStart(
            new BlmDutyComposition(8, 1),
            EnabledWithoutPotion with { DailyInCombatEnabled = false },
            expected: false,
            "高难预设不得回落到日常5+7");

        var clock = new FakeClock();
        var context = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(context, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        AssertEx.False(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                EnabledWithoutPotion with { HighEndCountdownEnabled = false },
                potionId: 0),
            "日常预设不得武装高难倒计时起手");
    }

    private static void AssertDailyStart(
        BlmDutyComposition composition,
        BlmOpenerPolicy policy,
        bool expected,
        string message)
    {
        var clock = new FakeClock();
        var context = ReadyOpenerContext(inCombat: true) with
        {
            DutyComposition = composition,
        };
        var tracker = new BlmStateTracker(context, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
        AssertEx.Equal(expected, service.OwnsExecution, message);
    }

    private static void CountdownFireThreeBridgesBeforeInCombatAndRebindsGeneration()
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        PAction? bridged = null;
        var service = CreateService(tracker, clock, action => bridged = action);

        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                EnabledWithoutPotion,
                potionId: 0),
            "高难倒计时必须成功武装");

        var fire3Ack = tracker.CreateAckEnvelope(
            initial.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(fire3Ack), "Tracker拒绝倒计时爆炎Ack");
        service.OnAcceptedAction(fire3Ack);
        tracker.Reconcile(initial with
        {
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        AssertEx.True(bridged is not null, "爆炎确认后必须在InCombat之前桥接下一步");
        AssertEx.Equal(BLMSkill.高闪雷, bridged!.ActionId, "战前桥接必须保持AE雷系位置");
        AssertEx.False(tracker.GetContextSnapshot().InCombat, "桥接验证现场必须仍未进入战斗");

        var enteringCombat = tracker.GetContextSnapshot() with
        {
            InCombat = true,
            CapturedAtMs = clock.NowMs,
        };
        tracker.Reconcile(enteringCombat);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        var snapshot = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Executing, snapshot.Status, "进战Generation切换不得取消高难起手");
        AssertEx.True(snapshot.HasPendingAction, "进战后必须保留并重绑定桥接雷的Pending");
        AssertEx.True(tracker.GetTrackerSnapshot().HasPendingIssuedAction, "Tracker必须持有重绑定后的唯一Pending");
    }

    private static void MissingAckCancelsInsteadOfSkippingTheStep()
    {
        var clock = new FakeClock();
        var context = ReadyOpenerContext(inCombat: true);
        var tracker = new BlmStateTracker(context, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        var fire3 = service.Resolve(BlmResolverChannel.Gcd, context);
        AssertEx.True(fire3 is not null, "日常起手必须先投递爆炎");

        clock.Advance(800);
        tracker.Reconcile(context with
        {
            CapturedAtMs = clock.NowMs,
            IsCasting = false,
            CurrentCastingActionId = 0,
        });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        var snapshot = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Cancelled, snapshot.Status, "爆炎未开始/无Ack必须取消整段");
        AssertEx.Equal(0, snapshot.StepIndex, "无Ack不得跳过爆炎步骤");
        AssertEx.False(snapshot.OwnsExecution, "取消后必须立即交还标准Resolver");
    }

    private static void TargetModeAndManualOverrideCancelTheWholeSequence()
    {
        AssertCancellation(
            context => context with { HasValidTarget = false, InRange = false },
            "目标失效必须取消整段");
        AssertCancellation(
            context => context with { IsAoeMode = true, EnemyCount = 3 },
            "切换AOE路线必须取消整段");

        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: true);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        var context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Gcd, context) is not null,
            "手动覆盖测试必须先投递爆炎");

        var manualAck = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.火炎,
            1,
            context.Phase,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(manualAck), "Tracker拒绝手动覆盖Ack");
        service.OnAcceptedAction(manualAck);
        tracker.Reconcile(context with { CapturedAtMs = clock.NowMs });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        var manualSnapshot = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Cancelled, manualSnapshot.Status, "不同GCD覆盖必须取消整段");
        AssertEx.Equal(0, manualSnapshot.StepIndex, "手动覆盖不得推进爆炎步骤");
        AssertEx.False(manualSnapshot.OwnsExecution, "手动覆盖后必须释放起手所有权");
    }

    private static void DotDisabledUsesAlwaysBridgeAfterFireThree()
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: true) with { DotEnabled = false };
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        var context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        var fire3 = service.Resolve(BlmResolverChannel.Gcd, context);
        AssertEx.True(fire3 is not null, "无Dot日常起手必须先投递爆炎");

        var ack = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(ack), "Tracker拒绝无Dot爆炎Ack");
        service.OnAcceptedAction(ack);
        tracker.Reconcile(context with
        {
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            GcdRemainSeconds = 0f,
            CapturedAtMs = clock.NowMs,
        });
        context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);

        var swiftcast = service.Resolve(BlmResolverChannel.Always, context);
        AssertEx.True(swiftcast is not null, "无Dot爆炎后即刻必须通过Always桥交付");
        AssertEx.Equal(MageUniversalSkill.即刻咏唱, swiftcast!.ActionId, "Always桥动作错误");
        AssertEx.Equal(ActionType.Always, swiftcast.Type, "无编织窗口时必须使用Always动作类型");
    }

    private static void PotionUsesAlwaysAndRetriesUntilCooldownConfirmation()
    {
        const uint basePotionId = 49_237;
        const uint highQualityPotionId = basePotionId + 1_000_000;
        var policy = new BlmOpenerPolicy(true, true);
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        uint directlySubmittedPotionId = 0;
        var submitCount = 0;
        var potionCooldown = 0f;
        var service = CreateService(
            tracker,
            clock,
            potionDispatcher: itemId =>
            {
                directlySubmittedPotionId = itemId;
                submitCount++;
                if (submitCount >= 2)
                {
                    potionCooldown = 270f;
                }

                return true;
            },
            potionCooldownProvider: _ => potionCooldown);
        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                policy,
                highQualityPotionId),
            "HQ药水测试必须先武装高难起手");

        var context = tracker.GetContextSnapshot();
        var fire3Ack = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(fire3Ack), "Tracker拒绝高难爆炎Ack");
        service.OnAcceptedAction(fire3Ack);
        tracker.Reconcile(context with
        {
            InCombat = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);

        var plan = BlmOpener57Definition.Build(
            BlmOpenerMode.HighEndCountdown,
            dotEnabled: true,
            highQualityPotionId);
        uint sequence = 2;
        for (var index = 1; index <= IndexOf(plan, "Fire4.Pre.1"); index++)
        {
            var step = plan.Steps[index];
            context = PrepareForDispatch(tracker.GetContextSnapshot(), step);
            tracker.Reconcile(context);
            context = tracker.GetContextSnapshot();
            service.Update(context, false, policy, 0f);
            var action = service.Resolve(step.Channel, context);
            AssertEx.True(action is not null, $"HQ药水前未投递 {step.Id}");
            var ack = tracker.CreateAckEnvelope(
                context.PlayerEntityId,
                action!.ActionId,
                sequence++,
                context.Phase,
                clock.NowMs);
            AssertEx.True(tracker.ApplyActionEffect(ack), $"Tracker拒绝 {step.Id} Ack");
            service.OnAcceptedAction(ack);
            clock.Advance(20);
            tracker.Reconcile(ApplyActionResult(context, step) with { CapturedAtMs = clock.NowMs });
            service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
        }

        var potionStep = plan.Steps[IndexOf(plan, "Potion")];
        context = PrepareForDispatch(tracker.GetContextSnapshot(), potionStep) with
        {
            GcdRemainSeconds = 0f,
        };
        tracker.Reconcile(context);
        context = tracker.GetContextSnapshot();
        service.Update(context, false, policy, 0f);
        var potion = service.Resolve(BlmResolverChannel.Always, context);
        AssertEx.True(potion is null, "药水不得再返回PR旧Item队列");
        AssertEx.Equal(highQualityPotionId, directlySubmittedPotionId, "HQ药水直接提交ID错误");
        AssertEx.Equal(1, submitCount, "首次药水提交次数错误");

        clock.Advance(251);
        tracker.Reconcile(context with { CapturedAtMs = clock.NowMs });
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
        AssertEx.Equal("Potion", service.GetSnapshot().StepId, "首次静默失败后必须保留药水步骤");

        clock.Advance(101);
        tracker.Reconcile(tracker.GetContextSnapshot() with { CapturedAtMs = clock.NowMs });
        context = tracker.GetContextSnapshot();
        service.Update(context, false, policy, 0f);
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Always, context) is null,
            "药水短重试不得返回PAction");
        AssertEx.Equal(2, submitCount, "静默失败后必须进行第二次药水提交");
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);

        var snapshot = service.GetSnapshot();
        AssertEx.Equal("LeyLines", snapshot.StepId, "药水公共冷却启动后必须推进到黑魔纹");
        AssertEx.False(snapshot.HasPendingAction, "药水冷却确认后不得残留起手Pending");
        AssertEx.False(
            tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "药水冷却确认后不得残留Tracker Pending");
    }

    private static void MissingPotionAckSkipsOptionalStepAndCompletesFivePlusSeven()
    {
        const uint highQualityPotionId = 1_049_237;
        var policy = new BlmOpenerPolicy(true, true);
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var submitCount = 0;
        var service = CreateService(
            tracker,
            clock,
            potionDispatcher: _ =>
            {
                submitCount++;
                return true;
            });
        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                policy,
                highQualityPotionId),
            "无药水Ack测试必须先武装带药高难起手");

        var context = tracker.GetContextSnapshot();
        var fire3Ack = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(fire3Ack), "Tracker拒绝无药水Ack测试爆炎Ack");
        service.OnAcceptedAction(fire3Ack);
        tracker.Reconcile(context with
        {
            InCombat = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);

        var plan = BlmOpener57Definition.Build(
            BlmOpenerMode.HighEndCountdown,
            dotEnabled: true,
            highQualityPotionId);
        var potionIndex = IndexOf(plan, "Potion");
        uint sequence = 2;
        for (var index = 1; index < potionIndex; index++)
        {
            sequence = ConfirmPlanStep(
                service,
                tracker,
                clock,
                plan.Steps[index],
                policy,
                sequence);
        }

        var potionStep = plan.Steps[potionIndex];
        context = PrepareForDispatch(tracker.GetContextSnapshot(), potionStep);
        tracker.Reconcile(context);
        context = tracker.GetContextSnapshot();
        service.Update(context, false, policy, 0f);
        AssertEx.True(
            service.Resolve(potionStep.Channel, context) is null,
            "直接提交药水不得返回PAction");
        AssertEx.Equal(1, submitCount, "药水首次直接提交次数错误");

        var retryElapsedMs = 0L;
        while (service.GetSnapshot().StepId == "Potion" && retryElapsedMs < 3_000)
        {
            clock.Advance(251);
            retryElapsedMs += 251;
            tracker.Reconcile(tracker.GetContextSnapshot() with { CapturedAtMs = clock.NowMs });
            service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
            if (service.GetSnapshot().StepId != "Potion")
            {
                break;
            }

            clock.Advance(101);
            retryElapsedMs += 101;
            tracker.Reconcile(tracker.GetContextSnapshot() with { CapturedAtMs = clock.NowMs });
            context = tracker.GetContextSnapshot();
            service.Update(context, false, policy, 0f);
            service.Resolve(BlmResolverChannel.OffGcd, context);
        }

        var skipped = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Executing, skipped.Status, "药水无Ack不得取消整段起手");
        AssertEx.Equal("LeyLines", skipped.StepId, "药水无Ack后必须继续黑魔纹步骤");
        AssertEx.True(skipped.OwnsExecution, "跳过可选药水后必须继续持有起手");
        AssertEx.True(submitCount > 1, "药水静默失败必须发生短重试");
        AssertEx.True(retryElapsedMs < 2_000, "药水失败降级不得再卡住2秒以上");

        for (var index = potionIndex + 1; index < plan.Steps.Length; index++)
        {
            sequence = ConfirmPlanStep(
                service,
                tracker,
                clock,
                plan.Steps[index],
                policy,
                sequence);
        }

        var completed = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Completed, completed.Status, "药水无Ack后必须完成5+7");
        AssertEx.Equal(5, completed.Fire4BeforeManafont, "跳过药水后的魔泉前炽炎计数错误");
        AssertEx.Equal(7, completed.Fire4AfterManafont, "跳过药水后的魔泉后炽炎计数错误");
    }

    private static void InCombatMayArriveBeforeCountdownFireThreeAck()
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                EnabledWithoutPotion,
                potionId: 0),
            "反向时序测试必须武装高难起手");

        tracker.Reconcile(initial with { InCombat = true, CapturedAtMs = clock.NowMs });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        AssertEx.True(service.OwnsExecution, "InCombat先到时必须等待爆炎Ack宽限期");
        AssertEx.Equal(BlmOpenerStatus.Armed, service.GetSnapshot().Status, "宽限期内不得误取消");

        clock.Advance(100);
        var context = tracker.GetContextSnapshot();
        var ack = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(ack), "Tracker拒绝延后到达的爆炎Ack");
        service.OnAcceptedAction(ack);
        tracker.Reconcile(context with
        {
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        var snapshot = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Executing, snapshot.Status, "延后爆炎Ack必须正常进入执行态");
        AssertEx.Equal("Thunder.Open", snapshot.StepId, "延后爆炎Ack后步骤推进错误");
    }

    private static void CancelledCountdownFactoryCannotEmitFireThree()
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var countdownCancelled = false;
        var normalQueueCleared = false;
        var service = CreateService(
            tracker,
            clock,
            countdownCanceller: _ => countdownCancelled = true,
            normalQueueClearer: () => normalQueueCleared = true);
        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                EnabledWithoutPotion,
                potionId: 0),
            "取消工厂测试必须武装高难起手");

        service.Cancel(tracker.GetContextSnapshot(), "测试取消");
        AssertEx.True(countdownCancelled, "取消高难起手必须重置PR倒计时处理器");
        AssertEx.True(normalQueueCleared, "取消起手必须清理普通动作队列");
        AssertEx.True(
            service.TryCreateCountdownPrecastAction(tracker.GetContextSnapshot()) is null,
            "取消后的倒计时工厂不得再生成爆炎");
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        AssertEx.False(service.OwnsExecution, "取消后必须释放起手所有权");
    }

    private static void CancellationDrainsActiveCommandBeforeResolverHandoff()
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: true);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var activeCommand = false;
        var resetCount = 0;
        var service = CreateService(
            tracker,
            clock,
            hasActiveCommandProvider: () => activeCommand,
            actionUpdaterResetter: () => resetCount++);
        var context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Gcd, context) is not null,
            "排空测试必须先投递爆炎");
        activeCommand = true;

        service.Update(tracker.GetContextSnapshot(), true, EnabledWithoutPotion, 0f);
        var draining = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Draining, draining.Status, "高优覆盖时必须进入排空态");
        AssertEx.Equal(0, resetCount, "高优覆盖不得重置玩家ActiveCommand");
        AssertEx.True(draining.OwnsExecution, "排空期间必须继续阻断标准Resolver");
        AssertEx.True(
            service.Resolve(BlmResolverChannel.Gcd, tracker.GetContextSnapshot()) is null,
            "排空期间不得投递新的起手动作");

        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        AssertEx.Equal(
            BlmOpenerStatus.Draining,
            service.GetSnapshot().Status,
            "ActiveCommand未清空时不得提前交权");

        activeCommand = false;
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        var cancelled = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Cancelled, cancelled.Status, "ActiveCommand排空后必须完成取消");
        AssertEx.False(cancelled.OwnsExecution, "排空完成后必须交还标准Resolver");
    }

    private static void InterruptedFireFourRetriesSameStepAndCompletesFivePlusSeven()
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: false);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var resetCount = 0;
        var service = CreateService(
            tracker,
            clock,
            actionUpdaterResetter: () => resetCount++);
        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                EnabledWithoutPotion,
                potionId: 0),
            "拉断测试必须先武装高难起手");

        var context = tracker.GetContextSnapshot();
        var fire3Ack = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(fire3Ack), "Tracker拒绝拉断测试爆炎Ack");
        service.OnAcceptedAction(fire3Ack);
        tracker.Reconcile(context with
        {
            InCombat = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        var plan = BlmOpener57Definition.Build(
            BlmOpenerMode.HighEndCountdown,
            dotEnabled: true,
            potionId: 0);
        var interruptedIndex = IndexOf(plan, "Fire4.Pre.3");
        uint sequence = 2;
        for (var index = 1; index < interruptedIndex; index++)
        {
            var step = plan.Steps[index];
            context = PrepareForDispatch(tracker.GetContextSnapshot(), step);
            tracker.Reconcile(context);
            context = tracker.GetContextSnapshot();
            service.Update(context, false, EnabledWithoutPotion, 0f);
            var action = service.Resolve(step.Channel, context);
            AssertEx.True(action is not null, $"拉断前未投递 {step.Id}");
            var ack = tracker.CreateAckEnvelope(
                context.PlayerEntityId,
                action!.ActionId,
                sequence++,
                context.Phase,
                clock.NowMs);
            AssertEx.True(tracker.ApplyActionEffect(ack), $"Tracker拒绝 {step.Id} Ack");
            service.OnAcceptedAction(ack);
            clock.Advance(20);
            tracker.Reconcile(ApplyActionResult(context, step) with { CapturedAtMs = clock.NowMs });
            service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        }

        context = PrepareForDispatch(tracker.GetContextSnapshot(), plan.Steps[interruptedIndex]) with
        {
            Mp = 4_800,
            AstralSoul = 2,
            HasSwiftcast = false,
            SwiftcastRemainSeconds = 0f,
            TriplecastStacks = 0,
        };
        tracker.Reconcile(context);
        context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        var interruptedFire4 = service.Resolve(BlmResolverChannel.Gcd, context);
        AssertEx.True(interruptedFire4 is not null, "必须先投递待拉断的第三发炽炎");
        AssertEx.Equal(BLMSkill.炽炎, interruptedFire4!.ActionId, "待拉断动作必须是炽炎");

        clock.Advance(100);
        tracker.Reconcile(context with
        {
            IsCasting = true,
            CurrentCastingActionId = BLMSkill.炽炎,
            CastRemainSeconds = 2.5f,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        clock.Advance(100);
        tracker.Reconcile(tracker.GetContextSnapshot() with
        {
            IsMoving = true,
            IsCasting = false,
            CurrentCastingActionId = 0,
            CastRemainSeconds = 0f,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        var retryArmed = service.GetSnapshot();
        AssertEx.Equal(1, resetCount, "硬读拉断必须且只能调用一次PR ActionUpdater.Reset");
        AssertEx.Equal(BlmOpenerStatus.Executing, retryArmed.Status, "硬读拉断后必须保留起手执行权");
        AssertEx.Equal(interruptedIndex, retryArmed.StepIndex, "硬读拉断不得推进起手步骤");
        AssertEx.True(retryArmed.OwnsExecution, "清除旧ActiveCommand后必须继续持有冻结起手");
        AssertEx.False(retryArmed.HasPendingAction, "拉断后的旧Pending必须清空后再重试");

        var lateAck = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            BLMSkill.炽炎,
            sequence,
            BlmPhase.Fire,
            clock.NowMs + 1);
        service.OnAcceptedAction(lateAck);
        AssertEx.Equal(
            interruptedIndex,
            service.GetSnapshot().StepIndex,
            "重新投递前到达的旧炽炎Ack不得推进冻结起手");

        clock.Advance(6_000);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        AssertEx.Equal(
            BlmOpenerStatus.Executing,
            service.GetSnapshot().Status,
            "拉断后持续移动不得触发步骤准备超时");

        var interruptedStep = plan.Steps[interruptedIndex];
        context = PrepareForDispatch(tracker.GetContextSnapshot(), interruptedStep) with
        {
            IsMoving = false,
        };
        tracker.Reconcile(context);
        context = tracker.GetContextSnapshot();
        service.Update(context, false, EnabledWithoutPotion, 0f);
        var retriedFire4 = service.Resolve(interruptedStep.Channel, context);
        AssertEx.True(retriedFire4 is not null, "停止移动后必须重新投递同一发炽炎");
        AssertEx.Equal(BLMSkill.炽炎, retriedFire4!.ActionId, "拉断后重试动作错误");
        var retriedAck = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            retriedFire4.ActionId,
            sequence++,
            context.Phase,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(retriedAck), "Tracker拒绝重试炽炎Ack");
        service.OnAcceptedAction(retriedAck);
        clock.Advance(20);
        tracker.Reconcile(ApplyActionResult(context, interruptedStep) with { CapturedAtMs = clock.NowMs });
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);

        for (var index = interruptedIndex + 1; index < plan.Steps.Length; index++)
        {
            var step = plan.Steps[index];
            context = PrepareForDispatch(tracker.GetContextSnapshot(), step);
            tracker.Reconcile(context);
            context = tracker.GetContextSnapshot();
            service.Update(context, false, EnabledWithoutPotion, 0f);
            var action = service.Resolve(step.Channel, context);
            AssertEx.True(action is not null, $"拉断恢复后未投递 {step.Id}");
            var ack = tracker.CreateAckEnvelope(
                context.PlayerEntityId,
                action!.ActionId,
                sequence++,
                context.Phase,
                clock.NowMs);
            AssertEx.True(tracker.ApplyActionEffect(ack), $"Tracker拒绝恢复后的 {step.Id} Ack");
            service.OnAcceptedAction(ack);
            clock.Advance(20);
            tracker.Reconcile(ApplyActionResult(context, step) with { CapturedAtMs = clock.NowMs });
            service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        }

        var completed = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Completed, completed.Status, "炽炎拉断恢复后必须完成整段起手");
        AssertEx.Equal(5, completed.Fire4BeforeManafont, "拉断恢复后的魔泉前炽炎计数错误");
        AssertEx.Equal(7, completed.Fire4AfterManafont, "拉断恢复后的魔泉后炽炎计数错误");
        AssertEx.False(completed.OwnsExecution, "完整5+7结束后必须交还标准Resolver");
    }

    private static void AssertCancellation(
        Func<BlmContext, BlmContext> mutate,
        string message)
    {
        var clock = new FakeClock();
        var initial = ReadyOpenerContext(inCombat: true);
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = CreateService(tracker, clock);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        AssertEx.True(service.OwnsExecution, "取消合同测试必须先启动日常起手");

        var changed = mutate(tracker.GetContextSnapshot()) with { CapturedAtMs = clock.NowMs };
        tracker.Reconcile(changed);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        service.Update(tracker.GetContextSnapshot(), false, EnabledWithoutPotion, 0f);
        var snapshot = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Cancelled, snapshot.Status, message);
        AssertEx.False(snapshot.OwnsExecution, $"{message}后必须释放起手所有权");
    }

    internal static BlmOpenerExecutionService CreateService(
        BlmStateTracker tracker,
        FakeClock clock,
        Action<PAction>? bridge = null,
        Action<string>? countdownCanceller = null,
        Action? normalQueueClearer = null,
        Func<bool>? hasActiveCommandProvider = null,
        Action? actionUpdaterResetter = null,
        Func<uint, bool>? potionDispatcher = null,
        Func<uint, float>? potionCooldownProvider = null)
        => new(
            tracker,
            IdentityBlmActionIdNormalizer.Instance,
            clock,
            policyProvider: () => EnabledWithoutPotion,
            countdownProvider: () => 0f,
            potionProvider: () => 0,
            adjustActionId: actionId => actionId,
            preCombatDispatcher: bridge ?? (_ => { }),
            countdownCanceller: countdownCanceller ?? (_ => { }),
            normalQueueClearer: normalQueueClearer ?? (() => { }),
            hasActiveCommandProvider: hasActiveCommandProvider ?? (() => false),
            actionUpdaterResetter: actionUpdaterResetter ?? (() => { }),
            potionDispatcher: potionDispatcher ?? (_ => true),
            potionCooldownProvider: potionCooldownProvider ?? (_ => 0f));

    private static uint ConfirmPlanStep(
        BlmOpenerExecutionService service,
        BlmStateTracker tracker,
        FakeClock clock,
        BlmOpenerStep step,
        BlmOpenerPolicy policy,
        uint sequence)
    {
        var context = PrepareForDispatch(tracker.GetContextSnapshot(), step);
        tracker.Reconcile(context);
        context = tracker.GetContextSnapshot();
        service.Update(context, false, policy, 0f);
        var action = service.Resolve(step.Channel, context);
        AssertEx.True(action is not null, $"药水降级后未投递 {step.Id}");
        var ack = tracker.CreateAckEnvelope(
            context.PlayerEntityId,
            action!.ActionId,
            sequence,
            context.Phase,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(ack), $"Tracker拒绝药水降级后的 {step.Id} Ack");
        service.OnAcceptedAction(ack);
        clock.Advance(20);
        tracker.Reconcile(ApplyActionResult(context, step) with { CapturedAtMs = clock.NowMs });
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
        return sequence + 1;
    }

    internal static BlmContext ReadyOpenerContext(bool inCombat)
        => TestContext.Base() with
        {
            InCombat = inCombat,
            DutyComposition = new BlmDutyComposition(8, 1),
            Phase = BlmPhase.Neutral,
            AfStacks = 0,
            IceStacks = 0,
            Mp = 10_000,
            MaxMp = 10_000,
            IsMoving = false,
            IsCasting = false,
            CanAct = true,
            IsAoeMode = false,
            InRange = true,
            DotEnabled = true,
            HasThunderhead = false,
            PolyglotStacks = 0,
            AstralSoul = 0,
            Swiftcast = TestContext.ReadyAction(MageUniversalSkill.即刻咏唱),
            Triplecast = TestContext.ReadyAction(BLMSkill.三连咏唱),
            LeyLines = TestContext.ReadyAction(BLMSkill.黑魔纹),
            Amplifier = TestContext.ReadyAction(BLMSkill.详述),
            Manafont = TestContext.ReadyAction(BLMSkill.魔泉),
            Transpose = TestContext.ReadyAction(BLMSkill.星灵移位),
        };

    internal static BlmContext PrepareForDispatch(BlmContext context, BlmOpenerStep step)
        => context with
        {
            IsCasting = false,
            CurrentCastingActionId = 0,
            AnimationLockSeconds = 0f,
            GcdRemainSeconds = step.Kind == BlmOpenerStepKind.Gcd ? 0f : 1.5f,
        };

    internal static BlmContext ApplyActionResult(BlmContext context, BlmOpenerStep step)
    {
        var next = context;
        switch (step.ActionId)
        {
            case BLMSkill.爆炎:
                next = next with
                {
                    Phase = BlmPhase.Fire,
                    AfStacks = 3,
                    HasThunderhead = true,
                };
                break;
            case BLMSkill.闪雷:
            case BLMSkill.高闪雷:
                next = next with { HasThunderhead = false };
                break;
            case MageUniversalSkill.即刻咏唱:
                next = next with { HasSwiftcast = true, SwiftcastRemainSeconds = 10f };
                break;
            case BLMSkill.详述:
                next = next with { PolyglotStacks = Math.Max(1, next.PolyglotStacks) };
                break;
            case BLMSkill.炽炎:
                next = next with
                {
                    AstralSoul = next.AstralSoul + 1,
                    HasSwiftcast = false,
                    SwiftcastRemainSeconds = 0f,
                    TriplecastStacks = Math.Max(0, next.TriplecastStacks - 1),
                };
                break;
            case BLMSkill.黑魔纹:
                next = next with { HasLeyLines = true, HasLeyLinesStatus737 = true };
                break;
            case BLMSkill.异言:
                next = next with { PolyglotStacks = Math.Max(0, next.PolyglotStacks - 1) };
                break;
            case BLMSkill.魔泉:
                next = next with
                {
                    Mp = 10_000,
                    UmbralHearts = 3,
                    HasParadox = true,
                    HasThunderhead = true,
                };
                break;
            case BLMSkill.耀星:
                next = next with { AstralSoul = 0 };
                break;
            case BLMSkill.悖论:
                next = next with { HasParadox = false };
                break;
            case BLMSkill.核爆:
                next = next with { AstralSoul = Math.Min(6, next.AstralSoul + 3) };
                break;
            case BLMSkill.三连咏唱:
                next = next with { TriplecastStacks = 3, TriplecastRemainSeconds = 15f };
                break;
            case BLMSkill.绝望:
                next = next with { Mp = 0 };
                break;
            case BLMSkill.星灵移位:
            case BLMSkill.冰封:
                next = next with
                {
                    Phase = BlmPhase.Ice,
                    AfStacks = 0,
                    IceStacks = step.ActionId == BLMSkill.冰封 ? 3 : 1,
                };
                break;
        }

        return next;
    }

    private static int CountPrefix(BlmOpenerPlan plan, string prefix)
        => plan.Steps.Count(step => step.Id.StartsWith(prefix, StringComparison.Ordinal));

    private static int CountAction(BlmOpenerPlan plan, uint actionId)
        => plan.Steps.Count(step => step.ActionId == actionId);

    private static int IndexOf(BlmOpenerPlan plan, string id)
    {
        for (var index = 0; index < plan.Steps.Length; index++)
        {
            if (string.Equals(plan.Steps[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
