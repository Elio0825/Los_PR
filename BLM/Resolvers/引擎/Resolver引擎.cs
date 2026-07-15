using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;

namespace LosPr.BLM.Resolvers.Level100;

internal static class Level100ResolverEngine
{
    public const string LosAeSpecificationCommit =
        "a3ee18d8c524238bd200f1bbdf906bd77e13038a";
    public const string LosAeTtkWorkspaceSha256 =
        "6AE8412BCE5EC6CFA24DAE4226B5C621C5E5408C203855A21434DC0A38BF102A";
    public const string LosAeBlmAcrSha256 =
        "0FF1EDBB1B44BEF58D7CB94EAFA58655941A57990E490BD905F7F1BB77AF14C7";
    public const string LosAeLevel100SingleTargetSha256 =
        "9871D1D5C5BA1B280CFAB1278622B7B97637FC1E1B5FF5BB3A7FDB60FA14A61D";
    public const string LosAeLevel90SingleTargetSha256 =
        "5BA6987888A057224F2CBC11B71A3A6185CA8640BE95D360793D77048068E58D";
    public const string LosAeLevel72SingleTargetSha256 =
        "1C0CFAD8A08805152ACB09A0D57F6B99B82839B054BCF1589AC3CED61726371B";
    public const string LosAeLevel60SingleTargetSha256 =
        "9A9BA4E0754528688DB8BDDD6D0EB20F2040C614274944526BD8EA65AE3F7649";
    public const string LosAeLevel35SingleTargetSha256 =
        "D05DE5AA2D38DD1ACFA26D7187DAAB93DAEA2432F975F9DF348EF4E3F3C1F06F";
    public const string LosAeLevel1SingleTargetSha256 =
        "3349A6E1E3763ADB929BAE2B3E9B7B315A93A26AF008D9E697A368F6158769B3";
    public const string LosAeTransposeSha256 =
        "F87155A138C95B561B54BC0575DDFFC21014FBC865021AA379D5CC65004F8C83";
    public const string LosAeSwiftcastSha256 =
        "EBAC890FBE20870102F1A4EA492266708D1C8A81E4EF010D433303E5A526EB41";
    public const string LosAeTriplecastSha256 =
        "3401718A45A66C7E5F619015D00E26F91F273BB05541D8229314C079F3234565";
    public const string LosAeManafontSha256 =
        "1F2102220502B790F62563F536F428C09A8DEADCDFA4C7C0EFA05929B6C70ACE";
    public const string LosAeInstantGcdTriggerSha256 =
        "5D70EB5EF186407B6FC6798CF14DCE81313207A763B0C4F82CF667451B6756BB";
    public const string LosAeLevel100AoeSha256 =
        "666F7335A2DD23D6396350FBC4EABA94C478AEDE1690A3FC5045A72569FD91C1";
    public const string LosAeLevel58AoeSha256 =
        "0BC18B66FCD9FBFAC8FFF51731AC64BF526DD21EEE88BE785F4A51B677DE4D1D";
    public const string LosAeLevel50AoeSha256 =
        "FE26010CB1B9B16A0C0AAB9995EE1A5141FF38D132BC5827239AD0D919C6E46D";
    public const string LosAeLevel35AoeSha256 =
        "C138C9DE40547BC619B18FB75A02193A6606C9CD680F1E80321C51F0BD3753D6";
    public const string LosAeLevel1AoeSha256 =
        "006D352FEFF157143938C2D97AC5360C0C1AE812CEFE0B84BFC8ADE93FE106D0";
    public const string LosAeAoeThunderSha256 =
        "A6D4B66E5D0EBE475B37D6AB8998D28748A50D1BC3DAC58580E2F2F3D33842A2";
    public const string LosAeFoulSha256 =
        "63B5137D3D7FB7C61E5FB56E7D4EAAD4CD73A63F867EFF1BA2B764593F30F553";
    public const string FrozenManifestSha256 =
        "2FC09B90C567836481DE95380C964EA89875BF56C2B9B2F1361588C10697E5D4";

