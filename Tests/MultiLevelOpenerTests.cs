using System.Linq;
using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Data;
using LosPr.BLM.Openers;
using LosPr.BLM.Resolvers;

namespace Los.Tests;

internal static class MultiLevelOpenerTests
{
    private static readonly BlmOpenerPolicy LowerLevelPolicy = new(
        Enabled: false,
        HighEndPotionEnabled: false,
        Level70To89Enabled: true);

    private static readonly BlmOpenerPolicy FlarePolicy = new(
        Enabled: false,
        HighEndPotionEnabled: false,
        Level100FlareEnabled: true);

    private static readonly BlmOpenerPolicy Level90Policy = new(
        Enabled: false,
        HighEndPotionEnabled: false,
        Level90To99Enabled: true);

    private static readonly BlmOpenerPolicy Level80NoTriplePolicy = new(
        Enabled: false,
        HighEndPotionEnabled: false,
        Level70To89Enabled: true,
        NoTriplecast: true);

    private static readonly BlmOpenerPolicy Level90NoTriplePolicy = new(
        Enabled: false,
        HighEndPotionEnabled: false,
        Level90To99Enabled: true,
        NoTriplecast: true);

    private static readonly BlmOpenerPolicy FlareNoTriplePolicy = new(
        Enabled: false,
        HighEndPotionEnabled: false,
        Level100FlareEnabled: true,
        NoTriplecast: true);

    private static readonly BlmOpenerPolicy StandardNoTriplePolicy = new(
        Enabled: true,
        HighEndPotionEnabled: false,
        NoTriplecast: true);

    public static void RunAll()
    {
        NewOpenersAreRegisteredAndDisabledByDefault();
        ConsoleSettingsAndPresetsHaveSafeDefaults();
        LevelBoundariesAreDisjoint();
        FrozenPlansMatchAeSequences();
        NoTriplecastPlansMatchAeBranches();
        LowerLevelAndFlarePlansNeverStartAsDailyOpeners();
        EveryMigratedPlanCompletesThroughTheSharedExecutionService();
    }

    private static void ConsoleSettingsAndPresetsHaveSafeDefaults()
    {
        var settings = new BlackMageSettings
        {
            UiLayoutVersion = 0,
            WindowWidth = 960f,
            WindowHeight = 660f,
            DangerLoopEnabled = true,
            DotHpThresholdPercent = 150,
            MoveTriplecastSeconds = float.NaN,
            StationaryLeyLinesSeconds = 99f,
        };
        settings.Normalize();

        AssertEx.Equal(4, settings.UiLayoutVersion, "旧控制台布局必须迁移到正式版热键布局");
        AssertEx.Equal(BlackMageSettings.DefaultWindowWidth, settings.WindowWidth, "默认窗口宽度迁移错误");
        AssertEx.Equal(BlackMageSettings.DefaultWindowHeight, settings.WindowHeight, "默认窗口高度迁移错误");
        AssertEx.Equal(BlmUiThemeStyle.AmethystCat, settings.UiThemeStyle, "默认主题必须是紫晶黑猫");
        AssertEx.Equal(100, settings.DotHpThresholdPercent, "DOT 阈值必须限制在 0–100% 范围");
        AssertEx.Equal(1.5f, settings.MoveTriplecastSeconds, "非法三连走位秒数必须恢复默认");
        AssertEx.Equal(30f, settings.StationaryLeyLinesSeconds, "原地黑魔纹秒数必须限制在 30 秒内");
        AssertEx.False(settings.DangerLoopEnabled, "未确认三项风险时不得保留开挂循环状态");
        AssertEx.False(BlackMageRotation.DailyPreset["TTK"], "日常预设必须与 Los-ae 一样关闭 TTK");
        AssertEx.False(BlackMageRotation.HighEndPreset["TTK"], "高难预设必须关闭TTK");
        AssertEx.True(BlackMageRotation.DailyPreset["黑魔纹"], "日常预设必须开启黑魔纹");
        AssertEx.False(BlackMageRotation.HighEndPreset["黑魔纹"], "高难预设必须关闭黑魔纹");
        AssertEx.True(BlackMageRotation.DailyPreset["移动三连"], "日常预设必须开启移动三连");
        AssertEx.False(BlackMageRotation.HighEndPreset["移动三连"], "高难预设必须关闭移动三连");
        AssertEx.True(BlackMageRotation.DailyPreset["三连进冰"], "日常预设必须开启三连进冰");
        AssertEx.False(BlackMageRotation.HighEndPreset["三连进冰"], "高难预设必须关闭三连进冰");
        AssertEx.Equal(BlackMageRotation.QtList.Count, BlackMageRotation.DailyPreset.Count, "日常预设QT覆盖不完整");
        AssertEx.Equal(BlackMageRotation.QtList.Count, BlackMageRotation.HighEndPreset.Count, "高难预设QT覆盖不完整");

        var legacyDebugTab = new BlackMageSettings
        {
            UiLayoutVersion = 3,
            ActiveTab = 3,
            QtPanelScale = 4f,
            HotkeyPanelScale = float.NaN,
        };
        legacyDebugTab.Normalize();
        AssertEx.Equal(4, legacyDebugTab.ActiveTab, "旧 Debug 页签必须迁移到新索引");
        AssertEx.Equal(1.5f, legacyDebugTab.QtPanelScale, "QT 独立缩放必须限制上限");
        AssertEx.Equal(1f, legacyDebugTab.HotkeyPanelScale, "非法 Hotkey 缩放必须恢复默认");
    }

