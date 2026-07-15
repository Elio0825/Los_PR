using System.Collections.Immutable;
using System.Reflection;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;

namespace Los.Tests;

internal static class Phase4C1AoeResolverTests
{
    private const long Generation = 1;
    private const long NowMs = 50_000;
    private const uint PlayerId = 100;
    private const uint TargetId = 200;
    private const uint CenterId = 201;

    public static void RunAll()
    {
        ManifestAndLevelRoutes();
        FeatureLevelBoundaries();
        LowLevelMpAndGaugeBoundaries();
        Level58And100PreserveFrozenIceRoutes();
        CommonPriorityAndHostQueueContract();
        AoeThunderGuardBoundaries();
        TransposeHoldAndFillGateEveryGcd();
        TargetFactsAndStableCenterRules();
        NativeAoeCountAndLosFallbackRemainAvailable();
        NextAlwaysUsesResolverDeliveryGateOnly();
        InstantPredictionIncludesAllAoeResources();
    }

    private static void ManifestAndLevelRoutes()
    {
        foreach (var order in new[] { 4, 8, 10, 11, 12, 13, 14 })
        {
            AssertEx.Equal(
                BlmResolverManifestDisposition.Active,
                Level100ResolverEngine.Manifest[order].Disposition,
                $"Manifest order {order} 必须激活");
        }

        AssertEx.Equal(
            Level100ResolverEngine.FrozenManifestSha256,
            Level100ResolverEngine.ManifestSha256,
            "4C-1 Manifest哈希错误");

        AssertEx.True(Evaluate(BaseInput(11)).GcdCandidate is null, "11级不得进入AOE主循环");
        AssertGcd(BaseInput(12), BLMSkill.火炎, "GCD.群体1_34", 14, "12级边界");
        AssertGcd(BaseInput(17), BLMSkill.火炎, "GCD.群体1_34", 14, "17级边界");
        AssertGcd(BaseInput(18), BLMSkill.烈炎, "GCD.群体1_34", 14, "18级边界");
        AssertGcd(BaseInput(34), BLMSkill.烈炎, "GCD.群体1_34", 14, "34级边界");
        AssertGcd(BaseInput(35), BLMSkill.烈炎, "GCD.群体35_49", 13, "35级边界");
        AssertGcd(BaseInput(49), BLMSkill.烈炎, "GCD.群体35_49", 13, "49级边界");
        AssertGcd(BaseInput(50), BLMSkill.烈炎, "GCD.群体50_57", 12, "50级边界");
        AssertGcd(BaseInput(57), BLMSkill.烈炎, "GCD.群体50_57", 12, "57级边界");
        AssertGcd(BaseInput(58), BLMSkill.冰冻, "GCD.群体58_99", 11, "58级边界");
        AssertGcd(BaseInput(99), BLMSkill.高冰冻, "GCD.群体58_99", 11, "99级边界");
        AssertGcd(BaseInput(100), BLMSkill.高冰冻, "GCD.群体100", 10, "100级边界");
    }

    private static void LowLevelMpAndGaugeBoundaries()
    {
        var level12 = BaseInput(12) with
        {
            Context = BaseInput(12).Context with { Mp = 4999 },
        };
        AssertGcd(level12, BLMSkill.冰冻, "GCD.群体1_34", 14, "12级Neutral低于50%回蓝");
        AssertGcd(
            level12 with { Context = level12.Context with { Mp = 5000 } },
            BLMSkill.火炎,
            "GCD.群体1_34",
            14,
            "12级Neutral恰好50%转火");

        var level18Fire = BaseInput(18) with
        {
            Context = Fire(BaseInput(18), 2999),
        };
        AssertGcd(level18Fire, BLMSkill.冰冻, "GCD.群体1_34", 14, "18级火态动态烈炎2999");
        AssertGcd(
            level18Fire with { Context = level18Fire.Context with { Mp = 3000 } },
            BLMSkill.烈炎,
            "GCD.群体1_34",
            14,
            "18级火态动态烈炎3000");

        var level39Ice = BaseInput(39) with
        {
            Context = Ice(BaseInput(39), mp: 9999, hearts: 0),
        };
        AssertGcd(level39Ice, BLMSkill.冰冻, "GCD.群体35_49", 13, "39级冰态未满蓝");
        AssertGcd(
            level39Ice with { Context = level39Ice.Context with { Mp = 10_000 } },
            BLMSkill.烈炎,
            "GCD.群体35_49",
            13,
            "39级冰态满蓝");

        var level40Ice = BaseInput(40) with
        {
            Context = Ice(BaseInput(40), mp: 8999, hearts: 0),
        };
        AssertGcd(level40Ice, BLMSkill.玄冰, "GCD.群体35_49", 13, "40级8999MP");
        AssertGcd(
            level40Ice with { Context = level40Ice.Context with { Mp = 9000 } },
            BLMSkill.烈炎,
            "GCD.群体35_49",
            13,
            "40级9000MP");

        var level49Fire = BaseInput(49) with
        {
            Context = Fire(BaseInput(49), 2999),
        };
        AssertGcd(level49Fire, BLMSkill.冰冻, "GCD.群体35_49", 13, "49级无冰针烈炎2999");
        AssertGcd(
            level49Fire with
            {
                Context = level49Fire.Context with { Mp = 1500, UmbralHearts = 1 },
            },
            BLMSkill.烈炎,
            "GCD.群体35_49",
            13,
            "49级有冰针烈炎1500");

        var level50Fire = BaseInput(50) with
        {
            Context = Fire(BaseInput(50), 2999),
        };
        AssertGcd(level50Fire, BLMSkill.核爆, "GCD.群体50_57", 12, "50级烈炎不足核爆兜底");
        AssertGcd(
            level50Fire with { Context = level50Fire.Context with { Mp = 799 } },
            BLMSkill.冰冻,
            "GCD.群体50_57",
            12,
            "50级799MP回冰");

        var level58Fire = BaseInput(58) with
        {
            Context = Fire(BaseInput(58), 799),
        };
        AssertEx.True(
            Evaluate(level58Fire).GcdCandidate is null,
            "58级火态799MP必须让给Manafont/星灵");
        AssertGcd(
            level58Fire with { Context = level58Fire.Context with { Mp = 800 } },
            BLMSkill.核爆,
            "GCD.群体58_99",
            11,
            "58级火态800MP核爆");
    }

