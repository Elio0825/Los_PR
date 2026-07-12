using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

public static class BlmCombatPanel
{
    public static void Draw(BlackMageSettingsStore store, float scale, bool reduceMotion)
    {
        LosSection.Draw(
            "combat_routing",
            "目标与路线",
            () => DrawAdaptivePair(
                "combat_routing_pair",
                () => LosCard.Draw(
                    "combat_target_mode",
                    () => DrawGroup(["AOE", "智能AOE"], "combat_target", store, scale, reduceMotion),
                    "目标数量策略",
                    "只决定循环路线，不是日常/高难预设",
                    height: 270f,
                    scale: scale),
                () => LosCard.Draw(
                    "combat_general_policy",
                    () => DrawGroup(["Dot", "TTK", "移动通晓", "移动三连"], "combat_general", store, scale, reduceMotion),
                    "通用策略",
                    "雷系、收尾与移动资源",
                    height: 270f,
                    scale: scale),
                scale),
            "单体、双目标与 3+ 目标的选择只由目标策略控制。",
            scale);

        LosSection.Draw(
            "combat_advanced",
            "进阶路线",
            () => DrawAdaptivePair(
                "combat_advanced_pair",
                () => LosCard.Draw(
                    "combat_resource_policy",
                    () => DrawGroup(
                        ["压缩火悖论", "不打冰悖论", "倾泻资源", "快速耀星"],
                        "combat_resource",
                        store,
                        scale,
                        reduceMotion),
                    "循环资源策略",
                    "悖论位置、通晓消耗与耀星节奏",
                    height: 370f,
                    scale: scale),
                () => LosCard.Draw(
                    "combat_ability_policy",
                    () => DrawGroup(
                        ["即刻进冰", "三连进冰", "黑魔纹", "详述", "魔泉"],
                        "combat_ability",
                        store,
                        scale,
                        reduceMotion),
                    "能力技策略",
                    "转冰保障、能力窗口与魔泉续火",
                    height: 445f,
                    scale: scale),
                scale),
            scale: scale);
    }

    private static void DrawGroup(
        IReadOnlyList<string> keys,
        string idPrefix,
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                BlmPanelPrimitives.DrawDivider(scale);

            var key = keys[i];
            BlmPanelPrimitives.DrawQtToggle($"{idPrefix}_{i}", key, store, scale, reduceMotion);
        }
    }

    private static void DrawAdaptivePair(
        string id,
        Action left,
        Action right,
        float scale)
    {
        if (ImGui.GetContentRegionAvail().X < 720f * scale)
        {
            left();
            right();
            return;
        }

        if (!ImGui.BeginTable($"##{id}", 2,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            return;

        try
        {
            ImGui.TableNextColumn();
            left();
            ImGui.TableNextColumn();
            right();
        }
        finally
        {
            ImGui.EndTable();
        }
    }
}
