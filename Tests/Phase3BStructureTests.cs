using System.Reflection;
using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.Resolvers.Production;

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
        AssertEx.True(BlackMageRotation.QtList.ContainsKey("不打冰悖论"), "生产 QT 必须包含冰悖论策略");
        AssertEx.False(BlackMageRotation.QtList.ContainsKey("实验_B4星灵绝望"), "已删除实验 QT 不得回归生产 manifest");
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
