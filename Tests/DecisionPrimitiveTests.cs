using LosPr.BLM.Core;
using PromeRotation.Data;

namespace Los.Tests;

internal static class DecisionPrimitiveTests
{
    public static void RunAll()
    {
        RecentlyUsedUsesBoundedNormalizedAckHistory();
        IssuedMetadataFreezesAliasesAndOccurrence();
        IssuedGcdObservationOverridesPrediction();
        IssuedMetadataMismatchExpiryAndReset();
        FrozenAliasesAreEvictedTogether();
        HistoryExpiresAndResetsWithGeneration();
        ManualGcdObservationFacts();
        ZeroSequenceDedupeIsBoundedAndExpires();
        AeAssistCooldownWindowBoundaries();
        ChargeAndAbilityReadyBoundaries();
        InstantAndWeaveCapacityFacts();
    }

    private static void RecentlyUsedUsesBoundedNormalizedAckHistory()
    {
        var mappings = new Dictionary<uint, uint>
        {
            [BLMSkill.冰冻] = BLMSkill.高冰冻,
        };
        var fixture = CreateFixture(new MappingActionIdNormalizer(mappings));
        var adjustedAck = CreateAck(fixture, BLMSkill.高冰冻, 1);
        AssertEx.True(fixture.Tracker.ApplyActionEffect(adjustedAck), "升级技能 Ack 应接收");
        AssertEx.True(
            fixture.Tracker.RecentlyUsed(BLMSkill.冰冻),
            "基础技能 ID 应命中升级形态的规范化历史");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰冻, out var adjusted),
            "应能按基础技能 ID 查询升级形态历史");
        AssertEx.Equal(BLMSkill.高冰冻, adjusted.ActualAckId, "应保留实际 Ack ID");
        AssertEx.Equal(BLMSkill.高冰冻, adjusted.AdjustedAtIssue, "手动 Ack 应冻结当前调整 ID");

        var repeated = CreateFixture();
        var first = CreateAck(repeated, BLMSkill.炽炎, 1);
        AssertEx.True(repeated.Tracker.ApplyActionEffect(first), "第一发连续 F4 应接收");
        AssertEx.False(repeated.Tracker.ApplyActionEffect(first), "重复事件不得写入第二条历史");
        repeated.Clock.Advance(400);
        AssertEx.True(
            repeated.Tracker.ApplyActionEffect(CreateAck(repeated, BLMSkill.炽炎, 2)),
            "第二发合法连续 F4 应接收");
        AssertEx.Equal(
            2,
            repeated.Tracker.GetTrackerSnapshot().AcknowledgedActionHistoryCount,
            "连续相同动作必须保存为两次成功 Ack");
        AssertEx.True(
            repeated.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var latest),
            "应查询到连续动作中的最后一次");
        AssertEx.Equal(2u, latest.GlobalSequence, "连续动作应以最新 sequence 为准");
        AssertEx.Equal(repeated.Clock.NowMs, latest.AcknowledgedAtMs, "连续动作应以最新时间为准");

        repeated.Clock.Advance(100);
        AssertEx.True(
            repeated.Tracker.ApplyActionEffect(CreateAck(repeated, BLMSkill.冰澈, 3)),
            "交错动作应接收");
        AssertEx.True(
            repeated.Tracker.RecentlyUsed(BLMSkill.炽炎, 1200),
            "后续其他动作不得覆盖 F4 的 RecentlyUsed 历史");

        var bounded = CreateFixture();
        for (var index = 0; index < BlmStateTracker.AcknowledgedActionHistoryCapacity + 5; index++)
        {
            AssertEx.True(
                bounded.Tracker.ApplyActionEffect(
                    CreateAck(bounded, (uint)(100_000 + index), (uint)(index + 1))),
                $"容量测试 Ack {index} 应接收");
        }

        AssertEx.Equal(
            BlmStateTracker.AcknowledgedActionHistoryCapacity,
            bounded.Tracker.GetTrackerSnapshot().AcknowledgedActionHistoryCount,
            "成功历史不得无界增长");
        AssertEx.False(
            bounded.Tracker.TryGetLastAcknowledgedAction(100_000, out _),
            "超过容量后最旧动作应从索引淘汰");
        AssertEx.True(
            bounded.Tracker.TryGetLastAcknowledgedAction(
                (uint)(100_000 + BlmStateTracker.AcknowledgedActionHistoryCapacity + 4),
                out _),
            "超过容量后最新动作必须保留");
    }

    private static void IssuedMetadataFreezesAliasesAndOccurrence()
    {
        var mappings = new Dictionary<uint, uint>
        {
            [BLMSkill.冰冻] = BLMSkill.高冰冻,
        };
        var fixture = CreateFixture(new MappingActionIdNormalizer(mappings));
        var issued = new BlmIssuedActionMetadata(
            fixture.Tracker.StateGeneration,
            BLMSkill.冰冻,
            BLMSkill.高冰冻,
            fixture.Clock.NowMs - 500,
            0,
            fixture.Clock.NowMs + 500,
            true,
            true);
        AssertEx.True(
            fixture.Tracker.TryRegisterIssuedAction(issued),
            "首次签发元数据应注册成功");
        AssertEx.True(
            fixture.Tracker.TryRegisterIssuedAction(issued),
            "相同签发元数据重复注册应幂等成功");
        AssertEx.Equal(
            0,
            fixture.Tracker.GetTrackerSnapshot().AcknowledgedActionHistoryCount,
            "签发但未 Ack 时不得写入成功历史");

        mappings[BLMSkill.冰冻] = BLMSkill.玄冰;
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(CreateAck(fixture, BLMSkill.高冰冻, 1)),
            "Ack 必须按签发时冻结的 adjusted ID 匹配");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰冻, out var success),
            "mapping 改变后仍应按冻结 requested alias 查询");
        AssertEx.Equal(BLMSkill.冰冻, success.RequestedId, "RequestedId 必须冻结");
        AssertEx.Equal(BLMSkill.高冰冻, success.AdjustedAtIssue, "AdjustedAtIssue 必须冻结");
        AssertEx.Equal(BLMSkill.高冰冻, success.ActualAckId, "ActualAckId 必须来自事件");
        AssertEx.Equal(issued.IssuedAtMs, success.OccurredAtMs, "签发动作发生时间应使用 IssuedAt");
        AssertEx.Equal(fixture.Clock.NowMs, success.AcknowledgedAtMs, "确认时间应使用 Ack 时刻");
        AssertEx.True(success.WasInstant, "瞬发事实必须来自签发元数据");
        AssertEx.True(success.IsGcd, "GCD 分类必须来自签发元数据");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.高冰冻, out var adjusted)
                && adjusted.Serial == success.Serial,
            "冻结 adjusted alias 应指向同一历史条目");
        AssertEx.False(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.玄冰, out _),
            "后续 normalizer 变化不得改写已冻结 alias");
        AssertEx.True(
            fixture.Tracker.RecentlyUsed(BLMSkill.冰冻, 501),
            "OccurredAt 后 500ms 在 501ms 窗口内应命中");
        AssertEx.False(
            fixture.Tracker.RecentlyUsed(BLMSkill.冰冻, 500),
            "RecentlyUsed 应以回溯后的 OccurredAt 严格比较");

        var issuedFallback = CreateFixture();
        var issueAtMs = issuedFallback.Clock.NowMs - 200;
        AssertEx.True(
            issuedFallback.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    issuedFallback.Tracker.StateGeneration,
                    BLMSkill.炽炎,
                    BLMSkill.炽炎,
                    issueAtMs,
                    0,
                    issuedFallback.Clock.NowMs + 500,
                    false,
                    true)),
            "IssuedAt 回溯测试签发应注册");
        AssertEx.True(
            issuedFallback.Tracker.ApplyActionEffect(
                CreateAck(issuedFallback, BLMSkill.炽炎, 1)),
            "IssuedAt 回溯测试 Ack 应接收");
        AssertEx.True(
            issuedFallback.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var fallback),
            "应能查询 issuedAt 回退条目");
        AssertEx.Equal(issueAtMs, fallback.OccurredAtMs, "签发动作应回溯到 issuedAt");

        var zeroSequence = CreateFixture();
        var zeroIssuedAtMs = zeroSequence.Clock.NowMs;
        AssertEx.True(
            zeroSequence.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    zeroSequence.Tracker.StateGeneration,
                    BLMSkill.冰澈,
                    BLMSkill.冰澈,
                    zeroIssuedAtMs,
                    100,
                    zeroSequence.Clock.NowMs + 500,
                    true,
                    true)),
            "零 sequence 签发应注册");
        AssertEx.True(
            zeroSequence.Tracker.ApplyActionEffect(
                CreateAck(zeroSequence, BLMSkill.冰澈, 0)),
            "合法零 sequence Ack 应匹配冻结签发");
        AssertEx.True(
            zeroSequence.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out var zeroMatch),
            "零 sequence Ack 应提交签发历史");
        AssertEx.Equal(zeroIssuedAtMs, zeroMatch.OccurredAtMs, "零 sequence Ack 应使用 IssuedAt");
        AssertEx.True(zeroMatch.WasInstant, "零 sequence Ack 应保留签发瞬发预测");
    }

    private static void IssuedGcdObservationOverridesPrediction()
    {
        var fixture = CreateFixture();
        var issuedAtMs = fixture.Clock.NowMs - 500;
        var observedStartedAtMs = fixture.Clock.NowMs - 100;
        AssertEx.True(
            fixture.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    fixture.Tracker.StateGeneration,
                    BLMSkill.炽炎,
                    BLMSkill.炽炎,
                    issuedAtMs,
                    0,
                    fixture.Clock.NowMs + 500,
                    true,
                    true)),
            "issued 实测覆盖测试签发应注册");
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(
                CreateAck(
                    fixture,
                    BLMSkill.炽炎,
                    1,
                    observedStartedAtMs,
                    1000f,
                    false)),
            "带完整实测事实的 issued GCD Ack 应接收");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var observed),
            "issued GCD 应提交实测历史");
        AssertEx.True(observedStartedAtMs > issuedAtMs, "测试前提要求 observed start 晚于 issued");
        AssertEx.Equal(
            observedStartedAtMs,
            observed.OccurredAtMs,
            "排队后实际起转应覆盖较早的 issuedAt");
        AssertEx.False(
            observed.WasInstant,
            "预测瞬发但实测 remain=1000ms 时必须按硬读记录");

        var ogcd = CreateFixture();
        var ogcdIssuedAtMs = ogcd.Clock.NowMs - 400;
        AssertEx.True(
            ogcd.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    ogcd.Tracker.StateGeneration,
                    BLMSkill.星灵移位,
                    BLMSkill.星灵移位,
                    ogcdIssuedAtMs,
                    0,
                    ogcd.Clock.NowMs + 500,
                    false,
                    false)),
            "issued oGCD 实测忽略测试签发应注册");
        AssertEx.True(
            ogcd.Tracker.ApplyActionEffect(
                CreateAck(
                    ogcd,
                    BLMSkill.星灵移位,
                    1,
                    ogcd.Clock.NowMs - 50,
                    1700f,
                    false)),
            "带 GCD 观测的 issued oGCD Ack 应接收");
        AssertEx.True(
            ogcd.Tracker.TryGetLastAcknowledgedAction(BLMSkill.星灵移位, out var ogcdFact),
            "issued oGCD 应提交历史");
        AssertEx.Equal(ogcdIssuedAtMs, ogcdFact.OccurredAtMs, "issued oGCD 必须继续使用 issuedAt");
        AssertEx.False(ogcdFact.WasInstant, "issued oGCD 不得采用 GCD 实测瞬发");
    }

    private static void IssuedMetadataMismatchExpiryAndReset()
    {
        var fixture = CreateFixture();
        var issuedAtMs = fixture.Clock.NowMs - 100;
        var metadata = new BlmIssuedActionMetadata(
            fixture.Tracker.StateGeneration,
            BLMSkill.冰澈,
            BLMSkill.冰澈,
            issuedAtMs,
            0,
            fixture.Clock.NowMs + 300,
            true,
            true);
        AssertEx.True(fixture.Tracker.TryRegisterIssuedAction(metadata), "待匹配签发应注册");
        AssertEx.False(
            fixture.Tracker.TryRegisterIssuedAction(metadata with { RequestedId = BLMSkill.炽炎 }),
            "单 Pending 存在时不得被另一签发覆盖");
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(CreateAck(fixture, BLMSkill.炽炎, 1)),
            "不匹配 Ack 应作为手动动作接收");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var manual),
            "不匹配 Ack 应拥有独立手动历史");
        AssertEx.Equal(manual.AcknowledgedAtMs, manual.OccurredAtMs, "手动动作应回退 AckAt");
        AssertEx.False(manual.WasInstant, "手动动作不得借用不匹配签发的瞬发事实");

        fixture.Clock.Advance(100);
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(CreateAck(fixture, BLMSkill.冰澈, 2)),
            "不匹配 Ack 不得偷走原 Pending");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out var matched),
            "后续正确 Ack 应命中原签发");
        AssertEx.Equal(issuedAtMs, matched.OccurredAtMs, "正确 Ack 应回溯到 issuedAt");
        AssertEx.True(matched.WasInstant, "正确 Ack 应继承签发瞬发事实");

        var superseded = CreateFixture();
        var supersededIssuedAtMs = superseded.Clock.NowMs;
        AssertEx.True(
            superseded.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    superseded.Tracker.StateGeneration,
                    BLMSkill.冰澈,
                    BLMSkill.冰澈,
                    supersededIssuedAtMs,
                    0,
                    supersededIssuedAtMs + 5000,
                    true,
                    true)),
            "手动覆盖测试签发应注册");
        superseded.Clock.Advance(10);
        AssertEx.True(
            superseded.Tracker.ApplyActionEffect(CreateAck(
                superseded,
                BLMSkill.炽炎,
                1,
                superseded.Clock.NowMs,
                2400f)),
            "签发后启动的手动 GCD 应被接受");
        AssertEx.False(
            superseded.Tracker.GetTrackerSnapshot().HasPendingIssuedAction,
            "签发后启动的新 GCD 必须清除已被覆盖的 Pending");
        AssertEx.True(
            superseded.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    superseded.Tracker.StateGeneration,
                    BLMSkill.炽炎,
                    BLMSkill.炽炎,
                    superseded.Clock.NowMs,
                    1,
                    superseded.Clock.NowMs + 5000,
                    false,
                    true)),
            "手动覆盖后应立即允许新签发");

        var stale = CreateFixture();
        var staleIssuedAtMs = stale.Clock.NowMs;
        AssertEx.True(
            stale.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    stale.Tracker.StateGeneration,
                    BLMSkill.冰澈,
                    BLMSkill.冰澈,
                    staleIssuedAtMs,
                    10,
                    stale.Clock.NowMs + 500,
                    true,
                    true)),
            "旧 Ack 隔离测试签发应注册");
        var earlyAck = stale.Tracker.CreateAckEnvelope(
            100,
            BLMSkill.冰澈,
            11,
            BlmPhase.Fire,
            staleIssuedAtMs - 1);
        AssertEx.True(stale.Tracker.ApplyActionEffect(earlyAck), "早于签发的 Ack 仍是合法手动事实");
        AssertEx.True(
            stale.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out var earlyFallback),
            "早到 Ack 应按手动动作回退");
        AssertEx.False(earlyFallback.WasInstant, "早到 Ack 不得匹配签发预测");
        AssertEx.True(
            stale.Tracker.ApplyActionEffect(CreateAck(stale, BLMSkill.冰澈, 10)),
            "等于 sequence baseline 的 Ack 仍应作为手动事实接收");
        AssertEx.True(
            stale.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out var baselineFallback),
            "baseline Ack 应留下手动历史");
        AssertEx.False(baselineFallback.WasInstant, "非新 sequence 不得匹配签发预测");
        AssertEx.True(
            stale.Tracker.ApplyActionEffect(CreateAck(stale, BLMSkill.冰澈, 12)),
            "新于 baseline 的正确 Ack 应匹配原 Pending");
        AssertEx.True(
            stale.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out var freshMatch),
            "正确新 Ack 应覆盖手动回退 alias");
        AssertEx.Equal(staleIssuedAtMs, freshMatch.OccurredAtMs, "新 Ack 应回溯到签发时刻");
        AssertEx.True(freshMatch.WasInstant, "新 Ack 应继承签发预测");

        var expired = CreateFixture();
        var expiredMetadata = new BlmIssuedActionMetadata(
            expired.Tracker.StateGeneration,
            BLMSkill.冰澈,
            BLMSkill.冰澈,
            expired.Clock.NowMs,
            0,
            expired.Clock.NowMs + 100,
            true,
            true);
        AssertEx.True(expired.Tracker.TryRegisterIssuedAction(expiredMetadata), "过期测试签发应注册");
        expired.Clock.Advance(101);
        AssertEx.True(
            expired.Tracker.ApplyActionEffect(CreateAck(expired, BLMSkill.冰澈, 1)),
            "过期后的同技能 Ack 仍应按手动动作接收");
        AssertEx.True(
            expired.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out var expiredFallback),
            "过期 Ack 应生成手动回退历史");
        AssertEx.Equal(
            expiredFallback.AcknowledgedAtMs,
            expiredFallback.OccurredAtMs,
            "过期签发不得回溯发生时间");
        AssertEx.False(expiredFallback.WasInstant, "过期签发不得泄漏瞬发事实");
        AssertEx.True(
            expired.Tracker.TryRegisterIssuedAction(
                expiredMetadata with
                {
                    IssuedAtMs = expired.Clock.NowMs,
                    AckSequenceBaseline = 1,
                    DeadlineAtMs = expired.Clock.NowMs + 100,
                }),
            "过期 Pending 清理后应允许新签发");

        var reset = CreateFixture();
        var oldGeneration = reset.Tracker.StateGeneration;
        AssertEx.True(
            reset.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    oldGeneration,
                    BLMSkill.冰澈,
                    BLMSkill.冰澈,
                    reset.Clock.NowMs,
                    0,
                    reset.Clock.NowMs + 500,
                    false,
                    true)),
            "重置测试签发应注册");
        reset.Tracker.EndCombat();
        AssertEx.True(
            reset.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    reset.Tracker.StateGeneration,
                    BLMSkill.炽炎,
                    BLMSkill.炽炎,
                    reset.Clock.NowMs,
                    0,
                    reset.Clock.NowMs + 500,
                    false,
                    true)),
            "硬重置必须清理旧 Pending");
    }

    private static void FrozenAliasesAreEvictedTogether()
    {
        const uint requestedId = 200_000;
        const uint adjustedId = 200_001;
        var fixture = CreateFixture();
        AssertEx.True(
            fixture.Tracker.TryRegisterIssuedAction(
                new BlmIssuedActionMetadata(
                    fixture.Tracker.StateGeneration,
                    requestedId,
                    adjustedId,
                    fixture.Clock.NowMs,
                    0,
                    fixture.Clock.NowMs + 500,
                    false,
                    false)),
            "多 alias 淘汰测试签发应注册");
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(CreateAck(fixture, adjustedId, 1)),
            "多 alias 淘汰测试 Ack 应接收");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(requestedId, out _),
            "淘汰前 requested alias 应存在");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(adjustedId, out _),
            "淘汰前 adjusted/actual alias 应存在");
        AssertEx.True(
            fixture.Tracker.ApplyActionEffect(CreateAck(fixture, adjustedId, 2)),
            "共享 alias 的后续手动 Ack 应接收");

        for (var index = 0; index < BlmStateTracker.AcknowledgedActionHistoryCapacity - 1; index++)
        {
            AssertEx.True(
                fixture.Tracker.ApplyActionEffect(
                    CreateAck(fixture, (uint)(300_000 + index), (uint)(index + 3))),
                $"alias 淘汰填充 Ack {index} 应接收");
        }

        AssertEx.Equal(
            BlmStateTracker.AcknowledgedActionHistoryCapacity,
            fixture.Tracker.GetTrackerSnapshot().AcknowledgedActionHistoryCount,
            "alias 淘汰后历史仍应保持固定容量");
        AssertEx.False(
            fixture.Tracker.TryGetLastAcknowledgedAction(requestedId, out _),
            "淘汰条目的 requested alias 必须移除");
        AssertEx.True(
            fixture.Tracker.TryGetLastAcknowledgedAction(adjustedId, out var retained)
                && retained.GlobalSequence == 2,
            "淘汰旧条目时不得移除已由新条目接管的共享 alias");
    }

    private static void HistoryExpiresAndResetsWithGeneration()
    {
        var fixture = CreateFixture();
        var ack = CreateAck(fixture, BLMSkill.冰澈, 1);
        AssertEx.True(fixture.Tracker.ApplyActionEffect(ack), "过期测试 Ack 应接收");
        fixture.Clock.Advance(1199);
        AssertEx.True(
            fixture.Tracker.RecentlyUsed(BLMSkill.冰澈),
            "默认 1200ms 窗口内应视为 RecentlyUsed");
        fixture.Clock.Advance(1);
        AssertEx.False(
            fixture.Tracker.RecentlyUsed(BLMSkill.冰澈),
            "恰好 1200ms 必须按 AEAssist 严格小于语义过期");

        var generation = fixture.Tracker.StateGeneration;
        fixture.Tracker.EndCombat();
        AssertEx.Equal(generation + 1, fixture.Tracker.StateGeneration, "战斗结束应推进 generation");
        AssertEx.Equal(
            0,
            fixture.Tracker.GetTrackerSnapshot().AcknowledgedActionHistoryCount,
            "generation 重置必须清空成功历史");
        AssertEx.False(
            fixture.Tracker.TryGetLastAcknowledgedAction(BLMSkill.冰澈, out _),
            "新 generation 不得读到旧动作");
        AssertEx.False(
            fixture.Tracker.ApplyActionEffect(ack),
            "旧 generation 的迟到 Ack 不得重新写入历史");
    }

    private static void ManualGcdObservationFacts()
    {
        var hardCast = CreateFixture();
        var hardCastStartedAtMs = hardCast.Clock.NowMs - 800;
        AssertEx.True(
            hardCast.Tracker.ApplyActionEffect(
                CreateAck(
                    hardCast,
                    BLMSkill.炽炎,
                    1,
                    hardCastStartedAtMs,
                    1000f,
                    false)),
            "手动硬读 GCD Ack 应接收");
        AssertEx.True(
            hardCast.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var hardCastFact),
            "手动硬读应写入可观测事实");
        AssertEx.Equal(hardCastStartedAtMs, hardCastFact.OccurredAtMs, "手动硬读应使用观测起点");
        AssertEx.False(hardCastFact.WasInstant, "低于 1700ms 的无咏速 GCD 不应判瞬发");

        var instant = CreateFixture();
        var instantStartedAtMs = instant.Clock.NowMs - 100;
        AssertEx.True(
            instant.Tracker.ApplyActionEffect(
                CreateAck(
                    instant,
                    BLMSkill.炽炎,
                    1,
                    instantStartedAtMs,
                    1700f,
                    false)),
            "手动瞬发 GCD Ack 应接收");
        AssertEx.True(
            instant.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var instantFact),
            "手动瞬发应写入可观测事实");
        AssertEx.Equal(instantStartedAtMs, instantFact.OccurredAtMs, "手动瞬发应使用观测起点");
        AssertEx.True(instantFact.WasInstant, "无咏速 1700ms 边界应判瞬发");

        var haste = CreateFixture();
        AssertEx.True(
            haste.Tracker.ApplyActionEffect(
                CreateAck(
                    haste,
                    BLMSkill.炽炎,
                    1,
                    haste.Clock.NowMs - 100,
                    1500f,
                    true)),
            "咏速阈值 GCD Ack 应接收");
        AssertEx.True(
            haste.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var hasteFact),
            "咏速阈值应写入事实");
        AssertEx.True(hasteFact.WasInstant, "有咏速 1500ms 边界应判瞬发");

        var noHaste = CreateFixture();
        AssertEx.True(
            noHaste.Tracker.ApplyActionEffect(
                CreateAck(
                    noHaste,
                    BLMSkill.炽炎,
                    1,
                    noHaste.Clock.NowMs - 100,
                    1500f,
                    false)),
            "无咏速对照 GCD Ack 应接收");
        AssertEx.True(
            noHaste.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var noHasteFact),
            "无咏速对照应写入事实");
        AssertEx.False(noHasteFact.WasInstant, "无咏速 1500ms 不应判瞬发");

        var unavailable = CreateFixture();
        AssertEx.True(
            unavailable.Tracker.ApplyActionEffect(CreateAck(unavailable, BLMSkill.炽炎, 1)),
            "无观测 GCD Ack 应接收");
        AssertEx.True(
            unavailable.Tracker.TryGetLastAcknowledgedAction(BLMSkill.炽炎, out var fallback),
            "无观测 GCD 应写入回退事实");
        AssertEx.Equal(fallback.AcknowledgedAtMs, fallback.OccurredAtMs, "无观测 GCD 应回退 AckAt");
        AssertEx.False(fallback.WasInstant, "无观测 GCD 不得猜测瞬发");

        var ogcd = CreateFixture();
        AssertEx.True(
            ogcd.Tracker.ApplyActionEffect(
                CreateAck(
                    ogcd,
                    BLMSkill.星灵移位,
                    1,
                    ogcd.Clock.NowMs - 500,
                    1700f,
                    false)),
            "手动 oGCD Ack 应接收");
        AssertEx.True(
            ogcd.Tracker.TryGetLastAcknowledgedAction(BLMSkill.星灵移位, out var ogcdFact),
            "手动 oGCD 应写入历史");
        AssertEx.Equal(ogcdFact.AcknowledgedAtMs, ogcdFact.OccurredAtMs, "oGCD 必须忽略 GCD 观测起点");
        AssertEx.False(ogcdFact.WasInstant, "oGCD 不得携带 GCD 瞬发事实");
        AssertEx.False(ogcdFact.IsGcd, "星灵移位必须保留 oGCD 分类");

        var leyLinesCompatibility = new BlmContext
        {
            HasLeyLines = true,
            HasLeyLinesHaste = false,
        };
        AssertEx.True(leyLinesCompatibility.HasLeyLines, "原 HasLeyLines 行为必须保持独立");
        AssertEx.False(
            leyLinesCompatibility.HasLeyLinesHaste,
            "黑魔纹存在不等于已获得独立咏速状态 738");
    }

    private static void ZeroSequenceDedupeIsBoundedAndExpires()
    {
        var bounded = CreateFixture();
        for (var index = 0; index < BlmStateTracker.ZeroSequenceDedupeCapacity; index++)
        {
            AssertEx.True(
                bounded.Tracker.ApplyActionEffect(
                    CreateAck(bounded, (uint)(400_000 + index), 0)),
                $"zero-sequence 容量填充 Ack {index} 应接收");
        }

        AssertEx.Equal(
            BlmStateTracker.ZeroSequenceDedupeCapacity,
            bounded.Tracker.GetTrackerSnapshot().ZeroSequenceDedupeCount,
            "zero-sequence 去重索引必须保持固定容量");
        AssertEx.True(
            bounded.Tracker.ApplyActionEffect(CreateAck(bounded, 500_000, 0)),
            "超过容量的新 zero-sequence Ack 应接收");
        AssertEx.Equal(
            BlmStateTracker.ZeroSequenceDedupeCapacity,
            bounded.Tracker.GetTrackerSnapshot().ZeroSequenceDedupeCount,
            "zero-sequence 超容量后计数不得增长");
        AssertEx.True(
            bounded.Tracker.ApplyActionEffect(CreateAck(bounded, 400_000, 0)),
            "容量淘汰后的最旧 ActionId 应可重新接收");
        bounded.Tracker.EndCombat();
        AssertEx.Equal(
            0,
            bounded.Tracker.GetTrackerSnapshot().ZeroSequenceDedupeCount,
            "硬重置必须清空 zero-sequence 去重索引");

        var expired = CreateFixture();
        const uint expiredActionId = 600_000;
        AssertEx.True(
            expired.Tracker.ApplyActionEffect(CreateAck(expired, expiredActionId, 0)),
            "zero-sequence 过期测试首个 Ack 应接收");
        expired.Clock.Advance(151);
        AssertEx.True(
            expired.Tracker.ApplyActionEffect(CreateAck(expired, expiredActionId, 0)),
            "150ms 窗口过期后相同 ActionId 应重新接收");
        AssertEx.Equal(
            1,
            expired.Tracker.GetTrackerSnapshot().ZeroSequenceDedupeCount,
            "过期条目应从 zero-sequence 索引移除");

        var delayed = CreateFixture();
        const uint delayedActionId = 650_000;
        var firstReceivedAtMs = delayed.Clock.NowMs;
        AssertEx.True(
            delayed.Tracker.ApplyActionEffect(
                delayed.Tracker.CreateAckEnvelope(
                    100,
                    delayedActionId,
                    0,
                    BlmPhase.Fire,
                    firstReceivedAtMs)),
            "延迟处理测试首个 Ack 应及时接收");
        delayed.Clock.Advance(200);
        AssertEx.False(
            delayed.Tracker.ApplyActionEffect(
                delayed.Tracker.CreateAckEnvelope(
                    100,
                    delayedActionId,
                    0,
                    BlmPhase.Fire,
                    firstReceivedAtMs + 50)),
            "处理时钟虽晚 200ms，ReceivedAt 仅差 50ms 的重复仍必须拒绝");
        AssertEx.Equal(
            1,
            delayed.Tracker.GetTrackerSnapshot().ZeroSequenceDedupeCount,
            "延迟处理不得提前淘汰 ReceivedAt 窗口内条目");

        var serialSafe = CreateFixture();
        const uint repeatedActionId = 700_000;
        var firstAtMs = serialSafe.Clock.NowMs;
        AssertEx.True(
            serialSafe.Tracker.ApplyActionEffect(
                serialSafe.Tracker.CreateAckEnvelope(
                    100,
                    repeatedActionId,
                    0,
                    BlmPhase.Fire,
                    firstAtMs)),
            "serial-safe 首个 Ack 应接收");
        AssertEx.True(
            serialSafe.Tracker.ApplyActionEffect(
                serialSafe.Tracker.CreateAckEnvelope(
                    100,
                    repeatedActionId,
                    0,
                    BlmPhase.Fire,
                    firstAtMs + 200)),
            "同 ActionId 的新时间戳 Ack 应更新 latest");
        for (var index = 0; index < BlmStateTracker.ZeroSequenceDedupeCapacity - 1; index++)
        {
            AssertEx.True(
                serialSafe.Tracker.ApplyActionEffect(
                    CreateAck(serialSafe, (uint)(800_000 + index), 0)),
                $"serial-safe 填充 Ack {index} 应接收");
        }

        AssertEx.False(
            serialSafe.Tracker.ApplyActionEffect(
                serialSafe.Tracker.CreateAckEnvelope(
                    100,
                    repeatedActionId,
                    0,
                    BlmPhase.Fire,
                    firstAtMs + 200)),
            "淘汰旧 serial 时不得误删同 ActionId 的最新时间戳");
    }

    private static void AeAssistCooldownWindowBoundaries()
    {
        const long nowMs = 5000;
        const long lastGcdStartedAtMs = 3000;
        const int actionQueueInMs = 300;

        AssertEx.True(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                2999.999,
                1,
                nowMs,
                lastGcdStartedAtMs,
                actionQueueInMs),
            "冷却窗口公式边界前应返回 true");
        AssertEx.False(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                3000,
                1,
                nowMs,
                lastGcdStartedAtMs,
                actionQueueInMs),
            "冷却窗口恰好相等时必须按严格小于返回 false");

        var action = new BlmActionAvailability
        {
            CooldownRemainSeconds = 2.999f,
        };
        AssertEx.True(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                action,
                1,
                nowMs,
                lastGcdStartedAtMs,
                actionQueueInMs),
            "动作快照重载必须使用同一毫秒公式");

        var defaultContext = new BlmContext();
        AssertEx.Equal(300, defaultContext.ActionQueueWindowMs, "Context 队列窗口默认值应为 300ms");
        AssertEx.Equal(
            50,
            BlmDecisionPrimitives.NormalizeActionQueueWindowMs(49),
            "队列窗口应 clamp 到 50ms 下限");
        AssertEx.Equal(
            50,
            BlmDecisionPrimitives.NormalizeActionQueueWindowMs(50),
            "队列窗口 50ms 边界应保留");
        AssertEx.Equal(
            1000,
            BlmDecisionPrimitives.NormalizeActionQueueWindowMs(1000),
            "队列窗口 1000ms 边界应保留");
        AssertEx.Equal(
            1000,
            BlmDecisionPrimitives.NormalizeActionQueueWindowMs(1001),
            "队列窗口应 clamp 到 1000ms 上限");
        var accelerated = new BlmContext
        {
            CapturedAtMs = nowMs,
            ActionQueueWindowMs = 300,
            GcdTotalSeconds = 2f,
            GcdRemainSeconds = 1f,
        };
        AssertEx.True(accelerated.HasActiveGcd, "2.0s GCD 且 remain=1.0s 应视为活跃");
        AssertEx.Equal(1f, accelerated.GcdElapsedSeconds, "加速 GCD elapsed 推导错误");
        AssertEx.True(
            BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(
                accelerated.CapturedAtMs,
                accelerated.GcdTotalSeconds,
                accelerated.GcdRemainSeconds,
                out var acceleratedStartedAtMs),
            "活跃 GCD 应能推导起转时刻");
        AssertEx.Equal(4000L, acceleratedStartedAtMs, "2.0s GCD 起转时刻推导错误");

        var fixedWindowProof = new BlmActionAvailability
        {
            CooldownRemainSeconds = 1f,
        };
        AssertEx.True(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                accelerated,
                fixedWindowProof,
                0),
            "加速 GCD 只影响 elapsed，AE 业务窗口仍必须固定为 2500ms");

        var queue300Boundary = new BlmActionAvailability
        {
            CooldownRemainSeconds = 1.5f,
        };
        AssertEx.False(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                accelerated,
                queue300Boundary,
                0),
            "300ms 队列窗口的等式边界必须返回 false");
        AssertEx.True(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                accelerated with { ActionQueueWindowMs = 200 },
                queue300Boundary,
                0),
            "队列窗口从 300ms 改为 200ms 应扩大业务判定窗口");
        AssertEx.False(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                accelerated with { ActionQueueWindowMs = 200 },
                new BlmActionAvailability { CooldownRemainSeconds = 1.6f },
                0),
            "200ms 队列窗口的等式边界必须返回 false");

        var inactive = accelerated with { GcdRemainSeconds = 0f };
        AssertEx.False(inactive.HasActiveGcd, "remain=0 时不得伪造活跃 GCD");
        AssertEx.Equal(0f, inactive.GcdElapsedSeconds, "非活跃 GCD elapsed 应为零");
        AssertEx.False(
            BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(
                inactive.CapturedAtMs,
                inactive.GcdTotalSeconds,
                inactive.GcdRemainSeconds,
                out _),
            "remain=0 时 GCD 起点应未知");
        AssertEx.False(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                inactive,
                fixedWindowProof,
                0),
            "GCD 非活跃且 Tracker 无起点事实时必须返回 false");
        AssertEx.True(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                inactive with
                {
                    Tracker = new BlmTrackerSnapshot { LastGcdStartedAtMs = 4000 },
                },
                fixedWindowProof,
                0),
            "remain=0 时应回退 Tracker 保存的同一 GCD 起点");
        AssertEx.False(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                inactive with
                {
                    CapturedAtMs = 10_000,
                    Tracker = new BlmTrackerSnapshot { LastGcdStartedAtMs = 4000 },
                },
                new BlmActionAvailability(),
                0),
            "保存的 GCD 起点经过足够时间后应自然退出预测窗口");
        AssertEx.False(
            BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(nowMs, 0f, 0f, out _),
            "总时长未知时不得推导 GCD 起点");
        AssertEx.False(
            BlmDecisionPrimitives.TryGetCurrentGcdStartedAtMs(nowMs, 2f, 2.1f, out _),
            "remain 大于 total 时不得推导 GCD 起点");

        var tracked = CreateFixture();
        var activeTrackedContext = tracked.Tracker.GetContextSnapshot() with
        {
            CapturedAtMs = tracked.Clock.NowMs,
            GcdTotalSeconds = 2f,
            GcdRemainSeconds = 1f,
            Tracker = BlmTrackerSnapshot.Empty,
        };
        tracked.Tracker.Reconcile(activeTrackedContext);
        AssertEx.Equal(
            tracked.Clock.NowMs - 1000,
            tracked.Tracker.GetTrackerSnapshot().LastGcdStartedAtMs,
            "Tracker 应在活跃 GCD 时保存推导起点");
        tracked.Clock.Advance(500);
        tracked.Tracker.Reconcile(activeTrackedContext with
        {
            CapturedAtMs = tracked.Clock.NowMs,
            GcdRemainSeconds = 0f,
            Tracker = BlmTrackerSnapshot.Empty,
        });
        AssertEx.Equal(
            tracked.Clock.NowMs - 1500,
            tracked.Tracker.GetTrackerSnapshot().LastGcdStartedAtMs,
            "remain=0 时 Tracker 应保留上一活跃 Tick 的起点");
        tracked.Tracker.EndCombat();
        AssertEx.Equal(
            0L,
            tracked.Tracker.GetTrackerSnapshot().LastGcdStartedAtMs,
            "生命周期硬重置必须清空 GCD 起点事实");
        AssertEx.False(
            BlmDecisionPrimitives.AbilityCooldownInNextGcdWindows(
                tracked.Tracker.GetContextSnapshot(),
                fixedWindowProof,
                0),
            "硬重置后 inactive Context 不得使用旧 GCD 起点");
    }

    private static void ChargeAndAbilityReadyBoundaries()
    {
        AssertEx.Equal(
            500,
            BlmDecisionPrimitives.AeAssistAbilityRepeatGuardMs,
            "AE ability 重复保护窗口必须显式固定为 500ms");
        AssertEx.True(
            BlmDecisionPrimitives.RecentlyUsed(
                10_000,
                9501,
                BlmDecisionPrimitives.AeAssistAbilityRepeatGuardMs),
            "ability 重复保护在 499ms 时应命中");
        AssertEx.False(
            BlmDecisionPrimitives.RecentlyUsed(
                10_000,
                9500,
                BlmDecisionPrimitives.AeAssistAbilityRepeatGuardMs),
            "ability 重复保护恰好 500ms 时应过期");
        AssertEx.False(BlmDecisionPrimitives.HasReadyCharge(0.999f), "不足一层充能不可用");
        AssertEx.True(BlmDecisionPrimitives.HasReadyCharge(1f), "恰好一层充能可用");
        AssertEx.False(
            BlmDecisionPrimitives.HasAnyChargeProgress(0f),
            "零充能进度不得满足严格大于零的进冰保障条件");
        AssertEx.True(
            BlmDecisionPrimitives.HasAnyChargeProgress(0.001f),
            "任意正充能进度都应满足进冰保障条件");
        AssertEx.True(
            BlmDecisionPrimitives.HasAnyChargeProgress(1f),
            "完整一层充能也应满足进冰保障条件");
        AssertEx.True(
            BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                true,
                true,
                false,
                1f,
                10_000),
            "已有充能时不应受冷却剩余影响");
        AssertEx.True(
            BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                true,
                true,
                false,
                0.5f,
                100),
            "不足一层充能但冷却恰好 100ms 时应允许 ability 排队");
        AssertEx.False(
            BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                true,
                true,
                false,
                0.5f,
                100.001),
            "ability 冷却超过 100ms 时不可用");
        AssertEx.False(
            BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                true,
                true,
                true,
                1f,
                0),
            "RecentlyUsed 防重复必须优先于充能");
        AssertEx.False(
            BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                false,
                true,
                false,
                1f,
                0),
            "未解锁 ability 不可用");
        AssertEx.False(
            BlmDecisionPrimitives.IsAbilityReadyWithCanCast(
                true,
                false,
                false,
                1f,
                0),
            "CanCast 失败时 ability 不可用");
    }

    private static void InstantAndWeaveCapacityFacts()
    {
        AssertEx.False(BlmDecisionPrimitives.HasInstantCast(false, 0), "无 buff 时不可瞬发");
        AssertEx.True(BlmDecisionPrimitives.HasInstantCast(true, 0), "即刻 buff 应提供瞬发");
        AssertEx.True(BlmDecisionPrimitives.HasInstantCast(false, 1), "三连层数应提供瞬发");
        AssertEx.False(
            BlmDecisionPrimitives.WasGcdObservedInstant(1699.999f, false),
            "无加速时低于 1700ms 不得判为实测瞬发");
        AssertEx.True(
            BlmDecisionPrimitives.WasGcdObservedInstant(1700f, false),
            "无加速时恰好 1700ms 应判为实测瞬发");
        AssertEx.False(
            BlmDecisionPrimitives.WasGcdObservedInstant(1499.999f, true),
            "有加速时低于 1500ms 不得判为实测瞬发");
        AssertEx.True(
            BlmDecisionPrimitives.WasGcdObservedInstant(1500f, true),
            "有加速时恰好 1500ms 应判为实测瞬发");

        AssertEx.Equal(
            2,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.炽炎, 100, true)),
            "已确认瞬发 GCD 应有双插容量");
        foreach (var actionId in new[]
                 {
                     BLMSkill.悖论,
                     BLMSkill.高闪雷,
                     BLMSkill.高震雷,
                     BLMSkill.异言,
                     BLMSkill.秽浊,
                 })
        {
            AssertEx.Equal(
                2,
                BlmDecisionPrimitives.ResolverAllowedWeaves(
                    new BlmResolverAllowedWeavesInput(actionId, 100, false)),
                $"固有瞬发 GCD {actionId} 应有双插容量");
        }

        AssertEx.Equal(
            2,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.绝望, 100, false)),
            "Lv100 绝望应有双插容量");
        AssertEx.Equal(
            0,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.绝望, 99, false)),
            "Lv99 绝望不得套用 Lv100 瞬发事实");
        AssertEx.Equal(
            1,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.爆炎, 100, false)),
            "火三应保留单插容量");
        AssertEx.Equal(
            1,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.冰封, 100, false)),
            "冰三应保留单插容量");
        AssertEx.Equal(
            0,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.炽炎, 100, false)),
            "普通读条默认没有安全插入容量");
        AssertEx.Equal(
            1,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.炽炎, 100, false, true)),
            "减少动画锁环境应通过显式参数开放容量");
        AssertEx.Equal(
            0,
            BlmDecisionPrimitives.ResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(0, 100, true, true)),
            "没有上一 GCD 时任何参数都不得产生插入容量");
        AssertEx.Equal(
            1,
            BlmDecisionPrimitives.RemainingResolverAllowedWeaves(
                new BlmResolverAllowedWeavesInput(BLMSkill.悖论, 100, false),
                1),
            "已使用一次后双插窗口应剩余一次");

        AssertEx.Equal(
            0,
            BlmDecisionPrimitives.ExecutorWeaveSlots(false, true),
            "没有已确认 GCD 时执行器不得开放槽位");
        AssertEx.Equal(
            1,
            BlmDecisionPrimitives.ExecutorWeaveSlots(true, false),
            "普通已确认 GCD 后执行器应开放一个槽位");
        AssertEx.Equal(
            2,
            BlmDecisionPrimitives.ExecutorWeaveSlots(true, true),
            "已确认瞬发 GCD 后执行器应开放两个槽位");

        AssertEx.False(
            BlmDecisionPrimitives.CanWeaveNow(false, 0.6f, 0f),
            "GCD 剩余恰好 0.6s 不得视为插入窗口");
        AssertEx.True(
            BlmDecisionPrimitives.CanWeaveNow(false, 0.601f, 0f),
            "GCD 剩余超过 0.6s 应进入插入窗口");
        AssertEx.False(
            BlmDecisionPrimitives.CanWeaveNow(false, 0.75f, 0.7f),
            "动画锁安全边界必须使用严格大于");
        AssertEx.False(
            BlmDecisionPrimitives.CanWeaveNow(true, 2f, 0f),
            "读条中不得插入能力技");
    }

    private static Fixture CreateFixture(IBlmActionIdNormalizer? normalizer = null)
    {
        var clock = new FakeClock(10_000);
        normalizer ??= new MappingActionIdNormalizer();
        var context = new BlmContext
        {
            CapturedAtMs = clock.NowMs,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            IsAvailable = true,
            AcrState = AcrState.On,
            PlayerEntityId = 100,
            JobId = 25,
            Level = 100,
            Mp = 10_000,
            MaxMp = 10_000,
            InCombat = true,
            IsAlive = true,
            CanAct = true,
            Phase = BlmPhase.Fire,
            AfStacks = 3,
        };
        return new Fixture(
            clock,
            new BlmStateTracker(context, clock, normalizer));
    }

    private static BlmActionEffectAck CreateAck(
        Fixture fixture,
        uint actionId,
        uint globalSequence,
        long observedGcdStartedAtMs = 0,
        float observedGcdRemainMs = 0f,
        bool hasHasteAtAck = false)
        => fixture.Tracker.CreateAckEnvelope(
            100,
            actionId,
            globalSequence,
            BlmPhase.Fire,
            fixture.Clock.NowMs,
            observedGcdStartedAtMs,
            observedGcdRemainMs,
            hasHasteAtAck);

    private sealed record Fixture(FakeClock Clock, BlmStateTracker Tracker);
}
