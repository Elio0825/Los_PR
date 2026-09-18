using System.Collections.Immutable;
using System.Reflection;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;

namespace Los.Tests;

internal static class Phase4C0AbilityCoordinationTests
{
    private const long StateGeneration = 1;
    private const long NowMs = 40_000;
    private const uint PlayerId = 100;
    private const uint TargetId = 200;

    public static void RunAll()
    {
        AoeChannelGateKeepsAbilitiesAndBlocksEveryGcd();
        AoeTransposePlanCoversPhaseCooldownAndManafont();
        SwiftcastModeMatrix();
        TriplecastModeMatrix();
        ManafontModeMatrix();
        AoeInstantFillSelectionMatchesFrozenPriority();
    }

    private static void AoeChannelGateKeepsAbilitiesAndBlocksEveryGcd()
    {
        var facts = BaseInput().Context;
        AssertEx.True(facts.IsAoeMode, "AOE模式必须从单体模式纯派生");
        AssertEx.False(facts.IsTwoTargetAoe, "三目标不得误判为双目标");
        AssertEx.True(facts.IsThreePlusAoe, "三目标派生事实错误");
        var twoTargets = facts with { EnemyCount = 2 };
        AssertEx.True(twoTargets.IsTwoTargetAoe, "双目标派生事实错误");

        var input = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                IsMoving = true,
                PolyglotStacks = 1,
                HasThunderhead = true,
            },
            Settings = BaseInput().Settings with
            {
                TtkEnabled = true,
                DotEnabled = true,
                MoveXenoglossyEnabled = true,
            },
            IsIdle = true,
        };
        input = SetAction(input, BLMSkill.秽浊, ready: true);
        input = SetAction(input, BLMSkill.震雷, ready: true);
        var frame = Level100ResolverEngine.Evaluate(input);
        AssertEx.True(frame.GcdCandidate is null, "AOE TTK/雷云/移动通晓不得泄漏GCD");
        AssertEx.False(frame.DeliveryBlocked, "AOE健康帧不得被全帧Gate阻断");

        foreach (var order in new[] { 4, 8, 10, 11, 12, 13, 14 })
        {
            AssertEx.Equal(
                BlmResolverManifestDisposition.Active,
                Level100ResolverEngine.Manifest[order].Disposition,
                $"4C-1 Manifest order {order} 必须激活");
        }

        AssertEx.Equal(
            Level100ResolverEngine.FrozenManifestSha256,
            Level100ResolverEngine.ManifestSha256,
            "4C-1 Manifest哈希不匹配");
        AssertEx.Equal(
            "5D70EB5EF186407B6FC6798CF14DCE81313207A763B0C4F82CF667451B6756BB",
            Level100ResolverEngine.LosAeInstantGcdTriggerSha256,
            "AOE瞬发触发器规格源哈希必须冻结");

        var aoeHold = SetTranspose(
            BaseInput() with
            {
                Context = Fire(BaseInput(), mp: 799),
            },
            cooldownMs: 1000);
        var aoeFrame = Level100ResolverEngine.Evaluate(aoeHold);
        AssertEx.True(aoeFrame.HoldGcdForTranspose, "AOE纯计划应报告Hold");
        AssertEx.True(aoeFrame.GcdCandidate is null, "AOE Hold不得激活GCD");

        var singleInput = aoeHold with
        {
            Context = aoeHold.Context with
            {
                IsSingleTargetMode = true,
                EnemyCount = 1,
                Mp = 10_000,
            },
        };
        var singleFrame = Level100ResolverEngine.Evaluate(singleInput);
        AssertEx.True(singleFrame.GcdCandidate is not null, "AOE切回单体必须立即恢复单体GCD");
        AssertEx.False(singleFrame.HoldGcdForTranspose, "模式切换不得残留AOE等待状态");
    }

    private static void AoeTransposePlanCoversPhaseCooldownAndManafont()
    {
        var level57 = SetTranspose(BaseInput(57), cooldownMs: 0);
        AssertPlan(level57, "None", "None", 0, "57级AOE星灵锁定");

        var level58 = SetTranspose(BaseInput(58), cooldownMs: 0);
        AssertPlan(level58, "ToFire", "CastNow", 0, "58级AOE转火");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(level58).AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "58级AOE星灵Always");

        var heartsTwo = level58 with
        {
            Context = level58.Context with { UmbralHearts = 2 },
        };
        AssertPlan(heartsTwo, "None", "None", 0, "冰针2层不得转火");

        var ackAhead = WithHistory(
            heartsTwo with
            {
                Context = heartsTwo.Context with
                {
                    UmbralIceStacks = 1,
                    UmbralHearts = 0,
                },
            },
            Success(BLMSkill.玄冰, 1));
        AssertPlan(ackAhead, "ToFire", "CastNow", 0, "玄冰Ack领先Gauge");

        var recentOnly = ackAhead with { PreviousGcd = null };
        AssertPlan(recentOnly, "ToFire", "CastNow", 0, "2500ms内玄冰历史");

        var mp800 = SetTranspose(
            BaseInput() with { Context = Fire(BaseInput(), mp: 800) },
            cooldownMs: 0);
        AssertPlan(mp800, "None", "None", 0, "AOE转冰800MP边界");
        var mp799 = mp800 with { Context = mp800.Context with { Mp = 799 } };
        AssertPlan(mp799, "ToIce", "CastNow", 0, "AOE转冰799MP边界");

        var fullSoul = mp799 with
        {
            Context = mp799.Context with { AstralSoulStacks = 6 },
        };
        AssertPlan(fullSoul, "None", "None", 0, "100级满耀星低蓝先拒绝星灵");

        var manafontReady = SetAction(
            mp799 with
            {
                Settings = mp799.Settings with { ManafontEnabled = true },
                IsIdle = false,
            },
            BLMSkill.魔泉,
            ready: true);
        AssertPlan(
            manafontReady,
            "ToIce",
            "DeferToManafont",
            0,
            "Manafont Ready优先");

        var idleBreak = manafontReady with
        {
            Context = manafontReady.Context with { GcdRemainSeconds = 0.499f },
            IsIdle = true,
        };
        AssertPlan(idleBreak, "ToIce", "CastNow", 0, "Manafont Ready空等破局");

        var manafontSoon = SetAction(
            WithHistory(
                mp799 with
                {
                    Settings = mp799.Settings with { ManafontEnabled = true },
                },
                Success(BLMSkill.爆炎, 2)),
            BLMSkill.魔泉,
            ready: false,
            cooldownMs: 1000,
            unlocked: true);
        AssertPlan(
            manafontSoon,
            "ToIce",
            "DeferToManafont",
            0,
            "Manafont两GCD内转好");

        var manafontRecent = SetAction(
            mp799 with
            {
                Settings = mp799.Settings with { ManafontEnabled = true },
                RecentHistory =
                [
                    Success(BLMSkill.魔泉, 3, isGcd: false),
                ],
            },
            BLMSkill.魔泉,
            ready: false,
            cooldownMs: 50_000,
            unlocked: true);
        AssertPlan(
            manafontRecent,
            "ToIce",
            "DeferToManafont",
            0,
            "Manafont RecentlyUsed优先");

        var hold2000 = SetTranspose(BaseInput(), cooldownMs: 2000);
        AssertPlan(hold2000, "ToFire", "Hold", 0, "星灵CD 2000ms边界");
        AssertEx.True(
            Level100ResolverEngine.Evaluate(hold2000).HoldGcdForTranspose,
            "AOE Hold应投影诊断事实");

        var fill2001 = SetTranspose(
            BaseInput() with
            {
                Context = BaseInput().Context with { PolyglotStacks = 1 },
            },
            cooldownMs: 2001);
        fill2001 = SetAction(
            fill2001,
            BLMSkill.秽浊,
            ready: false,
            cooldownMs: 5000,
            unlocked: true);
        AssertPlan(fill2001, "ToFire", "Fill", BLMSkill.秽浊, "星灵CD 2001ms使用填充");
        AssertEx.False(
            Level100ResolverEngine.Evaluate(fill2001).HoldGcdForTranspose,
            "Fill计划不得报告Hold");

        var noFill = SetTranspose(BaseInput(), cooldownMs: 2001);
        AssertPlan(noFill, "ToFire", "Hold", 0, "星灵CD 2001ms无填充继续Hold");
    }

    private static void SwiftcastModeMatrix()
    {
        AssertEx.True(SwiftFrame(17, ttk: true).OffGcdCandidate is null, "17级AOE即刻必须拒绝");
        AssertCandidate(
            SwiftFrame(18, ttk: true).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "18级AOE TTK即刻");
        AssertCandidate(
            SwiftFrame(100, ttk: true).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "100级AOE TTK即刻");
        AssertEx.True(
            SwiftFrame(100, ttk: false).OffGcdCandidate is null,
            "普通AOE不得使用即刻恢复或快速耀星");
    }

    private static void TriplecastModeMatrix()
    {
        AssertEx.True(TripleFrame(65, ttk: true).OffGcdCandidate is null, "65级AOE三连锁定");
        AssertCandidate(
            TripleFrame(66, ttk: true).OffGcdCandidate,
            BLMSkill.三连咏唱,
            "Ability.三连咏唱",
            "66级AOE TTK三连");

        var moving = SetAction(
            WithHistory(
                BaseInput(66) with
                {
                    Context = Fire(BaseInput(66), mp: 10_000) with
                    {
                        IsMoving = true,
                        GcdStarvationMs = 1_500,
                        IsCasting = false,
                    },
                    Settings = BaseInput(66).Settings with
                    {
                        MoveTriplecastEnabled = true,
                    },
                },
                Success(BLMSkill.爆炎, 10)),
            BLMSkill.三连咏唱,
            ready: true);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(moving).OffGcdCandidate,
            BLMSkill.三连咏唱,
            "Ability.三连咏唱",
            "AOE独立移动织入三连");

        var afterFreeze = WithHistory(
            moving,
            Success(BLMSkill.玄冰, 11));
        AssertEx.True(
            Level100ResolverEngine.Evaluate(afterFreeze).OffGcdCandidate is null,
            "上一玄冰必须拒绝三连");

        var fullSoul = SetAction(
            BaseInput() with
            {
                Context = Fire(BaseInput(), mp: 799) with
                {
                    AstralSoulStacks = 6,
                },
            },
            BLMSkill.三连咏唱,
            ready: true);
        AssertEx.True(
            Level100ResolverEngine.Evaluate(fullSoul).OffGcdCandidate is null,
            "AOE满耀星低蓝必须拒绝三连");

        var iceEntry = SetAction(
            BaseInput() with
            {
                Context = BaseInput().Context with
                {
                    UmbralIceStacks = 1,
                    UmbralHearts = 0,
                },
                Settings = BaseInput().Settings with
                {
                    TriplecastIntoIceEnabled = true,
                },
            },
            BLMSkill.三连咏唱,
            ready: true);
        AssertEx.True(
            Level100ResolverEngine.Evaluate(iceEntry).OffGcdCandidate is null,
            "AOE不得落入单体100级三连进冰");

        var alreadyInstant = iceEntry with
        {
            Context = iceEntry.Context with { HasSwiftcast = true },
        };
        AssertEx.True(
            Level100ResolverEngine.Evaluate(alreadyInstant).OffGcdCandidate is null,
            "已有瞬发Buff必须拒绝三连");
    }

    private static void ManafontModeMatrix()
    {
        AssertEx.True(ManafontFrame(29, afStacks: 3, mp: 799, soul: 0, gcdMs: 500).OffGcdCandidate is null, "29级Manafont锁定");
        AssertCandidate(
            ManafontFrame(30, afStacks: 3, mp: 799, soul: 0, gcdMs: 500).OffGcdCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            "30级AOE Manafont");
        AssertEx.True(ManafontFrame(100, afStacks: 2, mp: 799, soul: 0, gcdMs: 500).OffGcdCandidate is null, "AF2不得Manafont");
        AssertEx.True(ManafontFrame(100, afStacks: 3, mp: 800, soul: 0, gcdMs: 500).OffGcdCandidate is null, "800MP不得Manafont");
        AssertEx.True(ManafontFrame(100, afStacks: 3, mp: 799, soul: 6, gcdMs: 500).OffGcdCandidate is null, "满耀星不得Manafont");
        AssertEx.True(ManafontFrame(100, afStacks: 3, mp: 799, soul: 0, gcdMs: 499).OffGcdCandidate is null, "AOE GCD 499ms不得Manafont");
        AssertCandidate(
            ManafontFrame(100, afStacks: 3, mp: 799, soul: 0, gcdMs: 500).OffGcdCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            "AOE GCD 500ms Manafont边界");
    }

    private static void AoeInstantFillSelectionMatchesFrozenPriority()
    {
        var polyglot = BaseInput() with
        {
            Context = BaseInput().Context with { PolyglotStacks = 1 },
        };
        polyglot = SetAction(
            polyglot,
            BLMSkill.秽浊,
            ready: false,
            cooldownMs: 5000,
            unlocked: true);
        AssertEx.Equal(
            BLMSkill.秽浊,
            SelectAoeFill(polyglot),
            "公共GCD冷却中仍应规划秽浊Fill");
        AssertEx.Equal(
            0u,
            SelectAoeFill(polyglot with
            {
                Actions = polyglot.Actions.Select(action =>
                    action.RequestedActionId == BLMSkill.秽浊
                        ? action with { IsUnlocked = false }
                        : action).ToImmutableArray(),
            }),
            "未解锁秽浊不得规划Fill");

        var thunder = BaseInput() with
        {
            Context = BaseInput().Context with { HasThunderhead = true },
            Settings = BaseInput().Settings with { DotEnabled = true },
        };
        thunder = SetAdjustedAction(
            thunder,
            BLMSkill.震雷,
            BLMSkill.高震雷,
            ready: false);
        AssertEx.Equal(
            BLMSkill.高震雷,
            SelectAoeFill(thunder),
            "公共GCD冷却中仍应规划等级调整后的震雷Fill");
        AssertEx.Equal(
            0u,
            SelectAoeFill(thunder with
            {
                Actions = thunder.Actions.Select(action =>
                    action.RequestedActionId == BLMSkill.震雷
                        ? action with { IsUnlocked = false }
                        : action).ToImmutableArray(),
            }),
            "未解锁震雷不得规划Fill");

        var twoTargetFirestarter = BaseInput() with
        {
            Context = Fire(BaseInput(), mp: 10_000) with
            {
                EnemyCount = 2,
                AstralFireStacks = 2,
                HasFirestarter = true,
            },
        };
        AssertEx.Equal(BLMSkill.爆炎, SelectAoeFill(twoTargetFirestarter), "双目标火苗填充错误");
        var threeTarget = SetAction(
            twoTargetFirestarter with
            {
                Context = twoTargetFirestarter.Context with { EnemyCount = 3 },
            },
            MageUniversalSkill.即刻咏唱,
            ready: false,
            cooldownMs: 0,
            unlocked: true);
        AssertEx.Equal(0u, SelectAoeFill(threeTarget), "三目标不得使用双目标火苗分支");

        var paradox = BaseInput() with
        {
            Context = Fire(BaseInput(), mp: 4100) with { HasParadox = true },
        };
        AssertEx.Equal(BLMSkill.悖论, SelectAoeFill(paradox), "AOE火悖论4100MP边界错误");
        AssertEx.Equal(
            0u,
            SelectAoeFill(paradox with { Context = paradox.Context with { Mp = 4099 } }),
            "AOE火悖论4099MP必须拒绝");

        var iceParadox = BaseInput() with
        {
            Context = BaseInput().Context with { HasParadox = true },
        };
        AssertEx.Equal(
            BLMSkill.悖论,
            SelectAoeFill(iceParadox),
            "跳过策略废弃后AOE冰悖论必须始终作为填充");

        var despair = BaseInput() with
        {
            Context = Fire(BaseInput(), mp: 800),
        };
        AssertEx.Equal(BLMSkill.绝望, SelectAoeFill(despair), "AOE低蓝绝望填充错误");
    }

    private static BlmDecisionFrame SwiftFrame(int level, bool ttk)
    {
        var input = BaseInput(level) with
        {
            Settings = BaseInput(level).Settings with { TtkEnabled = ttk },
        };
        return Level100ResolverEngine.Evaluate(SetAction(
            input,
            MageUniversalSkill.即刻咏唱,
            ready: true));
    }

    private static BlmDecisionFrame TripleFrame(int level, bool ttk)
    {
        var input = BaseInput(level) with
        {
            Settings = BaseInput(level).Settings with { TtkEnabled = ttk },
        };
        return Level100ResolverEngine.Evaluate(SetAction(
            input,
            BLMSkill.三连咏唱,
            ready: true));
    }

    private static BlmDecisionFrame ManafontFrame(
        int level,
        int afStacks,
        long mp,
        int soul,
        int gcdMs)
    {
        var input = WithHistory(
            BaseInput(level) with
            {
                Context = Fire(BaseInput(level), mp) with
                {
                    AstralFireStacks = afStacks,
                    AstralSoulStacks = soul,
                    GcdRemainSeconds = gcdMs / 1000f,
                },
                Settings = BaseInput(level).Settings with
                {
                    ManafontEnabled = true,
                },
            },
            Success(BLMSkill.爆炎, 20));
        return Level100ResolverEngine.Evaluate(SetAction(
            input,
            BLMSkill.魔泉,
            ready: level >= 30,
            unlocked: level >= 30));
    }

    private static BlmResolverInput BaseInput(int level = 100)
        => new()
        {
            StateGeneration = StateGeneration,
            Context = new BlmResolverContextFacts
            {
                CapturedAtMs = NowMs,
                IsAvailable = true,
                AcrEnabled = true,
                PlayerEntityId = PlayerId,
                Level = level,
                Mp = 10_000,
                MaxMp = 10_000,
                InCombat = true,
                IsAlive = true,
                CanAct = true,
                IsSingleTargetMode = false,
                EnemyCount = 3,
                HasTarget = true,
                CanUseAttackActionOnTarget = true,
                CurrentTargetId = TargetId,
                ActionQueueWindowMs = 300,
                GcdTotalSeconds = 2.5f,
                GcdRemainSeconds = 1.5f,
                Phase = BlmPhase.Ice,
                UmbralIceStacks = 3,
                UmbralHearts = 3,
                MaxPolyglotStacks = level >= 98 ? 3 : level >= 80 ? 2 : level >= 70 ? 1 : 0,
            },
            Settings = BlmResolverSettings.Default with
            {
                ManafontEnabled = false,
                TtkEnabled = false,
                DotEnabled = false,
                DoubleDotEnabled = false,
                MoveXenoglossyEnabled = false,
                MoveTriplecastEnabled = false,
                AmplifierEnabled = false,
                LeyLinesEnabled = false,
                AutoMitigationEnabled = false,
                PotionEnabled = false,
            },
            Actions = DefaultActions(),
            DotTargets =
            [
                new BlmResolverDotTargetFact
                {
                    EntityId = TargetId,
                    IsValid = true,
                    IsTargetable = true,
                    IsAlive = true,
                    CanUseAttackActionOn = true,
                    IsInDotRange = true,
                    CurrentHp = 1_000_000,
                    MaxHp = 1_000_000,
                },
            ],
            FactCoverage = BlmResolverFactCoverage.Phase3A,
        };

    private static BlmResolverContextFacts Fire(
        BlmResolverInput input,
        long mp)
        => input.Context with
        {
            Phase = BlmPhase.Fire,
            Mp = mp,
            AstralFireStacks = 3,
            UmbralIceStacks = 0,
            UmbralHearts = 0,
        };

    private static ImmutableArray<BlmResolverActionFact> DefaultActions()
        =>
        [
            ReadyAction(BLMSkill.爆炎),
            ReadyAction(BLMSkill.悖论),
            ReadyAction(BLMSkill.绝望),
            UnavailableAction(BLMSkill.秽浊),
            UnavailableAction(BLMSkill.震雷),
            UnavailableAction(BLMSkill.星灵移位),
            UnavailableAction(MageUniversalSkill.即刻咏唱),
            UnavailableAction(BLMSkill.三连咏唱),
            UnavailableAction(MageUniversalSkill.醒梦),
            UnavailableAction(BLMSkill.详述),
            UnavailableAction(BLMSkill.魔泉),
            UnavailableAction(BLMSkill.黑魔纹),
        ];

    private static BlmResolverActionFact ReadyAction(uint actionId)
        => new()
        {
            RequestedActionId = actionId,
            AdjustedActionId = actionId,
            IsUnlocked = true,
            CanCast = true,
            Charges = 1f,
            MaxCharges = 1,
        };

    private static BlmResolverActionFact UnavailableAction(uint actionId)
        => new()
        {
            RequestedActionId = actionId,
            AdjustedActionId = actionId,
            IsUnlocked = false,
            CanCast = false,
            Charges = 0f,
            MaxCharges = 1,
            CooldownRemainMs = 10_000d,
        };

    private static BlmResolverInput SetTranspose(
        BlmResolverInput input,
        double cooldownMs)
        => SetAction(
            input,
            BLMSkill.星灵移位,
            ready: cooldownMs <= 0d,
            cooldownMs: cooldownMs,
            unlocked: true);

    private static BlmResolverInput SetAction(
        BlmResolverInput input,
        uint actionId,
        bool ready,
        double? cooldownMs = null,
        bool unlocked = true)
        => input with
        {
            Actions = input.Actions.Select(action =>
                action.RequestedActionId == actionId
                    ? action with
                    {
                        IsUnlocked = unlocked,
                        CanCast = ready,
                        Charges = ready ? 1f : 0f,
                        CooldownRemainMs = cooldownMs ?? (ready ? 0d : 10_000d),
                    }
                    : action).ToImmutableArray(),
        };

    private static BlmResolverInput SetAdjustedAction(
        BlmResolverInput input,
        uint requestedId,
        uint adjustedId,
        bool ready)
        => input with
        {
            Actions = input.Actions.Select(action =>
                action.RequestedActionId == requestedId
                    ? action with
                    {
                        AdjustedActionId = adjustedId,
                        IsUnlocked = true,
                        CanCast = ready,
                        Charges = ready ? 1f : 0f,
                        CooldownRemainMs = ready ? 0d : 10_000d,
                    }
                    : action).ToImmutableArray(),
        };

    private static BlmResolverInput WithHistory(
        BlmResolverInput input,
        params BlmActionSuccess[] history)
        => input with
        {
            RecentHistory = history.ToImmutableArray(),
            PreviousGcd = history
                .Where(success => success.IsGcd)
                .OrderByDescending(success => success.Serial)
                .Cast<BlmActionSuccess?>()
                .FirstOrDefault(),
        };

    private static BlmActionSuccess Success(
        uint actionId,
        long serial,
        bool isGcd = true)
        => new(
            StateGeneration,
            serial,
            actionId,
            actionId,
            actionId,
            TargetId,
            NowMs - 500,
            NowMs - 400,
            false,
            isGcd);

    private static uint SelectAoeFill(BlmResolverInput input)
    {
        var type = typeof(Level100ResolverEngine).Assembly.GetType(
            "LosPr.BLM.Resolvers.Level100.Level100SingleTargetResolvers")
            ?? throw new InvalidOperationException("找不到GCD Resolver类型");
        var method = type.GetMethod(
            "SelectAvailableAoeInstantGcdForTranspose",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到AOE瞬发填充选择器");
        return (uint)method.Invoke(null, new object?[] { input })!;
    }

    private static (string Direction, string Disposition, uint FillActionId) Plan(
        BlmResolverInput input)
    {
        var type = typeof(Level100ResolverEngine).Assembly.GetType(
            "LosPr.BLM.Resolvers.Level100.Level100AbilityResolvers")
            ?? throw new InvalidOperationException("找不到能力技Resolver类型");
        var method = type.GetMethod(
            "BuildAoeTransposePlan",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到AOE星灵纯计划");
        var plan = method.Invoke(null, new object?[] { input })
            ?? throw new InvalidOperationException("AOE星灵计划为空");
        var planType = plan.GetType();
        return (
            planType.GetProperty("Direction")!.GetValue(plan)!.ToString()!,
            planType.GetProperty("Disposition")!.GetValue(plan)!.ToString()!,
            (uint)planType.GetProperty("FillActionId")!.GetValue(plan)!);
    }

    private static void AssertPlan(
        BlmResolverInput input,
        string direction,
        string disposition,
        uint fillActionId,
        string scenario)
    {
        var plan = Plan(input);
        AssertEx.Equal(direction, plan.Direction, $"{scenario}方向错误");
        AssertEx.Equal(disposition, plan.Disposition, $"{scenario}处置错误");
        AssertEx.Equal(fillActionId, plan.FillActionId, $"{scenario}填充动作错误");
    }

    private static void AssertCandidate(
        BlmResolverCandidate? candidate,
        uint actionId,
        string resolverId,
        string scenario)
    {
        AssertEx.True(candidate is not null, $"{scenario}必须产生候选");
        AssertEx.Equal(actionId, candidate!.ActionId, $"{scenario}动作错误");
        AssertEx.Equal(resolverId, candidate.ResolverId, $"{scenario}Resolver错误");
    }
}
