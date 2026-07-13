using System.Collections.Immutable;
using System.Reflection;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;

namespace Los.Tests;

internal static class Level90ResolverParityTests
{
    private const long StateGeneration = 1;
    private const long NowMs = 20_000;
    private const uint PlayerId = 100;
    private const uint TargetId = 200;

    public static void RunAll()
    {
        ManifestAndLevelBoundaries();
        NeutralAndIcePhaseBoundaries();
        FireEntryAndLowMpBoundaries();
        FireParadoxCompressionBoundaries();
        Fire4ReserveAndDespairBoundaries();
        LocalLoopFactsFollowAcknowledgedHistory();
        ParadoxProjectionDistinguishesFireAndIceFacts();
        TransposeRespectsLevelSpecificRoutes();
        ManafontExtendsTheLevel90FirePhase();
        ManafontHardcastUsesAlwaysBridge();
    }

    private static void ManifestAndLevelBoundaries()
    {
        AssertEx.Equal(
            "5BA6987888A057224F2CBC11B71A3A6185CA8640BE95D360793D77048068E58D",
            Level100ResolverEngine.LosAeLevel90SingleTargetSha256,
            "单体90规格源哈希必须冻结");
        AssertEx.Equal(
            "0FF1EDBB1B44BEF58D7CB94EAFA58655941A57990E490BD905F7F1BB77AF14C7",
            Level100ResolverEngine.LosAeBlmAcrSha256,
            "90级注册顺序源哈希必须冻结");
        AssertEx.Equal(
            "85F6EB99AC19CA990720AE84A710E53F0E761C8AE0EA08732E149D8F34632E8D",
            Level100ResolverEngine.FrozenManifestSha256,
            "4A manifest 哈希合同错误");
        AssertEx.Equal(
            Level100ResolverEngine.FrozenManifestSha256,
            Level100ResolverEngine.ManifestSha256,
            "4A manifest 规范化哈希不匹配");
        AssertEx.Equal(
            BlmResolverManifestDisposition.Active,
            Level100ResolverEngine.Manifest[16].Disposition,
            "order 16 的90级单体Resolver必须激活");

        var input = BaseInput();
        var level89 = Evaluate(input, input.Context with { Level = 89 });
        AssertEx.True(level89.GcdCandidate is null, "89级必须保持 fail closed");
        AssertEx.True(level89.DeliveryBlocked, "89级必须被生命周期等级Gate阻断");

        AssertGcd(
            Evaluate(input, input.Context with { Level = 90 }),
            BLMSkill.炽炎,
            "GCD.单体90_99",
            16,
            "90级边界");
        AssertGcd(
            Evaluate(input, input.Context with { Level = 99 }),
            BLMSkill.炽炎,
            "GCD.单体90_99",
            16,
            "99级边界");
        AssertGcd(
            Evaluate(input, input.Context with { Level = 100 }),
            BLMSkill.炽炎,
            "GCD.单体100",
            15,
            "100级优先级");

        var aoe = Evaluate(input, input.Context with { IsSingleTargetMode = false });
        AssertEx.True(aoe.GcdCandidate is null, "90级AOE不得进入单体Resolver");
        var moving = Evaluate(input, input.Context with { IsMoving = true });
        AssertEx.True(moving.GcdCandidate is null, "无瞬发资源移动时90级主循环必须拒绝");
        var invalidTarget = Evaluate(
            input,
            input.Context with { CanUseAttackActionOnTarget = false });
        AssertEx.True(invalidTarget.GcdCandidate is null, "不可攻击目标必须 fail closed");
    }

