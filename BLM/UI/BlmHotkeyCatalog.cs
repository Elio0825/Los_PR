using Dalamud.Interface.Textures.TextureWraps;
using PromeRotation.Data;
using GameActionManager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager;

namespace LosPr.BLM.UI;

internal enum BlmHotkeyKind
{
    Action,
    LimitBreak,
    Potion,
}

internal readonly record struct BlmHotkeyDefinition(
    string Key,
    string Name,
    uint ActionId,
    ActionType Type,
    ActionTargetType Target,
    BlmHotkeyKind Kind = BlmHotkeyKind.Action,
    bool UseMouseGround = false,
    bool AllowDuringOpener = false);

internal readonly record struct BlmPotionDispatchParameters(uint ActionId, uint ExtraParam);

internal static class BlmHotkeyCatalog
{
    private const uint DefaultLimitBreakIconActionId = 203u;
    private const uint DefaultPotionItemId = 49237u;
    private const long ManualAbilityPendingTimeoutMs = 10_000;
    private const long ManualAbilityObservedTimeoutMs = 2_000;
    private const long PotionPendingTimeoutMs = 5_000;

    private static readonly ConcurrentDictionary<uint, uint> ItemIconIds = new();
    private static readonly ConcurrentDictionary<uint, long> ManualAbilityPending = new();
    private static readonly ConcurrentDictionary<uint, long> ManualAbilityObserved = new();
    private static readonly object PotionGate = new();
    private static Func<bool>? _openerActiveProvider;
    private static uint _pendingPotionItemId;
    private static long _pendingPotionExpiresAtMs;
    private static long _nextPotionAttemptAtMs;
    private static int _potionAttemptCount;

    public static IReadOnlyList<BlmHotkeyDefinition> Entries { get; } =
    [
        new("limit_break", "LB", DefaultLimitBreakIconActionId,
            ActionType.LimitBreak, ActionTargetType.Self, BlmHotkeyKind.LimitBreak),
        new("potion", "爆发药", DefaultPotionItemId,
            ActionType.Item, ActionTargetType.Self, BlmHotkeyKind.Potion,
            AllowDuringOpener: true),
        new("sprint", "疾跑", 3u, ActionType.OffGcd, ActionTargetType.Self,
            AllowDuringOpener: true),
        new("manaward", "魔罩", 157u, ActionType.OffGcd, ActionTargetType.Self,
            AllowDuringOpener: true),
        new("addle", "昏乱", 7560u, ActionType.OffGcd, ActionTargetType.Target,
            AllowDuringOpener: true),
        new("surecast", "沉稳咏唱", 7559u, ActionType.OffGcd, ActionTargetType.Self,
            AllowDuringOpener: true),
        new("ley_lines", "黑魔纹", 3573u, ActionType.OffGcd, ActionTargetType.Self),
        new("triplecast", "三连咏唱", 7421u, ActionType.OffGcd, ActionTargetType.Self),
        new("swiftcast", "即刻咏唱", 7561u, ActionType.OffGcd, ActionTargetType.Self),
        new("aetherial_manipulation", "以太步·鼠标", 155u,
            ActionType.OffGcd, ActionTargetType.MouseOver,
            AllowDuringOpener: true),
        new("between_the_lines", "魔纹步·鼠标", 7419u,
            ActionType.OffGcd, ActionTargetType.Self,
            UseMouseGround: true, AllowDuringOpener: true),
        new("xenoglossy", "异言", 16507u, ActionType.Gcd, ActionTargetType.Target),
    ];

    public static bool HasPendingManualAbility
    {
        get
        {
            CleanupExpiredManualPending();
            return !ManualAbilityPending.IsEmpty;
        }
    }

    public static void SetOpenerActiveProvider(Func<bool>? provider)
        => _openerActiveProvider = provider;

    public static uint ResolveActionId(in BlmHotkeyDefinition definition)
    {
        try
        {
            return definition.Kind switch
            {
                BlmHotkeyKind.LimitBreak => LimitBreakHelper.GetLimitBreakActionId(),
                BlmHotkeyKind.Potion => PRGameData.GetBestPotionId(),
                _ => definition.ActionId,
            };
        }
        catch
        {
            return definition.Kind == BlmHotkeyKind.Action
                ? definition.ActionId
                : 0u;
        }
    }