    private static void FeatureLevelBoundaries()
    {
        var level25 = ThunderInput(25);
        AssertEx.False(
            Evaluate(level25).GcdCandidate?.ResolverId == "GCD.雷2",
            "25级不得使用AOE雷");
        AssertGcd(ThunderInput(26), BLMSkill.震雷, "GCD.雷2", 8, "26级AOE雷边界");

        var level69 = BaseInput(69) with
        {
            Context = BaseInput(69).Context with { PolyglotStacks = 1 },
        };
        AssertEx.False(
            Evaluate(level69).GcdCandidate?.ResolverId == "GCD.秽浊",
            "69级不得使用秽浊");
        var level70 = BaseInput(70) with
        {
            Context = BaseInput(70).Context with { PolyglotStacks = 1 },
        };
        AssertGcd(level70, BLMSkill.秽浊, "GCD.秽浊", 4, "70级秽浊边界");

        var level79Moving = BaseInput(79) with
        {
            Context = BaseInput(79).Context with
            {
                PolyglotStacks = 1,
                IsMoving = true,
            },
            Settings = BaseInput(79).Settings with { MoveXenoglossyEnabled = true },
        };
        AssertEx.True(Evaluate(level79Moving).GcdCandidate is null, "79级移动秽浊必须拒绝");
        var level80Moving = level79Moving with
        {
            Context = level79Moving.Context with { Level = 80 },
            Actions = Actions(80),
        };
        AssertGcd(level80Moving, BLMSkill.秽浊, "GCD.秽浊", 4, "80级移动秽浊边界");

        AssertGcd(ThunderInput(91), BLMSkill.霹雷, "GCD.雷2", 8, "91级霹雷边界");
        AssertGcd(ThunderInput(92), BLMSkill.高震雷, "GCD.雷2", 8, "92级高震雷边界");

        var level97 = BaseInput(97) with
        {
            Context = BaseInput(97).Context with
            {
                PolyglotStacks = 1,
                PolyglotTimerMs = 6000,
            },
        };
        AssertGcd(level97, BLMSkill.秽浊, "GCD.秽浊", 4, "97级一层6000ms秽浊");
        AssertEx.False(
            Evaluate(level97 with
            {
                Context = level97.Context with { PolyglotTimerMs = 6001 },
            }).GcdCandidate?.ResolverId == "GCD.秽浊",
            "97级一层6001ms不得倾泻秽浊");
        var level98 = BaseInput(98) with
        {
            Context = BaseInput(98).Context with
            {
                PolyglotStacks = 2,
                PolyglotTimerMs = 7999,
            },
        };
        AssertGcd(level98, BLMSkill.秽浊, "GCD.秽浊", 4, "98级两层7999ms秽浊");
        AssertEx.False(
            Evaluate(level98 with
            {
                Context = level98.Context with { PolyglotTimerMs = 8000 },
            }).GcdCandidate?.ResolverId == "GCD.秽浊",
            "98级两层8000ms不得倾泻秽浊");
    }

