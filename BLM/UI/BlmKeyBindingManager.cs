using System.Runtime.InteropServices;
using LosPr.BLM.Data;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Panels;

namespace LosPr.BLM.UI;

internal enum BlmBindingKind
{
    Qt,
    Hotkey,
}

internal static class BlmKeyBindingManager
{
    private readonly record struct CaptureTarget(BlmBindingKind Kind, string Key);
    private readonly record struct BindableKey(string Name, string DisplayName, int VirtualKey);

    private const int VirtualKeyMiddleMouse = 0x04;
    private const int VirtualKeyMouseSide1 = 0x05;
    private const int VirtualKeyMouseSide2 = 0x06;
    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyAlt = 0x12;

    private static readonly BindableKey[] BindableKeys = BuildBindableKeys();
    private static readonly Dictionary<string, BindableKey> BindableKeysByName =
        BuildBindableKeyMap();
    private static readonly Dictionary<int, bool> CaptureKeyDown = [];
    private static readonly Dictionary<int, bool> TriggerKeyDown = [];
    private static readonly HashSet<int> LoggedKeyReadFailures = [];

    private static CaptureTarget? _captureTarget;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    public static bool IsCapturing => _captureTarget is not null;

    public static void ProcessBindings(BlackMageSettingsStore store)
    {
        if (IsCapturing || ImGui.GetIO().WantTextInput)
            return;

        var settings = store.Settings;
        var pressedKeys = PollNewBindingPresses(settings);
        if (pressedKeys.Count == 0)
            return;

        var ctrl = IsKeyDown(VirtualKeyControl);
        var shift = IsKeyDown(VirtualKeyShift);
        var alt = IsKeyDown(VirtualKeyAlt);
        foreach (var (key, binding) in settings.QtBindings)
        {
            if (!BlackMageRotation.QtList.ContainsKey(key)
                || !Matches(binding, pressedKeys, ctrl, shift, alt))
            {
                continue;
            }

            var enabled = !BlmPanelPrimitives.SafeGetQt(key);
            BlmPanelPrimitives.SafeSetQt(key, enabled, store);
            Svc.Log.Info($"[Los Keybind] QT 已触发：{key} -> {(enabled ? "开启" : "关闭")}");
        }

        foreach (var (key, binding) in settings.HotkeyBindings)
        {
            if (!Matches(binding, pressedKeys, ctrl, shift, alt))
                continue;

            var activated = BlmHotkeyCatalog.TryActivate(key);
            Svc.Log.Info(
                $"[Los Keybind] Hotkey 已触发：{GetHotkeyDisplayName(key)}，"
                + $"TryActivate={activated}");
        }
    }

    public static void DrawBindingButton(
        BlackMageSettingsStore store,
        BlmBindingKind kind,
        string key,
        float scale)
    {
        var target = new CaptureTarget(kind, key);
        var capturing = _captureTarget == target;
        var binding = GetBinding(store.Settings, kind, key);
        var label = capturing
            ? "请按键..."
            : binding is null
                ? "未绑定"
                : Format(binding);
        if (LosComponents.SecondaryButton(
                $"binding_{kind}_{key}",
                label,
                size: new Vector2(124f * scale, 28f * scale),
                scale: scale,
                tooltip: capturing
                    ? "按单键、组合键或鼠标侧键；Esc 取消。"
                    : "左键重新绑定，右键清除。支持单键、组合键和鼠标侧键。"))
        {
            BeginCapture(target);
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ClearBinding(store, kind, key);
            if (_captureTarget == target)
                EndCapture();
            return;
        }

        if (capturing)
            CaptureNextKey(store, target);
    }

    public static string Format(BlmKeyBinding binding)
    {
        var parts = new List<string>(4);
        if (binding.Ctrl)
            parts.Add("Ctrl");
        if (binding.Shift)
            parts.Add("Shift");
        if (binding.Alt)
            parts.Add("Alt");
        parts.Add(FormatKeyName(binding.Key));
        return string.Join("+", parts);
    }

    public static string FormatCompact(BlmKeyBinding binding)
    {
        var parts = new List<string>(4);
        if (binding.Ctrl)
            parts.Add("C");
        if (binding.Shift)
            parts.Add("S");
        if (binding.Alt)
            parts.Add("A");
        parts.Add(FormatCompactKeyName(binding.Key));
        return string.Join("+", parts);
    }

