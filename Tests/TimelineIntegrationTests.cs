using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Timeline;
using PromeRotation.Timeline.Core;

namespace Los.Tests;

internal static class TimelineIntegrationTests
{
    public static void RunAll()
    {
        RegistersScopedNodesForBothTimelineEngines();
        InvalidTimelineKeysAreRejected();
        ResourceConditionsCoverLosAeResourceKinds();
        ResourceConditionsFailClosedWithoutTrackerSnapshot();
    }

    private static void InvalidTimelineKeysAreRejected()
    {
        AssertThrows<InvalidOperationException>(
            () => BlmTimelineHotkeyAction.FromDto(new ActionDto
            {
                Params = new Dictionary<string, string> { ["key"] = "removed_hotkey" },
            }),
            "未知 Hotkey 不得静默回退为 LB");
        AssertThrows<InvalidOperationException>(
            () => BlmTimelineQtCondition.FromDto(new ConditionDto
            {
                Params = new Dictionary<string, string> { ["qt"] = "removed_qt" },
            }),
            "未知 QT 不得静默回退为 AOE");
        AssertThrows<InvalidOperationException>(
            () => BlmTimelineResourceCondition.FromDto(new ConditionDto
            {
                Params = new Dictionary<string, string> { ["resource"] = "removed_resource" },
            }),
            "未知资源类型不得静默回退为火状态");
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void RegistersScopedNodesForBothTimelineEngines()
    {
        var provider = BlackMageRotation.NodeProvider;
        AssertEx.Equal(2, provider.GetConditionDescriptors().Count, "时间轴条件节点数量错误");
        AssertEx.Equal(1, provider.GetActionDescriptors().Count, "时间轴动作节点数量错误");

        var context = new RotationNodeContext(25u, "Los.Tests");
        provider.RegisterNodes(context);
        try
        {
            var hotkeyDto = new BlmTimelineHotkeyAction("manaward").ToDto();
            var action = ActionFactory.Create(context, hotkeyDto.Type, hotkeyDto);
            AssertEx.True(action is BlmTimelineHotkeyAction, "Hotkey 动作未按 ACR 作用域注册");

            var resourceDto = new BlmTimelineResourceCondition(
                BlmTimelineResourceType.PolyglotStacks,
                BlmTimelineCompare.GreaterOrEqual,
                2,
                true).ToDto();
            var resource = ConditionFactory.Create(context, resourceDto.Type, resourceDto);
            AssertEx.True(resource is BlmTimelineResourceCondition, "资源条件未按 ACR 作用域注册");

            var qtDto = new BlmTimelineQtCondition("AOE", true).ToDto();
            var qt = ConditionFactory.Create(context, qtDto.Type, qtDto);
            AssertEx.True(qt is BlmTimelineQtCondition, "QT 条件未按 ACR 作用域注册");
        }
        finally
        {
            ActionFactory.ClearAcrRegistrations(context);
            ConditionFactory.ClearAcrRegistrations(context);
        }
    }

    private static void ResourceConditionsCoverLosAeResourceKinds()
    {
        AssertEx.Equal(9, Enum.GetValues<BlmTimelineResourceType>().Length, "Los-ae 资源种类迁移不完整");

        var context = new BlmContext
        {
            IsAvailable = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
            IceStacks = 0,
            AstralSoul = 4,
            HasFirestarter = true,
            UmbralHearts = 2,
            HasParadox = true,
            PolyglotStacks = 2,
        };
        BlmTimelineRuntime.SetContextProvider(() => context);
        try
        {
            AssertCondition(BlmTimelineResourceType.FireState, expected: true);
            AssertCondition(BlmTimelineResourceType.IceState, expected: false);
            AssertCondition(BlmTimelineResourceType.Firestarter, expected: true);
            AssertCondition(BlmTimelineResourceType.Paradox, expected: true);
            AssertCondition(BlmTimelineResourceType.FireStacks, 3);
            AssertCondition(BlmTimelineResourceType.IceStacks, 0);
            AssertCondition(BlmTimelineResourceType.AstralSoulStacks, 4);
            AssertCondition(BlmTimelineResourceType.UmbralHearts, 2);
            AssertCondition(BlmTimelineResourceType.PolyglotStacks, 2);
        }
        finally
        {
            BlmTimelineRuntime.SetContextProvider(null);
        }
    }

    private static void ResourceConditionsFailClosedWithoutTrackerSnapshot()
    {
        BlmTimelineRuntime.SetContextProvider(null);
        var condition = new BlmTimelineResourceCondition(
            BlmTimelineResourceType.FireState,
            BlmTimelineCompare.Equal,
            0,
            false);
        AssertEx.False(condition.EvaluateImmediate(), "资源快照不可用时不得把假状态误判为满足");
        AssertEx.False(condition.EvaluateWait(), "等待条件也必须在快照不可用时失败关闭");
    }

    private static void AssertCondition(BlmTimelineResourceType resource, bool expected)
    {
        var condition = new BlmTimelineResourceCondition(
            resource,
            BlmTimelineCompare.Equal,
            0,
            expected);
        AssertEx.True(condition.EvaluateImmediate(), $"资源条件 {resource} 布尔判断错误");
        AssertEx.True(condition.EvaluateWait(), $"资源条件 {resource} 等待判断错误");
    }

    private static void AssertCondition(BlmTimelineResourceType resource, int value)
    {
        var condition = new BlmTimelineResourceCondition(
            resource,
            BlmTimelineCompare.Equal,
            value,
            true);
        AssertEx.True(condition.EvaluateImmediate(), $"资源条件 {resource} 数值判断错误");
        AssertEx.True(condition.EvaluateWait(), $"资源条件 {resource} 等待判断错误");
    }
}
