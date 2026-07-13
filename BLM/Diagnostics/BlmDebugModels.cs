using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using LosPr.BLM.Resolvers;

namespace LosPr.BLM.Diagnostics;

public enum BlmDebugEventKind
{
    Decision,
    DispatchReturned,
    ActionEffectObserved,
    AckAccepted,
    AckRejected,
    GaugeReconciled,
    Lifecycle,
    AckQueueDropped,
    LoggerDropped,
    LoggerError,
    ResolverFrame,
}

public sealed record BlmDebugResolverSnapshot
{
    public long FrameSequence { get; init; }
    public long FrameCapturedAtMs { get; init; }
    public long FrameStateGeneration { get; init; }
    public BlmResolverChannel Channel { get; init; }
    public uint CandidateActionId { get; init; }
    public uint DeliverableActionId { get; init; }
    public string TargetKey { get; init; } = string.Empty;
    public BlmResolverTargetKind TargetKind { get; init; }
    public string ResolverId { get; init; } = string.Empty;
    public int CheckCode { get; init; }
    public bool HoldGcdForTranspose { get; init; }
    public bool GcdBlockedByTransposeHold { get; init; }
    public bool GcdBlockedByAlwaysBridge { get; init; }
    public bool DeliveryBlocked { get; init; }
    public string BlockReason { get; init; } = string.Empty;
    public bool HighPriorityQueueActive { get; init; }
    public int RemainingWeaves { get; init; }
    public string FactCoverage { get; init; } = string.Empty;
}

public sealed record BlmDebugResolverDraft
{
    public long FrameSequence { get; init; }
    public long FrameCapturedAtMs { get; init; }
    public long FrameStateGeneration { get; init; }
    public BlmResolverChannel Channel { get; init; }
    public uint CandidateActionId { get; init; }
    public uint DeliverableActionId { get; init; }
    public uint TargetEntityId { get; init; }
    public BlmResolverTargetKind TargetKind { get; init; }
    public string ResolverId { get; init; } = string.Empty;
    public int CheckCode { get; init; }
    public bool HoldGcdForTranspose { get; init; }
    public bool GcdBlockedByTransposeHold { get; init; }
    public bool GcdBlockedByAlwaysBridge { get; init; }
    public bool DeliveryBlocked { get; init; }
    public string BlockReason { get; init; } = string.Empty;
    public bool HighPriorityQueueActive { get; init; }
    public int RemainingWeaves { get; init; }
    public string FactCoverage { get; init; } = string.Empty;
}

public sealed record BlmDebugResourceSnapshot
{
    public long Mp { get; init; }
    public long MaxMp { get; init; }
    public BlmPhase Phase { get; init; }
    public int AfStacks { get; init; }
    public int IceStacks { get; init; }
    public int UmbralHearts { get; init; }
    public int AstralSoul { get; init; }
    public bool HasParadox { get; init; }
    public bool HasFirestarter { get; init; }
    public bool HasThunderhead { get; init; }
    public int PolyglotStacks { get; init; }
    public int MaxPolyglot { get; init; }
    public int PolyglotTimerMs { get; init; }
    public bool HasSwiftcast { get; init; }
    public float SwiftcastRemainSeconds { get; init; }
    public int TriplecastStacks { get; init; }
    public float TriplecastRemainSeconds { get; init; }
    public bool IsMoving { get; init; }
    public bool IsCasting { get; init; }
    public bool CanAct { get; init; }
    public float GcdTotalSeconds { get; init; }
    public float GcdRemainSeconds { get; init; }
    public float CastTotalSeconds { get; init; }
    public float CastRemainSeconds { get; init; }
    public float AnimationLockSeconds { get; init; }

    public static BlmDebugResourceSnapshot FromContext(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new BlmDebugResourceSnapshot
        {
            Mp = context.Mp,
            MaxMp = context.MaxMp,
            Phase = context.Phase,
            AfStacks = context.AfStacks,
            IceStacks = context.IceStacks,
            UmbralHearts = context.UmbralHearts,
            AstralSoul = context.AstralSoul,
            HasParadox = context.HasParadox,
            HasFirestarter = context.HasFirestarter,
            HasThunderhead = context.HasThunderhead,
            PolyglotStacks = context.PolyglotStacks,
            MaxPolyglot = context.MaxPolyglot,
            PolyglotTimerMs = context.PolyglotTimerMs,
            HasSwiftcast = context.HasSwiftcast,
            SwiftcastRemainSeconds = context.SwiftcastRemainSeconds,
            TriplecastStacks = context.TriplecastStacks,
            TriplecastRemainSeconds = context.TriplecastRemainSeconds,
            IsMoving = context.IsMoving,
            IsCasting = context.IsCasting,
            CanAct = context.CanAct,
            GcdTotalSeconds = context.GcdTotalSeconds,
            GcdRemainSeconds = context.GcdRemainSeconds,
            CastTotalSeconds = context.CastTotalSeconds,
            CastRemainSeconds = context.CastRemainSeconds,
            AnimationLockSeconds = context.AnimationLockSeconds,
        };
    }
}

