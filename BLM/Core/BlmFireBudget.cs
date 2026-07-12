namespace LosPr.BLM.Core;

public sealed record BlmFireBudget
{
    public const int Fire4HeartCost = 800;
    public const int Fire4FullCost = 1600;
    public const int Fire3HeartCost = 2000;
    public const int Fire3FullCost = 4000;
    public const int FireParadoxCost = 1600;
    public const int DespairMinimumMp = 800;
    public const int StandardFire4Limit = 6;
    public const int StandardParadoxPosition = 3;

    public int Fire4Count { get; init; }
    public int AstralSoul { get; init; }
    public int NextFire4Cost { get; init; }
    public int RequiredReserveAfterFire4 { get; init; }
    public bool ParadoxPending { get; init; }
    public bool CanAffordParadox { get; init; }
    public bool CanUseParadoxWithoutLosingDespair { get; init; }
    public bool ParadoxForcedByBudget { get; init; }
    public bool ShouldUseParadoxNow { get; init; }
    public bool CanUseNextFire4 { get; init; }
    public bool CanUseDespair { get; init; }
    public bool AstralSoulFull { get; init; }
    public bool Fire4CountAtOrPastPlan { get; init; }

    public static BlmFireBudget Build(
        BlmContext context,
        BlmTrackerSnapshot tracker)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tracker);

        var astralSoul = Math.Clamp(context.AstralSoul, 0, StandardFire4Limit);
        var trackedFire4Count = tracker.HistoryReliable
            ? Math.Max(0, tracker.Fire4Count)
            : 0;
        var fire4Count = Math.Max(trackedFire4Count, astralSoul);
        var nextFire4Cost = context.UmbralHearts > 0
            ? Fire4HeartCost
            : Fire4FullCost;
        var paradoxPending = context.HasParadox
            && !tracker.ParadoxUsedThisFire;
        var reserve = DespairMinimumMp
            + (paradoxPending ? FireParadoxCost : 0);
        var canAffordParadox = context.Mp >= FireParadoxCost;
        var canUseParadoxWithoutLosingDespair = context.Mp
            >= FireParadoxCost + DespairMinimumMp;
        var paradoxAtPlannedPosition = fire4Count >= StandardParadoxPosition;
        var fire4CountAtOrPastPlan = fire4Count >= StandardFire4Limit;
        var astralSoulFull = astralSoul >= StandardFire4Limit;
        // Flare Star consumes Soul but intentionally does not reset the diagnostic F4 count.
        var flareStarAlreadySpent = fire4CountAtOrPastPlan && astralSoul == 0;
        var canUseNextFire4 = !astralSoulFull
            && !flareStarAlreadySpent
            && astralSoul < StandardFire4Limit
            && context.Mp >= nextFire4Cost + reserve;
        var movementNeedsParadox = context.IsMoving
            && !context.HasUsableSwiftcast
            && !context.HasUsableTriplecast;
        var paradoxAtConfiguredPosition = context.CompressFireParadox
            ? movementNeedsParadox || fire4CountAtOrPastPlan || astralSoulFull
            : paradoxAtPlannedPosition;
        var paradoxForcedByBudget = paradoxPending
            && !astralSoulFull
            && !flareStarAlreadySpent
            && !canUseNextFire4
            && canUseParadoxWithoutLosingDespair;
        var shouldUseParadoxNow = paradoxPending
            && canUseParadoxWithoutLosingDespair
            && (paradoxAtConfiguredPosition || paradoxForcedByBudget);

        return new BlmFireBudget
        {
            Fire4Count = fire4Count,
            AstralSoul = astralSoul,
            NextFire4Cost = nextFire4Cost,
            RequiredReserveAfterFire4 = reserve,
            ParadoxPending = paradoxPending,
            CanAffordParadox = canAffordParadox,
            CanUseParadoxWithoutLosingDespair = canUseParadoxWithoutLosingDespair,
            ParadoxForcedByBudget = paradoxForcedByBudget,
            ShouldUseParadoxNow = shouldUseParadoxNow,
            CanUseNextFire4 = canUseNextFire4,
            CanUseDespair = context.Mp >= DespairMinimumMp,
            AstralSoulFull = astralSoulFull,
            Fire4CountAtOrPastPlan = fire4CountAtOrPastPlan,
        };
    }

    public static int Fire3Cost(BlmContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.UmbralHearts > 0
            ? Fire3HeartCost
            : Fire3FullCost;
    }
}
