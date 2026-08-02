using System.Collections.Immutable;
using System.Text.Json;
using LosPr.BLM.Core;
using LosPr.BLM.Resolvers;
using LosPr.BLM.Resolvers.Level100;

namespace Los.Tests;

internal static class Level100ResolverParityTests
{
    private const long NowMs = 10_000;
    private const uint PlayerId = 100;
    private const uint CurrentTargetId = 200;

    public static void Run()
    {
        ManifestAndDefaults();
        EligibilityAndDeliveryBlocks();
        NeutralAndPhaseRecovery();
        FireLoopBoundaries();
        TtkFastFlareForcedRecovery();
        ThunderMovementAndIdle();
        DoubleDotSelection();
        FireEndTransposeSwiftcastFrames();
        ManualIceRecoveryAndTriplecastSubstitute();
        ManafontArbitrationAndGaugeDelivery();
        OffGcdPriorityDefenseAndPotion();
        HoldAndWeaveBoundaries();
    }

    private static void ManifestAndDefaults()
    {
        AssertEx.True(
            Level100ResolverEngine.BehaviorClosureImplemented,
            "第二阶段 Resolver 行为闭包必须明确标记为已实现");
        AssertEx.Equal(
            "a3ee18d8c524238bd200f1bbdf906bd77e13038a",
            Level100ResolverEngine.LosAeSpecificationCommit,
            "los-ae 规格提交必须冻结");
        AssertEx.Equal(
            "0FF1EDBB1B44BEF58D7CB94EAFA58655941A57990E490BD905F7F1BB77AF14C7",
            Level100ResolverEngine.LosAeBlmAcrSha256,
            "BLMACR 注册源哈希必须冻结");
        AssertEx.Equal(
            "9871D1D5C5BA1B280CFAB1278622B7B97637FC1E1B5FF5BB3A7FDB60FA14A61D",
            Level100ResolverEngine.LosAeLevel100SingleTargetSha256,
            "单体100 规格源哈希必须冻结");
        AssertEx.Equal(
            "5BA6987888A057224F2CBC11B71A3A6185CA8640BE95D360793D77048068E58D",
            Level100ResolverEngine.LosAeLevel90SingleTargetSha256,
            "单体90 规格源哈希必须冻结");
        AssertEx.Equal(
            "F87155A138C95B561B54BC0575DDFFC21014FBC865021AA379D5CC65004F8C83",
            Level100ResolverEngine.LosAeTransposeSha256,
            "星灵移位规格源哈希必须冻结");
        AssertEx.Equal(
            "EBAC890FBE20870102F1A4EA492266708D1C8A81E4EF010D433303E5A526EB41",
            Level100ResolverEngine.LosAeSwiftcastSha256,
            "即刻规格源哈希必须冻结");
        AssertEx.Equal(
            "3401718A45A66C7E5F619015D00E26F91F273BB05541D8229314C079F3234565",
            Level100ResolverEngine.LosAeTriplecastSha256,
            "三连规格源哈希必须冻结");
        AssertEx.Equal(
            "1F2102220502B790F62563F536F428C09A8DEADCDFA4C7C0EFA05929B6C70ACE",
            Level100ResolverEngine.LosAeManafontSha256,
            "Manafont规格源哈希必须冻结");
        AssertEx.Equal(
            Level100ResolverEngine.FrozenManifestSha256,
            Level100ResolverEngine.ManifestSha256,
            "manifest 规范化哈希必须保持冻结");

        var expectedIds = new[]
        {
            "GCD.TTK",
            "GCD.快速耀星",
            "GCD.强制回冰",
            "GCD.异言#1",
            "GCD.秽浊",
            "GCD.异言#2",
            "GCD.双DOT",
            "GCD.雷1",
            "GCD.雷2",
            "GCD.瞬发gcd触发器",
            "GCD.群体100",
            "GCD.群体58_99",
            "GCD.群体50_57",
            "GCD.群体35_49",
            "GCD.群体1_34",
            "GCD.单体100",
            "GCD.单体90_99",
            "GCD.单体72_89",
            "GCD.单体60_71",
            "GCD.单体35_59",
            "GCD.单体1_34",
            "GCD.核爆补耀星",
            "Ability.星灵移位",
            "Ability.即刻",
            "Ability.三连咏唱",
            "Ability.醒梦",
            "Ability.详述",
            "Ability.墨泉",
            "Ability.黑魔纹",
            "Ability.Auto昏乱",
            "Ability.Auto魔罩",
            "Ability.爆发药",
        };
        AssertEx.Equal(32, Level100ResolverEngine.Manifest.Length, "manifest 必须完整保留32项");
        for (var index = 0; index < expectedIds.Length; index++)
        {
            var entry = Level100ResolverEngine.Manifest[index];
            AssertEx.Equal(index, entry.Order, $"manifest 第{index}项顺序错误");
            AssertEx.Equal(expectedIds[index], entry.ResolverId, $"manifest 第{index}项ID错误");
        }

        AssertEx.Equal(
            BlmResolverManifestDisposition.Active,
            Level100ResolverEngine.Manifest[3].Disposition,
            "第一份异言必须保留为有效注册");
        AssertEx.Equal(
            BlmResolverManifestDisposition.Active,
            Level100ResolverEngine.Manifest[5].Disposition,
            "重复异言必须原位保留为有效注册");
        AssertEx.Equal(
            BlmResolverManifestDisposition.Active,
            Level100ResolverEngine.Manifest[4].Disposition,
            "秽浊必须按原顺序激活并由模式显式拒绝单体80+");
        AssertEx.Equal(
            BlmResolverManifestDisposition.Inactive,
            Level100ResolverEngine.Manifest[21].Disposition,
            "永久 -99 的核爆补耀星必须与模式拒绝项区分");
        AssertEx.Equal(
            BlmResolverManifestDisposition.Active,
            Level100ResolverEngine.Manifest[16].Disposition,
            "90–99级单体Resolver必须按原顺序激活");
        AssertEx.Equal(
            22,
            Level100ResolverEngine.Manifest.Count(entry => entry.Channel == BlmResolverChannel.Gcd),
            "GCD 注册数量错误");
        AssertEx.Equal(
            1,
            Level100ResolverEngine.Manifest.Count(entry => entry.Channel == BlmResolverChannel.Always),
            "Always 注册数量错误");
        AssertEx.Equal(
            9,
            Level100ResolverEngine.Manifest.Count(entry => entry.Channel == BlmResolverChannel.OffGcd),
            "OffGCD 注册数量错误");

        var settings = BlmResolverSettings.Default;
        AssertEx.Equal(17, BlmResolverSettings.Mapping.Length, "离线设置映射必须完整17项");
        AssertEx.Equal(
            17,
            BlmResolverSettings.Mapping.Select(item => item.PropertyName).Distinct().Count(),
            "设置映射不得出现重复属性");
        AssertEx.True(settings.ManafontEnabled, "Manafont 默认应开启");
        AssertEx.False(settings.DumpPolyglotEnabled, "倾泻资源默认应关闭");
        AssertEx.False(settings.FastFlareStarEnabled, "快速耀星默认应关闭");
        AssertEx.False(settings.TtkEnabled, "TTK 默认应关闭");
        AssertEx.True(settings.DotEnabled, "Dot 默认应开启");
        AssertEx.False(settings.DoubleDotEnabled, "双DOT默认应关闭");
        AssertEx.True(settings.MoveXenoglossyEnabled, "移动异言默认应开启");
        AssertEx.True(settings.SwiftcastIntoIceEnabled, "即刻进冰默认应开启");
        AssertEx.True(settings.TriplecastIntoIceEnabled, "三连进冰默认应开启");
        AssertEx.True(settings.MoveTriplecastEnabled, "移动三连默认应开启");
        AssertEx.True(settings.AmplifierEnabled, "详述默认应开启");
        AssertEx.True(settings.LeyLinesEnabled, "黑魔纹默认应开启");
        AssertEx.True(settings.AutoMitigationEnabled, "自动减伤默认应开启");
        AssertEx.False(settings.PotionEnabled, "爆发药默认应关闭");
        AssertEx.True(settings.CompressFireParadox, "压缩火悖论默认应开启");
        AssertEx.True(settings.ReducedAnimationLockEnabled, "减少动画锁默认应开启");
        AssertEx.Equal(3, settings.DotHpThresholdPercent, "不上Dot阈值默认应为3%");
        AssertEx.False(
            typeof(BlmResolverSettings).GetProperties()
                .Any(property => property.Name.Contains("CompressIce", StringComparison.Ordinal)),
            "PR 特例禁止迁移压缩冰悖论设置");
        AssertEx.False(
            typeof(BlmResolverSettings).GetProperties()
                .Any(property => property.Name == "SkipIceParadox"),
            "废弃的跳过冰悖论设置不得残留");

        var input = BaseInput();
        var serializedBefore = JsonSerializer.Serialize(input);
        var first = Level100ResolverEngine.Evaluate(input);
        var second = Level100ResolverEngine.Evaluate(input);
        AssertEx.Equal(first, second, "相同输入重复计算必须得到完全相同的DecisionFrame");
        AssertEx.Equal(
            serializedBefore,
            JsonSerializer.Serialize(input),
            "Resolver 计算不得修改输入事实");
    }