    public static ImmutableArray<BlmResolverManifestEntry> Manifest { get; } =
    [
        Entry(0, BlmResolverChannel.Gcd, "GCD.TTK"),
        Entry(1, BlmResolverChannel.Gcd, "GCD.快速耀星"),
        Entry(2, BlmResolverChannel.Gcd, "GCD.强制回冰"),
        Entry(3, BlmResolverChannel.Gcd, "GCD.异言#1"),
        Entry(4, BlmResolverChannel.Gcd, "GCD.秽浊"),
        Entry(5, BlmResolverChannel.Gcd, "GCD.异言#2"),
        Entry(6, BlmResolverChannel.Gcd, "GCD.双DOT"),
        Entry(7, BlmResolverChannel.Gcd, "GCD.雷1"),
        Entry(8, BlmResolverChannel.Gcd, "GCD.雷2"),
        Entry(9, BlmResolverChannel.Gcd, "GCD.瞬发gcd触发器"),
        Entry(10, BlmResolverChannel.Gcd, "GCD.群体100"),
        Entry(11, BlmResolverChannel.Gcd, "GCD.群体58_99"),
        Entry(12, BlmResolverChannel.Gcd, "GCD.群体50_57"),
        Entry(13, BlmResolverChannel.Gcd, "GCD.群体35_49"),
        Entry(14, BlmResolverChannel.Gcd, "GCD.群体1_34"),
        Entry(15, BlmResolverChannel.Gcd, "GCD.单体100"),
        Entry(16, BlmResolverChannel.Gcd, "GCD.单体90_99"),
        Entry(17, BlmResolverChannel.Gcd, "GCD.单体72_89"),
        Entry(18, BlmResolverChannel.Gcd, "GCD.单体60_71"),
        Entry(19, BlmResolverChannel.Gcd, "GCD.单体35_59"),
        Entry(20, BlmResolverChannel.Gcd, "GCD.单体1_34"),
        Inactive(21, BlmResolverChannel.Gcd, "GCD.核爆补耀星"),
        Entry(22, BlmResolverChannel.Always, "Ability.星灵移位"),
        Entry(23, BlmResolverChannel.OffGcd, "Ability.即刻"),
        Entry(24, BlmResolverChannel.OffGcd, "Ability.三连咏唱"),
        Entry(25, BlmResolverChannel.OffGcd, "Ability.醒梦"),
        Entry(26, BlmResolverChannel.OffGcd, "Ability.详述"),
        Entry(27, BlmResolverChannel.OffGcd, "Ability.墨泉"),
        Entry(28, BlmResolverChannel.OffGcd, "Ability.黑魔纹"),
        Entry(29, BlmResolverChannel.OffGcd, "Ability.Auto昏乱"),
        Entry(30, BlmResolverChannel.OffGcd, "Ability.Auto魔罩"),
        Entry(31, BlmResolverChannel.OffGcd, "Ability.爆发药"),
    ];

    public static string ManifestCanonicalText { get; } = string.Join(
        '\n',
        Manifest.Select(static entry =>
            $"{entry.Order}|{entry.Channel}|{entry.ResolverId}|{entry.Disposition}"));