    private static void CaptureNextKey(
        BlackMageSettingsStore store,
        CaptureTarget target)
    {
        foreach (var key in BindableKeys)
        {
            var down = IsKeyDown(key.VirtualKey);
            var wasDown = CaptureKeyDown.GetValueOrDefault(key.VirtualKey);
            CaptureKeyDown[key.VirtualKey] = down;
            if (!down || wasDown)
                continue;

            if (string.Equals(key.Name, "Escape", StringComparison.Ordinal))
            {
                EndCapture();
                Svc.Log.Info($"[Los Keybind] 已取消绑定：{FormatTarget(target)}");
                return;
            }

            var binding = new BlmKeyBinding
            {
                Key = key.Name,
                Ctrl = IsKeyDown(VirtualKeyControl),
                Shift = IsKeyDown(VirtualKeyShift),
                Alt = IsKeyDown(VirtualKeyAlt),
            };
            store.Update(settings => BindingMap(settings, target.Kind)[target.Key] = binding);
            TriggerKeyDown[key.VirtualKey] = true;
            EndCapture();
            Svc.Log.Info(
                $"[Los Keybind] 已绑定：{FormatTarget(target)} -> {Format(binding)}");
            return;
        }
    }

    private static bool Matches(
        BlmKeyBinding binding,
        HashSet<int> pressedKeys,
        bool ctrl,
        bool shift,
        bool alt)
    {
        if (!BindableKeysByName.TryGetValue(binding.Key, out var key))
        {
            return false;
        }

        return pressedKeys.Contains(key.VirtualKey)
            && ctrl == binding.Ctrl
            && shift == binding.Shift
            && alt == binding.Alt;
    }

    internal static bool IsSupportedKeyName(string? name)
        => name is not null && BindableKeysByName.ContainsKey(name);

    internal static int GetVirtualKeyCode(string? name)
        => name is not null && BindableKeysByName.TryGetValue(name, out var key)
            ? key.VirtualKey
            : 0;

    private static BindableKey[] BuildBindableKeys()
    {
        var keys = new List<BindableKey>
        {
            new("MouseMiddle", "鼠标中键", VirtualKeyMiddleMouse),
            new("MouseX1", "鼠标侧键 1", VirtualKeyMouseSide1),
            new("MouseX2", "鼠标侧键 2", VirtualKeyMouseSide2),
            new("Escape", "Esc", 0x1B),
            new("Tab", "Tab", 0x09),
            new("Backspace", "Backspace", 0x08),
            new("Space", "Space", 0x20),
            new("Enter", "Enter", 0x0D),
            new("LeftArrow", "Left", 0x25),
            new("UpArrow", "Up", 0x26),
            new("RightArrow", "Right", 0x27),
            new("DownArrow", "Down", 0x28),
            new("PageUp", "Page Up", 0x21),
            new("PageDown", "Page Down", 0x22),
            new("End", "End", 0x23),
            new("Home", "Home", 0x24),
            new("Insert", "Insert", 0x2D),
            new("Delete", "Delete", 0x2E),
            new("Apostrophe", "'", 0xDE),
            new("Comma", ",", 0xBC),
            new("Minus", "-", 0xBD),
            new("Period", ".", 0xBE),
            new("Slash", "/", 0xBF),
            new("Semicolon", ";", 0xBA),
            new("Equal", "=", 0xBB),
            new("LeftBracket", "[", 0xDB),
            new("Backslash", "\\", 0xDC),
            new("RightBracket", "]", 0xDD),
            new("GraveAccent", "`", 0xC0),
            new("KeypadDecimal", "Num .", 0x6E),
            new("KeypadDivide", "Num /", 0x6F),
            new("KeypadMultiply", "Num *", 0x6A),
            new("KeypadSubtract", "Num -", 0x6D),
            new("KeypadAdd", "Num +", 0x6B),
        };

        for (var number = 0; number <= 9; number++)
        {
            keys.Add(new BindableKey($"Alpha{number}", number.ToString(), 0x30 + number));
            keys.Add(new BindableKey($"Keypad{number}", $"Num {number}", 0x60 + number));
        }

        for (var letter = 'A'; letter <= 'Z'; letter++)
        {
            keys.Add(new BindableKey(letter.ToString(), letter.ToString(), letter));
        }

        for (var number = 1; number <= 12; number++)
        {
            keys.Add(new BindableKey($"F{number}", $"F{number}", 0x6F + number));
        }

        return keys.ToArray();
    }

    private static Dictionary<string, BindableKey> BuildBindableKeyMap()
    {
        var result = new Dictionary<string, BindableKey>(StringComparer.Ordinal);
        foreach (var key in BindableKeys)
            result[key.Name] = key;

        // Windows does not expose a distinct virtual key for the numpad Enter key.
        result["KeypadEnter"] = new BindableKey("KeypadEnter", "Num Enter", 0x0D);
        return result;
    }