    private static void EligibilityAndDeliveryBlocks()
    {
        var input = BaseInput();
        var baseline = Level100ResolverEngine.Evaluate(input);
        AssertCandidate(baseline.GcdCandidate, BLMSkill.炽炎, "GCD.单体100", "基线GCD");
        AssertEx.False(baseline.DeliveryBlocked, "合法基线不应阻断交付");

        var invalidContexts = new[]
        {
            input.Context with { IsAvailable = false },
            input.Context with { AcrEnabled = false },
            input.Context with { Level = 0 },
            input.Context with { InCombat = false, AutoPullEnabled = false },
            input.Context with { IsAlive = false },
            input.Context with { CanAct = false },
            input.Context with { HasTarget = false },
            input.Context with { CanUseAttackActionOnTarget = false },
        };
        foreach (var context in invalidContexts)
        {
            var frame = Level100ResolverEngine.Evaluate(input with { Context = context });
            AssertEx.True(frame.GcdCandidate is null, "生命周期/目标Gate失败时不得产生GCD候选");
            AssertEx.True(frame.AlwaysCandidate is null, "生命周期/目标Gate失败时不得产生Always候选");
            AssertEx.True(frame.OffGcdCandidate is null, "生命周期/目标Gate失败时不得产生OffGCD候选");
            AssertEx.True(frame.DeliveryBlocked, "生命周期/目标Gate失败必须标记交付阻断");
            AssertEx.True(
                frame.BlockReason.Contains("LifecycleOrTargetGate", StringComparison.Ordinal),
                "生命周期/目标阻断必须给出稳定原因");
        }

        var autoPull = Level100ResolverEngine.Evaluate(input with
        {
            Context = input.Context with
            {
                InCombat = false,
                AutoPullEnabled = true,
                Phase = BlmPhase.Neutral,
                AstralFireStacks = 0,
                UmbralIceStacks = 0,
                UmbralHearts = 0,
            },
        });
        AssertCandidate(
            autoPull.GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "主动攻击脱战首发");
        AssertEx.False(autoPull.DeliveryBlocked, "主动攻击开启且目标有效时不应被生命周期Gate阻断");

        var aoe = Level100ResolverEngine.Evaluate(input with
        {
            Context = input.Context with
            {
                IsSingleTargetMode = false,
                EnemyCount = 3,
            },
        });
        AssertEx.True(aoe.GcdCandidate is null, "AOE健康帧不得产生GCD候选");
        AssertEx.False(aoe.DeliveryBlocked, "AOE健康帧不得被生命周期Gate阻断");

        var special = Level100ResolverEngine.Evaluate(input with { SpecialSequenceActive = true });
        AssertEx.True(special.GcdCandidate is null, "特殊序列必须排除普通Resolver");
        AssertEx.True(special.DeliveryBlocked, "特殊序列必须阻断普通交付");
        AssertEx.True(
            special.BlockReason.Contains("SpecialSequenceActive", StringComparison.Ordinal),
            "特殊序列阻断原因缺失");

        var highPriority = Level100ResolverEngine.Evaluate(input with
        {
            HighPriorityQueueActive = true,
        });
        AssertCandidate(
            highPriority.GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "高优队列存在时原始winner");
        AssertEx.True(highPriority.DeliveryBlocked, "高优队列必须只阻断交付");
        AssertEx.True(
            highPriority.BlockReason.Contains("HighPriorityQueueActive", StringComparison.Ordinal),
            "高优队列阻断原因缺失");

        var pendingGauge = Level100ResolverEngine.Evaluate(input with
        {
            PendingGaugeReconcile = true,
        });
        AssertCandidate(
            pendingGauge.GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "Gauge对账期间原始winner");
        AssertEx.True(pendingGauge.DeliveryBlocked, "Gauge对账必须只阻断交付");
        AssertEx.True(
            pendingGauge.BlockReason.Contains("PendingGaugeReconcile", StringComparison.Ordinal),
            "Gauge对账阻断原因缺失");
    }

    private static void NeutralAndPhaseRecovery()
    {
        var input = BaseInput();
        var neutral = Evaluate(input, input.Context with
        {
            Phase = BlmPhase.Neutral,
            AstralFireStacks = 0,
            UmbralIceStacks = 0,
            UmbralHearts = 0,
        });
        AssertCandidate(neutral.GcdCandidate, BLMSkill.冰封, "GCD.单体100", "Neutral恢复");

        var uiOne = input.Context with
        {
            Phase = BlmPhase.Ice,
            AstralFireStacks = 0,
            UmbralIceStacks = 1,
            UmbralHearts = 0,
            HasParadox = false,
        };
        AssertCandidate(
            Evaluate(input, uiOne).GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "UI1恢复");

        var swiftSoonInput = SetAction(
            input with { Context = uiOne with { HasParadox = true } },
            MageUniversalSkill.即刻咏唱,
            charges: 0f,
            cooldownRemainMs: 1500d);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(swiftSoonInput).GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "UI1等待即刻时冰悖论填充");

        var missingHearts = Evaluate(input, uiOne with
        {
            UmbralIceStacks = 3,
            UmbralHearts = 2,
        });
        AssertCandidate(
            missingHearts.GcdCandidate,
            BLMSkill.冰澈,
            "GCD.单体100",
            "UI3补冰针");

        var iceParadox = Evaluate(input, uiOne with
        {
            UmbralIceStacks = 3,
            UmbralHearts = 3,
            HasParadox = true,
        });
        AssertCandidate(
            iceParadox.GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "冰悖论");

        var delayedMpInput = WithPreviousGcd(
            input with
            {
                Context = uiOne with
                {
                    UmbralIceStacks = 3,
                    UmbralHearts = 3,
                    HasParadox = false,
                    Mp = 9000,
                },
            },
            Success(BLMSkill.冰封, 2, 9000, isInstant: false));
        AssertCandidate(
            Level100ResolverEngine.Evaluate(delayedMpInput).GcdCandidate,
            BLMSkill.冰澈,
            "GCD.单体100",
            "冰三后MP延迟恢复");

        var afOne = input.Context with
        {
            Phase = BlmPhase.Fire,
            AstralFireStacks = 1,
            UmbralIceStacks = 0,
            HasFirestarter = true,
            HasParadox = false,
        };
        AssertCandidate(
            Evaluate(input, afOne).GcdCandidate,
            BLMSkill.爆炎,
            "GCD.单体100",
            "AF1火苗恢复");
        AssertCandidate(
            Evaluate(input, afOne with
            {
                HasFirestarter = false,
                HasParadox = true,
            }).GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "AF1悖论恢复");
        AssertCandidate(
            Evaluate(input, afOne with
            {
                HasFirestarter = false,
                HasParadox = false,
                Mp = 0,
            }).GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "AF1空蓝恢复");

