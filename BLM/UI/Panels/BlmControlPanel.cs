using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

public static class BlmControlPanel
{
    private static string? _feedback;
    private static DateTime _feedbackUntilUtc;

    public static void Draw(BlackMageSettingsStore store, float scale, bool reduceMotion)
    {
        LosSection.Draw(
            "control_presets",
            "快速预设",
            () => LosCard.Draw(
                "control_preset_card",
                () => DrawPresets(store, scale),
                "策略偏好",
                "预设不会切换单体/AOE，也不会开启实验路线",
                height: 270f,
                scale: scale),
            scale: scale);

        LosSection.Draw(
            "control_all_qt",
            "全部 QT",
            () => LosCard.Draw(
                "control_all_qt_card",
                () => DrawAllQts(store, scale, reduceMotion),
                "即时开关",
                "只调整决策条件，不会从控制台直接释放技能",
                height: ImGui.GetContentRegionAvail().X >= 720f * scale ? 390f : 690f,
                scale: scale),
            scale: scale);
    }

    private static void DrawPresets(BlackMageSettingsStore store, float scale)
    {
        var gap = 8f * scale;
        var available = ImGui.GetContentRegionAvail().X;
        var stack = available < 620f * scale;
        var buttonWidth = stack ? available : Math.Max(150f * scale, (available - gap * 2f) / 3f);
        var buttonSize = new Vector2(buttonWidth, 36f * scale);

        if (LosComponents.PrimaryButton(
                "preset_daily",
                "应用日常预设",
                size: buttonSize,
                scale: scale,
                tooltip: "偏向自动收尾和移动容错；保留当前目标数量策略。"))
        {
            ApplyPreset(
                store,
                "已应用日常预设",
                new Dictionary<string, bool>
                {
                    ["Dot"] = true,
                    ["TTK"] = true,
                    ["移动通晓"] = true,
                    ["移动三连"] = true,
                    ["压缩火悖论"] = true,
                    ["即刻进冰"] = true,
                    ["三连进冰"] = true,
                    ["黑魔纹"] = true,
                    ["详述"] = true,
                });
        }

        if (!stack)
            ImGui.SameLine(0f, gap);
        if (LosComponents.SecondaryButton(
                "preset_high_end",
                "应用高难预设",
                size: buttonSize,
                scale: scale,
                tooltip: "关闭自动收尾，保留最终方案的悖论压缩默认值和当前目标数量策略。"))
        {
            ApplyPreset(
                store,
                "已应用高难预设",
                new Dictionary<string, bool>
                {
                    ["Dot"] = true,
                    ["TTK"] = false,
                    ["移动通晓"] = true,
                    ["移动三连"] = true,
                    ["压缩火悖论"] = true,
                    ["即刻进冰"] = true,
                    ["三连进冰"] = true,
                    ["黑魔纹"] = true,
                    ["详述"] = true,
                });
        }

        if (!stack)
            ImGui.SameLine(0f, gap);
        if (LosComponents.SecondaryButton(
                "preset_defaults",
                "恢复作者默认",
                size: buttonSize,
                scale: scale,
                tooltip: "恢复所有 QT 默认值，包括关闭实验路线。"))
        {
            ApplyPreset(store, "已恢复作者默认", BlackMageRotation.QtList);
        }

        ImGui.Dummy(new Vector2(0f, 8f * scale));
        BlmPanelPrimitives.DrawMuted(
            "日常/高难是策略偏好预设，不代表单体或群体模式。AOE 与智能AOE保持当前值，可在下方单独调整。");

        if (_feedback is not null && DateTime.UtcNow <= _feedbackUntilUtc)
        {
            ImGui.Dummy(new Vector2(0f, 8f * scale));
            LosComponents.StatusPill(_feedback, LosStatusTone.Success, scale);
        }
    }

    private static void DrawAllQts(BlackMageSettingsStore store, float scale, bool reduceMotion)
    {
        var entries = BlackMageRotation.QtList.ToArray();
        var twoColumns = ImGui.GetContentRegionAvail().X >= 720f * scale;
        if (!twoColumns)
        {
            DrawQtRange(entries, 0, entries.Length, "control_qt_single", store, scale, reduceMotion);
            return;
        }

        var split = (entries.Length + 1) / 2;
        if (!ImGui.BeginTable("##control_qt_columns", 2,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            return;

        try
        {
            ImGui.TableNextColumn();
            DrawQtRange(entries, 0, split, "control_qt_left", store, scale, reduceMotion);
            ImGui.TableNextColumn();
            DrawQtRange(entries, split, entries.Length, "control_qt_right", store, scale, reduceMotion);
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private static void DrawQtRange(
        KeyValuePair<string, bool>[] entries,
        int start,
        int end,
        string idPrefix,
        BlackMageSettingsStore store,
        float scale,
        bool reduceMotion)
    {
        for (var i = start; i < end; i++)
        {
            if (i > start)
                BlmPanelPrimitives.DrawDivider(scale);

            BlmPanelPrimitives.DrawQtToggle(
                $"{idPrefix}_{i}",
                entries[i].Key,
                store,
                scale,
                reduceMotion);
        }
    }

    private static void ApplyPreset(
        BlackMageSettingsStore store,
        string feedback,
        IReadOnlyDictionary<string, bool> values)
    {
        foreach (var (key, value) in values)
            BlmPanelPrimitives.SafeSetQt(key, value, store);

        _feedback = feedback;
        _feedbackUntilUtc = DateTime.UtcNow.AddSeconds(2.5);
    }
}