    public static string ManifestSha256 { get; } = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(ManifestCanonicalText)));

    public static bool BehaviorClosureImplemented => true;

    public static bool HasAvailableInstantGcd(BlmResolverInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Level100SingleTargetResolvers.HasAvailableInstantGcd(input);
    }

    public static BlmDecisionFrame Evaluate(BlmResolverInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!IsEligible(input))
        {
            return CreateFrame(input, null, null, null, false, false);
        }

        var aoePlan = input.Context.IsAoeMode
            ? Level100AbilityResolvers.BuildAoeTransposePlan(input)
            : BlmAoeTransposePlan.None;
        var gcdCandidate = EvaluateGcdChannel(input, aoePlan);
        var alwaysCandidate = EvaluateChannel(input, BlmResolverChannel.Always);
        var offGcdCandidate = EvaluateChannel(input, BlmResolverChannel.OffGcd);
        var aoeManafontBridgeCandidate = input.Context.IsAoeMode
            ? CreateAoeManafontBridgeCandidate(input)
            : null;
        var manafontCandidate = aoeManafontBridgeCandidate ?? offGcdCandidate;
        var holdGcdForTranspose =
            Level100AbilityResolvers.ShouldHoldGcdForTranspose(input);
        var bridgeManafontThroughAlways =
            Level100AbilityResolvers.ShouldBridgeManafontThroughAlways(
                input,
                gcdCandidate,
                manafontCandidate);
        if (bridgeManafontThroughAlways && input.Context.IsAoeMode)
        {
            gcdCandidate = null;
            alwaysCandidate = null;
            offGcdCandidate = manafontCandidate;
        }
        return CreateFrame(
            input,
            gcdCandidate,
            alwaysCandidate,
            offGcdCandidate,
            holdGcdForTranspose,
            bridgeManafontThroughAlways);
    }

    public static bool IsEligible(BlmResolverInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var context = input.Context;
        return context.IsAvailable
            && context.AcrEnabled
            && context.Level is >= 1 and <= 100
            && context.InCombat
            && context.IsAlive
            && context.CanAct
            && context.HasTarget
            && context.CanUseAttackActionOnTarget
            && !input.SpecialSequenceActive;
    }

    internal static BlmDecisionFrame CreateFrame(
        BlmResolverInput input,
        BlmResolverCandidate? gcdCandidate,
        BlmResolverCandidate? alwaysCandidate,
        BlmResolverCandidate? offGcdCandidate,
        bool holdGcdForTranspose,
        bool bridgeManafontThroughAlways)
    {
        ArgumentNullException.ThrowIfNull(input);
        var previousGcd = input.PreviousGcd;
        var hasConfirmedGcd = previousGcd is { IsGcd: true };
        var wasConfirmedInstant = hasConfirmedGcd && previousGcd!.Value.WasInstant;
        var normalizedActionId = hasConfirmedGcd
            ? EffectiveActionId(previousGcd!.Value)
            : 0;
        var executorWeaveSlots = BlmDecisionPrimitives.ExecutorWeaveSlots(
            hasConfirmedGcd,
            wasConfirmedInstant);
        var resolverAllowedWeaves = BlmDecisionPrimitives.ResolverAllowedWeaves(
            new BlmResolverAllowedWeavesInput(
                normalizedActionId,
                input.Context.Level,
                wasConfirmedInstant,
                input.Settings.ReducedAnimationLockEnabled));
        var effectiveCapacity = Math.Min(executorWeaveSlots, resolverAllowedWeaves);
        var remainingWeaves = Math.Max(0, effectiveCapacity - Math.Max(0, input.UsedWeaves));
        var blockReason = BuildBlockReason(input);
        var gcdBlockedByTransposeHold = holdGcdForTranspose
            && (input.Context.IsAoeMode
                || gcdCandidate is { ResolverId: "GCD.单体100" }
                    && gcdCandidate.ActionId
                        == Level100ResolverFacts.EffectiveActionId(
                            input,
                            BLMSkill.冰封));

        return new BlmDecisionFrame
        {
            GcdCandidate = gcdCandidate,
            AlwaysCandidate = alwaysCandidate,
            OffGcdCandidate = offGcdCandidate,
            AlwaysBridgeCandidate = bridgeManafontThroughAlways
                ? offGcdCandidate
                : null,
            HoldGcdForTranspose = holdGcdForTranspose,
            GcdBlockedByTransposeHold = gcdBlockedByTransposeHold,
            GcdBlockedByAlwaysBridge = bridgeManafontThroughAlways,
            ExecutorWeaveSlots = executorWeaveSlots,
            ResolverAllowedWeaves = resolverAllowedWeaves,
            RemainingWeaves = remainingWeaves,
            DeliveryBlocked = blockReason.Length > 0,
            BlockReason = blockReason,
        };
    }

    private static BlmResolverCandidate? EvaluateChannel(
        BlmResolverInput input,
        BlmResolverChannel channel,
        uint allowedGcdActionId = 0)
    {
        foreach (var entry in Manifest)
        {
            if (entry.Channel != channel
                || entry.Disposition != BlmResolverManifestDisposition.Active)
            {
                continue;
            }

            var result = channel == BlmResolverChannel.Gcd
                ? Level100SingleTargetResolvers.Evaluate(entry.ResolverId, input)
                : Level100AbilityResolvers.Evaluate(entry.ResolverId, input);
            if (!result.IsAccepted)
            {
                continue;
            }

            if (channel == BlmResolverChannel.Gcd
                && allowedGcdActionId != 0
                && result.ActionId != allowedGcdActionId)
            {
                continue;
            }

            return new BlmResolverCandidate
            {
                ActionId = result.ActionId,
                TargetId = result.TargetId,
                TargetKind = result.TargetKind,
                ResolverId = entry.ResolverId,
                ManifestOrder = entry.Order,
                CheckCode = result.CheckCode,
            };
        }

        return null;
    }

    private static BlmResolverCandidate? EvaluateGcdChannel(
        BlmResolverInput input,
        BlmAoeTransposePlan aoePlan)
    {
        if (input.Context.IsSingleTargetMode)
        {
            return EvaluateChannel(input, BlmResolverChannel.Gcd);
        }

        if (aoePlan.Disposition is BlmAoeTransposeDisposition.CastNow
            or BlmAoeTransposeDisposition.Hold)
        {
            return null;
        }

        if (aoePlan.Disposition == BlmAoeTransposeDisposition.Fill
            && Level100ResolverFacts.RecentlyUsed(input, aoePlan.FillActionId))
        {
            return null;
        }

        return EvaluateChannel(
            input,
            BlmResolverChannel.Gcd,
            aoePlan.Disposition == BlmAoeTransposeDisposition.Fill
                ? aoePlan.FillActionId
                : 0);
    }

    private static BlmResolverCandidate? CreateAoeManafontBridgeCandidate(
        BlmResolverInput input)
    {
        var result = Level100AbilityResolvers.CheckManafontForAoeAlwaysBridge(input);
        return !result.IsAccepted
            ? null
            : new BlmResolverCandidate
            {
                ActionId = result.ActionId,
                TargetId = result.TargetId,
                TargetKind = result.TargetKind,
                ResolverId = "Ability.墨泉",
                ManifestOrder = 27,
                CheckCode = result.CheckCode,
            };
    }

    private static string BuildBlockReason(BlmResolverInput input)
    {
        var reasons = new List<string>();
        var context = input.Context;
        if (!context.IsAvailable
            || !context.AcrEnabled
            || context.Level is < 1 or > 100
            || !context.InCombat
            || !context.IsAlive
            || !context.CanAct
            || !context.HasTarget
            || !context.CanUseAttackActionOnTarget)
        {
            reasons.Add("LifecycleOrTargetGate");
        }

        if (input.SpecialSequenceActive)
        {
            reasons.Add("SpecialSequenceActive");
        }

        if (input.HighPriorityQueueActive)
        {
            reasons.Add("HighPriorityQueueActive");
        }

        if (input.PendingGaugeReconcile)
        {
            reasons.Add("PendingGaugeReconcile");
        }

        return string.Join(';', reasons);
    }

    internal static uint EffectiveActionId(BlmActionSuccess action)
    {
        if (action.ActualAckId != 0)
        {
            return action.ActualAckId;
        }

        return action.AdjustedAtIssue != 0
            ? action.AdjustedAtIssue
            : action.RequestedId;
    }

    private static BlmResolverManifestEntry Entry(
        int order,
        BlmResolverChannel channel,
        string resolverId)
        => new(
            order,
            channel,
            resolverId,
            BlmResolverManifestDisposition.Active);

    private static BlmResolverManifestEntry Reject(
        int order,
        BlmResolverChannel channel,
        string resolverId)
        => new(
            order,
            channel,
            resolverId,
            BlmResolverManifestDisposition.RejectSingleTarget);

    private static BlmResolverManifestEntry Inactive(
        int order,
        BlmResolverChannel channel,
        string resolverId)
        => new(
            order,
            channel,
            resolverId,
            BlmResolverManifestDisposition.Inactive);
}
