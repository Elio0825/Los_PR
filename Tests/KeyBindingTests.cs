using LosPr.BLM.Data;
using LosPr.BLM.UI;
using System.Text.Json;

namespace Los.Tests;

internal static class KeyBindingTests
{
    public static void RunAll()
    {
        ExplicitWhitelistAcceptsKeyboardAndSideMouseKeys();
        BindingFormattingIsStable();
        BindingConfigurationRoundTrips();
    }

    private static void ExplicitWhitelistAcceptsKeyboardAndSideMouseKeys()
    {
        foreach (var key in new[]
        {
            "A", "Z", "Alpha0", "Alpha9", "F1", "F12", "Space",
            "LeftArrow", "Delete", "Semicolon", "Keypad0", "KeypadEnter",
            "MouseMiddle", "MouseX1", "MouseX2",
        })
        {
            AssertEx.True(
                BlmKeyBindingManager.IsSupportedKeyName(key),
                $"常用键盘键必须允许绑定：{key}");
        }

        foreach (var key in new[]
        {
            "", "None", "COUNT", "NamedKey_BEGIN", "NamedKey_END",
            "LegacyNativeKey_BEGIN", "LegacyNativeKey_END",
            "KeysData_SIZE", "KeysData_OFFSET", "ModCtrl", "ReservedForModCtrl",
            "LeftCtrl", "RightShift", "MouseLeft", "MouseRight", "GamepadStart",
        })
        {
            AssertEx.False(
                BlmKeyBindingManager.IsSupportedKeyName(key),
                $"ImGui 内部值、修饰键和非键盘输入不得进入捕获列表：{key}");
        }

        AssertEx.Equal(0x05, BlmKeyBindingManager.GetVirtualKeyCode("MouseX1"), "鼠标侧键 1 VK 错误");
        AssertEx.Equal(0x06, BlmKeyBindingManager.GetVirtualKeyCode("MouseX2"), "鼠标侧键 2 VK 错误");
        AssertEx.Equal(0x41, BlmKeyBindingManager.GetVirtualKeyCode("A"), "字母 A VK 错误");
        AssertEx.Equal(0x31, BlmKeyBindingManager.GetVirtualKeyCode("Alpha1"), "数字 1 VK 错误");
    }

    private static void BindingFormattingIsStable()
    {
        AssertEx.Equal(
            "Ctrl+Shift+7",
            BlmKeyBindingManager.Format(new BlmKeyBinding
            {
                Key = "Alpha7",
                Ctrl = true,
                Shift = true,
            }),
            "数字组合键显示格式错误");
        AssertEx.Equal(
            "Alt+Num Enter",
            BlmKeyBindingManager.Format(new BlmKeyBinding
            {
                Key = "KeypadEnter",
                Alt = true,
            }),
            "小键盘组合键显示格式错误");
        AssertEx.Equal(
            "鼠标侧键 1",
            BlmKeyBindingManager.Format(new BlmKeyBinding { Key = "MouseX1" }),
            "单独鼠标侧键显示格式错误");
        AssertEx.Equal(
            "C+S+F12",
            BlmKeyBindingManager.FormatCompact(new BlmKeyBinding
            {
                Key = "F12",
                Ctrl = true,
                Shift = true,
            }),
            "浮窗组合键紧凑格式错误");
        AssertEx.Equal(
            "M5",
            BlmKeyBindingManager.FormatCompact(new BlmKeyBinding { Key = "MouseX2" }),
            "浮窗鼠标侧键紧凑格式错误");
    }

    private static void BindingConfigurationRoundTrips()
    {
        var settings = new BlackMageSettings();
        settings.QtBindings["自动醒梦"] = new BlmKeyBinding
        {
            Key = "F6",
            Ctrl = true,
        };
        settings.HotkeyBindings["manaward"] = new BlmKeyBinding
        {
            Key = "MouseX2",
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<BlackMageSettings>(json)
            ?? throw new InvalidOperationException("快捷键配置反序列化失败");
        restored.Normalize();

        AssertEx.Equal("F6", restored.QtBindings["自动醒梦"].Key, "QT 绑定键位未持久化");
        AssertEx.True(restored.QtBindings["自动醒梦"].Ctrl, "QT Ctrl 修饰键未持久化");
        AssertEx.Equal("MouseX2", restored.HotkeyBindings["manaward"].Key, "鼠标侧键绑定未持久化");
    }
}
