using System.Reflection;
using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.Resolvers.Production;
using LosPr.BLM.UI;

namespace Los.Tests;

internal static class Phase3BStructureTests
{
    private static readonly string[] RemovedTypeNames =
    [
        "LosPr.BLM.Engine.BlmActionDispatcher",
        "LosPr.BLM.Engine.BlmCoordinator",
        "LosPr.BLM.Engine.BlmFollowUpCoordinator",
        "LosPr.BLM.Engine.BlmDecisionEngine",
        "LosPr.BLM.Engine.BlmOverrideChain",
        "LosPr.BLM.Core.BlmIntent",
        "LosPr.BLM.Core.BlmFollowUpIntent",
        "LosPr.BLM.Core.BlmDecision",
        "LosPr.BLM.Core.BlmFireBudget",
        "LosPr.BLM.Core.BlmCastSafety",
    ];

    public static void RunAll()
    {
        RemovedGraphTypesStayDeleted();
        ProductionEntryPointsUseOnlyResolverExecution();
        TrackerAndDebugExposeResolverEraContracts();
        ProductionSourceKeepsComplexityGates();
        QtManifestMatchesProductionResolvers();
        ConsoleUiKeepsExpandableCardAndEmbeddedFamiliarContracts();
        QuickOverlayKeepsHostAndVisualContracts();
        QuickHotkeyPanelKeepsNativeIconAndGridContracts();
        FormalUiKeepsVisibilityBindingAndCompactDebugContracts();
        OverviewTargetDiagnosticsReflectRuntimeFacts();
    }

    private static void RemovedGraphTypesStayDeleted()
    {
        var assembly = typeof(BlackMageRotation).Assembly;
        foreach (var typeName in RemovedTypeNames)
        {
            AssertEx.True(assembly.GetType(typeName) is null, $"旧事务类型不得回归：{typeName}");
        }

        var root = FindProjectRoot();
        string[] removedFiles =
        [
            "BLM/Engine/BlmActionDispatcher.cs",
            "BLM/Engine/BlmCoordinator.cs",
            "BLM/Engine/BlmFollowUpCoordinator.cs",
            "BLM/Engine/BlmDecisionEngine.cs",
            "BLM/Engine/BlmOverrideChain.cs",
            "BLM/Strategies/StandardOffGcdStrategy.cs",
            "BLM/Strategies/StandardSingleTargetStrategy.cs",
        ];
        foreach (var relativePath in removedFiles)
        {
            AssertEx.False(File.Exists(Path.Combine(root, relativePath)), $"旧事务源码不得回归：{relativePath}");
        }
    }

