using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Theme;
using PromeRotation.Data;

namespace LosPr.BLM.UI.Panels;

internal static class BlmPanelPrimitives
{
    private static readonly Dictionary<string, string> QtDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AOE"] = "允许循环根据当前目标组进入群体路线。",
            ["智能AOE"] = "开启后 2 个目标即可进入双目标路线；关闭时从 3 个目标开始。",
            ["Dot"] = "允许自动维持当前路线对应的雷系持续伤害。",
            ["TTK"] = "目标即将死亡时，允许消耗不适合继续保留的资源。",
            ["移动通晓"] = "移动且缺少更合适的瞬发手段时，允许使用通晓技能。",
            ["移动三连"] = "移动且下一主体读条不安全时，允许主动释放三连咏唱。",
            ["压缩火悖论"] = "满足资源与时序条件时，允许采用火悖论压缩路线。",
            ["即刻进冰"] = "允许即刻咏唱保障星灵移位后的冰封。",
            ["三连进冰"] = "允许三连咏唱保障星灵移位后的冰封。",
            ["黑魔纹"] = "允许在安全织入窗口自动释放黑魔纹。",
            ["详述"] = "允许在通晓不会溢出时自动释放详述。",
            ["魔泉"] = "火段资源耗尽且条件满足时，允许使用魔泉继续火段。",
            ["倾泻资源"] = "允许主动消耗通晓层数，降低资源溢出风险。",
            ["快速耀星"] = "满足火段资源条件时，允许更早释放耀星。",
            ["不打冰悖论"] = "跳过冰阶段悖论，直接推进冰段资源与回火流程。",
        };

    public static string DescriptionForQt(string key)
        => QtDescriptions.TryGetValue(key, out var description)
            ? description
            : "控制当前循环策略中的对应功能。";

    public static bool DrawQtToggle(
        string id,
        string key,
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        var value = SafeGetQt(key);
        return DrawToggleRow(
            id,
            key,
            DescriptionForQt(key),
            value,
            newValue => SafeSetQt(key, newValue, store),
            scale,
            reduceMotion);
    }

    public static bool DrawToggleRow(
        string id,
        string title,
        string description,
        bool value,
        Action<bool> onChanged,
        float scale,
        bool reduceMotion)
    {
        var changed = false;
        var flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings;
        if (!ImGui.BeginTable($"##{id}_row", 2, flags))
            return false;

        try
        {
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("control", ImGuiTableColumnFlags.WidthFixed, LosMetrics.Scale(48f, scale));
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(title);
            LosComponents.TooltipIfHovered(description, scale);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + LosMetrics.Scale(2f, scale));
            var mutable = value;
            if (LosComponents.Toggle($"##{id}_toggle", ref mutable, scale, reduceMotion, description))
            {
                changed = true;
                onChanged(mutable);
            }
        }
        finally
        {
            ImGui.EndTable();
        }

        return changed;
    }

    public static void DrawMuted(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, LosPalette.TextMuted);
        ImGui.PushTextWrapPos(0f);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
    }

    public static void DrawDivider(float scale)
    {
        ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(4f, scale)));
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(4f, scale)));
    }

    public static bool SafeGetQt(string key)
    {
        try
        {
            return PromeSettings.Instance.GetQt(key);
        }
        catch
        {
            return false;
        }
    }

    public static void SafeSetQt(string key, bool value, BlackMageSettingsStore store)
    {
        try
        {
            PromeSettings.Instance.SetQt(key, value);
            store.Update(settings => settings.QtStates[key] = value);
        }
        catch
        {
            // UI 必须保持可绘制；状态源下帧恢复后会重新读取。
        }
    }
}
