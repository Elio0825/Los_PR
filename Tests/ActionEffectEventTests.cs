using LosPr.BLM;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Production;
using PromeRotation.LogSystem;

namespace Los.Tests;

internal static class ActionEffectEventTests
{
    public static void RunAll()
    {
        DeduplicatesExpandedAoePackets();
        FallsBackToTimestampWhenSequenceIsUnavailable();
        EventHandlerOwnsSubscriptionLifetime();
    }

    private static void DeduplicatesExpandedAoePackets()
    {
        var filter = new BlmActionEffectPacketFilter();
        var timestamp = new DateTime(2026, 7, 16, 23, 0, 0, DateTimeKind.Local);
        var firstTarget = Event(100, BLMSkill.高冰冻, timestamp, 0);
        var secondTarget = Event(100, BLMSkill.高冰冻, timestamp, 1);
        var nextCast = Event(100, BLMSkill.高冰冻, timestamp.AddSeconds(2), 0);

        AssertEx.True(filter.TryAccept(firstTarget, 500), "AOE 首个目标事件应被接受");
        AssertEx.False(filter.TryAccept(secondTarget, 500), "同一动作序号的 AOE 展开事件必须去重");
        AssertEx.True(filter.TryAccept(nextCast, 501), "不同动作序号的同技能不得被误判为重复");

        filter.Clear();
        AssertEx.True(filter.TryAccept(firstTarget, 500), "生命周期清理后不得残留旧动作序号");
    }

    private static void FallsBackToTimestampWhenSequenceIsUnavailable()
    {
        var filter = new BlmActionEffectPacketFilter();
        var timestamp = new DateTime(2026, 7, 16, 23, 0, 0, DateTimeKind.Local);
        var firstTarget = Event(100, BLMSkill.高冰冻, timestamp, 0);
        var secondTarget = Event(100, BLMSkill.高冰冻, timestamp, 1);
        var laterCast = Event(100, BLMSkill.高冰冻, timestamp.AddTicks(1), 0);

        AssertEx.True(filter.TryAccept(firstTarget, 0), "无序号事件的首个目标应被接受");
        AssertEx.False(filter.TryAccept(secondTarget, 0), "无序号 AOE 应按时间戳和技能去重");
        AssertEx.True(filter.TryAccept(laterCast, 0), "不同时间戳的无序号同技能不得被误判为重复");
    }

    private static void EventHandlerOwnsSubscriptionLifetime()
    {
        var clock = new FakeClock();
        var context = TestContext.Base();
        var tracker = new BlmStateTracker(context, clock, new MappingActionIdNormalizer());
        var execution = new BlmResolverExecutionService(tracker);
        var events = new TestLogSystemEventSource();
        var handler = new BlackMageEventHandler(
            tracker,
            new BlmResolverInputAdapter(),
            execution,
            events,
            clock);

        AssertEx.Equal(1, events.SubscriptionCount, "事件处理器应订阅一次新日志 ActionEffect");
        handler.Dispose();
        AssertEx.Equal(0, events.SubscriptionCount, "事件处理器释放时应取消新日志订阅");
    }

    private static LogSystemActionEffectEvent Event(
        ulong sourceId,
        uint actionId,
        DateTime timestamp,
        int targetIndex) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        ActionId = actionId,
        TargetIndex = targetIndex,
    };
}
