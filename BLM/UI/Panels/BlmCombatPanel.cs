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
            () => LosCard.Draw(
                "combat_paradox",
                () => DrawGroup(
                    ["压缩火悖论", "即刻进冰", "三连进冰", "黑魔纹", "详述"],
                    "combat_paradox_group",
                    store,
                    scale,
                    reduceMotion),
                "单体能力策略",
                "悖论位置、转冰瞬发与常规能力技",
                height: 445f,
                scale: scale),
            scale: scale);

        LosSection.Draw(
            "combat_experimental",
            "危险区",
            () => LosCard.Draw(
                "combat_experimental_card",
                () =>
                {
                    LosComponents.StatusPill("实验功能", LosStatusTone.Danger, scale);
                    ImGui.SameLine();
                    LosComponents.StatusPill("默认关闭", LosStatusTone.Warning, scale);
                    ImGui.Dummy(new Vector2(0f, 8f * scale));
                    BlmPanelPrimitives.DrawMuted(
                        "实验路线可能改变标准资源规划。它与稳定路线隔离，未主动开启时不会参与决策。\n");
                    BlmPanelPrimitives.DrawDivider(scale);
                    BlmPanelPrimitives.DrawQtToggle(
                        "combat_experimental_b4",
                        "实验_B4星灵绝望",
                        store,
                        scale,
                        reduceMotion);
                },
                "B4 星灵绝望",
                "不随任何预设自动开启",
                height: 235f,
                scale: scale),
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