    public static IDalamudTextureWrap? ResolveIcon(in BlmHotkeyDefinition definition)
    {
        try
        {
            if (definition.Kind == BlmHotkeyKind.Potion)
            {
                var itemId = NormalizeItemId(ResolveActionId(definition));
                if (itemId == 0)
                    itemId = DefaultPotionItemId;

                var iconId = ItemIconIds.GetOrAdd(itemId, ReadItemIconId);
                return iconId == 0 ? null : iconId.GetGameIcon();
            }

            var actionId = ResolveActionId(definition);
            if (actionId == 0 && definition.Kind == BlmHotkeyKind.LimitBreak)
                actionId = DefaultLimitBreakIconActionId;
            return actionId.GetActionIcon();
        }
        catch
        {
            return null;
        }
    }

    public static bool IsAvailable(in BlmHotkeyDefinition definition)
    {
        if (IsBlockedByOpener(definition))
            return false;

        var actionId = ResolveActionId(definition);
        if (actionId == 0)
            return false;

        if (definition.Kind == BlmHotkeyKind.Potion)
            return GetCooldown(definition) <= 0.1f;
        if (definition.Kind == BlmHotkeyKind.LimitBreak)
            return true;

        try
        {
            return ActionHelper.IsUnlocked(actionId);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsPending(in BlmHotkeyDefinition definition)
    {
        if (definition.Kind == BlmHotkeyKind.Potion)
        {
            lock (PotionGate)
            {
                CleanupExpiredPotionNoLock();
                return _pendingPotionItemId != 0;
            }
        }

        var actionId = ResolveActionId(definition);
        if (actionId == 0)
            return false;

        if (definition.Type == ActionType.OffGcd)
        {
            CleanupExpiredManualPending();
            return ManualAbilityPending.ContainsKey(actionId);
        }

        try
        {
            return HotkeyQueueManager.IsPending(actionId);
        }
        catch
        {
            return false;
        }
    }

    public static float GetCooldown(in BlmHotkeyDefinition definition)
    {
        var actionId = ResolveActionId(definition);
        if (actionId == 0)
            return 0f;

        try
        {
            return definition.Kind == BlmHotkeyKind.Potion
                ? ActionHelper.GetItemCooldown(NormalizeItemId(actionId))
                : ActionHelper.GetActionCooldown(actionId);
        }
        catch
        {
            return 0f;
        }
    }

    public static int GetCharges(in BlmHotkeyDefinition definition)
    {
        if (definition.Kind != BlmHotkeyKind.Action)
            return 0;

        try
        {
            var actionId = ResolveActionId(definition);
            return ActionHelper.GetMaxCharges(actionId) > 1
                ? Math.Max(0, (int)ActionHelper.GetActionCharges(actionId))
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static bool TryActivate(in BlmHotkeyDefinition definition)
    {
        if (IsBlockedByOpener(definition))
            return false;

        var actionId = ResolveActionId(definition);
        if (actionId == 0)
            return false;

        if (definition.Kind == BlmHotkeyKind.Potion)
            return TryQueuePotion(actionId);
        if (!IsAvailable(definition))
            return false;

        if (definition.Type == ActionType.OffGcd)
            return TryQueueManualAbility(definition, actionId);

        var action = new PAction(actionId, definition.Type, definition.Target);
        if (definition.UseMouseGround)
        {
            action.IsLocationAction = true;
            action.Position = PosHelper.ScreenToWorld();
        }

        try
        {
            HotkeyQueueManager.TryEnqueue(action);
            return true;
        }
        catch (Exception exception)
        {
            Svc.Log.Warning(exception, $"[Los] Hotkey 入队失败：{definition.Name}。");
            return false;
        }
    }

    public static bool TryActivate(string key)
    {
        foreach (var definition in Entries)
        {
            if (string.Equals(definition.Key, key, StringComparison.Ordinal))
                return TryActivate(definition);
        }

        return false;
    }

    public static void NotifyActionObserved(uint actionId)
    {
        if (actionId == 0)
            return;

        if (ManualAbilityPending.TryRemove(actionId, out _))
        {
            ManualAbilityObserved[actionId] = Environment.TickCount64
                + ManualAbilityObservedTimeoutMs;
        }

        lock (PotionGate)
        {
            if (_pendingPotionItemId != 0
                && NormalizeItemId(_pendingPotionItemId) == NormalizeItemId(actionId))
            {
                ClearPendingPotionNoLock();
            }
        }
    }

    public static bool ConsumeManualAbilityObservation(uint actionId)
    {
        CleanupExpiredManualPending();
        return actionId != 0 && ManualAbilityObserved.TryRemove(actionId, out _);
    }

    public static void ClearPending()
    {
        ManualAbilityPending.Clear();
        ManualAbilityObserved.Clear();
        lock (PotionGate)
            ClearPendingPotionNoLock();
    }

    public static string BuildTooltip(in BlmHotkeyDefinition definition)
    {
        if (IsBlockedByOpener(definition))
            return $"{definition.Name}\n起手期间锁定，避免破坏固定技能顺序";

        var actionId = ResolveActionId(definition);
        if (actionId == 0)
        {
            return definition.Kind switch
            {
                BlmHotkeyKind.Potion => $"{definition.Name}\n背包内没有支持的智力爆发药",
                BlmHotkeyKind.LimitBreak => $"{definition.Name}\n当前没有可用的 LB 档位",
                _ => $"{definition.Name}\n技能不可用",
            };
        }

        var cooldown = GetCooldown(definition);
        if (cooldown > 0.1f)
            return $"{definition.Name}\n冷却剩余 {cooldown:0.0} 秒";
        if (!IsAvailable(definition))
            return $"{definition.Name}\n当前等级尚未解锁";
        return $"{definition.Name}\n点击加入预输入";
    }

    private static uint ReadItemIconId(uint itemId)
    {
        try
        {
            return Svc.Data
                .GetExcelSheet<Lumina.Excel.Sheets.Item>()
                .GetRow(itemId)
                .Icon;
        }
        catch
        {
            return 0u;
        }
    }

    private static uint NormalizeItemId(uint itemId)
        => itemId >= 1_000_000u ? itemId - 1_000_000u : itemId;

    private static bool TryQueueManualAbility(
        in BlmHotkeyDefinition definition,
        uint actionId)
    {
        CleanupExpiredManualPending();
        if (ManualAbilityPending.ContainsKey(actionId))
            return false;

        var cooldown = GetCooldown(definition);
        if (cooldown > 0.05f && GetCharges(definition) == 0)
            return false;

        var action = new PAction(actionId, ActionType.Always, definition.Target);
        if (definition.UseMouseGround)
        {
            action.IsLocationAction = true;
            action.Position = PosHelper.ScreenToWorld();
        }

        try
        {
            ManualAbilityPending[actionId] = Environment.TickCount64
                + ManualAbilityPendingTimeoutMs;
            ActionQueueManager.Enqueue(action, isHighPriority: true);
            Svc.Log.Info(
                $"[Los Hotkey] 强制能力技已排入 Always：{definition.Name}({actionId})，"
                + $"Casting={PRCore.Me?.IsCasting == true}，"
                + $"GcdRemain={ActionHelper.GetGcdRemain():0.000}s");
            return true;
        }
        catch (Exception exception)
        {
            ManualAbilityPending.TryRemove(actionId, out _);
            Svc.Log.Warning(exception, $"[Los] Hotkey 强制能力技入队失败：{definition.Name}。");
            return false;
        }
    }

    private static void CleanupExpiredManualPending()
    {
        var now = Environment.TickCount64;
        foreach (var pending in ManualAbilityPending)
        {
            if (pending.Value <= now)
                ManualAbilityPending.TryRemove(pending.Key, out _);
        }

        foreach (var observed in ManualAbilityObserved)
        {
            if (observed.Value <= now)
                ManualAbilityObserved.TryRemove(observed.Key, out _);
        }
    }

    public static void ProcessPendingDirectActions()
    {
        uint itemId;
        lock (PotionGate)
        {
            CleanupExpiredPotionNoLock();
            itemId = _pendingPotionItemId;
            if (itemId == 0 || Environment.TickCount64 < _nextPotionAttemptAtMs)
                return;
        }

        var normalizedItemId = NormalizeItemId(itemId);
        if (normalizedItemId == 0)
        {
            lock (PotionGate)
                ClearPendingPotionNoLock();
            return;
        }

        var cooldown = ActionHelper.GetItemCooldown(normalizedItemId);
        if (cooldown > 0.1f)
        {
            lock (PotionGate)
            {
                if (_pendingPotionItemId == itemId)
                {
                    Svc.Log.Info(
                        $"[Los Hotkey] 爆发药公共冷却已启动：Item={itemId}，"
                        + $"Cooldown={cooldown:0.000}s，Attempts={_potionAttemptCount}");
                }

                ClearPendingPotionNoLock();
            }
            return;
        }

        if (PRCore.Me?.IsCasting == true || ActionHelper.GetAnimationLock() > 0.05f)
            return;

        var dispatched = DispatchPotion(itemId);
        lock (PotionGate)
        {
            if (_pendingPotionItemId != itemId)
                return;

            _potionAttemptCount++;
            _nextPotionAttemptAtMs = Environment.TickCount64
                + (dispatched ? 300L : 100L);
        }

        Svc.Log.Info(
            $"[Los Hotkey] 爆发药提交：Item={itemId}，Normalized={normalizedItemId}，"
            + $"Attempt={_potionAttemptCount}，UseActionReturn={dispatched}，"
            + $"GcdRemain={ActionHelper.GetGcdRemain():0.000}s");
    }

    private static bool TryQueuePotion(uint itemId)
    {
        var normalizedItemId = NormalizeItemId(itemId);
        if (normalizedItemId == 0 || ActionHelper.GetItemCooldown(normalizedItemId) > 0.1f)
            return false;

        lock (PotionGate)
        {
            CleanupExpiredPotionNoLock();
            if (_pendingPotionItemId != 0)
                return false;

            _pendingPotionItemId = itemId;
            _pendingPotionExpiresAtMs = Environment.TickCount64 + PotionPendingTimeoutMs;
            _nextPotionAttemptAtMs = 0;
            _potionAttemptCount = 0;
        }

        Svc.Log.Info(
            $"[Los Hotkey] 爆发药已进入等待：Item={itemId}，"
            + $"HQ={itemId >= 1_000_000u}，Casting={PRCore.Me?.IsCasting == true}，"
            + $"GcdRemain={ActionHelper.GetGcdRemain():0.000}s");
        return true;
    }

    private static unsafe bool DispatchPotion(uint itemId)
    {
        if (PRCore.Me is not { } player)
            return false;

        var actionManager = GameActionManager.Instance();
        if (actionManager == null)
            return false;

        var parameters = BuildPotionDispatchParameters(itemId);
        return actionManager->UseAction(
            FFXIVClientStructs.FFXIV.Client.Game.ActionType.Item,
            parameters.ActionId,
            player.GameObjectId,
            parameters.ExtraParam);
    }

    internal static BlmPotionDispatchParameters BuildPotionDispatchParameters(uint itemId)
        => new(itemId, 0xFFFFu);

    private static bool IsBlockedByOpener(in BlmHotkeyDefinition definition)
    {
        if (definition.AllowDuringOpener)
            return false;

        try
        {
            return _openerActiveProvider?.Invoke() == true;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanupExpiredPotionNoLock()
    {
        if (_pendingPotionItemId != 0
            && _pendingPotionExpiresAtMs <= Environment.TickCount64)
        {
            Svc.Log.Warning(
                $"[Los Hotkey] 爆发药等待超时：Item={_pendingPotionItemId}，"
                + $"Attempts={_potionAttemptCount}");
            ClearPendingPotionNoLock();
        }
    }

    private static void ClearPendingPotionNoLock()
    {
        _pendingPotionItemId = 0;
        _pendingPotionExpiresAtMs = 0;
        _nextPotionAttemptAtMs = 0;
        _potionAttemptCount = 0;
    }
}
