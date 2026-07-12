using System;
using LosPr.BLM.Core;
using PromeRotation.Data;

namespace LosPr.BLM.UI;

/// <summary>
/// 控制台使用的一帧只读投影。所有事实均来自同一个 <see cref="BlmContext"/>。
/// </summary>
public sealed record BlmUiSnapshot
{
    public long CapturedAtMs { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public bool IsAvailable { get; init; }
    public string AvailabilityText { get; init; } = "等待游戏状态";
    public string? CaptureError { get; init; }

    public AcrState AcrState { get; init; } = AcrState.Off;
    public int Level { get; init; }
    public long Mp { get; init; }
    public long MaxMp { get; init; }
    public bool InCombat { get; init; }
    public bool IsCasting { get; init; }
    public bool IsAlive { get; init; }

    public bool InAstralFire { get; init; }
    public bool InUmbralIce { get; init; }
    public int AstralFireStacks { get; init; }
    public int UmbralIceStacks { get; init; }
    public int UmbralHearts { get; init; }
    public int PolyglotStacks { get; init; }
    public int MaximumPolyglot { get; init; }
    public float PolyglotTimerSeconds { get; init; }
    public int AstralSoulStacks { get; init; }
    public bool HasParadox { get; init; }

    public float GcdTotalSeconds { get; init; }
    public float GcdRemainSeconds { get; init; }
    public float CastTotalSeconds { get; init; }
    public float CastRemainSeconds { get; init; }
    public float AnimationLockSeconds { get; init; }

    public bool HasTarget { get; init; }
    public bool CanAttackTarget { get; init; }
    public string TargetName { get; init; } = "无目标";
    public float TargetDistance { get; init; }
    public long TargetHp { get; init; }
    public long TargetMaxHp { get; init; }

    public bool HistoryReliable { get; init; }
    public long CombatSerial { get; init; }
    public long StateGeneration { get; init; }
    public long FirePhaseSerial { get; init; }
    public long IcePhaseSerial { get; init; }
    public int Fire4Count { get; init; }
    public int Fire4CountSinceManafont { get; init; }
    public long ManafontUseSerial { get; init; }
    public uint LastAckActionId { get; init; }
    public uint LastAckSequence { get; init; }
    public long LastAckAtMs { get; init; }
    public bool PendingGaugeReconcile { get; init; }
    public uint LastGaugeReconciledActionId { get; init; }
    public long LastGaugeReconciledAtMs { get; init; }
    public string LastResetReason { get; init; } = "初始化";
    public BlmIntent Transition { get; init; } = BlmIntent.Empty;
    public BlmFollowUpIntent FollowUp { get; init; } = BlmFollowUpIntent.Empty;

    public bool IsFactLayerConnected => StateGeneration > 0;
    public bool IsDecisionEngineConnected => true;
    public string DecisionStatus => "100级标准单体循环已接入";

    public string PhaseLabel => InAstralFire
        ? $"AF {AstralFireStacks}"
        : InUmbralIce
            ? $"UI {UmbralIceStacks}"
            : "中立";

    public string AcrStateLabel => AcrState switch
    {
        AcrState.On => "On",
        AcrState.Hold => "Hold",
        _ => "Off",
    };

    public float MpFraction => MaxMp <= 0 ? 0f : Math.Clamp((float)Mp / MaxMp, 0f, 1f);

    public float GcdFraction => GcdTotalSeconds <= 0f
        ? 1f
        : Math.Clamp(1f - GcdRemainSeconds / GcdTotalSeconds, 0f, 1f);

    public float CastFraction => CastTotalSeconds <= 0f
        ? 0f
        : Math.Clamp(1f - CastRemainSeconds / CastTotalSeconds, 0f, 1f);

    public long LastAckAgeMs => AgeSince(LastAckAtMs);

    public long LastGaugeReconcileAgeMs => AgeSince(LastGaugeReconciledAtMs);

    public static BlmUiSnapshot FromContext(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tracker = context.Tracker ?? BlmTrackerSnapshot.Empty;

        return new BlmUiSnapshot
        {
            CapturedAtMs = context.CapturedAtMs,
            CapturedAt = context.CapturedAtUtc,
            IsAvailable = context.IsAvailable,
            AvailabilityText = context.AvailabilityText,
            CaptureError = context.CaptureError,
            AcrState = context.AcrState,
            Level = context.Level,
            Mp = context.Mp,
            MaxMp = context.MaxMp,
            InCombat = context.InCombat,
            IsCasting = context.IsCasting,
            IsAlive = context.IsAlive,
            InAstralFire = context.InFire,
            InUmbralIce = context.InIce,
            AstralFireStacks = context.AfStacks,
            UmbralIceStacks = context.IceStacks,
            UmbralHearts = context.UmbralHearts,
            PolyglotStacks = context.PolyglotStacks,
            MaximumPolyglot = context.MaxPolyglot,
            PolyglotTimerSeconds = Math.Max(0f, context.PolyglotTimerMs / 1000f),
            AstralSoulStacks = context.AstralSoul,
            HasParadox = context.HasParadox,
            GcdTotalSeconds = context.GcdTotalSeconds,
            GcdRemainSeconds = context.GcdRemainSeconds,
            CastTotalSeconds = context.CastTotalSeconds,
            CastRemainSeconds = context.CastRemainSeconds,
            AnimationLockSeconds = context.AnimationLockSeconds,
            HasTarget = context.HasTarget,
            CanAttackTarget = context.HasValidTarget,
            TargetName = context.TargetName,
            TargetDistance = context.TargetDistance,
            TargetHp = context.TargetHp,
            TargetMaxHp = context.TargetMaxHp,
            HistoryReliable = tracker.HistoryReliable,
            CombatSerial = tracker.CombatSerial,
            StateGeneration = tracker.StateGeneration,
            FirePhaseSerial = tracker.FirePhaseSerial,
            IcePhaseSerial = tracker.IcePhaseSerial,
            Fire4Count = tracker.Fire4Count,
            Fire4CountSinceManafont = tracker.Fire4CountSinceManafont,
            ManafontUseSerial = tracker.ManafontUseSerial,
            LastAckActionId = tracker.LastAckActionId,
            LastAckSequence = tracker.LastAckGlobalSequence,
            LastAckAtMs = tracker.LastAckAtMs,
            PendingGaugeReconcile = tracker.PendingGaugeReconcile,
            LastGaugeReconciledActionId = tracker.LastGaugeReconciledActionId,
            LastGaugeReconciledAtMs = tracker.LastGaugeReconciledAtMs,
            LastResetReason = tracker.LastResetReason,
            Transition = tracker.Transition,
            FollowUp = tracker.FollowUp,
        };
    }

    private long AgeSince(long timestampMs)
        => timestampMs <= 0 || CapturedAtMs < timestampMs
            ? -1
            : CapturedAtMs - timestampMs;
}
