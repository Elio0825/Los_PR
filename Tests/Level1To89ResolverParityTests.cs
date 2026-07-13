using System.Collections.Immutable;
using System.Reflection;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;

namespace Los.Tests;

internal static class Level1To89ResolverParityTests
{
    private const long StateGeneration = 1;
    private const long NowMs = 30_000;
    private const uint PlayerId = 100;
    private const uint TargetId = 200;

    public static void RunAll()
    {
        FrozenSourcesAndManifestAreActive();
        LevelBoundariesRouteToOneResolver();
        NeutralIceAndFireBranchesMatchSnapshot();
        Level60IceAckWinsOverLaggingGauge();
        MpAndFire4CountBoundariesMatchSnapshot();
        MovementAndSpecialSequenceRemainFailClosed();
        ManafontAlwaysBridgeUsesLevelSpecificIceReturn();
        SharedAbilityLevelBoundaries();
        SmartAoeLevelGateAndFailClosedAoeEntries();
    }

    private static void FrozenSourcesAndManifestAreActive()
    {
        AssertEx.Equal(
            "1C0CFAD8A08805152ACB09A0D57F6B99B82839B054BCF1589AC3CED61726371B",
            Level100ResolverEngine.LosAeLevel72SingleTargetSha256,
            "单体72–89规格源哈希必须冻结");
        AssertEx.Equal(
            "9A9BA4E0754528688DB8BDDD6D0EB20F2040C614274944526BD8EA65AE3F7649",
            Level100ResolverEngine.LosAeLevel60SingleTargetSha256,
            "单体60–71规格源哈希必须冻结");
        AssertEx.Equal(
            "D05DE5AA2D38DD1ACFA26D7187DAAB93DAEA2432F975F9DF348EF4E3F3C1F06F",
            Level100ResolverEngine.LosAeLevel35SingleTargetSha256,
            "单体35–59规格源哈希必须冻结");
        AssertEx.Equal(
            "3349A6E1E3763ADB929BAE2B3E9B7B315A93A26AF008D9E697A368F6158769B3",
            Level100ResolverEngine.LosAeLevel1SingleTargetSha256,
            "单体1–34规格源哈希必须冻结");
        AssertEx.Equal(
            Level100ResolverEngine.FrozenManifestSha256,
            Level100ResolverEngine.ManifestSha256,
            "4B manifest规范化哈希不匹配");

        for (var order = 17; order <= 20; order++)
        {
            AssertEx.Equal(
                BlmResolverManifestDisposition.Active,
                Level100ResolverEngine.Manifest[order].Disposition,
                $"order {order} 的低等级单体Resolver必须激活");
        }
    }

    private static void LevelBoundariesRouteToOneResolver()
    {
        var boundaries = new[]
        {
            (Level: 1, Resolver: "GCD.单体1_34", Order: 20, Action: BLMSkill.火炎),
            (Level: 34, Resolver: "GCD.单体1_34", Order: 20, Action: BLMSkill.火炎),
            (Level: 35, Resolver: "GCD.单体35_59", Order: 19, Action: BLMSkill.火炎),
            (Level: 59, Resolver: "GCD.单体35_59", Order: 19, Action: BLMSkill.火炎),
            (Level: 60, Resolver: "GCD.单体60_71", Order: 18, Action: BLMSkill.炽炎),
            (Level: 71, Resolver: "GCD.单体60_71", Order: 18, Action: BLMSkill.炽炎),
            (Level: 72, Resolver: "GCD.单体72_89", Order: 17, Action: BLMSkill.炽炎),
            (Level: 89, Resolver: "GCD.单体72_89", Order: 17, Action: BLMSkill.炽炎),
            (Level: 90, Resolver: "GCD.单体90_99", Order: 16, Action: BLMSkill.炽炎),
            (Level: 100, Resolver: "GCD.单体100", Order: 15, Action: BLMSkill.炽炎),
        };

        foreach (var boundary in boundaries)
        {
            AssertCandidate(
                Level100ResolverEngine.Evaluate(BaseInput(boundary.Level)).GcdCandidate,
                boundary.Action,
                boundary.Resolver,
                boundary.Order,
                $"{boundary.Level}级路由");
        }
    }