        var recovering = input with
        {
            Context = uiOne with
            {
                UmbralIceStacks = 3,
                UmbralHearts = 3,
            },
            Level100Loop = input.Level100Loop with { RecoveringAfterSpecial = true },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(recovering).GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "特殊循环异常恢复第一步");
        var recoveringAfterB3 = WithPreviousGcd(
            recovering,
            Success(BLMSkill.冰封, 3, 9000, isInstant: false));
        AssertCandidate(
            Level100ResolverEngine.Evaluate(recoveringAfterB3).GcdCandidate,
            BLMSkill.冰澈,
            "GCD.单体100",
            "特殊循环异常恢复第二步");
    }

    private static void FireLoopBoundaries()
    {
        var input = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                HasParadox = true,
                AstralSoulStacks = 0,
                Mp = 10_000,
            },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(input with
            {
                Level100Loop = new BlmLevel100LoopFacts { Fire4Count = 0 },
            }).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "压缩火悖论0发火四边界");

        var threeFire4 = input with
        {
            Context = input.Context with { AstralSoulStacks = 3 },
            Level100Loop = new BlmLevel100LoopFacts { Fire4Count = 3 },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(threeFire4).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "压缩火悖论3发时继续火四");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(threeFire4 with
            {
                Settings = threeFire4.Settings with { CompressFireParadox = false },
            }).GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "不压缩时3发后打火悖论");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(threeFire4 with
            {
                Context = threeFire4.Context with
                {
                    IsMoving = true,
                    HasSwiftcast = true,
                },
            }).GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "移动瞬发时压缩设置不得阻止火悖论");

        var sixFire4 = input with
        {
            Context = input.Context with { AstralSoulStacks = 6 },
            Level100Loop = new BlmLevel100LoopFacts { Fire4Count = 6 },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(sixFire4).GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "压缩火悖论6发边界");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(sixFire4 with
            {
                Level100Loop = sixFire4.Level100Loop with { FireParadoxUsed = true },
            }).GcdCandidate,
            BLMSkill.耀星,
            "GCD.单体100",
            "火悖论已记账后释放耀星");
    }

    private static void TtkFastFlareForcedRecovery()
    {
        var input = BaseInput();
        var ttk = input with
        {
            Context = input.Context with { PolyglotStacks = 1 },
            Settings = input.Settings with { TtkEnabled = true },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(ttk).GcdCandidate,
            BLMSkill.异言,
            "GCD.TTK",
            "TTK通晓优先");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(ttk with
            {
                Context = ttk.Context with
                {
                    PolyglotStacks = 0,
                    HasParadox = true,
                },
            }).GcdCandidate,
            BLMSkill.悖论,
            "GCD.TTK",
            "TTK悖论次优先");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(ttk with
            {
                Context = ttk.Context with
                {
                    PolyglotStacks = 0,
                    HasParadox = false,
                    AstralSoulStacks = 6,
                },
            }).GcdCandidate,
            BLMSkill.耀星,
            "GCD.TTK",
            "TTK耀星优先于绝望");

        var fast = input with
        {
            Context = input.Context with { AstralSoulStacks = 3 },
            Settings = input.Settings with { FastFlareStarEnabled = true },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(fast).GcdCandidate,
            BLMSkill.核爆,
            "GCD.快速耀星",
            "快速耀星3层核爆");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(fast with
            {
                Context = fast.Context with
                {
                    AstralSoulStacks = 5,
                    IsCasting = true,
                },
                CurrentCastingActionId = BLMSkill.炽炎,
            }).GcdCandidate,
            BLMSkill.耀星,
            "GCD.快速耀星",
            "快速耀星读条预判6层");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(fast with
            {
                Context = fast.Context with
                {
                    AstralSoulStacks = 5,
                    IsCasting = false,
                },
                CurrentCastingActionId = BLMSkill.炽炎,
            }).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.快速耀星",
            "非读条状态必须忽略残留CastActionId");

        var forced = input with { NeedsForcedIceRecovery = true };
        var forcedFrame = Level100ResolverEngine.Evaluate(forced);
        AssertCandidate(
            forcedFrame.GcdCandidate,
            BLMSkill.冰封,
            "GCD.强制回冰",
            "强制回冰优先于主循环");
        AssertEx.Equal(500, forcedFrame.GcdCandidate!.CheckCode, "强制回冰放行码必须保留500");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(ttk with
            {
                NeedsForcedIceRecovery = true,
            }).GcdCandidate,
            BLMSkill.异言,
            "GCD.TTK",
            "TTK必须保持在强制恢复之前");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(forced with
            {
                Context = forced.Context with
                {
                    PolyglotStacks = 3,
                    PolyglotTimerMs = 5000,
                    HasThunderhead = true,
                },
                DotTargets =
                [
                    Target(CurrentTargetId, 1_000_000, 900_000, 10f, 0f, 0f),
                    forced.DotTargets[1],
                ],
            }).GcdCandidate,
            BLMSkill.冰封,
            "GCD.强制回冰",
            "强制恢复必须先于异言和雷Resolver");
    }

    private static void ThunderMovementAndIdle()
    {
        var input = BaseInput() with
        {
            Context = BaseInput().Context with { HasThunderhead = true },
        };
        input = input with
        {
            DotTargets =
            [
                Target(CurrentTargetId, 1_000_000, 900_000, 10f, 3500f, 3500f),
                Target(201, 800_000, 700_000, 12f, 10_000f, 10_000f),
            ],
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(input).GcdCandidate,
            BLMSkill.高闪雷,
            "GCD.雷1",
            "雷Dot恰好3500ms应补");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(input with
            {
                DotTargets =
                [
                    Target(CurrentTargetId, 1_000_000, 900_000, 10f, 3500.1f, 3500.1f),
                    input.DotTargets[1],
                ],
            }).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "雷Dot超过3500ms不得提前补");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(input with
            {
                DotTargets =
                [
                    Target(CurrentTargetId, 1_000_000, 29_999, 10f, 0f, 0f),
                    input.DotTargets[1],
                ],
            }).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "目标低于3%不得上雷");

        var moving = input with
        {
            Context = input.Context with
            {
                IsMoving = true,
                HasThunderhead = false,
                PolyglotStacks = 1,
            },
            DotTargets = DefaultTargets(),
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(moving).GcdCandidate,
            BLMSkill.异言,
            "GCD.瞬发gcd触发器",
            "移动时使用异言");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(moving with
            {
                Context = moving.Context with { IsMoving = false },
                IsIdle = true,
            }).GcdCandidate,
            BLMSkill.异言,
            "GCD.瞬发gcd触发器",
            "发呆恢复使用异言");

        var earlyThunder = input with
        {
            Context = input.Context with { IsMoving = true },
            DotTargets =
            [
                Target(CurrentTargetId, 1_000_000, 900_000, 10f, 5000f, 5000f),
                input.DotTargets[1],
            ],
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(earlyThunder).GcdCandidate,
            BLMSkill.高闪雷,
            "GCD.瞬发gcd触发器",
            "移动时应使用6000ms提前雷窗口");

        var swiftReady = SetAction(
            moving with
            {
                Context = moving.Context with { PolyglotStacks = 0 },
                Settings = moving.Settings with { MoveXenoglossyEnabled = false },
            },
            MageUniversalSkill.即刻咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);
        var noMovementSwift = Level100ResolverEngine.Evaluate(swiftReady);
        AssertEx.True(noMovementSwift.GcdCandidate is null, "无瞬发GCD资源的移动状态应保持空GCD");
        AssertEx.True(noMovementSwift.OffGcdCandidate is null, "PR即刻不得作为普通移动资源");

        var moveTriplecast = SetAction(
            moving with
            {
                Context = moving.Context with { PolyglotStacks = 0 },
                Settings = moving.Settings with { MoveXenoglossyEnabled = false },
            },
            BLMSkill.三连咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);
        var moveTriplecastFrame = Level100ResolverEngine.Evaluate(moveTriplecast);
        AssertEx.True(moveTriplecastFrame.GcdCandidate is null, "移动三连前原始GCD应为空");
        AssertCandidate(
            moveTriplecastFrame.OffGcdCandidate,
            BLMSkill.三连咏唱,
            "Ability.三连咏唱",
            "独立移动三连");
        AssertEx.Equal(50, moveTriplecastFrame.OffGcdCandidate!.CheckCode, "移动三连放行码必须保留50");
        AssertEx.True(
            Level100ResolverEngine.Evaluate(moveTriplecast with
            {
                Settings = moveTriplecast.Settings with { MoveTriplecastEnabled = false },
            }).OffGcdCandidate is null,
            "关闭独立移动三连后不得消耗三连");
    }

    private static void DoubleDotSelection()
    {
        var input = BaseInput() with
        {
            Context = BaseInput().Context with { HasThunderhead = true },
            Settings = BaseInput().Settings with { DoubleDotEnabled = true },
            DotTargets =
            [
                Target(CurrentTargetId, 1_000_000, 900_000, 8f, 10_000f, 10_000f),
                Target(201, 900_000, 800_000, 12f, 0f, 0f),
                Target(202, 800_000, 700_000, 6f, 0f, 0f),
            ],
        };
        var chosen = Level100ResolverEngine.Evaluate(input).GcdCandidate;
        AssertCandidate(chosen, BLMSkill.高闪雷, "GCD.双DOT", "双DOT缺失目标选择");
        AssertEx.Equal(201u, chosen!.TargetId, "双DOT缺失目标应优先最大MaxHP");
        AssertEx.Equal(
            BlmResolverTargetKind.SpecifiedTarget,
            chosen.TargetKind,
            "双DOT必须保留指定目标类型");

        var currentMissing = input with
        {
            DotTargets =
            [
                Target(CurrentTargetId, 1_000_000, 900_000, 8f, 0f, 0f),
                Target(201, 900_000, 800_000, 12f, 10_000f, 10_000f),
            ],
        };
        AssertEx.Equal(
            CurrentTargetId,
            Level100ResolverEngine.Evaluate(currentMissing).GcdCandidate!.TargetId,
            "当前目标缺Dot时允许选择当前目标");

        var repeatProtected = input with
        {
            DotTargets =
            [
                input.DotTargets[0],
                Target(201, 900_000, 800_000, 12f, 0f, 0f),
            ],
            DoubleDotMemory = new BlmDoubleDotMemory(201, NowMs - 2499),
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(repeatProtected).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "双DOT 2499ms重复保护");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(repeatProtected with
            {
                DoubleDotMemory = new BlmDoubleDotMemory(201, NowMs - 2500),
            }).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "2500ms非重复边界仍受3500ms已投递保护");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(repeatProtected with
            {
                DoubleDotMemory = new BlmDoubleDotMemory(201, NowMs - 3499),
            }).GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "双DOT 3499ms已投递保护");
        var expired = Level100ResolverEngine.Evaluate(repeatProtected with
        {
            DoubleDotMemory = new BlmDoubleDotMemory(201, NowMs - 3500),
        }).GcdCandidate;
        AssertCandidate(expired, BLMSkill.高闪雷, "GCD.双DOT", "双DOT 3500ms过期边界");
        AssertEx.Equal(201u, expired!.TargetId, "3500ms边界应重新允许原目标");
    }

    private static void FireEndTransposeSwiftcastFrames()
    {
        var afterDespair = WithPreviousGcd(
            BaseInput() with
            {
                Context = BaseInput().Context with
                {
                    Mp = 0,
                    AstralSoulStacks = 0,
                    HasParadox = false,
                },
            },
            Success(BLMSkill.绝望, 10, 9000, isInstant: true));
        afterDespair = SetAction(
            afterDespair,
            MageUniversalSkill.即刻咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);

        var fireEndFrame = Level100ResolverEngine.Evaluate(afterDespair);
        AssertCandidate(
            fireEndFrame.GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "绝望后的原始GCD winner");
        AssertCandidate(
            fireEndFrame.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "绝望后星灵");
        AssertEx.True(fireEndFrame.HoldGcdForTranspose, "绝望后必须标记Hold等待星灵");
        AssertEx.True(
            fireEndFrame.GcdBlockedByTransposeHold,
            "Hold必须阻断主循环硬读冰三");
        AssertEx.True(fireEndFrame.OffGcdCandidate is null, "火态不得提前释放即刻");

        var transposeAck = Success(
            BLMSkill.星灵移位,
            11,
            9400,
            isInstant: true,
            isGcd: false);
        var afterTranspose = afterDespair with
        {
            Context = afterDespair.Context with
            {
                Phase = BlmPhase.Ice,
                AstralFireStacks = 0,
                UmbralIceStacks = 1,
                UmbralHearts = 3,
                HasParadox = true,
            },
            RecentHistory = afterDespair.RecentHistory.Add(transposeAck),
        };
        afterTranspose = SetAction(
            afterTranspose,
            BLMSkill.星灵移位,
            charges: 0f,
            cooldownRemainMs: 5000d);
        var transposeFrame = Level100ResolverEngine.Evaluate(afterTranspose);
        AssertCandidate(
            transposeFrame.GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "星灵后即刻Ack前的原始冰悖论填充");
        AssertCandidate(
            transposeFrame.OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "星灵后即刻");
        AssertEx.False(transposeFrame.HoldGcdForTranspose, "星灵生效后不得继续保留Hold");
        AssertEx.False(
            transposeFrame.GcdBlockedByTransposeHold,
            "没有Hold时不得阻断冰悖论填充");

        var swiftAck = Success(
            MageUniversalSkill.即刻咏唱,
            12,
            9600,
            isInstant: true,
            isGcd: false);
        var afterSwiftcast = afterTranspose with
        {
            Context = afterTranspose.Context with { HasSwiftcast = true },
            RecentHistory = afterTranspose.RecentHistory.Add(swiftAck),
        };
        var swiftFrame = Level100ResolverEngine.Evaluate(afterSwiftcast);
        AssertCandidate(
            swiftFrame.GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "即刻后冰三");
        AssertEx.True(
            swiftFrame.OffGcdCandidate is null,
            "即刻Ack后不得重复返回即刻能力");
    }

    private static void ManualIceRecoveryAndTriplecastSubstitute()
    {
        var fullIceLowMp = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                Phase = BlmPhase.Ice,
                AstralFireStacks = 0,
                UmbralIceStacks = 3,
                UmbralHearts = 3,
                Mp = 9499,
                HasParadox = false,
            },
        };
        var fullIceLowMpFrame = Level100ResolverEngine.Evaluate(fullIceLowMp);
        AssertCandidate(
            fullIceLowMpFrame.GcdCandidate,
            BLMSkill.冰澈,
            "GCD.单体100",
            "100级UI3冰针3的9499MP继续冰澈");
        AssertEx.True(
            fullIceLowMpFrame.AlwaysCandidate is null,
            "100级9499MP不得提前星灵转火");
        var ttkFullIceLowMpFrame = Level100ResolverEngine.Evaluate(
            fullIceLowMp with
            {
                Settings = fullIceLowMp.Settings with { TtkEnabled = true },
            });
        AssertCandidate(
            ttkFullIceLowMpFrame.GcdCandidate,
            BLMSkill.冰澈,
            "GCD.单体100",
            "100级TTK不得绕过9499MP离冰门");
        AssertEx.True(
            ttkFullIceLowMpFrame.AlwaysCandidate is null,
            "100级TTK低蓝不得星灵");

        var fullIceThresholdFrame = Level100ResolverEngine.Evaluate(
            fullIceLowMp with
            {
                Context = fullIceLowMp.Context with { Mp = 9500 },
            });
        AssertEx.True(
            fullIceThresholdFrame.GcdCandidate is null,
            "100级9500MP必须允许离冰");
        AssertCandidate(
            fullIceThresholdFrame.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "100级9500MP离冰边界");

        var iceFourAckWithDelayedGauge = WithPreviousGcd(
            fullIceLowMp with
            {
                Context = fullIceLowMp.Context with { UmbralHearts = 2 },
            },
            Success(BLMSkill.冰澈, 2, NowMs - 100, isInstant: false));
        var delayedGaugeAfterIceFour =
            Level100ResolverEngine.Evaluate(iceFourAckWithDelayedGauge);
        AssertEx.True(
            delayedGaugeAfterIceFour.GcdCandidate is null,
            "100级冰澈Ack领先Gauge时不得连打第二发冰澈");
        AssertCandidate(
            delayedGaugeAfterIceFour.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "100级冰澈Ack领先Gauge允许转火");

        var paradoxAfterIceFour = Level100ResolverEngine.Evaluate(
            iceFourAckWithDelayedGauge with
            {
                Context = iceFourAckWithDelayedGauge.Context with
                {
                    HasParadox = true,
                },
            });
        AssertCandidate(
            paradoxAfterIceFour.GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "跳过策略废弃后冰悖论必须始终消费");
        AssertEx.True(
            paradoxAfterIceFour.AlwaysCandidate is null,
            "冰悖论存在时不得星灵转火");

        var manualUiOne = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                Phase = BlmPhase.Ice,
                AstralFireStacks = 0,
                UmbralIceStacks = 1,
                UmbralHearts = 0,
                HasParadox = false,
            },
            Settings = BaseInput().Settings with { SwiftcastIntoIceEnabled = false },
        };
        manualUiOne = SetAction(
            manualUiOne,
            MageUniversalSkill.即刻咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(manualUiOne).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "手动进入UI1仍应实时即刻自愈");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(manualUiOne with
            {
                Context = manualUiOne.Context with { HasParadox = true },
            }).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "异常UI1悖论状态仍应即刻自愈");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(manualUiOne with
            {
                Settings = manualUiOne.Settings with { TtkEnabled = true },
            }).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "TTK开关不得阻止UI1异常恢复");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(manualUiOne with
            {
                Settings = manualUiOne.Settings with { FastFlareStarEnabled = true },
            }).OffGcdCandidate,
            MageUniversalSkill.即刻咏唱,
            "Ability.即刻",
            "快速耀星开关不得阻止UI1异常恢复");
        var fireWithModes = manualUiOne with
        {
            Context = manualUiOne.Context with
            {
                Phase = BlmPhase.Fire,
                AstralFireStacks = 3,
                UmbralIceStacks = 0,
            },
            Settings = manualUiOne.Settings with
            {
                TtkEnabled = true,
                FastFlareStarEnabled = true,
            },
        };
        AssertEx.True(
            Level100ResolverEngine.Evaluate(fireWithModes).OffGcdCandidate is null,
            "火态TTK/快速耀星不得征用即刻");

        var tripleRoute = WithPreviousGcd(
            BaseInput() with
            {
                Context = BaseInput().Context with
                {
                    Mp = 0,
                    AstralSoulStacks = 0,
                },
            },
            Success(BLMSkill.绝望, 20, 9000, isInstant: true));
        tripleRoute = SetAction(
            tripleRoute,
            BLMSkill.三连咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);
        var fireFrame = Level100ResolverEngine.Evaluate(tripleRoute);
        AssertCandidate(
            fireFrame.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "三连可用时仍应先星灵");

        var afterTranspose = tripleRoute with
        {
            Context = tripleRoute.Context with
            {
                Phase = BlmPhase.Ice,
                AstralFireStacks = 0,
                UmbralIceStacks = 1,
                HasParadox = true,
            },
        };
        afterTranspose = SetAction(
            afterTranspose,
            BLMSkill.星灵移位,
            charges: 0f,
            cooldownRemainMs: 5000d);
        var paradoxFrame = Level100ResolverEngine.Evaluate(afterTranspose);
        AssertCandidate(
            paradoxFrame.GcdCandidate,
            BLMSkill.悖论,
            "GCD.单体100",
            "三连替代路线先用UI1悖论填充");
        AssertEx.True(
            paradoxFrame.OffGcdCandidate is null,
            "仍有冰悖论时三连必须等待");

        var afterIceParadox = WithPreviousGcd(
            afterTranspose with
            {
                Context = afterTranspose.Context with { HasParadox = false },
            },
            Success(BLMSkill.悖论, 21, 9700, isInstant: true));
        var tripleFrame = Level100ResolverEngine.Evaluate(afterIceParadox);
        AssertCandidate(
            tripleFrame.GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "冰悖论后原始冰三winner");
        AssertCandidate(
            tripleFrame.OffGcdCandidate,
            BLMSkill.三连咏唱,
            "Ability.三连咏唱",
            "即刻不可用时三连替代");
        AssertEx.Equal(2, tripleFrame.OffGcdCandidate!.CheckCode, "三连进冰放行码必须保留2");

        var afterTriplecast = afterIceParadox with
        {
            Context = afterIceParadox.Context with { TriplecastStacks = 3 },
        };
        AssertCandidate(
            Level100ResolverEngine.Evaluate(afterTriplecast).GcdCandidate,
            BLMSkill.冰封,
            "GCD.单体100",
            "三连Buff后冰三");
    }

    private static void ManafontArbitrationAndGaugeDelivery()
    {
        var current = BaseInput() with
        {
            Context = BaseInput().Context with
            {
                Mp = 0,
                AstralSoulStacks = 0,
            },
        };
        current = SetAction(
            current,
            BLMSkill.魔泉,
            charges: 1f,
            cooldownRemainMs: 0d);
        var currentFrame = Level100ResolverEngine.Evaluate(current);
        AssertCandidate(
            currentFrame.OffGcdCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            "Manafont当前可用");
        AssertEx.True(currentFrame.AlwaysCandidate is null, "Manafont可用时星灵必须让路");
        AssertEx.False(currentFrame.HoldGcdForTranspose, "Manafont可用时不得Hold等待星灵");

        var upcoming = SetAction(
            current with
            {
                Context = current.Context with { PolyglotStacks = 1 },
            },
            BLMSkill.魔泉,
            charges: 0f,
            cooldownRemainMs: 1500d);
        var upcomingFrame = Level100ResolverEngine.Evaluate(upcoming);
        AssertCandidate(
            upcomingFrame.GcdCandidate,
            BLMSkill.异言,
            "GCD.异言#1",
            "Manafont即将转好时异言等待窗口");
        AssertEx.Equal(4, upcomingFrame.GcdCandidate!.CheckCode, "Manafont等待异言放行码必须保留4");
        AssertEx.True(upcomingFrame.AlwaysCandidate is null, "Manafont即将转好时星灵必须让路");
        AssertEx.True(upcomingFrame.OffGcdCandidate is null, "Manafont未转好时不得提前返回能力");

        var previousIce4 = WithPreviousGcd(
            current,
            Success(BLMSkill.冰澈, 30, 9000, isInstant: false));
        previousIce4 = previousIce4 with
        {
            RecentHistory = previousIce4.RecentHistory.Add(
                Success(BLMSkill.星灵移位, 31, 9400, isInstant: true, isGcd: false)),
        };
        var excluded = Level100ResolverEngine.Evaluate(previousIce4);
        AssertEx.True(
            excluded.OffGcdCandidate is null,
            "上一GCD冰澈且上一能力星灵时Manafont必须排除");

        var pendingGauge = Level100ResolverEngine.Evaluate(current with
        {
            PendingGaugeReconcile = true,
        });
        AssertCandidate(
            pendingGauge.OffGcdCandidate,
            BLMSkill.魔泉,
            "Ability.墨泉",
            "Manafont对账前原始winner");
        AssertEx.True(pendingGauge.DeliveryBlocked, "Manafont对账前必须阻断交付");

        var manafontAck = Success(
            BLMSkill.魔泉,
            32,
            9600,
            isInstant: true,
            isGcd: false);
        var restored = current with
        {
            Context = current.Context with
            {
                Mp = 10_000,
                UmbralHearts = 3,
                HasParadox = true,
                HasThunderhead = true,
            },
            RecentHistory = current.RecentHistory.Add(manafontAck),
            PendingGaugeReconcile = false,
        };
        restored = SetAction(
            restored,
            BLMSkill.魔泉,
            charges: 0f,
            cooldownRemainMs: 120_000d);
        var restoredFrame = Level100ResolverEngine.Evaluate(restored);
        AssertCandidate(
            restoredFrame.GcdCandidate,
            BLMSkill.炽炎,
            "GCD.单体100",
            "Manafont资源对账后恢复主循环");
        AssertEx.True(restoredFrame.OffGcdCandidate is null, "Manafont对账后不得重复返回Manafont");
        AssertEx.False(restoredFrame.DeliveryBlocked, "资源对账完成后必须解除交付阻断");
    }

    private static void OffGcdPriorityDefenseAndPotion()
    {
        var input = BaseInput();
        var triple = SetAction(
            input with
            {
                Settings = input.Settings with { TtkEnabled = true },
            },
            BLMSkill.三连咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);
        var tripleCandidate = Level100ResolverEngine.Evaluate(triple).OffGcdCandidate;
        AssertCandidate(
            tripleCandidate,
            BLMSkill.三连咏唱,
            "Ability.三连咏唱",
            "OffGCD三连优先级");
        AssertEx.Equal(24, tripleCandidate!.ManifestOrder, "三连manifest顺序错误");

        var lucid = SetAction(
            input with
            {
                Settings = input.Settings with { TtkEnabled = true },
            },
            MageUniversalSkill.醒梦,
            charges: 1f,
            cooldownRemainMs: 0d);
        var lucidCandidate = Level100ResolverEngine.Evaluate(lucid).OffGcdCandidate;
        AssertCandidate(lucidCandidate, MageUniversalSkill.醒梦, "Ability.醒梦", "OffGCD醒梦优先级");
        AssertEx.Equal(25, lucidCandidate!.ManifestOrder, "醒梦manifest顺序错误");

        var amplifier = SetAction(
            input,
            BLMSkill.详述,
            charges: 1f,
            cooldownRemainMs: 0d);
        var amplifierCandidate = Level100ResolverEngine.Evaluate(amplifier).OffGcdCandidate;
        AssertCandidate(amplifierCandidate, BLMSkill.详述, "Ability.详述", "OffGCD详述优先级");
        AssertEx.Equal(26, amplifierCandidate!.ManifestOrder, "详述manifest顺序错误");

        var manaAndAmplifier = SetAction(
            amplifier with
            {
                Context = amplifier.Context with { Mp = 0 },
            },
            BLMSkill.魔泉,
            charges: 1f,
            cooldownRemainMs: 0d);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(manaAndAmplifier).OffGcdCandidate,
            BLMSkill.详述,
            "Ability.详述",
            "详述必须先于Manafont");

        var leyLines = SetAction(
            input,
            BLMSkill.黑魔纹,
            charges: 1f,
            cooldownRemainMs: 0d);
        var leyCandidate = Level100ResolverEngine.Evaluate(leyLines).OffGcdCandidate;
        AssertCandidate(leyCandidate, BLMSkill.黑魔纹, "Ability.黑魔纹", "OffGCD黑魔纹优先级");
        AssertEx.Equal(28, leyCandidate!.ManifestOrder, "黑魔纹manifest顺序错误");
        AssertEx.True(
            Level100ResolverEngine.Evaluate(leyLines with
            {
                Context = leyLines.Context with
                {
                    HasLeyLinesStatus737 = true,
                    HasLeyLinesHaste738 = false,
                },
            }).OffGcdCandidate is null,
            "状态737存在时不得重复放黑魔纹");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(leyLines with
            {
                Context = leyLines.Context with
                {
                    HasLeyLinesStatus737 = false,
                    HasLeyLinesHaste738 = true,
                },
            }).OffGcdCandidate,
            BLMSkill.黑魔纹,
            "Ability.黑魔纹",
            "状态738不得冒充黑魔纹状态737");

        var casualDying = leyLines with
        {
            CasualCombat = new BlmCasualCombatFacts
            {
                IsCasualDutyNonBoss = true,
                NearbyEnemiesTotalHpRatio = 0.1f,
                NearbyEnemiesAverageTtkMs = 20_000f,
            },
        };
        AssertEx.True(
            Level100ResolverEngine.Evaluate(casualDying).OffGcdCandidate is null,
            "日常小怪即将死亡时应保留黑魔纹");
        var casualHealthy = SetAction(
            casualDying with
            {
                CasualCombat = casualDying.CasualCombat with
                {
                    NearbyEnemiesTotalHpRatio = 0.5f,
                },
            },
            BLMSkill.黑魔纹,
            charges: 2f,
            cooldownRemainMs: 0d);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(casualHealthy).OffGcdCandidate,
            BLMSkill.黑魔纹,
            "Ability.黑魔纹",
            "日常健康目标且双层黑魔纹");

        var addle = SetAction(
            input with
            {
                DefensiveCast = new BlmDefensiveCastFacts
                {
                    TargetCastIsDeathSentenceWithin3Seconds = true,
                },
            },
            MageUniversalSkill.昏乱,
            charges: 1f,
            cooldownRemainMs: 0d);
        var addleCandidate = Level100ResolverEngine.Evaluate(addle).OffGcdCandidate;
        AssertCandidate(addleCandidate, MageUniversalSkill.昏乱, "Ability.Auto昏乱", "自动昏乱");
        AssertEx.Equal(0, addleCandidate!.CheckCode, "Auto昏乱必须接受CheckCode=0");
        AssertEx.Equal(29, addleCandidate.ManifestOrder, "Auto昏乱manifest顺序错误");

        var manaward = SetAction(
            SetAction(
                input with
                {
                    DefensiveCast = new BlmDefensiveCastFacts
                    {
                        TargetCastIsBossAoeWithin3Seconds = true,
                    },
                },
                MageUniversalSkill.昏乱,
                charges: 1f,
                cooldownRemainMs: 0d),
            BLMSkill.魔罩,
            charges: 1f,
            cooldownRemainMs: 0d);
        var manawardCandidate = Level100ResolverEngine.Evaluate(manaward).OffGcdCandidate;
        AssertCandidate(manawardCandidate, BLMSkill.魔罩, "Ability.Auto魔罩", "自动魔罩");
        AssertEx.Equal(0, manawardCandidate!.CheckCode, "Auto魔罩必须接受CheckCode=0");
        AssertEx.Equal(30, manawardCandidate.ManifestOrder, "Auto魔罩manifest顺序错误");

        var addleFallback = SetAction(
            manaward,
            BLMSkill.魔罩,
            charges: 0f,
            cooldownRemainMs: 10_000d);
        AssertCandidate(
            Level100ResolverEngine.Evaluate(addleFallback).OffGcdCandidate,
            MageUniversalSkill.昏乱,
            "Ability.Auto昏乱",
            "魔罩不可用时AOE昏乱兜底");

        var potion = input with
        {
            Settings = input.Settings with { PotionEnabled = true },
            IsPotionAvailable = true,
        };
        var potionCandidate = Level100ResolverEngine.Evaluate(potion).OffGcdCandidate;
        AssertCandidate(potionCandidate, 0, "Ability.爆发药", "爆发药");
        AssertEx.Equal(
            BlmResolverTargetKind.Potion,
            potionCandidate!.TargetKind,
            "爆发药必须保留独立目标类型");
        AssertEx.Equal(31, potionCandidate.ManifestOrder, "爆发药manifest顺序错误");

        var amplifierTooLate = amplifier with
        {
            Context = amplifier.Context with { GcdRemainSeconds = 0.499f },
        };
        AssertEx.True(
            Level100ResolverEngine.Evaluate(amplifierTooLate).OffGcdCandidate is null,
            "详述在499ms不得放行");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(amplifierTooLate with
            {
                Context = amplifierTooLate.Context with { GcdRemainSeconds = 0.5f },
            }).OffGcdCandidate,
            BLMSkill.详述,
            "Ability.详述",
            "详述500ms边界");

        var addleTooLate = addle with
        {
            Context = addle.Context with { GcdRemainSeconds = 0.099f },
        };
        AssertEx.True(
            Level100ResolverEngine.Evaluate(addleTooLate).OffGcdCandidate is null,
            "自动减伤在99ms不得放行");
        AssertCandidate(
            Level100ResolverEngine.Evaluate(addleTooLate with
            {
                Context = addleTooLate.Context with { GcdRemainSeconds = 0.1f },
            }).OffGcdCandidate,
            MageUniversalSkill.昏乱,
            "Ability.Auto昏乱",
            "自动减伤100ms边界");
    }

    private static void HoldAndWeaveBoundaries()
    {
        var holdInput = WithPreviousGcd(
            BaseInput() with
            {
                Context = BaseInput().Context with
                {
                    Mp = 0,
                    AstralSoulStacks = 0,
                },
            },
            Success(BLMSkill.绝望, 40, 9000, isInstant: true));
        holdInput = SetAction(
            holdInput,
            MageUniversalSkill.即刻咏唱,
            charges: 1f,
            cooldownRemainMs: 0d);
        var ready = Level100ResolverEngine.Evaluate(holdInput);
        AssertCandidate(ready.GcdCandidate, BLMSkill.冰封, "GCD.单体100", "Hold时原始GCD");
        AssertCandidate(
            ready.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "星灵ready Hold");
        AssertEx.True(ready.HoldGcdForTranspose, "星灵ready时必须Hold");
        AssertEx.True(
            ready.GcdBlockedByTransposeHold,
            "星灵ready时主循环硬读冰三必须被Hold阻断");

        var transpose1000 = SetAction(
            holdInput,
            BLMSkill.星灵移位,
            charges: 0f,
            cooldownRemainMs: 1000d);
        var notReady = Level100ResolverEngine.Evaluate(transpose1000);
        AssertCandidate(notReady.GcdCandidate, BLMSkill.冰封, "GCD.单体100", "星灵未ready原始GCD");
        AssertEx.True(notReady.AlwaysCandidate is null, "星灵未ready不得产生Always候选");
        AssertEx.True(notReady.HoldGcdForTranspose, "星灵1000ms内转好仍必须Hold");
        AssertEx.True(
            notReady.GcdBlockedByTransposeHold,
            "星灵未ready但即将转好时仍须阻断主循环硬读冰三");
        AssertEx.True(
            Level100ResolverEngine.Evaluate(SetAction(
                holdInput,
                BLMSkill.星灵移位,
                charges: 0f,
                cooldownRemainMs: 2000d)).HoldGcdForTranspose,
            "星灵恰好2000ms必须Hold");
        AssertEx.False(
            Level100ResolverEngine.Evaluate(SetAction(
                holdInput,
                BLMSkill.星灵移位,
                charges: 0f,
                cooldownRemainMs: 2000.1d)).HoldGcdForTranspose,
            "星灵超过2000ms不得Hold");

        var xenoglossyFill = transpose1000 with
        {
            Context = transpose1000.Context with { PolyglotStacks = 1 },
            Settings = transpose1000.Settings with { DumpPolyglotEnabled = true },
        };
        var xenoglossyFillFrame = Level100ResolverEngine.Evaluate(xenoglossyFill);
        AssertCandidate(
            xenoglossyFillFrame.GcdCandidate,
            BLMSkill.异言,
            "GCD.异言#1",
            "等待星灵时异言填充");
        AssertEx.True(xenoglossyFillFrame.HoldGcdForTranspose, "异言填充时星灵路线仍应Hold");
        AssertEx.False(
            xenoglossyFillFrame.GcdBlockedByTransposeHold,
            "Hold不得吞掉前置异言填充");

        var thunderFill = transpose1000 with
        {
            Context = transpose1000.Context with { HasThunderhead = true },
            DotTargets =
            [
                Target(CurrentTargetId, 1_000_000, 900_000, 10f, 0f, 0f),
                transpose1000.DotTargets[1],
            ],
        };
        var thunderFillFrame = Level100ResolverEngine.Evaluate(thunderFill);
        AssertCandidate(
            thunderFillFrame.GcdCandidate,
            BLMSkill.高闪雷,
            "GCD.雷1",
            "等待星灵时雷填充");
        AssertEx.True(thunderFillFrame.HoldGcdForTranspose, "雷填充时星灵路线仍应Hold");
        AssertEx.False(
            thunderFillFrame.GcdBlockedByTransposeHold,
            "Hold不得吞掉前置雷填充");

        var blockedHold = Level100ResolverEngine.Evaluate(holdInput with
        {
            HighPriorityQueueActive = true,
            PendingGaugeReconcile = true,
        });
        AssertCandidate(
            blockedHold.AlwaysCandidate,
            BLMSkill.星灵移位,
            "Ability.星灵移位",
            "阻断期间原始星灵winner");
        AssertEx.True(blockedHold.HoldGcdForTranspose, "交付阻断不得擦除Hold事实");
        AssertEx.True(
            blockedHold.GcdBlockedByTransposeHold,
            "高优/Gauge阻断不得擦除GCD专用Hold事实");
        AssertEx.True(blockedHold.DeliveryBlocked, "高优/Gauge必须阻断交付");

        var manafontWins = SetAction(
            holdInput,
            BLMSkill.魔泉,
            charges: 1f,
            cooldownRemainMs: 0d);
        AssertEx.False(
            Level100ResolverEngine.Evaluate(manafontWins).HoldGcdForTranspose,
            "Manafont仲裁成立时不得Hold星灵");

        var normal = Level100ResolverEngine.Evaluate(BaseInput());
        AssertEx.Equal(1, normal.ExecutorWeaveSlots, "普通确认GCD执行器应有1槽");
        AssertEx.Equal(1, normal.ResolverAllowedWeaves, "减少动画锁下普通GCD Resolver应有1槽");
        AssertEx.Equal(1, normal.RemainingWeaves, "普通GCD未插入时应剩1槽");
        AssertEx.Equal(
            0,
            Level100ResolverEngine.Evaluate(BaseInput() with { UsedWeaves = 1 }).RemainingWeaves,
            "单插用尽后剩余应为0");
        AssertEx.Equal(
            0,
            Level100ResolverEngine.Evaluate(BaseInput() with { UsedWeaves = 5 }).RemainingWeaves,
            "UsedWeaves超额时必须钳制为0");

        var reducedOff = Level100ResolverEngine.Evaluate(BaseInput() with
        {
            Settings = BaseInput().Settings with { ReducedAnimationLockEnabled = false },
        });
        AssertEx.Equal(1, reducedOff.ExecutorWeaveSlots, "执行器容量不得被Resolver设置改写");
        AssertEx.Equal(0, reducedOff.ResolverAllowedWeaves, "普通读条且无减少动画锁应为0槽");
        AssertEx.Equal(0, reducedOff.RemainingWeaves, "有效剩余容量必须取两层最小值");

        var noPrevious = Level100ResolverEngine.Evaluate(BaseInput() with
        {
            PreviousGcd = null,
            RecentHistory = [],
        });
        AssertEx.Equal(0, noPrevious.ExecutorWeaveSlots, "无确认GCD执行器容量应为0");
        AssertEx.Equal(0, noPrevious.ResolverAllowedWeaves, "无确认GCD Resolver容量应为0");
        AssertEx.Equal(0, noPrevious.RemainingWeaves, "无确认GCD剩余容量应为0");

        var instantPrevious = WithPreviousGcd(
            BaseInput(),
            Success(BLMSkill.炽炎, 41, 9000, isInstant: true));
        var instant = Level100ResolverEngine.Evaluate(instantPrevious);
        AssertEx.Equal(2, instant.ExecutorWeaveSlots, "确认瞬发GCD执行器应有2槽");
        AssertEx.Equal(2, instant.ResolverAllowedWeaves, "确认瞬发GCD Resolver应有2槽");
        AssertEx.Equal(2, instant.RemainingWeaves, "确认瞬发GCD未插入应剩2槽");
        AssertEx.Equal(
            1,
            Level100ResolverEngine.Evaluate(instantPrevious with { UsedWeaves = 1 }).RemainingWeaves,
            "双插使用一次后应剩1槽");

        var xenoglossyPrevious = WithPreviousGcd(
            BaseInput(),
            Success(BLMSkill.异言, 42, 9000, isInstant: false));
        var xenoglossy = Level100ResolverEngine.Evaluate(xenoglossyPrevious);
        AssertEx.Equal(1, xenoglossy.ExecutorWeaveSlots, "执行器只认确认瞬发事实");
        AssertEx.Equal(2, xenoglossy.ResolverAllowedWeaves, "异言固有Resolver容量应为2");
        AssertEx.Equal(1, xenoglossy.RemainingWeaves, "有效容量仍须取执行器较小值");
        var confirmedInstantXenoglossy = Level100ResolverEngine.Evaluate(
            WithPreviousGcd(
                BaseInput(),
                Success(BLMSkill.异言, 44, 9000, isInstant: true)));
        AssertEx.Equal(2, confirmedInstantXenoglossy.ExecutorWeaveSlots, "确认瞬发异言执行器应有2槽");
        AssertEx.Equal(2, confirmedInstantXenoglossy.ResolverAllowedWeaves, "确认瞬发异言Resolver应有2槽");
        AssertEx.Equal(2, confirmedInstantXenoglossy.RemainingWeaves, "确认瞬发异言应剩2槽");

        var fireThreePrevious = WithPreviousGcd(
            BaseInput(),
            Success(BLMSkill.爆炎, 43, 9000, isInstant: false));
        var fireThree = Level100ResolverEngine.Evaluate(fireThreePrevious);
        AssertEx.Equal(1, fireThree.ExecutorWeaveSlots, "火三执行器应有1槽");
        AssertEx.Equal(1, fireThree.ResolverAllowedWeaves, "火三Resolver应有1槽");
        var iceThree = Level100ResolverEngine.Evaluate(WithPreviousGcd(
            BaseInput(),
            Success(BLMSkill.冰封, 45, 9000, isInstant: false)));
        AssertEx.Equal(1, iceThree.ExecutorWeaveSlots, "冰三执行器应有1槽");
        AssertEx.Equal(1, iceThree.ResolverAllowedWeaves, "冰三Resolver应有1槽");
        AssertEx.Equal(
            2,
            Level100ResolverEngine.Evaluate(instantPrevious with { UsedWeaves = -1 }).RemainingWeaves,
            "负UsedWeaves必须按0处理并保留完整双插容量");
    }

    private static BlmResolverInput BaseInput()
    {
        var previous = Success(BLMSkill.炽炎, 1, 9000, isInstant: false);
        return new BlmResolverInput
        {
            Context = new BlmResolverContextFacts
            {
                CapturedAtMs = NowMs,
                IsAvailable = true,
                AcrEnabled = true,
                PlayerEntityId = PlayerId,
                Level = 100,
                Mp = 10_000,
                MaxMp = 10_000,
                InCombat = true,
                IsAlive = true,
                CanAct = true,
                IsSingleTargetMode = true,
                HasTarget = true,
                CanUseAttackActionOnTarget = true,
                CurrentTargetId = CurrentTargetId,
                ActionQueueWindowMs = 300,
                GcdTotalSeconds = 2.5f,
                GcdRemainSeconds = 1.5f,
                Phase = BlmPhase.Fire,
                AstralFireStacks = 3,
                UmbralHearts = 3,
            },
            Settings = BlmResolverSettings.Default,
            Actions = AllActionFacts(),
            RecentHistory = [previous],
            PreviousGcd = previous,
            Level100Loop = new BlmLevel100LoopFacts(),
            DotTargets = DefaultTargets(),
            CanSpecifyDotTarget = true,
            CasualCombat = new BlmCasualCombatFacts(),
            DefensiveCast = new BlmDefensiveCastFacts(),
        };
    }

    private static ImmutableArray<BlmResolverActionFact> AllActionFacts()
        =>
        [
            ReadyAction(BLMSkill.火炎),
            ReadyAction(BLMSkill.冰结),
            ReadyAction(BLMSkill.闪雷, BLMSkill.高闪雷),
            ReadyAction(BLMSkill.烈炎, BLMSkill.高烈炎),
            ReadyAction(BLMSkill.星灵移位),
            ReadyAction(BLMSkill.爆炎),
            ReadyAction(BLMSkill.冰封),
            ReadyAction(BLMSkill.玄冰),
            ReadyAction(BLMSkill.核爆),
            ReadyAction(BLMSkill.冰澈),
            ReadyAction(BLMSkill.炽炎),
            ReadyAction(BLMSkill.秽浊),
            ReadyAction(BLMSkill.绝望),
            ReadyAction(BLMSkill.异言),
            ReadyAction(BLMSkill.冰冻, BLMSkill.高冰冻),
            ReadyAction(BLMSkill.悖论),
            ReadyAction(BLMSkill.高闪雷),
            ReadyAction(BLMSkill.高震雷),
            ReadyAction(BLMSkill.耀星),
            UnavailableAction(MageUniversalSkill.即刻咏唱),
            UnavailableAction(BLMSkill.三连咏唱),
            UnavailableAction(MageUniversalSkill.醒梦),
            UnavailableAction(BLMSkill.详述),
            UnavailableAction(BLMSkill.魔泉),
            UnavailableAction(BLMSkill.黑魔纹),
            UnavailableAction(MageUniversalSkill.昏乱),
            UnavailableAction(BLMSkill.魔罩),
        ];

    private static BlmResolverActionFact ReadyAction(
        uint requestedActionId,
        uint adjustedActionId = 0)
        => new()
        {
            RequestedActionId = requestedActionId,
            AdjustedActionId = adjustedActionId == 0 ? requestedActionId : adjustedActionId,
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
            CanCast = true,
            Charges = 0f,
            MaxCharges = 1,
            CooldownRemainMs = 10_000d,
            RecastTotalMs = 60_000d,
        };

    private static BlmResolverInput SetAction(
        BlmResolverInput input,
        uint actionId,
        float charges,
        double cooldownRemainMs,
        bool canCast = true)
    {
        var found = false;
        var actions = input.Actions.Select(action =>
        {
            if (action.RequestedActionId != actionId)
            {
                return action;
            }

            found = true;
            return action with
            {
                IsUnlocked = true,
                CanCast = canCast,
                Charges = charges,
                CooldownRemainMs = cooldownRemainMs,
            };
        }).ToImmutableArray();
        AssertEx.True(found, $"fixture 缺少动作事实 {actionId}");
        return input with { Actions = actions };
    }

    private static BlmResolverInput WithPreviousGcd(
        BlmResolverInput input,
        BlmActionSuccess success)
    {
        var history = input.RecentHistory
            .Where(item => !item.IsGcd)
            .Append(success)
            .ToImmutableArray();
        return input with
        {
            PreviousGcd = success,
            RecentHistory = history,
        };
    }

    private static BlmActionSuccess Success(
        uint actionId,
        long serial,
        long occurredAtMs,
        bool isInstant,
        bool isGcd = true)
        => new(
            1,
            serial,
            actionId,
            actionId,
            actionId,
            (uint)serial,
            occurredAtMs,
            occurredAtMs + 100,
            isInstant,
            isGcd);

    private static ImmutableArray<BlmResolverDotTargetFact> DefaultTargets()
        =>
        [
            Target(CurrentTargetId, 1_000_000, 900_000, 10f, 10_000f, 10_000f),
            Target(201, 800_000, 700_000, 12f, 10_000f, 10_000f),
        ];

    private static BlmResolverDotTargetFact Target(
        uint entityId,
        long maxHp,
        long currentHp,
        float distance,
        float singleDotMs,
        float aoeDotMs)
        => new()
        {
            EntityId = entityId,
            IsValid = true,
            IsTargetable = true,
            IsAlive = true,
            CanUseAttackActionOn = true,
            IsInDotRange = true,
            CanCastDot = true,
            CurrentHp = currentHp,
            MaxHp = maxHp,
            Distance = distance,
            SingleTargetDotRemainingMs = singleDotMs,
            AoeDotRemainingMs = aoeDotMs,
        };

    private static BlmDecisionFrame Evaluate(
        BlmResolverInput input,
        BlmResolverContextFacts context)
        => Level100ResolverEngine.Evaluate(input with { Context = context });

    private static void AssertCandidate(
        BlmResolverCandidate? candidate,
        uint expectedActionId,
        string expectedResolverId,
        string message)
    {
        AssertEx.True(candidate is not null, $"{message}应产生候选");
        AssertEx.Equal(expectedActionId, candidate!.ActionId, $"{message}动作错误");
        AssertEx.Equal(expectedResolverId, candidate.ResolverId, $"{message}Resolver错误");
    }
}