    private static void AoeThunderGuardBoundaries()
    {
        var ready = ThunderInput(92);
        AssertGcd(ready, BLMSkill.高震雷, "GCD.雷2", 8, "AOE雷基线");

        foreach (var rejected in new[]
        {
            ready with { Context = Fire(ready, 10_000) with { HasThunderhead = true } },
            ready with { Context = ready.Context with { IsCasting = true } },
            ready with { Context = ready.Context with { AoeTargetHitCount = 2 } },
        })
        {
            AssertEx.False(
                Evaluate(rejected).GcdCandidate?.ResolverId == "GCD.雷2",
                "AOE雷必须拒绝火态、读条或命中不足");
        }

        var dot = ready.DotTargets[0];
        var hpBelow = ready with
        {
            DotTargets = [dot with { CurrentHp = 29_999, MaxHp = 1_000_000 }],
        };
        AssertEx.False(
            Evaluate(hpBelow).GcdCandidate?.ResolverId == "GCD.雷2",
            "AOE雷必须拒绝低于3% HP阈值的主目标");
        AssertGcd(
            hpBelow with
            {
                DotTargets = [dot with { CurrentHp = 30_000, MaxHp = 1_000_000 }],
            },
            BLMSkill.高震雷,
            "GCD.雷2",
            8,
            "AOE雷恰好3% HP阈值");

        var dot3500 = ready with
        {
            DotTargets =
            [
                dot with
                {
                    SingleTargetDotRemainingMs = 3500,
                    AoeDotRemainingMs = 3500,
                },
            ],
        };
        AssertGcd(dot3500, BLMSkill.高震雷, "GCD.雷2", 8, "AOE雷DOT 3500ms边界");
        AssertEx.False(
            Evaluate(dot3500 with
            {
                DotTargets =
                [
                    dot with
                    {
                        SingleTargetDotRemainingMs = 3501,
                        AoeDotRemainingMs = 3501,
                    },
                ],
            }).GcdCandidate?.ResolverId == "GCD.雷2",
            "AOE雷DOT 3501ms必须拒绝刷新");

        var fallback = ThunderInput(26) with
        {
            Context = ThunderInput(26).Context with { IsMoving = true },
            DotTargets =
            [
                ThunderInput(26).DotTargets[0] with
                {
                    CurrentHp = 1,
                    SingleTargetDotRemainingMs = 30_000,
                    AoeDotRemainingMs = 30_000,
                },
            ],
        };
        AssertGcd(
            fallback,
            BLMSkill.震雷,
            "GCD.瞬发gcd触发器",
            9,
            "26级雷云最终兜底忽略目标血量与DOT剩余");
        AssertEx.False(
            Evaluate(fallback with
            {
                Context = fallback.Context with { Level = 25 },
            }).GcdCandidate?.ActionId == BLMSkill.震雷,
            "25级雷云最终兜底不得使用群体雷");
    }

    private static void Level58And100PreserveFrozenIceRoutes()
    {
        var level99Two = BaseInput(99, enemyCount: 2) with
        {
            Context = Ice(BaseInput(99, enemyCount: 2), mp: 10_000, hearts: 2),
        };
        AssertGcd(level99Two, BLMSkill.玄冰, "GCD.群体58_99", 11, "99级双目标仍用玄冰");

        var level100Two = BaseInput(100, enemyCount: 2) with
        {
            Context = Ice(BaseInput(100, enemyCount: 2), mp: 10_000, hearts: 2),
        };
        AssertGcd(level100Two, BLMSkill.冰澈, "GCD.群体100", 10, "100级双目标改用冰澈");

        var level100Three = BaseInput(100) with
        {
            Context = Ice(BaseInput(100), mp: 10_000, hearts: 2),
        };
        AssertGcd(level100Three, BLMSkill.玄冰, "GCD.群体100", 10, "100级三目标使用玄冰");

        var fullHearts = level100Three with
        {
            Context = level100Three.Context with { UmbralHearts = 3 },
        };
        AssertEx.True(Evaluate(fullHearts).GcdCandidate is null, "冰针3层必须让给星灵");

        var fire = BaseInput(100) with
        {
            Context = Fire(BaseInput(100), 800) with { AstralSoulStacks = 5 },
        };
        AssertGcd(fire, BLMSkill.核爆, "GCD.群体100", 10, "100级800MP核爆");
        AssertEx.True(
            Evaluate(fire with { Context = fire.Context with { Mp = 799 } }).GcdCandidate is null,
            "100级799MP必须让给Manafont/星灵");
        AssertGcd(
            fire with { Context = fire.Context with { Mp = 799, AstralSoulStacks = 6 } },
            BLMSkill.耀星,
            "GCD.群体100",
            10,
            "耀星6层必须优先低蓝换相");
    }