    private static void NeutralIceAndFireBranchesMatchSnapshot()
    {
        var level1 = BaseInput(1);
        AssertGcd(
            Evaluate(level1, level1.Context with
            {
                Phase = BlmPhase.Neutral,
                Mp = 10_000,
            }),
            BLMSkill.火炎,
            "1级满蓝Neutral");
        AssertGcd(
            Evaluate(level1, level1.Context with
            {
                Phase = BlmPhase.Neutral,
                Mp = 9_999,
            }),
            BLMSkill.冰结,
            "1级非满蓝Neutral");

        var level35 = BaseInput(35);
        AssertGcd(
            Evaluate(level35, Ice(level35, stacks: 2, hearts: 0)),
            BLMSkill.冰封,
            "35级UI2补冰层");
        AssertGcd(
            Evaluate(level35, Ice(level35, stacks: 3, hearts: 0) with { Mp = 9_999 }),
            BLMSkill.冰结,
            "35级UI3回蓝");
        AssertGcd(
            Evaluate(level35, Ice(level35, stacks: 3, hearts: 0) with { Mp = 10_000 }),
            BLMSkill.爆炎,
            "35级满蓝转火");
        AssertGcd(
            Evaluate(level35, level35.Context with
            {
                Phase = BlmPhase.Neutral,
                AstralFireStacks = 0,
            }),
            BLMSkill.爆炎,
            "35级Neutral转火");

        foreach (var level in new[] { 60, 72 })
        {
            var input = BaseInput(level);
            AssertGcd(
                Evaluate(input, Ice(input, stacks: 2, hearts: 0)),
                BLMSkill.冰封,
                $"{level}级UI2补冰层");
            AssertGcd(
                Evaluate(input, Ice(input, stacks: 3, hearts: 2)),
                BLMSkill.冰澈,
                $"{level}级UI3补冰针");
            AssertGcd(
                Evaluate(input, Ice(input, stacks: 3, hearts: 3)),
                BLMSkill.爆炎,
                $"{level}级冰资源完整转火");
            AssertGcd(
                Evaluate(input, input.Context with
                {
                    Phase = BlmPhase.Neutral,
                    AstralFireStacks = 0,
                    UmbralHearts = 0,
                }),
                BLMSkill.冰封,
                $"{level}级Neutral恢复");
        }
    }

    private static void MpAndFire4CountBoundariesMatchSnapshot()
    {
        var level1 = BaseInput(34) with
        {
            Context = BaseInput(34).Context with { UmbralHearts = 0 },
        };
        AssertGcd(Evaluate(level1, level1.Context with { Mp = 1_599 }), BLMSkill.冰结, "34级火一蓝量下界");
        AssertGcd(Evaluate(level1, level1.Context with { Mp = 1_600 }), BLMSkill.火炎, "34级火一蓝量边界");

        var level35 = BaseInput(59) with
        {
            Context = BaseInput(59).Context with { UmbralHearts = 0 },
        };
        AssertGcd(Evaluate(level35, level35.Context with { Mp = 1_599 }), BLMSkill.冰封, "59级火一蓝量下界");
        AssertGcd(Evaluate(level35, level35.Context with { Mp = 1_600 }), BLMSkill.火炎, "59级火一蓝量边界");
        AssertGcd(
            Evaluate(level35, level35.Context with { Mp = 0, HasFirestarter = true }),
            BLMSkill.爆炎,
            "59级火苗优先火三");

        var level60 = BaseInput(71) with
        {
            Context = BaseInput(71).Context with { UmbralHearts = 0 },
        };
        AssertGcd(Evaluate(level60, level60.Context with { Mp = 1_599 }), BLMSkill.冰封, "71级火四蓝量下界");
        AssertGcd(Evaluate(level60, level60.Context with { Mp = 1_600 }), BLMSkill.炽炎, "71级火四蓝量边界");
        AssertGcd(
            Evaluate(level60, level60.Context with { Mp = 800, UmbralHearts = 1 }),
            BLMSkill.炽炎,
            "71级冰针火四蓝量边界");

        var level72 = BaseInput(89) with
        {
            Context = BaseInput(89).Context with { UmbralHearts = 0 },
        };
        AssertGcd(Evaluate(level72, level72.Context with { Mp = 2_399 }), BLMSkill.绝望, "89级火四保留绝望预算");
        AssertGcd(Evaluate(level72, level72.Context with { Mp = 2_400 }), BLMSkill.炽炎, "89级火四加绝望预算边界");
        AssertGcd(
            Evaluate(level72, level72.Context with { Mp = 1_600, UmbralHearts = 1 }),
            BLMSkill.炽炎,
            "89级冰针火四加绝望预算边界");
        AssertGcd(
            Level100ResolverEngine.Evaluate(level72 with
            {
                Level100Loop = new BlmLevel100LoopFacts { Fire4Count = 7 },
            }),
            BLMSkill.绝望,
            "89级Tracker已确认七发火四");
        AssertGcd(
            Level100ResolverEngine.Evaluate(level72 with
            {
                Level100Loop = new BlmLevel100LoopFacts { Fire4Count = 6 },
            }),
            BLMSkill.炽炎,
            "89级Tracker已确认六发火四");
        AssertGcd(Evaluate(level72, level72.Context with { Mp = 799 }), BLMSkill.冰封, "89级不足绝望蓝量回冰");
    }

