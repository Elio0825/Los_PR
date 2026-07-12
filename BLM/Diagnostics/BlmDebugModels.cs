using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace LosPr.BLM.Diagnostics;

public enum BlmDebugEventKind
{
    Decision,
    DispatchReturned,
    ActionEffectObserved,
    AckAccepted,
    AckRejected,
    GaugeReconciled,
    TransitionChanged,
    FollowUpChanged,
    Lifecycle,
    QueueCleared,
    AckQueueDropped,
    LoggerDropped,
    LoggerError,
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

public sealed record BlmDebugTransitionSnapshot
{
    public TransitionKind Kind { get; init; }
    public TransitionStep Step { get; init; }
    public TransitionStage Stage { get; init; }
    public TransitionDeliveryChannel DeliveryChannel { get; init; }
    public long Serial { get; init; }
    public int StepIndex { get; init; }
    public uint ExpectedActionId { get; init; }
    public string ExpectedActionName { get; init; } = string.Empty;
    public uint AcknowledgedActionId { get; init; }
    public uint AcknowledgedGlobalSequence { get; init; }
    public long StepDeadlineAtMs { get; init; }
    public long TotalDeadlineAtMs { get; init; }
    public string Reason { get; init; } = string.Empty;

    public static BlmDebugTransitionSnapshot FromIntent(BlmIntent? intent)
    {
        intent ??= BlmIntent.Empty;
        return new BlmDebugTransitionSnapshot
        {
            Kind = intent.Kind,
            Step = intent.Step,
            Stage = intent.Stage,
            DeliveryChannel = intent.DeliveryChannel,
            Serial = intent.Serial,
            StepIndex = intent.StepIndex,
            ExpectedActionId = intent.ExpectedActionId,
            ExpectedActionName = BlmActionNames.Get(intent.ExpectedActionId),
            AcknowledgedActionId = intent.AcknowledgedActionId,
            AcknowledgedGlobalSequence = intent.AcknowledgedGlobalSequence,
            StepDeadlineAtMs = intent.ExpireAtMs,
            TotalDeadlineAtMs = intent.TransitionExpireAtMs,
            Reason = BlmDebugText.Clean(intent.Reason),
        };
    }
}

public sealed record BlmDebugFollowUpSnapshot
{
    public BlmFollowUpKind Kind { get; init; }
    public BlmFollowUpStage Stage { get; init; }
    public long Serial { get; init; }
    public uint TriggerActionId { get; init; }
    public string TriggerActionName { get; init; } = string.Empty;
    public uint RequiredActionId { get; init; }
    public string RequiredActionName { get; init; } = string.Empty;
    public uint TriggerAckGlobalSequence { get; init; }
    public uint RequiredAckGlobalSequence { get; init; }
    public long StageDeadlineAtMs { get; init; }
    public long TotalDeadlineAtMs { get; init; }
    public string Reason { get; init; } = string.Empty;

    public static BlmDebugFollowUpSnapshot FromIntent(BlmFollowUpIntent? intent)
    {
        intent ??= BlmFollowUpIntent.Empty;
        return new BlmDebugFollowUpSnapshot
        {
            Kind = intent.Kind,
            Stage = intent.Stage,
            Serial = intent.Serial,
            TriggerActionId = intent.TriggerActionId,
            TriggerActionName = BlmActionNames.Get(intent.TriggerActionId),
            RequiredActionId = intent.RequiredActionId,
            RequiredActionName = BlmActionNames.Get(intent.RequiredActionId),
            TriggerAckGlobalSequence = intent.TriggerAckGlobalSequence,
            RequiredAckGlobalSequence = intent.RequiredAckGlobalSequence,
            StageDeadlineAtMs = intent.StageDeadlineAtMs,
            TotalDeadlineAtMs = intent.TotalExpireAtMs,
            Reason = BlmDebugText.Clean(intent.Reason),
        };
    }
}

public sealed record BlmDebugEvent
{
    public const int CurrentSchemaVersion = 1;

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
    public BlmDebugTransitionSnapshot Transition { get; init; } = new();
    public BlmDebugFollowUpSnapshot FollowUp { get; init; } = new();
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
    public BlmIntent? Transition { get; init; }
    public BlmFollowUpIntent? FollowUp { get; init; }
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