    private static void ProductionEntryPointsUseOnlyResolverExecution()
    {
        var fields = typeof(BlackMageRotation).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic);
        AssertEx.True(
            fields.Any(field => field.FieldType == typeof(BlmResolverExecutionService)),
            "生产 Rotation 必须持有唯一 Resolver 执行器");
        AssertEx.False(
            fields.Any(field => field.FieldType.FullName?.Contains("Dispatcher", StringComparison.Ordinal) == true),
            "生产 Rotation 不得重新持有 Dispatcher");
    }

    private static void TrackerAndDebugExposeResolverEraContracts()
    {
        var trackerProperties = typeof(BlmTrackerSnapshot)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        AssertEx.True(trackerProperties.Contains(nameof(BlmTrackerSnapshot.HasPendingIssuedAction)), "Tracker 必须暴露通用 Pending");
        AssertEx.True(trackerProperties.Contains(nameof(BlmTrackerSnapshot.PendingGaugeReconcile)), "Tracker 必须保留 Gauge 对账");
        AssertEx.False(trackerProperties.Contains("Transition"), "Tracker 不得恢复 Transition");
        AssertEx.False(trackerProperties.Contains("FollowUp"), "Tracker 不得恢复 Follow-up");

        var debugProperties = typeof(BlmDebugEvent)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        AssertEx.True(debugProperties.Contains(nameof(BlmDebugEvent.Resolver)), "Debug Schema 必须暴露 Resolver");
        AssertEx.False(debugProperties.Contains("Transition"), "Debug Schema 不得恢复 Transition");
        AssertEx.False(debugProperties.Contains("FollowUp"), "Debug Schema 不得恢复 Follow-up");
        AssertEx.False(debugProperties.Contains("Shadow"), "Debug Schema 不得恢复 Shadow");
        AssertEx.Equal(2, BlmDebugEvent.CurrentSchemaVersion, "3B Debug Schema 必须固定为 2");
    }

    private static void ProductionSourceKeepsComplexityGates()
    {
        var root = FindProjectRoot();
        var sourceFiles = Directory.GetFiles(Path.Combine(root, "BLM"), "*.cs", SearchOption.AllDirectories);
        var sources = sourceFiles.Select(File.ReadAllText).ToArray();
        AssertEx.Equal(
            1,
            sources.Sum(source => Count(source, "Level100ResolverEngine.Evaluate(")),
            "纯 Resolver 在生产代码中只能由执行器单点求值");
        AssertEx.Equal(
            0,
            sources.Sum(source => Count(source, "PAction.RequiresVerification")),
            "3B 不得引入第二套 RequiresVerification 生命周期");
        AssertEx.Equal(
            0,
            sources.Sum(source => Count(source, "BlmActionDispatcher")),
            "生产源码不得残留旧 Dispatcher 引用");

        var rotation = File.ReadAllText(Path.Combine(root, "BLM", "BlackMageRotation.cs"));
        AssertEx.Equal(3, Count(rotation, "_execution.Resolve("), "PR 三入口必须全部交给 Resolver 执行器");
    }

    private static void QtManifestMatchesProductionResolvers()
    {
        AssertEx.True(BlackMageRotation.QtList.ContainsKey("魔泉"), "生产 QT 必须包含魔泉");
        AssertEx.True(BlackMageRotation.QtList.ContainsKey("倾泻资源"), "生产 QT 必须包含资源倾泻");
        AssertEx.True(BlackMageRotation.QtList.ContainsKey("快速耀星"), "生产 QT 必须包含快速耀星");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("压缩火悖论"), "压缩火悖论必须由控制台设置管理");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("不打冰悖论"), "废弃的冰悖论跳过 QT 不得残留");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("实验_B4星灵绝望"), "已删除实验 QT 不得回归生产 manifest");

        var rotation = File.ReadAllText(Path.Combine(
            FindProjectRoot(),
            "BLM",
            "BlackMageRotation.cs"));
        AssertEx.True(
            rotation.Contains(
                "QtStates.Remove(\"不打冰悖论\")",
                StringComparison.Ordinal),
            "启动迁移必须清理配置文件中的废弃冰悖论键");
        AssertEx.True(
            rotation.Contains(
                "QtStates.Remove(\"压缩火悖论\", out var compressedFireParadox)",
                StringComparison.Ordinal),
            "启动迁移必须把旧压缩火悖论 QT 转为控制台设置");
    }

    private static void ConsoleUiKeepsExpandableCardAndEmbeddedFamiliarContracts()
    {
        var assembly = typeof(BlackMageRotation).Assembly;
        AssertEx.True(
            assembly.GetManifestResourceNames().Contains(
                "LosPr.BLM.UI.Assets.BlackCatFamiliar.png",
                StringComparer.Ordinal),
            "猫使魔图片必须内嵌进 DLL，不能依赖用户本地路径");

        var root = FindProjectRoot();
        var containers = File.ReadAllText(Path.Combine(
            root,
            "BLM",
            "UI",
            "Layout",
            "LosContainers.cs"));
        AssertEx.True(
            containers.Contains("ImGui.BeginTable(\"##card_layout\"", StringComparison.Ordinal)
            && containers.Contains("BackgroundMeasurements", StringComparison.Ordinal),
            "控制台卡片必须按本帧内容自然撑高，并仅缓存背景预测高度");
        AssertEx.False(
            containers.Contains("ImGui.BeginChild(", StringComparison.Ordinal),
            "普通设置卡片不得使用固定高度子窗口裁切内容");

        var familiarTexture = File.ReadAllText(Path.Combine(
            root,
            "BLM",
            "UI",
            "Assets",
            "BlmFamiliarTexture.cs"));
        AssertEx.True(
            familiarTexture.Contains("GetFromManifestResource", StringComparison.Ordinal)
            && familiarTexture.Contains("GetWrapOrDefault", StringComparison.Ordinal),
            "猫使魔必须使用 Dalamud 共享清单纹理，不能自行管理异步 GPU 资源");
    }

    private static void QuickOverlayKeepsHostAndVisualContracts()
    {
        AssertEx.True(
            typeof(PromeRotation.Rotation.IRotationLifecycle).IsAssignableFrom(typeof(BlackMageRotation)),
            "独立快捷窗口必须跟随 ACR 生命周期启停");

        var root = FindProjectRoot();
        var overlay = File.ReadAllText(Path.Combine(root, "BLM", "UI", "BlmQuickOverlay.cs"));
        AssertEx.True(
            overlay.Contains("PromeSettings.Instance.HiddenQts.Add", StringComparison.Ordinal)
            && overlay.Contains("PromeSettings.Instance.HiddenQts.Remove", StringComparison.Ordinal)
            && overlay.Contains("Plugin.Instance.CloseQtWindow()", StringComparison.Ordinal)
            && overlay.Contains("Plugin.Instance.OpenQtWindow()", StringComparison.Ordinal),
            "独立快捷窗口必须成对隐藏并恢复 PR 本体窗口");
        AssertEx.True(
            overlay.Contains("DrawCatEar(", StringComparison.Ordinal)
            && overlay.Contains("DrawTail(", StringComparison.Ordinal)
            && overlay.Contains("DrawControlCatFace(", StringComparison.Ordinal),
            "方案 B 必须保留 QT 猫耳/卷尾和控制条猫脸元素");
        AssertEx.False(
            overlay.Contains("##los_auto_pull", StringComparison.Ordinal),
            "主动攻击开关应放在展开后的控制台，而不是悬浮控制条");

        var console = File.ReadAllText(Path.Combine(root, "BLM", "UI", "BlmConsoleWindow.cs"));
        AssertEx.True(
            console.Contains("DrawAutoPullControl(", StringComparison.Ordinal)
            && console.Contains("PromeSettings.Instance.AutoPull", StringComparison.Ordinal)
            && console.Contains("BlmPanelPrimitives.DrawToggleRow(", StringComparison.Ordinal),
            "展开后的控制台标题后必须提供直接绑定 PR AutoPull 的开关");

        var qtButtonStart = overlay.IndexOf("private static void DrawQtButton(", StringComparison.Ordinal);
        var nextMethod = overlay.IndexOf("private static void DrawPanelBody(", qtButtonStart, StringComparison.Ordinal);
        var qtButtonSource = overlay[qtButtonStart..nextMethod];
        AssertEx.False(
            qtButtonSource.Contains("Whisker", StringComparison.OrdinalIgnoreCase),
            "定稿后的 QT 按键不得绘制胡须");
    }

    private static void QuickHotkeyPanelKeepsNativeIconAndGridContracts()
    {
        var root = FindProjectRoot();
        var overlay = File.ReadAllText(Path.Combine(root, "BLM", "UI", "BlmQuickOverlay.cs"));
        var catalog = File.ReadAllText(Path.Combine(root, "BLM", "UI", "BlmHotkeyCatalog.cs"));
        var settings = File.ReadAllText(Path.Combine(root, "BLM", "Data", "BlackMageSettings.cs"));
        var opener = File.ReadAllText(Path.Combine(
            root,
            "BLM",
            "Openers",
            "BlmOpenerExecutionService.cs"));
        var eventHandler = File.ReadAllText(Path.Combine(root, "BLM", "BlackMageEventHandler.cs"));

        AssertEx.True(
            overlay.Contains("private const int HotkeyColumns = 4", StringComparison.Ordinal)
            && overlay.Contains("DrawHotkeyWindow", StringComparison.Ordinal)
            && overlay.Contains("DrawHotkeyChrome", StringComparison.Ordinal)
            && overlay.Contains("DrawHotkeyButton", StringComparison.Ordinal),
            "Hotkey 必须保持可拖动的 4x3 独立猫耳窗口");
        AssertEx.False(
            overlay.Contains("冷却 · 充能", StringComparison.Ordinal)
            || overlay.Contains("使魔 Hotkey", StringComparison.Ordinal),
            "简约 Hotkey 窗口不得恢复顶部标题或底部状态图例");
        AssertEx.Equal(12, Count(catalog, "        new(\""), "Hotkey 首版必须完整注册 12 个技能");
        AssertEx.True(
            catalog.Contains("GetActionIcon()", StringComparison.Ordinal)
            && catalog.Contains("GetGameIcon()", StringComparison.Ordinal)
            && catalog.Contains("GetExcelSheet<Lumina.Excel.Sheets.Item>()", StringComparison.Ordinal)
            && catalog.Contains("LimitBreakHelper.GetLimitBreakActionId()", StringComparison.Ordinal),
            "Action、Item 与动态 LB 必须分别读取游戏原生图标");
        AssertEx.True(
            catalog.Contains("HotkeyQueueManager.TryEnqueue(action)", StringComparison.Ordinal)
            && catalog.Contains("new PAction(actionId, ActionType.Always", StringComparison.Ordinal)
            && catalog.Contains("ActionQueueManager.Enqueue(action, isHighPriority: true)", StringComparison.Ordinal)
            && catalog.Contains("BuildPotionDispatchParameters", StringComparison.Ordinal)
            && catalog.Contains("new(itemId, 0xFFFFu)", StringComparison.Ordinal),
            "GCD/LB 保留 Hotkey 预输入，手动能力技必须强制走 Always，爆发药必须沿用 PR 物品编码");
        AssertEx.True(
            catalog.Contains("AllowDuringOpener", StringComparison.Ordinal)
            && opener.Contains("BlmHotkeyCatalog.HasPendingManualAbility", StringComparison.Ordinal)
            && opener.Contains("ConsumeManualAbilityObservation", StringComparison.Ordinal),
            "安全 Hotkey 插入不得取消或重排正在执行的起手计划");
        AssertEx.True(
            catalog.Contains("ProcessPendingDirectActions", StringComparison.Ordinal)
            && catalog.Contains("TryQueuePotion", StringComparison.Ordinal)
            && eventHandler.Contains(
                "BlmHotkeyCatalog.ProcessPendingDirectActions()",
                StringComparison.Ordinal),
            "爆发药 Hotkey 必须跨读条保持 Pending 并在 Framework Tick 重试");
        AssertEx.True(
            settings.Contains("QuickHotkeyWindowX", StringComparison.Ordinal)
            && settings.Contains("QuickHotkeyWindowY", StringComparison.Ordinal),
            "Hotkey 独立窗口必须持久化用户拖动位置");
    }

    private static void FormalUiKeepsVisibilityBindingAndCompactDebugContracts()
    {
        var root = FindProjectRoot();
        var navigation = File.ReadAllText(Path.Combine(
            root,
            "BLM",
            "UI",
            "Navigation",
            "BlmConsoleTab.cs"));
        var settings = File.ReadAllText(Path.Combine(root, "BLM", "Data", "BlackMageSettings.cs"));
        var overlay = File.ReadAllText(Path.Combine(root, "BLM", "UI", "BlmQuickOverlay.cs"));
        var bindingManager = File.ReadAllText(Path.Combine(root, "BLM", "UI", "BlmKeyBindingManager.cs"));
        var bindingPanel = File.ReadAllText(Path.Combine(root, "BLM", "UI", "Panels", "BlmHotkeyPanel.cs"));
        var familiar = File.ReadAllText(Path.Combine(root, "BLM", "UI", "Panels", "BlmFamiliarPanel.cs"));
        var debug = File.ReadAllText(Path.Combine(root, "BLM", "UI", "Panels", "BlmSystemPanel.cs"));
        var combat = File.ReadAllText(Path.Combine(root, "BLM", "UI", "Panels", "BlmCombatPanel.cs"));

        AssertEx.True(
            navigation.Contains("Hotkeys", StringComparison.Ordinal)
            && navigation.IndexOf("Style", StringComparison.Ordinal)
                < navigation.IndexOf("Hotkeys", StringComparison.Ordinal)
            && navigation.IndexOf("Hotkeys", StringComparison.Ordinal)
                < navigation.IndexOf("Debug", StringComparison.Ordinal),
            "热键页必须位于风格与 Debug 之间");
        AssertEx.True(
            settings.Contains("HiddenQtKeys", StringComparison.Ordinal)
            && settings.Contains("HiddenHotkeyKeys", StringComparison.Ordinal)
            && settings.Contains("QtBindings", StringComparison.Ordinal)
            && settings.Contains("HotkeyBindings", StringComparison.Ordinal)
            && settings.Contains("QtPanelScale", StringComparison.Ordinal)
            && settings.Contains("HotkeyPanelScale", StringComparison.Ordinal),
            "正式版设置必须持久化浮窗可见性、组合键和独立缩放");
        AssertEx.True(
            bindingPanel.Contains("BlmBindingKind.Qt", StringComparison.Ordinal)
            && bindingPanel.Contains("BlmBindingKind.Hotkey", StringComparison.Ordinal)
            && bindingManager.Contains("SafeSetQt", StringComparison.Ordinal)
            && bindingManager.Contains("BlmHotkeyCatalog.TryActivate", StringComparison.Ordinal),
            "热键页绑定必须实际控制 QT 与 Hotkey");
        AssertEx.True(
            overlay.Contains("settings.QtBindings.TryGetValue", StringComparison.Ordinal)
            && overlay.Contains("settings.HotkeyBindings.TryGetValue", StringComparison.Ordinal)
            && overlay.Contains("DrawBindingBadge", StringComparison.Ordinal)
            && overlay.Contains("BlmKeyBindingManager.FormatCompact", StringComparison.Ordinal),
            "QT 与 Hotkey 浮窗必须在按钮左上角显示已绑定键位");

        var qtChromeStart = overlay.IndexOf("private void DrawQtChrome(", StringComparison.Ordinal);
        var qtButtonsStart = overlay.IndexOf("private void DrawQtButtons(", qtChromeStart, StringComparison.Ordinal);
        AssertEx.False(
            overlay[qtChromeStart..qtButtonsStart].Contains("使魔快捷咒式", StringComparison.Ordinal),
            "QT 浮窗不得继续绘制顶部标题");
        var hotkeyButtonStart = overlay.IndexOf("private static void DrawHotkeyButton(", StringComparison.Ordinal);
        var badgeStart = overlay.IndexOf("private static void DrawHotkeyBadge(", hotkeyButtonStart, StringComparison.Ordinal);
        AssertEx.False(
            overlay[hotkeyButtonStart..badgeStart].Contains("definition.Name", StringComparison.Ordinal),
            "Hotkey 图标下方不得常驻显示技能名");

        AssertEx.True(
            familiar.Contains("BlmDebugPanel.DrawCompactControls", StringComparison.Ordinal)
            && debug.Contains("DrawCompactControls", StringComparison.Ordinal)
            && !debug.Contains("DrawDiagnostics(store", StringComparison.Ordinal),
            "Debug 开关必须移到右侧黑猫栏的紧凑区域");
        AssertEx.True(
            combat.Contains("请确保 FuckAnimationLock", StringComparison.Ordinal)
            && combat.Contains("请确保 DR", StringComparison.Ordinal),
            "危险循环依赖项必须明确提示用户自行确认");
    }

    private static void OverviewTargetDiagnosticsReflectRuntimeFacts()
    {
        var context = TestContext.Base() with
        {
            DutyComposition = new BlmDutyComposition(8, 1),
            EnemyCount = 1,
            AoeEnabled = true,
            SmartAoeEnabled = false,
            IsAoeMode = false,
        };
        var snapshot = BlmUiSnapshot.FromContext(context);
        AssertEx.Equal("8 人", snapshot.DutySizeLabel, "概览必须显示副本额定人数");
        AssertEx.Equal(1, snapshot.ValidEnemyCount, "概览必须直接展示PR敌人数事实");
        AssertEx.Equal(
            "PR 计数 1，需要至少 3 个",
            snapshot.AoeDecisionLabel,
            "概览必须解释标准AOE阈值未满足");

        var smartAoe = BlmUiSnapshot.FromContext(context with
        {
            SmartAoeEnabled = true,
        });
        AssertEx.Equal(
            "PR 计数 1，需要至少 2 个",
            smartAoe.AoeDecisionLabel,
            "概览必须解释智能AOE阈值未满足");

        var alliance = BlmUiSnapshot.FromContext(context with
        {
            DutyComposition = new BlmDutyComposition(8, 3),
            EnemyCount = 3,
            IsAoeMode = true,
        });
        AssertEx.Equal("24 人（3 队）", alliance.DutySizeLabel, "概览必须正确显示多队副本编制");
        AssertEx.Equal("已进入群体路线", alliance.AoeDecisionLabel, "概览必须显示已进入AOE");

        var noTarget = BlmUiSnapshot.FromContext(context with
        {
            HasValidTarget = false,
        });
        AssertEx.Equal("当前没有可攻击目标", noTarget.AoeDecisionLabel, "无目标原因显示错误");
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Los.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("无法定位 Los.csproj，不能执行 3B 结构 Gate。");
    }
}
