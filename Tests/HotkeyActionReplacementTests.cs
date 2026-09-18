using LosPr.BLM.UI;
using PromeRotation.Data;
using ManualAbilityRequest = LosPr.BLM.UI.BlmHotkeyCatalog.ManualAbilityRequest;

namespace Los.Tests;

internal static class HotkeyActionReplacementTests
{
    private const uint LeyLines = 3573u;
    private const uint Retrace = 36988u;
    private const uint Triplecast = 7421u;

    public static void RunAll()
    {
        RetraceUsesItsOwnCooldownWhenLeyLinesHasNoCharges();
        RetraceObservationClearsOnlyTheMatchingIntent();
        ExpiredLeyLinesCancelsRetraceWithoutSpendingACharge();
        AcceptedDispatchKeepsWaitingForItsExactObservation();
        ReplacementsShareOnePendingSlot();
        OrdinaryTriplecastKeepsItsIdentityAndChargeAllowance();
    }

    private static void RetraceUsesItsOwnCooldownWhenLeyLinesHasNoCharges()
    {
        var resolved = BlmHotkeyCatalog.ResolveManualActionId(LeyLines, id =>
        {
            AssertEx.Equal(LeyLines, id, "替换查询必须从黑魔纹本体开始");
            return Retrace;
        });
        var queried = new List<uint>();
        float Cooldown(uint id)
        {
            queried.Add(id);
            return id == Retrace ? 0f : 100f;
        }

        AssertEx.True(
            BlmHotkeyCatalog.IsManualAbilityReady(resolved, Cooldown, _ => 0),
            "本体零充能时，可用的魔纹重置仍应通过就绪检查");
        AssertEx.Equal(1, queried.Count, "就绪检查只应查询冻结的实际技能");
        AssertEx.Equal(Retrace, queried[0], "不可继续读取黑魔纹的恢复冷却");
        AssertEx.False(
            BlmHotkeyCatalog.IsManualAbilityReady(LeyLines, Cooldown, _ => 0),
            "没有魔纹重置时，零充能的黑魔纹仍不可释放");
        AssertEx.False(
            BlmHotkeyCatalog.IsManualAbilityReady(Retrace, _ => 2f, _ => 0),
            "魔纹重置自己的冷却仍必须遵守");
    }

    private static void RetraceObservationClearsOnlyTheMatchingIntent()
    {
        var request = CreateRequest(LeyLines, Retrace);
        request.AwaitingAck = true;
        var pending = Queue(request);

        AssertEx.True(
            BlmHotkeyCatalog.RemoveObservedManualAbility(pending, LeyLines) == null,
            "黑魔纹本体回执不能冒充魔纹重置成功");
        AssertEx.Equal(1, pending.Count, "错误形态回执不得删除请求");
        AssertEx.True(
            ReferenceEquals(request, BlmHotkeyCatalog.RemoveObservedManualAbility(pending, Retrace)),
            "魔纹重置回执必须清理按本体 ID 去重的请求");
        AssertEx.Equal(0, pending.Count, "正确回执后不能残留并触发两秒超时");

        var baseRequest = CreateRequest(LeyLines, LeyLines);
        pending = Queue(baseRequest);
        AssertEx.True(
            BlmHotkeyCatalog.RemoveObservedManualAbility(pending, Retrace) == null,
            "魔纹重置回执同样不能冒充新黑魔纹成功");
    }

    private static void ExpiredLeyLinesCancelsRetraceWithoutSpendingACharge()
    {
        var request = CreateRequest(LeyLines, Retrace);
        var pending = Queue(request);

        AssertEx.True(
            BlmHotkeyCatalog.CancelChangedManualAbility(pending, request, LeyLines),
            "等待期间魔纹消失后应取消原重置意图");
        AssertEx.Equal(0, pending.Count, "形态变化后不得继续排队等待或提交");
        AssertEx.Equal(Retrace, request.Action.ActionId, "禁止把原重置请求改写为消耗充能的黑魔纹");
        AssertEx.Equal(0, request.Attempts, "取消不得通过试放黑魔纹消耗一次尝试或充能");
        AssertEx.Equal(6_000L, request.ExpiresAtMs, "保留从按下开始五秒的意图生命周期");
    }

    private static void AcceptedDispatchKeepsWaitingForItsExactObservation()
    {
        var request = CreateRequest(LeyLines, LeyLines);
        request.AwaitingAck = true;
        var pending = Queue(request);

        AssertEx.False(
            BlmHotkeyCatalog.CancelChangedManualAbility(pending, request, Retrace),
            "黑魔纹已提交后，按钮变为重置不得取消原技能回执等待");
        AssertEx.True(
            ReferenceEquals(request, BlmHotkeyCatalog.RemoveObservedManualAbility(pending, LeyLines)),
            "即使按钮形态已变，也应接受最初实际提交的黑魔纹回执");
    }

    private static void ReplacementsShareOnePendingSlot()
    {
        var first = CreateRequest(LeyLines, LeyLines);
        var replacement = CreateRequest(LeyLines, Retrace);
        var pending = Queue(first);

        AssertEx.Equal(first.PendingKey, replacement.PendingKey, "本体和替换技能必须共享同一热键去重键");
        AssertEx.False(
            pending.TryAdd(replacement.PendingKey, replacement),
            "形态切换期间连按不能建立第二个待执行请求");
        AssertEx.True(ReferenceEquals(first, pending[LeyLines]), "重复按键不得覆盖原意图或延长等待期");
    }

    private static void OrdinaryTriplecastKeepsItsIdentityAndChargeAllowance()
    {
        var resolved = BlmHotkeyCatalog.ResolveManualActionId(Triplecast,
            _ => throw new InvalidOperationException("普通三连不应新增技能形态查询"));
        AssertEx.Equal(Triplecast, resolved, "普通三连必须保留原技能 ID");
        AssertEx.True(
            BlmHotkeyCatalog.IsManualAbilityReady(resolved, _ => 30f, _ => 1),
            "三连有一层完整充能时仍应允许进入等待");
        AssertEx.False(
            BlmHotkeyCatalog.IsManualAbilityReady(resolved, _ => 30f, _ => 0),
            "三连没有完整充能时仍应拒绝");

        var request = CreateRequest(Triplecast, resolved);
        var pending = Queue(request);
        AssertEx.False(
            BlmHotkeyCatalog.CancelChangedManualAbility(pending, request, resolved),
            "三连保持原形态时不得取消等待读条结束的请求");
        AssertEx.True(
            ReferenceEquals(request, BlmHotkeyCatalog.RemoveObservedManualAbility(pending, Triplecast)),
            "普通三连的实际技能回执仍应清理请求");
    }

    private static ManualAbilityRequest CreateRequest(uint originalActionId, uint actualActionId)
    {
        var definition = BlmHotkeyCatalog.Entries.Single(entry => entry.ActionId == originalActionId);
        return new ManualAbilityRequest(definition,
            new PAction(actualActionId, definition.Type, definition.Target), 1_000L);
    }

    private static Dictionary<uint, ManualAbilityRequest> Queue(ManualAbilityRequest request)
        => new() { [request.PendingKey] = request };
}