    private static void CommonPriorityAndHostQueueContract()
    {
        var ttk = BaseInput(100) with
        {
            Context = BaseInput(100).Context with { PolyglotStacks = 1 },
            Settings = BaseInput(100).Settings with { TtkEnabled = true },
        };
        AssertGcd(ttk, BLMSkill.秽浊, "GCD.TTK", 0, "AOE TTK通晓优先");

        var foul = BaseInput(80) with
        {
            Context = BaseInput(80).Context with
            {
                PolyglotStacks = 1,
                PolyglotTimerMs = 6000,
            },
        };
        AssertGcd(foul, BLMSkill.秽浊, "GCD.秽浊", 4, "80级独立秽浊");

        var level70Moving = BaseInput(70) with
        {
            Context = BaseInput(70).Context with
            {
                PolyglotStacks = 1,
                IsMoving = true,
            },
        };
        AssertEx.True(Evaluate(level70Moving).GcdCandidate is null, "70–79移动秽浊必须保持拒绝");

        var thunder = BaseInput(92) with
        {
            Context = Ice(BaseInput(92), 10_000, hearts: 3) with
            {
                HasThunderhead = true,
            },
            Settings = BaseInput(92).Settings with { DotEnabled = true },
        };
        AssertGcd(thunder, BLMSkill.高震雷, "GCD.雷2", 8, "92级高震雷调整动作");

        var lockedHost = BaseInput(58) with
        {
            Context = Ice(BaseInput(58), 10_000, hearts: 2),
        };
        var freeze = lockedHost.Actions.First(action =>
            action.RequestedActionId == BLMSkill.玄冰);
        AssertEx.False(freeze.CanCast, "测试必须模拟公共GCD运行中的CanCast=false");
        AssertGcd(lockedHost, BLMSkill.玄冰, "GCD.群体58_99", 11, "公共GCD运行中仍规划NextGcd");

        foreach (var level in new[] { 12, 34 })
        {
            var forcedLowLevel = BaseInput(level) with
            {
                Context = Fire(BaseInput(level), 10_000),
                NeedsForcedIceRecovery = true,
            };
            AssertEx.False(
                forcedLowLevel.Actions.First(action =>
                    action.RequestedActionId == BLMSkill.冰结).CanCast,
                $"{level}级强制回冰测试必须保持公共GCD CanCast=false");
            AssertGcd(
                forcedLowLevel,
                BLMSkill.冰结,
                "GCD.强制回冰",
                2,
                $"{level}级AOE强制回冰");
        }

        var forcedLevel35 = BaseInput(35) with
        {
            Context = Fire(BaseInput(35), 10_000),
            NeedsForcedIceRecovery = true,
        };
        AssertGcd(
            forcedLevel35,
            BLMSkill.冰封,
            "GCD.强制回冰",
            2,
            "35级AOE强制回冰边界");

        var forcedBeforeFoul = BaseInput(80) with
        {
            Context = Fire(BaseInput(80), 10_000) with
            {
                PolyglotStacks = 1,
                PolyglotTimerMs = 6000,
            },
            NeedsForcedIceRecovery = true,
        };
        AssertGcd(
            forcedBeforeFoul,
            BLMSkill.冰封,
            "GCD.强制回冰",
            2,
            "AOE强制回冰必须先于order4秽浊");
        AssertGcd(
            forcedBeforeFoul with
            {
                Settings = forcedBeforeFoul.Settings with { TtkEnabled = true },
            },
            BLMSkill.秽浊,
            "GCD.TTK",
            0,
            "AOE TTK必须先于强制回冰");

        var movingLevel70 = BaseInput(70) with
        {
            Context = BaseInput(70).Context with
            {
                PolyglotStacks = 1,
                IsMoving = true,
            },
        };
        AssertEx.False(
            Evaluate(movingLevel70).GcdCandidate?.ResolverId == "GCD.秽浊",
            "70级移动时order4独立秽浊必须按源拒绝");
        AssertGcd(
            movingLevel70 with
            {
                Settings = movingLevel70.Settings with { TtkEnabled = true },
            },
            BLMSkill.秽浊,
            "GCD.TTK",
            0,
            "70级移动时order0 TTK必须按源允许秽浊");
    }