    private static void NeutralAndIcePhaseBoundaries()
    {
        var input = BaseInput();
        var neutral = Evaluate(input, input.Context with
        {
            Phase = BlmPhase.Neutral,
            AstralFireStacks = 0,
            UmbralIceStacks = 0,
            UmbralHearts = 0,
        });
        AssertGcd(neutral, BLMSkill.冰封, "GCD.单体90_99", 16, "Neutral恢复");

        var uiOneContext = IceContext(input, uiStacks: 1, hearts: 0) with
        {
            HasParadox = true,
        };
        AssertGcd(
            Evaluate(input, uiOneContext),
            BLMSkill.冰封,
            "GCD.单体90_99",
            16,
            "UI1无即将到来的瞬发");

        var tripleSoon = SetAction(
            input,
            BLMSkill.三连咏唱,
            charges: 0.5f,
            cooldownRemainMs: 10_000d,
            canCast: false);
        AssertGcd(
            Evaluate(tripleSoon, uiOneContext),
            BLMSkill.悖论,
            "GCD.单体90_99",
            16,
            "UI1等待三连时先打悖论");

        AssertGcd(
            Evaluate(input, IceContext(input, uiStacks: 3, hearts: 2)),
            BLMSkill.冰澈,
            "GCD.单体90_99",
            16,
            "UI3补冰针");

        var fullIceWithParadox = IceContext(input, uiStacks: 3, hearts: 3) with
        {
            HasParadox = true,
        };
        AssertGcd(
            Evaluate(input, fullIceWithParadox),
            BLMSkill.悖论,
            "GCD.单体90_99",
            16,
            "默认冰悖论");

        var skipIceParadox = input with
        {
            Settings = input.Settings with { SkipIceParadox = true },
        };
        var skipped = Evaluate(skipIceParadox, fullIceWithParadox);
        AssertEx.True(skipped.GcdCandidate is null, "跳过冰悖论时主GCD必须让出");
        AssertAlwaysTranspose(skipped, "跳过冰悖论");

        var fullIce = Evaluate(input, IceContext(input, uiStacks: 3, hearts: 3));
        AssertEx.True(fullIce.GcdCandidate is null, "冰资源完整时主GCD必须返回空");
        AssertAlwaysTranspose(fullIce, "冰资源完整");

        var afterBlizzardThree = WithHistory(
            input,
            Success(BLMSkill.冰封, 10));
        AssertGcd(
            Evaluate(
                afterBlizzardThree,
                IceContext(afterBlizzardThree, uiStacks: 3, hearts: 3) with
                {
                    Mp = 9000,
                }),
            BLMSkill.冰澈,
            "GCD.单体90_99",
            16,
            "冰三后等待回蓝");
    }

    private static void FireEntryAndLowMpBoundaries()
    {
        var input = BaseInput();
        var afOne = input.Context with
        {
            AstralFireStacks = 1,
            UmbralIceStacks = 0,
            UmbralHearts = 0,
        };
        AssertAction(
            Evaluate(input, afOne with { HasFirestarter = true }),
            BLMSkill.爆炎,
            "AF1火苗");
        AssertAction(
            Evaluate(input, afOne with { HasParadox = true, Mp = 1600 }),
            BLMSkill.悖论,
            "AF1火悖论");
        AssertAction(
            Evaluate(input, afOne with { HasParadox = true, Mp = 1599 }),
            BLMSkill.爆炎,
            "AF1悖论蓝量不足");
        AssertAction(
            Evaluate(input, afOne with { Mp = 799 }),
            BLMSkill.冰封,
            "AF1低蓝回冰");
        AssertAction(
            Evaluate(input, afOne with { Mp = 800 }),
            BLMSkill.爆炎,
            "AF1蓝量边界进火");
    }