    private static void Level60IceAckWinsOverLaggingGauge()
    {
        var input = BaseInput(60) with
        {
            Context = Ice(BaseInput(60), stacks: 1, hearts: 2),
        };
        input = WithHistory(input, Success(BLMSkill.冰封, 1, wasInstant: false));
        AssertGcd(
            Level100ResolverEngine.Evaluate(input),
            BLMSkill.冰澈,
            "60级冰封Ack领先Gauge且冰针不足");

        var fullHearts = input with
        {
            Context = input.Context with
            {
                UmbralIceStacks = 0,
                UmbralHearts = 3,
            },
        };
        AssertGcd(
            Level100ResolverEngine.Evaluate(fullHearts),
            BLMSkill.爆炎,
            "60级冰封Ack领先Gauge且冰针已满");
    }

    private static void MovementAndSpecialSequenceRemainFailClosed()
    {
        foreach (var level in new[] { 1, 35, 60, 72, 89 })
        {
            var input = BaseInput(level);
            var moving = Evaluate(input, input.Context with { IsMoving = true });
            AssertEx.True(moving.GcdCandidate is null, $"{level}级无瞬发移动必须拒绝主循环");

            AssertEx.True(
                Evaluate(input, input.Context with
                {
                    IsMoving = true,
                    HasSwiftcast = true,
                }).GcdCandidate is not null,
                $"{level}级持有瞬发移动不得停转");

            var special = Level100ResolverEngine.Evaluate(input with
            {
                SpecialSequenceActive = true,
            });
            AssertEx.True(special.GcdCandidate is null, $"{level}级特殊序列必须优先");
            AssertEx.True(special.DeliveryBlocked, $"{level}级特殊序列必须阻断交付");
        }
    }

    private static void ManafontAlwaysBridgeUsesLevelSpecificIceReturn()
    {
        foreach (var level in new[] { 30, 34, 35, 59, 60, 71, 72, 89, 90, 100 })
        {
            var input = BaseInput(level) with
            {
                Context = BaseInput(level).Context with
                {
                    Mp = 0,
                    UmbralHearts = 0,
                    AstralSoulStacks = 0,
                    IsCasting = true,
                    GcdRemainSeconds = 0.9f,
                },
                Settings = BaseInput(level).Settings with { ManafontEnabled = true },
            };
            input = SetAction(input, BLMSkill.魔泉, ready: true);
            input = WithHistory(input, Success(BLMSkill.火炎, 1, wasInstant: true));

            var frame = Level100ResolverEngine.Evaluate(input);
            var expectedIce = level < 35 ? BLMSkill.冰结 : BLMSkill.冰封;
            AssertGcd(frame, expectedIce, $"{level}级Manafont前回冰候选");
            AssertCandidate(
                frame.OffGcdCandidate,
                BLMSkill.魔泉,
                "Ability.墨泉",
                27,
                $"{level}级Manafont候选");
            AssertCandidate(
                frame.AlwaysBridgeCandidate,
                BLMSkill.魔泉,
                "Ability.墨泉",
                27,
                $"{level}级Manafont Always桥");
            AssertEx.True(frame.GcdBlockedByAlwaysBridge, $"{level}级Manafont桥必须阻止回冰GCD");
        }
    }