    private static void TransposeHoldAndFillGateEveryGcd()
    {
        var completedThreeTargetIceResource = SetAction(
            BaseInput(100) with
            {
                Context = Ice(BaseInput(100), 10_000, hearts: 3) with
                {
                    UmbralIceStacks = 1,
                    PolyglotStacks = 3,
                    PolyglotTimerMs = 0,
                    HasThunderhead = true,
                },
                Settings = BaseInput(100).Settings with { DotEnabled = true },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 5000);
        AssertGcd(
            completedThreeTargetIceResource,
            BLMSkill.秽浊,
            "GCD.秽浊",
            4,
            "UI1加三冰针不得重复玄冰并应进入星灵Fill");

        var pendingThreeTargetIceResource = SetAction(
            completedThreeTargetIceResource with
            {
                Context = completedThreeTargetIceResource.Context with
                {
                    UmbralHearts = 2,
                },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 5000);
        AssertGcd(
            pendingThreeTargetIceResource,
            BLMSkill.秽浊,
            "GCD.秽浊",
            4,
            "三目标通晓溢出必须先于主循环玄冰");
        AssertGcd(
            pendingThreeTargetIceResource with
            {
                Settings = pendingThreeTargetIceResource.Settings with
                {
                    TtkEnabled = true,
                },
            },
            BLMSkill.秽浊,
            "GCD.TTK",
            0,
            "TTK必须继续先于冰资源主循环");

        var pendingTwoTargetIceResource = SetAction(
            BaseInput(100, enemyCount: 2) with
            {
                Context = Ice(BaseInput(100, enemyCount: 2), 10_000, hearts: 2) with
                {
                    UmbralIceStacks = 1,
                    PolyglotStacks = 3,
                    HasThunderhead = true,
                },
                Settings = BaseInput(100, enemyCount: 2).Settings with { DotEnabled = true },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 5000);
        AssertGcd(
            pendingTwoTargetIceResource,
            BLMSkill.秽浊,
            "GCD.秽浊",
            4,
            "双目标通晓溢出必须先于主循环冰澈");

        var pendingLevel92IceResource = SetAction(
            BaseInput(92) with
            {
                Context = Ice(BaseInput(92), 10_000, hearts: 3) with
                {
                    UmbralIceStacks = 1,
                    UmbralHearts = 2,
                    PolyglotStacks = 0,
                    HasThunderhead = true,
                },
                Settings = BaseInput(92).Settings with { DotEnabled = true },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 5000);
        AssertGcd(
            pendingLevel92IceResource,
            BLMSkill.高震雷,
            "GCD.雷2",
            8,
            "92级雷云刷新必须先于主循环玄冰");

        var pendingLevel58IceResource = SetAction(
            BaseInput(58) with
            {
                Context = Ice(BaseInput(58), 10_000, hearts: 3) with
                {
                    UmbralIceStacks = 1,
                    UmbralHearts = 2,
                    HasThunderhead = true,
                },
                Settings = BaseInput(58).Settings with { DotEnabled = true },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 5000);
        AssertGcd(
            pendingLevel58IceResource,
            BLMSkill.震雷,
            "GCD.雷2",
            8,
            "58级雷云刷新必须先于主循环玄冰");

        var plainPendingIceResource = pendingLevel58IceResource with
        {
            Context = pendingLevel58IceResource.Context with
            {
                HasThunderhead = false,
            },
        };
        AssertGcd(
            plainPendingIceResource,
            BLMSkill.玄冰,
            "GCD.群体58_99",
            11,
            "高优条件全部拒绝后才由主循环玄冰");

        var iceResourceAck = RecentlyUsedGcd(BLMSkill.玄冰);
        var afterIceResource = pendingThreeTargetIceResource with
        {
            Context = pendingThreeTargetIceResource.Context with
            {
                PolyglotStacks = 1,
                PolyglotTimerMs = 7000,
                HasThunderhead = false,
            },
            RecentHistory = [iceResourceAck],
            PreviousGcd = iceResourceAck,
        };
        AssertCandidate(
            Evaluate(afterIceResource).GcdCandidate,
            BLMSkill.秽浊,
            "GCD.瞬发gcd触发器",
            9,
            "冰资源确认后星灵CD大于2秒才允许瞬发Fill");

        var twoTargetIceResourceAck = RecentlyUsedGcd(BLMSkill.冰澈);
        var afterTwoTargetIceResource = pendingTwoTargetIceResource with
        {
            Context = pendingTwoTargetIceResource.Context with
            {
                PolyglotStacks = 1,
                PolyglotTimerMs = 7000,
                HasThunderhead = false,
            },
            RecentHistory = [twoTargetIceResourceAck],
            PreviousGcd = twoTargetIceResourceAck,
        };
        AssertCandidate(
            Evaluate(afterTwoTargetIceResource).GcdCandidate,
            BLMSkill.秽浊,
            "GCD.瞬发gcd触发器",
            9,
            "双目标冰澈Ack领先Gauge后不得重复冰澈");

        var afterLevel92IceResource = SetAction(
            pendingLevel92IceResource with
            {
                RecentHistory = [iceResourceAck],
                PreviousGcd = iceResourceAck,
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 0);
        var level92CastNowFrame = Evaluate(afterLevel92IceResource);
        AssertEx.True(level92CastNowFrame.GcdCandidate is null, "92级玄冰Ack后星灵就绪不得重复玄冰");
        AssertCandidate(
            level92CastNowFrame.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            22,
            "92级玄冰Ack领先Gauge后立即星灵");

        var afterLevel58IceResource = SetAction(
            pendingLevel58IceResource with
            {
                RecentHistory = [iceResourceAck],
                PreviousGcd = iceResourceAck,
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 2000);
        var level58HoldFrame = Evaluate(afterLevel58IceResource);
        AssertEx.True(level58HoldFrame.GcdCandidate is null, "58级玄冰Ack后星灵CD两秒必须Hold");
        AssertEx.True(level58HoldFrame.HoldGcdForTranspose, "58级玄冰Ack领先Gauge后Hold事实错误");

        var hold = SetAction(
            BaseInput(100) with
            {
                Context = Ice(BaseInput(100), 10_000, hearts: 3),
                Settings = BaseInput(100).Settings with { TtkEnabled = true },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 2000);
        var holdFrame = Evaluate(hold);
        AssertEx.True(holdFrame.GcdCandidate is null, "星灵Hold必须阻断TTK及全部AOE GCD");
        AssertEx.True(holdFrame.HoldGcdForTranspose, "星灵Hold诊断事实错误");

        var castNow = SetAction(hold, BLMSkill.星灵移位, unlocked: true, cooldownMs: 0);
        var castFrame = Evaluate(castNow);
        AssertEx.True(castFrame.GcdCandidate is null, "星灵CastNow必须阻断全部GCD");
        AssertCandidate(castFrame.AlwaysCandidate, BLMSkill.星灵移位, "Ability.星灵移位", 22, "星灵CastNow");

        var fill = SetAction(
            hold with
            {
                Context = hold.Context with
                {
                    PolyglotStacks = 1,
                    PolyglotTimerMs = 7000,
                    HasParadox = true,
                },
                Settings = hold.Settings with { TtkEnabled = false },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 2001);
        var fillFrame = Evaluate(fill);
        AssertCandidate(fillFrame.GcdCandidate, BLMSkill.秽浊, "GCD.瞬发gcd触发器", 9, "星灵Fill只放行计划动作");

        var thunderFill = SetAction(
            hold with
            {
                Context = hold.Context with
                {
                    HasThunderhead = true,
                    UmbralHearts = 3,
                },
                Settings = hold.Settings with { DotEnabled = true },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 2001);
        AssertTransposeFill(
            thunderFill,
            BLMSkill.高震雷,
            "GCD.雷2",
            8,
            "星灵震雷Fill");

        var firestarterFill = SetAction(
            BaseInput(100, enemyCount: 2) with
            {
                Context = Fire(BaseInput(100, enemyCount: 2), 799) with
                {
                    AstralFireStacks = 2,
                    HasFirestarter = true,
                },
            },
            BLMSkill.星灵移位,
            unlocked: true,
            cooldownMs: 2001);
        AssertTransposeFill(
            firestarterFill,
            BLMSkill.爆炎,
            "GCD.瞬发gcd触发器",
            9,
            "星灵非AOE动作Fill");

        AssertTransposeFill(
            fill,
            BLMSkill.秽浊,
            "GCD.瞬发gcd触发器",
            9,
            "星灵秽浊Fill");
    }

    private static void TargetFactsAndStableCenterRules()
    {
        var current = BaseInput(100);
        var currentCandidate = Evaluate(current).GcdCandidate;
        AssertEx.True(currentCandidate is not null, "当前中心必须产生候选");
        AssertEx.Equal(BlmResolverTargetKind.CurrentTarget, currentCandidate!.TargetKind, "当前中心TargetKind错误");

        var specified = BaseInput(100, targetIsCurrent: false);
        var specifiedCandidate = Evaluate(specified).GcdCandidate;
        AssertCandidate(specifiedCandidate, BLMSkill.高冰冻, "GCD.群体100", 10, "指定智能中心");
        AssertEx.Equal(BlmResolverTargetKind.SpecifiedTarget, specifiedCandidate!.TargetKind, "非当前中心必须指定目标");
        AssertEx.Equal(CenterId, specifiedCandidate.TargetId, "智能中心ID错误");

        AssertEx.True(
            IsBetterAoeTarget(4, false, 10f, 300, 3, true, 1f, 200),
            "命中数更多必须优先");
        AssertEx.True(
            IsBetterAoeTarget(3, true, 10f, 300, 3, false, 1f, 200),
            "同命中数当前主目标必须优先");
        AssertEx.True(
            IsBetterAoeTarget(3, false, 5f, 300, 3, false, 6f, 200),
            "同命中数距离近者必须优先");
        AssertEx.True(
            IsBetterAoeTarget(3, false, 5f, 100, 3, false, 5f, 200),
            "完全并列EntityId小者必须优先");

        var insufficient = specified with
        {
            Context = specified.Context with { AoeTargetHitCount = 2 },
        };
        AssertEx.True(Evaluate(insufficient).GcdCandidate is null, "三目标中心命中不足必须拒绝");
        AssertEx.True(
            Evaluate(specified with
            {
                Context = specified.Context with { AoeTargetCanUseAttack = false },
            }).GcdCandidate is null,
            "不可攻击智能中心必须拒绝");
    }

    private static void NativeAoeCountAndLosFallbackRemainAvailable()
    {
        var contextSource = File.ReadAllText(FindRepositoryFile("BLM", "Core", "BlmContext.cs"));
        var productionStart = contextSource.IndexOf(
            "public static int CountEnemiesAroundTarget(",
            StringComparison.Ordinal);
        var fallbackStart = contextSource.IndexOf(
            "private static int CountEnemiesAroundTargetWithLosEligibility(",
            StringComparison.Ordinal);
        var fallbackEnd = contextSource.IndexOf(
            "internal static bool IsLiveAoeObject(",
            StringComparison.Ordinal);
        AssertEx.True(productionStart >= 0, "必须保留公共AOE计数入口");
        AssertEx.True(fallbackStart > productionStart, "必须保留可编译的Los资格过滤备用实现");
        AssertEx.True(fallbackEnd > fallbackStart, "Los备用实现边界错误");

        var productionSource = contextSource[productionStart..fallbackStart];
        AssertEx.True(
            productionSource.Contains(
                "TargetHelper.EnemyInRangeTarget(center, damageRange)",
                StringComparison.Ordinal),
            "生产AOE计数必须调用PR原生EnemyInRangeTarget");
        AssertEx.False(
            productionSource.Contains("Svc.Objects", StringComparison.Ordinal),
            "生产AOE计数不得同时执行Los对象表过滤");

        var fallbackSource = contextSource[fallbackStart..fallbackEnd];
        AssertEx.True(
            fallbackSource.Contains("Svc.Objects", StringComparison.Ordinal)
                && fallbackSource.Contains("IsCountableAoeEnemy", StringComparison.Ordinal),
            "Los资格过滤备用实现必须保持可回退");

        AssertEx.True(
            BlmContext.IsLiveAoeObject(isTargetable: true, isDead: false, currentHp: 1),
            "Los备用过滤必须识别存活可选中对象");
        AssertEx.False(
            BlmContext.IsLiveAoeObject(isTargetable: true, isDead: false, currentHp: 0),
            "Los备用过滤必须拒绝HP归零但IsDead尚未结算的对象");
        AssertEx.False(
            BlmContext.IsLiveAoeObject(isTargetable: true, isDead: true, currentHp: 1),
            "Los备用过滤必须拒绝已死亡对象");
        AssertEx.False(
            BlmContext.IsLiveAoeObject(isTargetable: false, isDead: false, currentHp: 1),
            "Los备用过滤必须拒绝不可选中对象");
        AssertEx.True(
            BlmContext.IsCountableAoeEnemy(
                isTargetable: true,
                isDead: false,
                currentHp: 1,
                isHostile: true,
                canUseAttackActionOn: true),
            "Los备用过滤必须接受真正可攻击的存活敌人");
        AssertEx.False(
            BlmContext.IsCountableAoeEnemy(
                isTargetable: true,
                isDead: false,
                currentHp: 1,
                isHostile: true,
                canUseAttackActionOn: false),
            "Los备用过滤必须拒绝不可攻击的存活敌对机制对象");

        AssertEx.False(
            BlmContext.ShouldUseAoeMode(
                level: 100,
                enemyCount: 2,
                aoeEnabled: true,
                smartAoeEnabled: false),
            "智能AOE关闭时双目标必须回到单体路由");
        AssertEx.True(
            BlmContext.ShouldUseAoeMode(
                level: 100,
                enemyCount: 2,
                aoeEnabled: true,
                smartAoeEnabled: true),
            "智能AOE开启时100级双目标必须进入冰澈路线");
        AssertEx.True(
            BlmContext.ShouldUseAoeMode(
                level: 100,
                enemyCount: 3,
                aoeEnabled: true,
                smartAoeEnabled: false),
            "三目标必须继续进入玄冰路线");
    }

    private static string FindRepositoryFile(params string[] relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "Los.csproj")))
            {
                continue;
            }

            return Path.Combine([directory.FullName, .. relativePath]);
        }

        throw new InvalidOperationException("无法定位Los仓库根目录");
    }

    private static void NextAlwaysUsesResolverDeliveryGateOnly()
    {
        var source = File.ReadAllText(FindRepositoryFile("BLM", "BlackMageRotation.cs"));
        var alwaysStart = source.IndexOf("public PAction? NextAlways()", StringComparison.Ordinal);
        var gcdStart = source.IndexOf("public PAction? NextGcd()", StringComparison.Ordinal);
        AssertEx.True(alwaysStart >= 0 && gcdStart > alwaysStart, "无法定位NextAlways入口");

        var nextAlways = source[alwaysStart..gcdStart];
        AssertEx.False(
            nextAlways.Contains("HasActiveCommand", StringComparison.Ordinal),
            "NextAlways不得保留Los额外ActiveCommand前置门");
        AssertEx.True(
            nextAlways.Contains("_execution.Resolve", StringComparison.Ordinal)
                && nextAlways.Contains("BlmResolverChannel.Always", StringComparison.Ordinal),
            "NextAlways必须继续通过Resolver生产交付链");
    }

    private static void InstantPredictionIncludesAllAoeResources()
    {
        var context = TestContext.Base();
        foreach (var actionId in new[]
        {
            BLMSkill.震雷,
            BLMSkill.霹雷,
            BLMSkill.高震雷,
            BLMSkill.秽浊,
        })
        {
            AssertEx.True(PredictInstant(context, actionId), $"动作{actionId}必须预测为瞬发");
        }
    }

    private static BlmResolverInput BaseInput(
        int level,
        int enemyCount = 3,
        bool targetIsCurrent = true)
        => new()
        {
            StateGeneration = Generation,
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
                EnemyCount = enemyCount,
                AoeTargetId = targetIsCurrent ? TargetId : CenterId,
                AoeTargetCanUseAttack = true,
                AoeTargetHitCount = enemyCount,
                AoeTargetIsCurrentTarget = targetIsCurrent,
                HasTarget = true,
                CanUseAttackActionOnTarget = true,
                CurrentTargetId = TargetId,
                GcdTotalSeconds = 2.5f,
                GcdRemainSeconds = 1.5f,
                Phase = BlmPhase.Neutral,
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
            Actions = Actions(level),
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

    private static BlmResolverInput ThunderInput(int level)
        => BaseInput(level) with
        {
            Context = Ice(BaseInput(level), 10_000, hearts: 3) with
            {
                HasThunderhead = true,
            },
            Settings = BaseInput(level).Settings with { DotEnabled = true },
        };

    private static ImmutableArray<BlmResolverActionFact> Actions(int level)
        =>
        [
            GcdFact(BLMSkill.火炎),
            GcdFact(BLMSkill.冰结),
            GcdFact(BLMSkill.烈炎, level >= 82 ? BLMSkill.高烈炎 : BLMSkill.烈炎),
            GcdFact(BLMSkill.冰冻, level >= 82 ? BLMSkill.高冰冻 : BLMSkill.冰冻),
            GcdFact(BLMSkill.爆炎),
            GcdFact(BLMSkill.冰封),
            GcdFact(BLMSkill.玄冰),
            GcdFact(BLMSkill.核爆),
            GcdFact(BLMSkill.冰澈),
            GcdFact(BLMSkill.秽浊),
            GcdFact(BLMSkill.异言),
            GcdFact(BLMSkill.绝望),
            GcdFact(BLMSkill.悖论),
            GcdFact(BLMSkill.耀星),
            GcdFact(
                BLMSkill.震雷,
                level >= 92 ? BLMSkill.高震雷 : level >= 64 ? BLMSkill.霹雷 : BLMSkill.震雷),
            AbilityFact(BLMSkill.星灵移位, false, 10_000),
            AbilityFact(BLMSkill.三连咏唱, false, 10_000),
            AbilityFact(MageUniversalSkill.即刻咏唱, false, 10_000),
            AbilityFact(BLMSkill.详述, false, 10_000),
            AbilityFact(BLMSkill.魔泉, false, 10_000),
        ];

    private static BlmResolverActionFact GcdFact(uint requestedId, uint adjustedId = 0)
        => new()
        {
            RequestedActionId = requestedId,
            AdjustedActionId = adjustedId == 0 ? requestedId : adjustedId,
            IsUnlocked = true,
            CanCast = false,
            CooldownRemainMs = 1500,
        };

    private static BlmResolverActionFact AbilityFact(
        uint actionId,
        bool ready,
        double cooldownMs)
        => new()
        {
            RequestedActionId = actionId,
            AdjustedActionId = actionId,
            IsUnlocked = true,
            CanCast = ready,
            Charges = ready ? 1f : 0f,
            MaxCharges = 1,
            CooldownRemainMs = cooldownMs,
        };

    private static BlmResolverContextFacts Fire(BlmResolverInput input, long mp)
        => input.Context with
        {
            Phase = BlmPhase.Fire,
            Mp = mp,
            AstralFireStacks = 3,
            UmbralIceStacks = 0,
            UmbralHearts = 0,
        };

    private static BlmResolverContextFacts Ice(
        BlmResolverInput input,
        long mp,
        int hearts)
        => input.Context with
        {
            Phase = BlmPhase.Ice,
            Mp = mp,
            AstralFireStacks = 0,
            UmbralIceStacks = 3,
            UmbralHearts = hearts,
        };

    private static BlmResolverInput SetAction(
        BlmResolverInput input,
        uint actionId,
        bool unlocked,
        double cooldownMs)
        => input with
        {
            Actions = input.Actions.Select(action =>
                action.RequestedActionId == actionId
                    ? action with
                    {
                        IsUnlocked = unlocked,
                        CanCast = cooldownMs <= 0,
                        Charges = cooldownMs <= 0 ? 1f : 0f,
                        CooldownRemainMs = cooldownMs,
                    }
                    : action).ToImmutableArray(),
        };

    private static BlmDecisionFrame Evaluate(BlmResolverInput input)
        => Level100ResolverEngine.Evaluate(input);

    private static void AssertGcd(
        BlmResolverInput input,
        uint actionId,
        string resolverId,
        int order,
        string scenario)
        => AssertCandidate(
            Evaluate(input).GcdCandidate,
            actionId,
            resolverId,
            order,
            scenario);

    private static void AssertTransposeFill(
        BlmResolverInput input,
        uint actionId,
        string resolverId,
        int order,
        string scenario)
    {
        AssertCandidate(Evaluate(input).GcdCandidate, actionId, resolverId, order, scenario);
        var recent = RecentlyUsedGcd(actionId);
        AssertEx.True(
            Evaluate(input with { RecentHistory = [recent] }).GcdCandidate is null,
            $"{scenario}刚确认过时必须拒绝重复候选");
    }

    private static BlmActionSuccess RecentlyUsedGcd(uint actionId)
        => new(
            Generation,
            1,
            actionId,
            actionId,
            actionId,
            1,
            NowMs - 100,
            NowMs - 50,
            true,
            true);

    private static void AssertCandidate(
        BlmResolverCandidate? candidate,
        uint actionId,
        string resolverId,
        int order,
        string scenario)
    {
        AssertEx.True(candidate is not null, $"{scenario}必须产生候选");
        AssertEx.Equal(actionId, candidate!.ActionId, $"{scenario}动作错误");
        AssertEx.Equal(resolverId, candidate.ResolverId, $"{scenario}Resolver错误");
        AssertEx.Equal(order, candidate.ManifestOrder, $"{scenario}顺序错误");
    }

    private static bool IsBetterAoeTarget(
        int hitCount,
        bool isCurrent,
        float distance,
        uint entityId,
        int bestHits,
        bool bestCurrent,
        float bestDistance,
        uint bestId)
    {
        var method = typeof(BlmContext).GetMethod(
            "IsBetterAoeTarget",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到智能AOE稳定排序原语");
        return (bool)method.Invoke(
            null,
            new object[]
            {
                hitCount,
                isCurrent,
                distance,
                entityId,
                bestHits,
                bestCurrent,
                bestDistance,
                bestId,
            })!;
    }

    private static bool PredictInstant(BlmContext context, uint actionId)
    {
        var method = typeof(Level100ResolverEngine).Assembly.GetType(
            "LosPr.BLM.Resolvers.Production.BlmResolverExecutionService")!
            .GetMethod("PredictInstant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到生产瞬发预测");
        return (bool)method.Invoke(null, new object[] { context, actionId })!;
    }
}