public sealed record BlmDebugEvent
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long EventSequence { get; init; }
    public DateTimeOffset Utc { get; init; }
    public long MonotonicMs { get; init; }
    public string SessionId { get; init; } = string.Empty;
    public BlmDebugEventKind Kind { get; init; }
    public string EntryPoint { get; init; } = string.Empty;
    public uint ActionId { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public uint NormalizedActionId { get; init; }
    public uint GlobalSequence { get; init; }
    public bool? Accepted { get; init; }
    public string PActionType { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string TargetKey { get; init; } = string.Empty;
    public long CombatSerial { get; init; }
    public long StateGeneration { get; init; }
    public long FirePhaseSerial { get; init; }
    public long IcePhaseSerial { get; init; }
    public long DroppedCount { get; init; }
    public BlmDebugResourceSnapshot Resources { get; init; } = new();
    public BlmDebugResolverSnapshot? Resolver { get; init; }
}

public sealed record BlmDebugEventDraft
{
    public BlmDebugEventKind Kind { get; init; }
    public BlmContext Context { get; init; } = BlmContext.Unavailable;
    public long MonotonicMs { get; init; }
    public string EntryPoint { get; init; } = string.Empty;
    public uint ActionId { get; init; }
    public uint NormalizedActionId { get; init; }
    public uint GlobalSequence { get; init; }
    public bool? Accepted { get; init; }
    public string PActionType { get; init; } = string.Empty;
    public string RuleId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public uint TargetEntityId { get; init; }
    public long DroppedCount { get; init; }
    public BlmDebugResolverDraft? Resolver { get; init; }
}

public sealed record BlmDebugSnapshot
{
    private static readonly IReadOnlyList<BlmDebugEvent> EmptyEvents =
        Array.AsReadOnly(Array.Empty<BlmDebugEvent>());

    public static BlmDebugSnapshot Empty { get; } = new();

    public DateTimeOffset CapturedAtUtc { get; init; }
    public bool FileLoggingEnabled { get; init; }
    public bool WriterHealthy { get; init; } = true;
    public string LogDirectory { get; init; } = string.Empty;
    public string CurrentFilePath { get; init; } = string.Empty;
    public int PendingCount { get; init; }
    public long AcceptedCount { get; init; }
    public long WrittenCount { get; init; }
    public long DroppedCount { get; init; }
    public string LastError { get; init; } = string.Empty;
    public IReadOnlyList<BlmDebugEvent> RecentEvents { get; init; } = EmptyEvents;

    internal static IReadOnlyList<BlmDebugEvent> Freeze(BlmDebugEvent[] events)
        => new ReadOnlyCollection<BlmDebugEvent>(events);
}

public static class BlmActionNames
{
    public static string Get(uint actionId) => actionId switch
    {
        0 => "-",
        BLMSkill.火炎 => "火炎",
        BLMSkill.冰结 => "冰结",
        BLMSkill.闪雷 => "闪雷",
        BLMSkill.烈炎 => "烈炎",
        BLMSkill.星灵移位 => "星灵移位",
        BLMSkill.爆炎 => "爆炎",
        BLMSkill.暴雷 => "暴雷",
        BLMSkill.冰封 => "冰封",
        BLMSkill.以太步 => "以太步",
        BLMSkill.崩溃 => "崩溃",
        BLMSkill.魔罩 => "魔罩",
        BLMSkill.魔泉 => "魔泉",
        BLMSkill.玄冰 => "玄冰",
        BLMSkill.核爆 => "核爆",
        BLMSkill.黑魔纹 => "黑魔纹",
        BLMSkill.冰澈 => "冰澈",
        BLMSkill.炽炎 => "炽炎",
        BLMSkill.魔纹步 => "魔纹步",
        BLMSkill.霹雷 => "霹雷",
        BLMSkill.三连咏唱 => "三连咏唱",
        BLMSkill.秽浊 => "秽浊",
        BLMSkill.震雷 => "震雷",
        BLMSkill.绝望 => "绝望",
        BLMSkill.灵极魂 => "灵极魂",
        BLMSkill.异言 => "异言",
        BLMSkill.冰冻 => "冰冻",
        BLMSkill.高烈炎 => "高烈炎",
        BLMSkill.高冰冻 => "高冰冻",
        BLMSkill.详述 => "详述",
        BLMSkill.悖论 => "悖论",
        BLMSkill.高闪雷 => "高闪雷",
        BLMSkill.高震雷 => "高震雷",
        BLMSkill.魔纹重置 => "魔纹重置",
        BLMSkill.耀星 => "耀星",
        MageUniversalSkill.沉稳咏唱 => "沉稳咏唱",
        MageUniversalSkill.昏乱 => "昏乱",
        MageUniversalSkill.即刻咏唱 => "即刻咏唱",
        MageUniversalSkill.醒梦 => "醒梦",
        _ => $"Action {actionId}",
    };
}

internal static class BlmDebugText
{
    private const int MaxLength = 768;
    private static readonly Regex AbsoluteWindowsPath = new(
        @"(?i)(?:[a-z]:[\\/]|\\\\)[^\s\""']+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        text = AbsoluteWindowsPath.Replace(text, "<path>");
        return text.Length <= MaxLength ? text : text[..MaxLength];
    }
}