    private static void SharedAbilityLevelBoundaries()
    {
        AssertEx.True(SwiftcastFrame(17, ttk: true, iceStacks: 0).OffGcdCandidate is null, "17级即刻必须锁定");
        AssertCandidate(
            SwiftcastFrame(18, ttk: true, iceStacks: 0).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            23,
            "18级TTK即刻");
        AssertCandidate(
            SwiftcastFrame(49, ttk: true, iceStacks: 0).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            23,
            "49级TTK即刻");
        AssertEx.True(SwiftcastFrame(49, ttk: false, iceStacks: 1).OffGcdCandidate is null, "49级非TTK不得即刻恢复");
        AssertCandidate(
            SwiftcastFrame(50, ttk: false, iceStacks: 1).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            23,
            "50级UI1即刻恢复");

        AssertEx.True(TriplecastFrame(65).OffGcdCandidate is null, "65级三连必须锁定");
        AssertCandidate(
            TriplecastFrame(66).OffGcdCandidate,
            BLMSkill.三连咏唱,
            "Ability.三连咏唱",
            24,
            "66级TTK三连");

        AssertEx.True(AmplifierFrame(85, stacks: 0, timerMs: 10_000).OffGcdCandidate is null, "85级详述必须锁定");
        AssertEx.True(AmplifierFrame(86, stacks: 2, timerMs: 10_000).OffGcdCandidate is null, "86级两层通晓时详述必须拒绝");
        AssertEx.True(AmplifierFrame(86, stacks: 1, timerMs: 3_999).OffGcdCandidate is null, "86级即将涨满时详述必须拒绝");
        AssertCandidate(
            AmplifierFrame(86, stacks: 1, timerMs: 4_000).OffGcdCandidate,
            BLMSkill.详述,
            "Ability.详述",
            26,
            "86级详述计时边界");

        var lowLeyLines = SetAction(
            BaseInput(89) with
            {
                Settings = BaseInput(89).Settings with { LeyLinesEnabled = true },
            },
            BLMSkill.黑魔纹,
            ready: true);
        AssertEx.True(
            Level100ResolverEngine.Evaluate(lowLeyLines).OffGcdCandidate is null,
            "52–89级黑魔纹在缺少日常事实时必须生产fail closed");

        var level90LeyLines = SetAction(
            BaseInput(90) with
            {
                Settings = BaseInput(90).Settings with { LeyLinesEnabled = true },
            },
            BLMSkill.黑魔纹,
            ready: true);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(level90LeyLines).OffGcdCandidate,
            BLMSkill.黑魔纹,
            "Ability.黑魔纹",
            28,
            "90级黑魔纹兼容");