    private static void FireParadoxCompressionBoundaries()
    {
        var input = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                UmbralHearts = 0,
                HasParadox = true,
            },
        };
        var nonCompressed = input with
        {
            Settings = input.Settings with { CompressFireParadox = false },
        };
        AssertAction(
            Level100ResolverEngine.Evaluate(WithLoop(nonCompressed, 0, false)),
            BLMSkill.炽炎,
            "非压缩0火四");
        AssertAction(
            Level100ResolverEngine.Evaluate(WithLoop(nonCompressed, 3, false)),
            BLMSkill.悖论,
            "非压缩3火四");
        AssertAction(
            Level100ResolverEngine.Evaluate(WithLoop(nonCompressed, 5, false)),
            BLMSkill.悖论,
            "非压缩5火四");

        var compressed = input with
        {
            Settings = input.Settings with { CompressFireParadox = true },
        };
        AssertAction(
            Level100ResolverEngine.Evaluate(WithLoop(compressed, 3, false)),
            BLMSkill.炽炎,
            "压缩3火四");
        AssertAction(
            Level100ResolverEngine.Evaluate(WithLoop(compressed, 6, false)),
            BLMSkill.悖论,
            "压缩6火四");
        AssertAction(
            Evaluate(
                WithLoop(compressed, 3, false),
                compressed.Context with { IsMoving = true, HasSwiftcast = true }),
            BLMSkill.悖论,
            "移动瞬发提前火悖论");
    }

    private static void Fire4ReserveAndDespairBoundaries()
    {
        var input = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                UmbralHearts = 0,
                HasParadox = false,
            },
        };
        AssertAction(
            Evaluate(input, input.Context with { Mp = 2400 }),
            BLMSkill.炽炎,
            "无冰针时火四保留800MP");
        AssertAction(
            Evaluate(input, input.Context with { Mp = 2399 }),
            BLMSkill.绝望,
            "无冰针时不足火四预算");
        AssertAction(
            Evaluate(input, input.Context with { Mp = 1600, UmbralHearts = 1 }),
            BLMSkill.炽炎,
            "有冰针时火四保留800MP");
        AssertAction(
            Evaluate(input, input.Context with { Mp = 1599, UmbralHearts = 1 }),
            BLMSkill.绝望,
            "有冰针时不足火四预算");

        var completed = WithLoop(input, 6, true);
        AssertAction(
            Evaluate(completed, completed.Context with { Mp = 800 }),
            BLMSkill.绝望,
            "绝望800MP边界");
        AssertAction(
            Evaluate(completed, completed.Context with { Mp = 799 }),
            BLMSkill.冰封,
            "绝望不足800MP回冰");
    }

    private static void LocalLoopFactsFollowAcknowledgedHistory()
    {
        var input = WithLoop(BaseInput(), 6, true);
        var afterFireThree = WithHistory(
            input,
            Success(BLMSkill.炽炎, 1),
            Success(BLMSkill.炽炎, 2),
            Success(BLMSkill.炽炎, 3),
            Success(BLMSkill.炽炎, 4),
            Success(BLMSkill.炽炎, 5),
            Success(BLMSkill.炽炎, 6),
            Success(BLMSkill.爆炎, 7));
        AssertAction(
            Level100ResolverEngine.Evaluate(afterFireThree),
            BLMSkill.炽炎,
            "手动火三后局部计数清零");

        var afterParadox = WithHistory(
            input with { Context = input.Context with { HasParadox = false } },
            Success(BLMSkill.炽炎, 1),
            Success(BLMSkill.炽炎, 2),
            Success(BLMSkill.炽炎, 3),
            Success(BLMSkill.悖论, 4),
            Success(BLMSkill.炽炎, 5),
            Success(BLMSkill.炽炎, 6),
            Success(BLMSkill.炽炎, 7));
        AssertAction(
            Level100ResolverEngine.Evaluate(afterParadox),
            BLMSkill.炽炎,
            "火悖论后只统计后续火四");

        var afterDespair = WithHistory(
            input,
            Success(BLMSkill.炽炎, 1),
            Success(BLMSkill.炽炎, 2),
            Success(BLMSkill.炽炎, 3),
            Success(BLMSkill.炽炎, 4),
            Success(BLMSkill.炽炎, 5),
            Success(BLMSkill.炽炎, 6),
            Success(BLMSkill.绝望, 7));
        AssertAction(
            Level100ResolverEngine.Evaluate(afterDespair),
            BLMSkill.炽炎,
            "绝望后局部计数清零");

        var previousOnly = input with
        {
            PreviousGcd = Success(BLMSkill.冰封, 8),
            RecentHistory = [],
        };
        AssertAction(
            Level100ResolverEngine.Evaluate(previousOnly),
            BLMSkill.炽炎,
            "历史缺失时前一冰三仍应清零");
    }

    private static void ParadoxProjectionDistinguishesFireAndIceFacts()
    {
        var recentFireParadox = WithHistory(
            WithLoop(BaseInput(), 3, true),
            Success(BLMSkill.炽炎, 1),
            Success(BLMSkill.炽炎, 2),
            Success(BLMSkill.炽炎, 3),
            Success(BLMSkill.悖论, 4));
        var fireFacts = ProjectLoopFacts(recentFireParadox);
        AssertEx.Equal(0, fireFacts.Fire4Count, "最近火悖论必须清零局部火四计数");
        AssertEx.True(fireFacts.FireParadoxUsed, "最近火悖论必须标记本子循环已使用悖论");

        var transposedAfterIceParadox = WithHistory(
            WithLoop(BaseInput(), 0, false),
            Success(BLMSkill.悖论, 5));
        var iceFacts = ProjectLoopFacts(transposedAfterIceParadox);
        AssertEx.Equal(0, iceFacts.Fire4Count, "冰悖论转火不得携带火四计数");
        AssertEx.False(
            iceFacts.FireParadoxUsed,
            "冰悖论经星灵转火后不得误标为火悖论");

        var unconfirmedManafont = WithHistory(
            WithLoop(BaseInput(), 6, true),
            Success(BLMSkill.炽炎, 1),
            Success(BLMSkill.炽炎, 2),
            Success(BLMSkill.炽炎, 3),
            Success(BLMSkill.炽炎, 4),
            Success(BLMSkill.炽炎, 5),
            Success(BLMSkill.炽炎, 6),
            Success(BLMSkill.魔泉, 7, isGcd: false));
        var unconfirmedFacts = ProjectLoopFacts(unconfirmedManafont);
        AssertEx.Equal(
            6,
            unconfirmedFacts.Fire4Count,
            "只有Manafont Ack、Gauge未确认时不得重置局部计数");
        AssertEx.True(
            unconfirmedFacts.FireParadoxUsed,
            "Manafont Gauge未确认时不得提前重置火悖论事实");

        var confirmedManafont = unconfirmedManafont with
        {
            Level100Loop = new BlmLevel100LoopFacts
            {
                Fire4Count = 0,
                FireParadoxUsed = false,
            },
        };
        var confirmedFacts = ProjectLoopFacts(confirmedManafont);
        AssertEx.Equal(0, confirmedFacts.Fire4Count, "Manafont Gauge确认后必须采用后段计数");
        AssertEx.False(
            confirmedFacts.FireParadoxUsed,
            "Manafont Gauge确认后必须重置火悖论事实");
    }

    private static void TransposeRespectsLevelSpecificRoutes()
    {
        var input = WithLoop(BaseInput(), 6, true);
        var level99FireEnd = input with
        {
            Context = input.Context with
            {
                Level = 99,
                Mp = 0,
                UmbralHearts = 0,
                HasParadox = false,
                HasSwiftcast = true,
            },
        };
        var fireEnd = Level100ResolverEngine.Evaluate(level99FireEnd);
        AssertAction(fireEnd, BLMSkill.冰封, "99级火末主GCD回冰");
        AssertEx.True(fireEnd.AlwaysCandidate is null, "99级火末不得星灵转冰");
        AssertEx.False(fireEnd.HoldGcdForTranspose, "99级火末不得建立星灵Hold");

        var ttkFireEnd = Level100ResolverEngine.Evaluate(level99FireEnd with
        {
            Settings = level99FireEnd.Settings with { TtkEnabled = true },
        });
        AssertAlwaysTranspose(ttkFireEnd, "99级TTK火末");
        AssertEx.True(ttkFireEnd.HoldGcdForTranspose, "99级TTK火末必须保留星灵窗口");

        var level100FireEnd = Level100ResolverEngine.Evaluate(level99FireEnd with
        {
            Context = level99FireEnd.Context with { Level = 100 },
        });
        AssertAlwaysTranspose(level100FireEnd, "100级火末兼容");
        AssertEx.True(level100FireEnd.HoldGcdForTranspose, "100级火末必须保留星灵Hold");

        var iceReady = Evaluate(
            input,
            IceContext(input, uiStacks: 3, hearts: 3));
        AssertEx.True(iceReady.GcdCandidate is null, "90级冰资源完整时主GCD应让出");
        AssertAlwaysTranspose(iceReady, "90级冰转火");
    }

    private static void ManafontExtendsTheLevel90FirePhase()
    {
        var input = SetAction(
            BaseInput() with
            {
                Context = BaseInput().Context with
                {
                    Mp = 0,
                    UmbralHearts = 0,
                    GcdRemainSeconds = 1.5f,
                },
                Settings = BaseInput().Settings with { ManafontEnabled = true },
            },
            BLMSkill.魔泉,
            charges: 1f,
            cooldownRemainMs: 0d,
            canCast: true);
        input = WithHistory(input, Success(BLMSkill.绝望, 1));
        var fireEnd = Level100ResolverEngine.Evaluate(input);
        AssertCandidate(
            fireEnd.OffGcdCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            27,
            "90级火末Manafont");
        AssertEx.True(fireEnd.AlwaysCandidate is null, "Manafont成立时90级火末不得产生星灵");

        var restored = WithLoop(input, 2, false) with
        {
            Context = input.Context with
            {
                Mp = 8400,
                UmbralHearts = 1,
                HasParadox = true,
            },
        };
        restored = WithHistory(
            restored,
            Success(BLMSkill.炽炎, 1),
            Success(BLMSkill.炽炎, 2),
            Success(BLMSkill.炽炎, 3),
            Success(BLMSkill.魔泉, 4, isGcd: false),
            Success(BLMSkill.炽炎, 5),
            Success(BLMSkill.炽炎, 6));
        AssertAction(
            Level100ResolverEngine.Evaluate(restored),
            BLMSkill.炽炎,
            "Manafont后按Fire4CountSinceManafont续火");
    }

    private static void ManafontHardcastUsesAlwaysBridge()
    {
        var input = SetAction(
            BaseInput() with
            {
                Context = BaseInput().Context with
                {
                    Level = 99,
                    Mp = 0,
                    UmbralHearts = 0,
                    IsCasting = true,
                    GcdRemainSeconds = 0.9f,
                },
                Settings = BaseInput().Settings with { ManafontEnabled = true },
            },
            BLMSkill.魔泉,
            charges: 1f,
            cooldownRemainMs: 0d,
            canCast: true);
        input = WithHistory(input, Success(BLMSkill.绝望, 1));

        var duringHardcast = Level100ResolverEngine.Evaluate(input);
        AssertGcd(
            duringHardcast,
            BLMSkill.冰封,
            "GCD.单体90_99",
            16,
            "硬读条火末原始回冰候选");
        AssertCandidate(
            duringHardcast.OffGcdCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            27,
            "硬读条火末魔泉候选");
        AssertCandidate(
            duringHardcast.AlwaysBridgeCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            27,
            "硬读条火末Always桥候选");
        AssertEx.True(
            duringHardcast.GcdBlockedByAlwaysBridge,
            "硬读条火末必须阻止冰三提前入队");

        var queueEdge = Evaluate(
            input,
            input.Context with { GcdRemainSeconds = 0.3f });
        AssertEx.True(
            queueEdge.AlwaysBridgeCandidate?.ActionId == BLMSkill.魔泉,
            "进入GCD排队窗口后不得丢失魔泉桥候选");
        AssertEx.True(
            queueEdge.GcdBlockedByAlwaysBridge,
            "GCD排队窗口仍必须阻止冰三");

        var afterHardcast = Evaluate(
            input,
            input.Context with
            {
                IsCasting = false,
                GcdRemainSeconds = 0.3f,
            });
        AssertEx.True(
            afterHardcast.AlwaysBridgeCandidate?.ActionId == BLMSkill.魔泉,
            "读条结束后必须保留可交付的魔泉桥候选");

        var normalWeave = Evaluate(
            input,
            input.Context with
            {
                IsCasting = false,
                GcdRemainSeconds = 1.5f,
            });
        AssertEx.True(
            normalWeave.AlwaysBridgeCandidate is null,
            "正常weave窗口不得启用Always桥");
        AssertEx.False(
            normalWeave.GcdBlockedByAlwaysBridge,
            "正常weave窗口不得阻止GCD");
    }

    private static BlmResolverInput BaseInput()
        => new()
        {
            StateGeneration = StateGeneration,
            Context = new BlmResolverContextFacts
            {
                CapturedAtMs = NowMs,
                IsAvailable = true,
                AcrEnabled = true,
                PlayerEntityId = PlayerId,
                Level = 90,
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
                UmbralIceStacks = 0,
                UmbralHearts = 3,
            },
            Settings = BlmResolverSettings.Default with
            {
                ManafontEnabled = false,
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

    private static BlmResolverContextFacts IceContext(
        BlmResolverInput input,
        int uiStacks,
        int hearts)
        => input.Context with
        {
            Phase = BlmPhase.Ice,
            AstralFireStacks = 0,
            UmbralIceStacks = uiStacks,
            UmbralHearts = hearts,
            AstralSoulStacks = 0,
            HasParadox = false,
            HasFirestarter = false,
        };

    private static BlmResolverInput WithLoop(
        BlmResolverInput input,
        int fire4Count,
        bool fireParadoxUsed)
        => input with
        {
            Level100Loop = input.Level100Loop with
            {
                Fire4Count = fire4Count,
                FireParadoxUsed = fireParadoxUsed,
            },
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

    private static BlmResolverInput SetAction(
        BlmResolverInput input,
        uint actionId,
        float charges,
        double cooldownRemainMs,
        bool canCast)
        => input with
        {
            Actions = input.Actions.Select(action =>
                action.RequestedActionId == actionId
                    ? action with
                    {
                        IsUnlocked = true,
                        CanCast = canCast,
                        Charges = charges,
                        CooldownRemainMs = cooldownRemainMs,
                    }
                    : action).ToImmutableArray(),
        };

    private static (int Fire4Count, bool FireParadoxUsed) ProjectLoopFacts(
        BlmResolverInput input)
    {
        var resolverType = typeof(Level100ResolverEngine).Assembly.GetType(
            "LosPr.BLM.Resolvers.Level100.Level90SingleTargetResolvers")
            ?? throw new InvalidOperationException("找不到90级单体Resolver类型");
        var method = resolverType.GetMethod(
            "ProjectLoopFacts",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到90级局部计数投影方法");
        return ((int Fire4Count, bool FireParadoxUsed))method.Invoke(
            null,
            new object?[] { input })!;
    }

    private static ImmutableArray<BlmResolverActionFact> DefaultActions()
        =>
        [
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
            UnavailableAction(BLMSkill.魔泉),
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
            IsUnlocked = true,
            CanCast = false,
            Charges = 0f,
            MaxCharges = 1,
            CooldownRemainMs = 10_000d,
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
            (uint)serial,
            NowMs - 1000 + serial,
            NowMs - 900 + serial,
            false,
            isGcd);

    private static BlmDecisionFrame Evaluate(
        BlmResolverInput input,
        BlmResolverContextFacts context)
        => Level100ResolverEngine.Evaluate(input with { Context = context });

    private static void AssertGcd(
        BlmDecisionFrame frame,
        uint actionId,
        string resolverId,
        int manifestOrder,
        string scenario)
        => AssertCandidate(
            frame.GcdCandidate,
            actionId,
            resolverId,
            manifestOrder,
            scenario);

    private static void AssertAction(
        BlmDecisionFrame frame,
        uint actionId,
        string scenario)
    {
        AssertEx.True(frame.GcdCandidate is not null, $"{scenario}必须产生GCD候选");
        AssertEx.Equal(actionId, frame.GcdCandidate!.ActionId, $"{scenario}动作错误");
    }

    private static void AssertAlwaysTranspose(BlmDecisionFrame frame, string scenario)
        => AssertCandidate(
            frame.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            22,
            scenario);

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