    private static void NewOpenersAreRegisteredAndDisabledByDefault()
    {
        var settings = new BlackMageSettings();
        AssertEx.Equal(BlmOpenerSelection.None, settings.OpenerSelection, "等级起手必须默认关闭");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("70–89级高难起手"), "70–89起手不得占用QT");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("90–99级高难起手"), "90–99起手不得占用QT");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("100级核爆起手"), "核爆起手不得占用QT");

        AssertEx.Equal("Lv.70 起手", BlmLevel70Opener.Name, "70级起手注册名必须与控制台一致");
        AssertEx.Equal("Lv.80 起手", BlmLevel80Opener.Name, "80级起手注册名必须与控制台一致");
        AssertEx.Equal("Lv.90 起手", BlmLevel90Opener.Name, "90级起手注册名必须与控制台一致");
        AssertEx.Equal("Lv.100 标准 5+7", BlmLevel100Opener.Name, "100级标准起手注册名必须与控制台一致");
        AssertEx.Equal("Lv.100 核爆起手", BlmLevel100FlareOpener.Name, "100级核爆起手注册名必须与控制台一致");

        AssertRegistered<BlmLevel70Opener>(BlmLevel70Opener.Name);
        AssertRegistered<BlmLevel80Opener>(BlmLevel80Opener.Name);
        AssertRegistered<BlmLevel90Opener>(BlmLevel90Opener.Name);
        AssertRegistered<BlmLevel100FlareOpener>(BlmLevel100FlareOpener.Name);
        AssertEx.Equal(0, new BlmLevel70Opener().InCombatSequence.Count, "70级PR动作组必须为空");
        AssertEx.Equal(0, new BlmLevel80Opener().InCombatSequence.Count, "80级PR动作组必须为空");
        AssertEx.Equal(0, new BlmLevel90Opener().InCombatSequence.Count, "90级PR动作组必须为空");
        AssertEx.Equal(
            0,
            new BlmLevel100FlareOpener().InCombatSequence.Count,
            "核爆PR动作组必须为空");
    }

    private static void LevelBoundariesAreDisjoint()
    {
        AssertEx.False(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level70, 69),
            "69级不得进入70级起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level70, 70),
            "70级必须进入70级起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level70, 79),
            "79级必须保留70级起手");
        AssertEx.False(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level70, 80),
            "80级不得继续使用70级起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level80, 80),
            "80级必须进入80级起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level80, 89),
            "89级必须保留80级起手");
        AssertEx.False(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level80, 90),
            "90级不得继续使用80级起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level90, 90),
            "90级必须进入90级起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level90, 99),
            "99级必须保留90级起手");
        AssertEx.False(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Level90, 100),
            "100级不得继续使用90级起手");
        AssertEx.False(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Standard57, 99),
            "99级不得借用100级标准起手");
        AssertEx.True(
            BlmAdditionalOpenerDefinitions.SupportsLevel(BlmOpenerVariant.Flare, 100),
            "100级必须允许核爆起手");
    }

    private static void FrozenPlansMatchAeSequences()
    {
        const uint potionId = 1_049_237;
        var level70 = Build(BlmOpenerVariant.Level70, 70, potionId: 0);
        var level70WithPotion = Build(BlmOpenerVariant.Level70, 79, potionId);
        AssertPlanCounts(level70, 4, 7);
        AssertEx.Equal(1, CountAction(level70, BLMSkill.三连咏唱), "70级无药必须插入一次三连");
        AssertEx.Equal(0, CountAction(level70WithPotion, BLMSkill.三连咏唱), "70级有药必须沿用AE不三连分支");
        AssertEx.Equal(1, CountAction(level70WithPotion, potionId), "70级有药计划必须包含药水");
        AssertBefore(level70, "Fire4.Pre.4", "Manafont");
        AssertBefore(level70, "Fire4.Post.7", "Thunder.Refresh");

        var level80 = Build(BlmOpenerVariant.Level80, 80, potionId: 0);
        AssertPlanCounts(level80, 4, 7);
        AssertEx.Equal(2, CountAction(level80, BLMSkill.三连咏唱), "80级必须包含前后两次三连");
        AssertEx.Equal(2, CountAction(level80, BLMSkill.绝望), "80级必须包含魔泉前后两次绝望");
        AssertBefore(level80, "Despair.Pre", "Manafont");
        AssertBefore(level80, "Despair.Post", "Transpose");

        var level90 = Build(BlmOpenerVariant.Level90, 90, potionId: 0);
        AssertPlanCounts(level90, 4, 6);
        AssertEx.Equal(2, CountAction(level90, BLMSkill.三连咏唱), "90级必须包含前后两次三连");
        AssertEx.Equal(2, CountAction(level90, BLMSkill.绝望), "90级必须包含魔泉前后两次绝望");
        AssertEx.Equal(1, CountAction(level90, BLMSkill.异言), "90级必须在魔泉前使用一次异言");
        AssertEx.Equal(1, CountAction(level90, BLMSkill.悖论), "90级必须在后段使用一次悖论");
        AssertBefore(level90, "Despair.Pre", "Xenoglossy");
        AssertBefore(level90, "Xenoglossy", "Manafont");
        AssertBefore(level90, "Paradox", "Triplecast.Post");
        AssertBefore(level90, "Triplecast.Post", "Despair.Post");

        var flare = Build(BlmOpenerVariant.Flare, 100, potionId: 0);
        AssertPlanCounts(flare, 4, 6);
        AssertEx.Equal(1, CountAction(flare, BLMSkill.核爆), "核爆起手必须且只能包含一发核爆");
        AssertEx.Equal(2, CountAction(flare, BLMSkill.耀星), "核爆起手必须包含两发耀星");
        AssertEx.Equal(1, CountAction(flare, BLMSkill.悖论), "核爆起手必须包含火悖论");
        AssertBefore(flare, "Paradox", "Triplecast");
        AssertBefore(flare, "Triplecast", "Flare");
        AssertBefore(flare, "Flare", "FlareStar.2");
        AssertBefore(flare, "FlareStar.2", "Transpose");
    }

    private static void NoTriplecastPlansMatchAeBranches()
    {
        var level80 = Build(BlmOpenerVariant.Level80, 80, potionId: 0, noTriplecast: true);
        AssertEx.Equal(1, CountAction(level80, BLMSkill.三连咏唱), "80级不三连仍必须保留前段三连");
        AssertEx.Equal(0, CountAction(level80, BLMSkill.星灵移位), "80级不三连必须删除星灵收尾");

        var level90 = Build(BlmOpenerVariant.Level90, 90, potionId: 0, noTriplecast: true);
        AssertEx.Equal(1, CountAction(level90, BLMSkill.三连咏唱), "90级不三连仍必须保留前段三连");
        AssertEx.Equal(0, CountAction(level90, BLMSkill.星灵移位), "90级不三连必须删除星灵收尾");

        var standard = BlmOpener57Definition.Build(
            BlmOpenerMode.HighEndCountdown,
            dotEnabled: true,
            potionId: 0,
            noTriplecast: true);
        AssertEx.Equal(0, CountAction(standard, BLMSkill.三连咏唱), "100标准不三连必须删除尾段三连");
        AssertEx.Equal(1, CountAction(standard, BLMSkill.星灵移位), "100标准必须保留原项目星灵收尾");

        var flare = Build(BlmOpenerVariant.Flare, 100, potionId: 0, noTriplecast: true);
        AssertEx.Equal(0, CountAction(flare, BLMSkill.三连咏唱), "核爆不三连不得使用三连");
        AssertEx.Equal(0, CountAction(flare, BLMSkill.星灵移位), "核爆不三连不得使用星灵");
        AssertEx.Equal(1, CountAction(flare, BLMSkill.冰封), "核爆不三连必须改用冰封收尾");
    }

    private static void LowerLevelAndFlarePlansNeverStartAsDailyOpeners()
    {
        AssertNoDailyStart(70, LowerLevelPolicy, "70级无倒计时必须交给普通Resolver");
        AssertNoDailyStart(89, LowerLevelPolicy, "89级无倒计时必须交给普通Resolver");
        AssertNoDailyStart(90, Level90Policy, "90级无倒计时必须交给普通Resolver");
        AssertNoDailyStart(99, Level90Policy, "99级无倒计时必须交给普通Resolver");
        AssertNoDailyStart(100, FlarePolicy, "100级核爆不得在日常进战后自动启动");
    }

    private static void EveryMigratedPlanCompletesThroughTheSharedExecutionService()
    {
        AssertCompletes(BlmOpenerVariant.Level70, 70, LowerLevelPolicy);
        AssertCompletes(BlmOpenerVariant.Level70, 79, LowerLevelPolicy);
        AssertCompletes(BlmOpenerVariant.Level80, 80, LowerLevelPolicy);
        AssertCompletes(BlmOpenerVariant.Level80, 89, LowerLevelPolicy);
        AssertCompletes(BlmOpenerVariant.Level90, 90, Level90Policy);
        AssertCompletes(BlmOpenerVariant.Level90, 99, Level90Policy);
        AssertCompletes(BlmOpenerVariant.Flare, 100, FlarePolicy);
        AssertCompletes(BlmOpenerVariant.Level80, 80, Level80NoTriplePolicy);
        AssertCompletes(BlmOpenerVariant.Level90, 90, Level90NoTriplePolicy);
        AssertCompletes(BlmOpenerVariant.Standard57, 100, StandardNoTriplePolicy);
        AssertCompletes(BlmOpenerVariant.Flare, 100, FlareNoTriplePolicy);
    }

    private static void AssertNoDailyStart(
        int level,
        BlmOpenerPolicy policy,
        string message)
    {
        var clock = new FakeClock();
        var context = Level100OpenerTests.ReadyOpenerContext(inCombat: true) with { Level = level };
        var tracker = new BlmStateTracker(context, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = Level100OpenerTests.CreateService(tracker, clock);
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
        AssertEx.False(service.OwnsExecution, message);
    }

    private static void AssertCompletes(
        BlmOpenerVariant variant,
        int level,
        BlmOpenerPolicy policy)
    {
        var clock = new FakeClock();
        var initial = Level100OpenerTests.ReadyOpenerContext(inCombat: false) with
        {
            Level = level,
        };
        var tracker = new BlmStateTracker(initial, clock, IdentityBlmActionIdNormalizer.Instance);
        var service = Level100OpenerTests.CreateService(tracker, clock);
        AssertEx.True(
            service.TryArmCountdown(
                tracker.GetContextSnapshot(),
                variant,
                policy,
                potionId: 0),
            $"{level}级{variant}未能武装");
        AssertEx.Equal(variant, service.GetSnapshot().Variant, $"{level}级武装了错误计划");

        var fireThreeAck = tracker.CreateAckEnvelope(
            initial.PlayerEntityId,
            BLMSkill.爆炎,
            1,
            BlmPhase.Neutral,
            clock.NowMs);
        AssertEx.True(tracker.ApplyActionEffect(fireThreeAck), $"{level}级Tracker拒绝爆炎Ack");
        service.OnAcceptedAction(fireThreeAck);
        tracker.Reconcile(initial with
        {
            InCombat = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            HasThunderhead = true,
            CapturedAtMs = clock.NowMs,
        });
        service.Update(tracker.GetContextSnapshot(), false, policy, 0f);

        var plan = Build(
            variant,
            level,
            potionId: 0,
            noTriplecast: policy.NoTriplecast);
        uint sequence = 2;
        foreach (var step in plan.Steps.Skip(1))
        {
            var context = Level100OpenerTests.PrepareForDispatch(
                tracker.GetContextSnapshot(),
                step);
            tracker.Reconcile(context);
            context = tracker.GetContextSnapshot();
            service.Update(context, false, policy, 0f);
            var action = service.Resolve(step.Channel, context);
            AssertEx.True(action is not null, $"{level}级未投递{step.Id}");
            AssertEx.Equal(step.ActionId, action!.ActionId, $"{level}级{step.Id}动作错误");

            var ack = tracker.CreateAckEnvelope(
                context.PlayerEntityId,
                action.ActionId,
                sequence++,
                context.Phase,
                clock.NowMs);
            AssertEx.True(tracker.ApplyActionEffect(ack), $"{level}级Tracker拒绝{step.Id} Ack");
            service.OnAcceptedAction(ack);
            clock.Advance(20);
            tracker.Reconcile(
                Level100OpenerTests.ApplyActionResult(context, step) with
                {
                    CapturedAtMs = clock.NowMs,
                });
            service.Update(tracker.GetContextSnapshot(), false, policy, 0f);
        }

        var completed = service.GetSnapshot();
        AssertEx.Equal(BlmOpenerStatus.Completed, completed.Status, $"{level}级起手未完成");
        AssertEx.False(completed.OwnsExecution, $"{level}级完成后未交还Resolver");
        AssertEx.Equal(
            plan.ExpectedFire4BeforeManafont,
            completed.Fire4BeforeManafont,
            $"{level}级魔泉前炽炎计数错误");
        AssertEx.Equal(
            plan.ExpectedFire4AfterManafont,
            completed.Fire4AfterManafont,
            $"{level}级魔泉后炽炎计数错误");
    }

    private static BlmOpenerPlan Build(
        BlmOpenerVariant variant,
        int level,
        uint potionId,
        bool noTriplecast = false)
        => BlmAdditionalOpenerDefinitions.BuildCountdown(
            variant,
            level,
            dotEnabled: true,
            potionId,
            noTriplecast);

    private static void AssertRegistered<TOpener>(string name)
    {
        AssertEx.True(
            BlackMageRotation.Openers.TryGetValue(name, out var openerType),
            $"PR起手注册表缺少{name}");
        AssertEx.Equal(typeof(TOpener), openerType!, $"{name}注册类型错误");
    }

    private static void AssertPlanCounts(
        BlmOpenerPlan plan,
        int expectedPre,
        int expectedPost)
    {
        AssertEx.Equal(expectedPre, CountPrefix(plan, "Fire4.Pre."), $"{plan.DisplayName}前段计数错误");
        AssertEx.Equal(expectedPost, CountPrefix(plan, "Fire4.Post."), $"{plan.DisplayName}后段计数错误");
        AssertEx.Equal(expectedPre, plan.ExpectedFire4BeforeManafont, $"{plan.DisplayName}前段不变量错误");
        AssertEx.Equal(expectedPost, plan.ExpectedFire4AfterManafont, $"{plan.DisplayName}后段不变量错误");
    }

    private static void AssertBefore(BlmOpenerPlan plan, string first, string second)
        => AssertEx.True(
            IndexOf(plan, first) >= 0 && IndexOf(plan, first) < IndexOf(plan, second),
            $"{plan.DisplayName}顺序错误：{first}必须早于{second}");

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