        var lockedLucid = SetAction(
            BaseInput(100) with
            {
                Settings = BaseInput(100).Settings with { TtkEnabled = true },
            },
            MageUniversalSkill.醒梦,
            ready: true,
            unlocked: false);
        AssertEx.True(
            Level100ResolverEngine.Evaluate(lockedLucid).OffGcdCandidate is null,
            "醒梦必须校验动作解锁事实");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(SetAction(
                lockedLucid,
                MageUniversalSkill.醒梦,
                ready: true,
                unlocked: true)).OffGcdCandidate,
            MageUniversalSkill.醒梦,
            "Ability.醒梦",
            25,
            "醒梦动作事实就绪");
    }

    private static void SmartAoeLevelGateAndFailClosedAoeEntries()
    {
        AssertEx.False(ShouldUseAoeMode(11, 3, smart: true), "11级三目标不得切AOE");
        AssertEx.True(ShouldUseAoeMode(12, 3, smart: true), "12级三目标必须切AOE");
        AssertEx.False(ShouldUseAoeMode(57, 2, smart: true), "57级双目标不得切AOE");
        AssertEx.True(ShouldUseAoeMode(58, 2, smart: true), "58级双目标必须切AOE");
        AssertEx.False(ShouldUseAoeMode(100, 3, aoeEnabled: false, smart: true), "关闭AOE时不得切换");

        for (var order = 10; order <= 14; order++)
        {
            AssertEx.Equal(
                BlmResolverManifestDisposition.RejectSingleTarget,
                Level100ResolverEngine.Manifest[order].Disposition,
                $"4B AOE order {order} 必须保持fail closed");
        }

        var lowLevelSingleTarget = Level100ResolverEngine.Evaluate(BaseInput(11));
        AssertEx.True(lowLevelSingleTarget.GcdCandidate is not null, "低于AOE门槛必须继续单体而非停转");

        var aoeFrame = Evaluate(
            BaseInput(12),
            BaseInput(12).Context with { IsSingleTargetMode = false });
        AssertEx.True(aoeFrame.GcdCandidate is null, "AOE模式不得误入单体Resolver");
        AssertEx.False(aoeFrame.DeliveryBlocked, "4C-0 AOE健康帧不得阻断能力技通道");
    }

    private static BlmDecisionFrame SwiftcastFrame(int level, bool ttk, int iceStacks)
    {
        var input = BaseInput(level) with
        {
            Context = Ice(BaseInput(level), iceStacks, hearts: 0),
            Settings = BaseInput(level).Settings with { TtkEnabled = ttk },
        };
        return Level100ResolverEngine.Evaluate(SetAction(
            input,
            MageUniversalSkill.即刻咏唱,
            ready: true));
    }

    private static BlmDecisionFrame TriplecastFrame(int level)
    {
        var input = BaseInput(level) with
        {
            Settings = BaseInput(level).Settings with { TtkEnabled = true },
        };
        return Level100ResolverEngine.Evaluate(SetAction(
            input,
            BLMSkill.三连咏唱,
            ready: true));
    }

    private static BlmDecisionFrame AmplifierFrame(int level, int stacks, int timerMs)
    {
        var input = BaseInput(level) with
        {
            Context = BaseInput(level).Context with
            {
                PolyglotStacks = stacks,
                MaxPolyglotStacks = level >= 98 ? 3 : 2,
                PolyglotTimerMs = timerMs,
            },
            Settings = BaseInput(level).Settings with { AmplifierEnabled = true },
        };
        return Level100ResolverEngine.Evaluate(SetAction(
            input,
            BLMSkill.详述,
            ready: true));
    }

    private static bool ShouldUseAoeMode(
        int level,
        int enemyCount,
        bool aoeEnabled = true,
        bool smart = true)
    {
        var method = typeof(BlmContext).GetMethod(
            "ShouldUseAoeMode",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到等级感知智能AOE门");
        return (bool)method.Invoke(
            null,
            new object[] { level, enemyCount, aoeEnabled, smart })!;
    }

    private static BlmResolverInput BaseInput(int level)
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
                IsSingleTargetMode = true,
                HasTarget = true,
                CanUseAttackActionOnTarget = true,
                CurrentTargetId = TargetId,
                ActionQueueWindowMs = 300,
                GcdTotalSeconds = 2.5f,
                GcdRemainSeconds = 1.5f,
                Phase = BlmPhase.Fire,
                AstralFireStacks = 3,
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
            Level100Loop = new BlmLevel100LoopFacts(),
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

    private static BlmResolverContextFacts Ice(
        BlmResolverInput input,
        int stacks,
        int hearts)
        => input.Context with
        {
            Phase = BlmPhase.Ice,
            AstralFireStacks = 0,
            UmbralIceStacks = stacks,
            UmbralHearts = hearts,
            AstralSoulStacks = 0,
            HasFirestarter = false,
        };

    private static ImmutableArray<BlmResolverActionFact> DefaultActions()
        =>
        [
            ReadyAction(BLMSkill.火炎),
            ReadyAction(BLMSkill.冰结),
            ReadyAction(BLMSkill.爆炎),
            ReadyAction(BLMSkill.冰封),
            ReadyAction(BLMSkill.冰澈),
            ReadyAction(BLMSkill.炽炎),
            ReadyAction(BLMSkill.绝望),
            ReadyAction(BLMSkill.悖论),
            ReadyAction(BLMSkill.异言),
            ReadyAction(BLMSkill.高闪雷),
            ReadyAction(BLMSkill.星灵移位),
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

    private static BlmResolverInput SetAction(
        BlmResolverInput input,
        uint actionId,
        bool ready,
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
        bool wasInstant)
        => new(
            StateGeneration,
            serial,
            actionId,
            actionId,
            actionId,
            TargetId,
            NowMs - 2_000 + serial,
            NowMs - 1_900 + serial,
            wasInstant,
            true);

    private static BlmDecisionFrame Evaluate(
        BlmResolverInput input,
        BlmResolverContextFacts context)
        => Level100ResolverEngine.Evaluate(input with { Context = context });

    private static void AssertGcd(
        BlmDecisionFrame frame,
        uint actionId,
        string scenario)
    {
        AssertEx.True(frame.GcdCandidate is not null, $"{scenario}必须产生GCD候选");
        AssertEx.Equal(actionId, frame.GcdCandidate!.ActionId, $"{scenario}动作错误");
    }

    private static void AssertCandidate(
        BlmResolverCandidate? candidate,
        uint actionId,
        string resolverId,
        int manifestOrder,
        string scenario)
    {
        AssertEx.True(candidate is not null, $"{scenario}必须产生候选");
        AssertEx.Equal(actionId, candidate!.ActionId, $"{scenario}动作错误");
        AssertEx.Equal(resolverId, candidate.ResolverId, $"{scenario}Resolver错误");
        AssertEx.Equal(manifestOrder, candidate.ManifestOrder, $"{scenario}manifest顺序错误");
    }
}
