using Dalamud.Interface.Textures.TextureWraps;
using PromeRotation.Data;
using PromeRotation.Resolvers;
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
    private const long ManualAbilityIntentTimeoutMs = 5_000;
    private const long ManualAbilityAckTimeoutMs = 2_000;
    private const long ManualAbilityRetryDelayMs = 250;
    private const long ManualAbilityObservedTimeoutMs = 2_000;
    private const int ManualAbilityMaxAttempts = 3;
    private const float ManualAbilityAnimationLockThreshold = 0.05f;
    private const long PotionPendingTimeoutMs = 5_000;

    private static readonly ConcurrentDictionary<uint, uint> ItemIconIds = new();
    private static readonly Dictionary<uint, ManualAbilityRequest> ManualAbilityPending = new();
    private static readonly ConcurrentDictionary<uint, long> ManualAbilityObserved = new();
    private static readonly object ManualAbilityGate = new();
    private static readonly object PotionGate = new();
    private static Func<bool>? _openerActiveProvider;
    private static uint _pendingPotionItemId;
    private static long _pendingPotionExpiresAtMs;
    private static long _nextPotionAttemptAtMs;
    private static int _potionAttemptCount;

    internal sealed class ManualAbilityRequest
    {
        public ManualAbilityRequest(
            in BlmHotkeyDefinition definition,
            PAction action,
            long now)
        {
            Definition = definition;
            Action = action;
            ExpiresAtMs = now + ManualAbilityIntentTimeoutMs;
        }

        public BlmHotkeyDefinition Definition { get; }
        public PAction Action { get; }
        // 同一个热键的不同形态共享去重键，Action 则保留按下时的实际技能。
        public uint PendingKey => Definition.ActionId;
        public long ExpiresAtMs { get; set; }
        public long NextAttemptAtMs { get; set; }
        public int Attempts { get; set; }
        public bool AwaitingAck { get; set; }

        public bool MatchesCurrentAction(uint currentActionId)
            => Action.ActionId == currentActionId;
    }

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
            lock (ManualAbilityGate)
            {
                CleanupExpiredManualPendingNoLock(Environment.TickCount64);
                return ManualAbilityPending.Count > 0;
            }
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
                _ => ResolveManualActionId(definition.ActionId, ActionHelper.GetAdjustedActionId),
            };
        }
        catch
        {
            return definition.Kind == BlmHotkeyKind.Action && definition.ActionId != 3573u
                ? definition.ActionId
                : 0u;
        }
    }

    internal static uint ResolveManualActionId(uint actionId, Func<uint, uint> getAdjustedActionId)
        => actionId == 3573u ? getAdjustedActionId(actionId) : actionId;

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
        => IsAvailable(definition, ResolveActionId(definition));

    private static bool IsAvailable(in BlmHotkeyDefinition definition, uint actionId)
    {
        if (IsBlockedByOpener(definition))
            return false;

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

        if (definition.Type == ActionType.OffGcd)
        {
            lock (ManualAbilityGate)
            {
                CleanupExpiredManualPendingNoLock(Environment.TickCount64);
                return ManualAbilityPending.ContainsKey(definition.ActionId);
            }
        }

        var actionId = ResolveActionId(definition);
        if (actionId == 0)
            return false;

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
        => GetCooldown(ResolveActionId(definition), definition.Kind);

    private static float GetCooldown(uint actionId, BlmHotkeyKind kind)
    {
        if (actionId == 0)
            return 0f;

        try
        {
            return kind == BlmHotkeyKind.Potion
                ? ActionHelper.GetItemCooldown(NormalizeItemId(actionId))
                : ActionHelper.GetActionCooldown(actionId);
        }
        catch
        {
            return 0f;
        }
    }

    public static int GetCharges(in BlmHotkeyDefinition definition)
        => definition.Kind == BlmHotkeyKind.Action ? GetCharges(ResolveActionId(definition)) : 0;

    private static int GetCharges(uint actionId)
    {
        if (actionId == 0)
            return 0;

        try
        {
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
        if (!IsAvailable(definition, actionId))
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

        lock (ManualAbilityGate)
        {
            var request = RemoveObservedManualAbility(ManualAbilityPending, actionId);
            if (request != null)
            {
                ManualAbilityObserved[actionId] = Environment.TickCount64
                    + ManualAbilityObservedTimeoutMs;
                Svc.Log.Info(
                    $"[Los Hotkey] 手动能力技已观察到回执：{request.Definition.Name}，"
                    + $"Original={request.PendingKey}，Actual={actionId}，AwaitingAck={request.AwaitingAck}");
            }
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

    internal static ManualAbilityRequest? RemoveObservedManualAbility(
        Dictionary<uint, ManualAbilityRequest> pending,
        uint observedActionId)
    {
        foreach (var entry in pending)
        {
            // 只接受冻结的实际技能，不能把黑魔纹与魔纹重置互相当作回执。
            if (entry.Value.Action.ActionId != observedActionId)
                continue;

            pending.Remove(entry.Key);
            return entry.Value;
        }

        return null;
    }

    internal static bool IsManualAbilityReady(
        uint actionId,
        Func<uint, float> readCooldown,
        Func<uint, int> readCharges)
        => readCooldown(actionId) <= 0.05f || readCharges(actionId) > 0;

    internal static bool CancelChangedManualAbility(
        Dictionary<uint, ManualAbilityRequest> pending,
        ManualAbilityRequest request,
        uint currentActionId)
    {
        if (request.AwaitingAck || request.MatchesCurrentAction(currentActionId))
            return false;
        if (!pending.TryGetValue(request.PendingKey, out var current)
            || !ReferenceEquals(current, request))
        {
            return false;
        }

        return pending.Remove(request.PendingKey);
    }

    public static bool ConsumeManualAbilityObservation(uint actionId)
    {
        CleanupExpiredManualPending();
        return actionId != 0 && ManualAbilityObserved.TryRemove(actionId, out _);
    }

    public static void ClearPending()
    {
        lock (ManualAbilityGate)
        {
            ManualAbilityPending.Clear();
            ManualAbilityObserved.Clear();
        }
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
        if (!IsManualAbilityReady(actionId, ReadManualAbilityCooldown, GetCharges))
        {
            Svc.Log.Info(
                $"[Los Hotkey] 手动能力技拒绝：{definition.Name}，Reason=Cooldown，"
                + $"Original={definition.ActionId}，Actual={actionId}，"
                + $"Cooldown={ReadManualAbilityCooldown(actionId):0.000}s，Charges={GetCharges(actionId)}");
            return false;
        }

        var action = new PAction(actionId, definition.Type, definition.Target);
        if (definition.UseMouseGround)
        {
            action.IsLocationAction = true;
            action.Position = PosHelper.ScreenToWorld();
        }

        try
        {
            var now = Environment.TickCount64;
            lock (ManualAbilityGate)
            {
                CleanupExpiredManualPendingNoLock(now);
                var request = new ManualAbilityRequest(definition, action, now);
                if (!ManualAbilityPending.TryAdd(request.PendingKey, request))
                {
                    Svc.Log.Info(
                        $"[Los Hotkey] 手动能力技拒绝：{definition.Name}，Reason=AlreadyPending，"
                        + $"Original={definition.ActionId}，Actual={actionId}");
                    return false;
                }
            }

            Svc.Log.Info(
                $"[Los Hotkey] 手动能力技进入等待：{definition.Name}({actionId})，"
                + $"Original={definition.ActionId}，Actual={actionId}，"
                + $"Casting={PRCore.Me?.IsCasting == true}，"
                + $"GcdRemain={ActionHelper.GetGcdRemain():0.000}s");
            return true;
        }
        catch (Exception exception)
        {
            lock (ManualAbilityGate)
                ManualAbilityPending.Remove(definition.ActionId);
            Svc.Log.Warning(exception, $"[Los] Hotkey 手动能力技注册失败：{definition.Name}。");
            return false;
        }
    }

    private static void CleanupExpiredManualPending()
    {
        lock (ManualAbilityGate)
        {
            CleanupExpiredManualPendingNoLock(Environment.TickCount64);
        }
    }

    private static void CleanupExpiredManualPendingNoLock(long now)
    {
        var pendingSnapshot = new List<KeyValuePair<uint, ManualAbilityRequest>>(
            ManualAbilityPending);
        foreach (var pending in pendingSnapshot)
        {
            if (!pending.Value.AwaitingAck && pending.Value.ExpiresAtMs <= now)
            {
                ManualAbilityPending.Remove(pending.Key);
                Svc.Log.Warning(
                    $"[Los Hotkey] 手动能力技等待超时：{pending.Value.Definition.Name}，"
                    + $"Original={pending.Key}，Actual={pending.Value.Action.ActionId}，"
                    + $"Attempts={pending.Value.Attempts}，AwaitingAck={pending.Value.AwaitingAck}");
            }
        }

        foreach (var observed in ManualAbilityObserved)
        {
            if (observed.Value <= now)
                ManualAbilityObserved.TryRemove(observed.Key, out _);
        }
    }

    public static void ProcessPendingDirectActions()
    {
        ProcessPendingManualAbilities();

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

    private static void ProcessPendingManualAbilities()
    {
        var now = Environment.TickCount64;
        ManualAbilityRequest[] requests;
        lock (ManualAbilityGate)
        {
            CleanupExpiredManualPendingNoLock(now);
            requests = new List<ManualAbilityRequest>(ManualAbilityPending.Values).ToArray();
        }

        foreach (var request in requests)
        {
            if (request.AwaitingAck)
            {
                if (now < request.ExpiresAtMs)
                    continue;

                lock (ManualAbilityGate)
                {
                    if (!ManualAbilityPending.TryGetValue(request.PendingKey, out var current)
                        || !ReferenceEquals(current, request))
                    {
                        continue;
                    }

                    ManualAbilityPending.Remove(request.PendingKey);
                    ManualAbilityObserved[request.Action.ActionId] = now
                        + ManualAbilityObservedTimeoutMs;
                }

                Svc.Log.Warning(
                    $"[Los Hotkey] 手动能力技等待服务器回执超时：{request.Definition.Name}"
                    + $"，Original={request.PendingKey}，Actual={request.Action.ActionId}，Attempts={request.Attempts}");
                continue;
            }

            var currentActionId = ResolveActionId(request.Definition);
            if (!request.MatchesCurrentAction(currentActionId))
            {
                bool cancelled;
                lock (ManualAbilityGate)
                {
                    cancelled = CancelChangedManualAbility(ManualAbilityPending, request, currentActionId);
                }

                if (cancelled)
                {
                    Svc.Log.Info(
                        $"[Los Hotkey] 手动能力技取消：{request.Definition.Name}，Reason=ActionChanged，"
                        + $"Original={request.PendingKey}，Requested={request.Action.ActionId}，Current={currentActionId}");
                }
                continue;
            }

            if (now < request.NextAttemptAtMs)
                continue;

            if (!IsManualAbilityWindowReady(request))
                continue;

            var dispatched = DispatchManualAbility(request.Action);
            var submittedAt = Environment.TickCount64;
            lock (ManualAbilityGate)
            {
                if (!ManualAbilityPending.TryGetValue(request.PendingKey, out var current)
                    || !ReferenceEquals(current, request))
                {
                    continue;
                }

                request.Attempts++;
                if (dispatched)
                {
                    request.AwaitingAck = true;
                    request.ExpiresAtMs = submittedAt + ManualAbilityAckTimeoutMs;
                }
                else if (request.Attempts >= ManualAbilityMaxAttempts)
                {
                    ManualAbilityPending.Remove(request.PendingKey);
                }
                else
                {
                    request.NextAttemptAtMs = submittedAt + ManualAbilityRetryDelayMs;
                }
            }

            Svc.Log.Info(
                $"[Los Hotkey] 手动能力技提交：{request.Definition.Name}"
                + $"({request.Action.ActionId})，Attempt={request.Attempts}，"
                + $"Original={request.PendingKey}，Actual={request.Action.ActionId}，"
                + $"UseActionReturn={dispatched}，Casting={PRCore.Me?.IsCasting == true}，"
                + $"GcdRemain={ActionHelper.GetGcdRemain():0.000}s");
        }
    }

    private static bool IsManualAbilityWindowReady(ManualAbilityRequest request)
    {
        if (PromeSettings.Instance.EnableAcr != AcrState.On
            || PRGameData.IsPlayerOccupied())
        {
            return false;
        }

        if (PRCore.Me?.IsCasting == true
            || ActionHelper.GetAnimationLock() > ManualAbilityAnimationLockThreshold)
        {
            return false;
        }

        if (!IsManualAbilityReady(request.Action.ActionId, ReadManualAbilityCooldown, GetCharges))
            return false;

        if (request.Action.IsLocationAction)
            return true;

        try
        {
            return TargetResolver.Resolve(request.Action.Target) != null;
        }
        catch
        {
            return false;
        }
    }

    private static float ReadManualAbilityCooldown(uint actionId)
        => GetCooldown(actionId, BlmHotkeyKind.Action);

    private static unsafe bool DispatchManualAbility(PAction action)
    {
        try
        {
            var actionManager = GameActionManager.Instance();
            if (actionManager == null)
                return false;

            var gameActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action;
            if (action.IsLocationAction)
            {
                var position = action.Position;
                return actionManager->UseActionLocation(gameActionType, action.ActionId, 0, &position);
            }

            var targetObject = TargetResolver.Resolve(action.Target);
            if (targetObject == null)
                return false;

            return actionManager->UseAction(
                gameActionType,
                action.ActionId,
                targetObject.GameObjectId);
        }
        catch (Exception exception)
        {
            Svc.Log.Warning(exception, $"[Los] 手动能力技调用失败：{action.ActionId}");
            return false;
        }
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