    private static HashSet<int> PollNewBindingPresses(BlackMageSettings settings)
    {
        var requestedKeys = new HashSet<int>();
        AddRequestedKeys(settings.QtBindings, requestedKeys);
        AddRequestedKeys(settings.HotkeyBindings, requestedKeys);

        var pressedKeys = new HashSet<int>();
        foreach (var virtualKey in requestedKeys)
        {
            var down = IsKeyDown(virtualKey);
            if (TriggerKeyDown.TryGetValue(virtualKey, out var wasDown)
                && down
                && !wasDown)
            {
                pressedKeys.Add(virtualKey);
            }

            TriggerKeyDown[virtualKey] = down;
        }

        return pressedKeys;
    }

    private static void AddRequestedKeys(
        Dictionary<string, BlmKeyBinding> bindings,
        HashSet<int> requestedKeys)
    {
        foreach (var binding in bindings.Values)
        {
            if (BindableKeysByName.TryGetValue(binding.Key, out var key))
                requestedKeys.Add(key.VirtualKey);
        }
    }

    private static bool IsKeyDown(int virtualKey)
    {
        try
        {
            if (virtualKey is VirtualKeyMiddleMouse or VirtualKeyMouseSide1 or VirtualKeyMouseSide2)
                return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

            if (Svc.KeyState.IsVirtualKeyValid(virtualKey))
                return Svc.KeyState[virtualKey];
        }
        catch (Exception exception)
        {
            if (LoggedKeyReadFailures.Add(virtualKey))
                Svc.Log.Warning(exception, $"[Los Keybind] Dalamud 键位读取失败：VK={virtualKey}。");
        }

        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private static void BeginCapture(CaptureTarget target)
    {
        _captureTarget = target;
        CaptureKeyDown.Clear();
        foreach (var key in BindableKeys)
        {
            CaptureKeyDown[key.VirtualKey] = IsKeyDown(key.VirtualKey);
        }

        Svc.Log.Info($"[Los Keybind] 开始捕获：{FormatTarget(target)}");
    }

    private static void EndCapture()
    {
        _captureTarget = null;
        CaptureKeyDown.Clear();
    }

    private static string FormatKeyName(string name)
    {
        if (BindableKeysByName.TryGetValue(name, out var key))
            return key.DisplayName;

        return name;
    }

    private static string FormatCompactKeyName(string name)
        => name switch
        {
            "MouseMiddle" => "M3",
            "MouseX1" => "M4",
            "MouseX2" => "M5",
            "Backspace" => "BS",
            "Space" => "Spc",
            "Enter" => "Ent",
            "Escape" => "Esc",
            "LeftArrow" => "Left",
            "RightArrow" => "Right",
            "UpArrow" => "Up",
            "DownArrow" => "Down",
            "PageUp" => "PgU",
            "PageDown" => "PgD",
            "Insert" => "Ins",
            "Delete" => "Del",
            "KeypadEnter" => "NEnt",
            "KeypadDecimal" => "N.",
            "KeypadDivide" => "N/",
            "KeypadMultiply" => "N*",
            "KeypadSubtract" => "N-",
            "KeypadAdd" => "N+",
            _ when name.StartsWith("Keypad", StringComparison.Ordinal)
                && name.Length == 7 => $"N{name[^1]}",
            _ => FormatKeyName(name),
        };

    private static BlmKeyBinding? GetBinding(
        BlackMageSettings settings,
        BlmBindingKind kind,
        string key)
        => BindingMap(settings, kind).TryGetValue(key, out var binding)
            ? binding
            : null;

    private static Dictionary<string, BlmKeyBinding> BindingMap(
        BlackMageSettings settings,
        BlmBindingKind kind)
        => kind == BlmBindingKind.Qt
            ? settings.QtBindings
            : settings.HotkeyBindings;

    private static void ClearBinding(
        BlackMageSettingsStore store,
        BlmBindingKind kind,
        string key)
    {
        var removed = false;
        store.Update(settings => removed = BindingMap(settings, kind).Remove(key));
        if (removed)
        {
            Svc.Log.Info(
                $"[Los Keybind] 已清除绑定：{FormatTarget(new CaptureTarget(kind, key))}");
        }
    }

    private static string FormatTarget(CaptureTarget target)
        => target.Kind == BlmBindingKind.Qt
            ? $"QT「{target.Key}」"
            : $"Hotkey「{GetHotkeyDisplayName(target.Key)}」";

    private static string GetHotkeyDisplayName(string key)
    {
        foreach (var definition in BlmHotkeyCatalog.Entries)
        {
            if (string.Equals(definition.Key, key, StringComparison.Ordinal))
                return definition.Name;
        }

        return key;
    }
}
