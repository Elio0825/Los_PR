using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.Data;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

internal static class BlmHotkeyPanel
{
    public static void Draw(BlackMageSettingsStore store, float scale)
    {
        if (ImGui.GetContentRegionAvail().X < LosMetrics.Scale(700f, scale))
        {
            DrawQtSettings(store, scale);
            DrawHotkeySettings(store, scale);
            return;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.CellPadding,
            new Vector2(LosMetrics.Scale(8f, scale), 0f));
        try
        {
            if (!ImGui.BeginTable(
                    "##binding_columns",
                    2,
                    ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings))
            {
                return;
            }

            try
            {
                ImGui.TableNextColumn();
                DrawQtSettings(store, scale);
                ImGui.TableNextColumn();
                DrawHotkeySettings(store, scale);
            }
            finally
            {
                ImGui.EndTable();
            }
        }
        finally
        {
            ImGui.PopStyleVar();
        }
    }

    private static void DrawQtSettings(BlackMageSettingsStore store, float scale)
    {
        LosCard.Draw(
            "hotkeys_qt_card",
            () => DrawRows(
                store,
                BlmBindingKind.Qt,
                BlackMageRotation.QtList.Keys.Select(key => (key, key)),
                scale),
            "QT 按键",
            "支持单键、组合键与鼠标侧键",
            height: 630f,
            scale: scale);
    }

    private static void DrawHotkeySettings(BlackMageSettingsStore store, float scale)
    {
        LosCard.Draw(
            "hotkeys_action_card",
            () => DrawRows(
                store,
                BlmBindingKind.Hotkey,
                BlmHotkeyCatalog.Entries.Select(entry => (entry.Key, entry.Name)),
                scale),
            "技能 Hotkey",
            "隐藏图标不影响快捷键；支持鼠标侧键",
            height: 590f,
            scale: scale);
    }

    private static void DrawRows(
        BlackMageSettingsStore store,
        BlmBindingKind kind,
        IEnumerable<(string Key, string Label)> entries,
        float scale)
    {
        if (!ImGui.BeginTable(
                $"##{kind}_binding_table",
                3,
                ImGuiTableFlags.SizingStretchProp
                | ImGuiTableFlags.RowBg
                | ImGuiTableFlags.NoSavedSettings))
        {
            return;
        }

        try
        {
            ImGui.TableSetupColumn("显示", ImGuiTableColumnFlags.WidthFixed, 48f * scale);
            ImGui.TableSetupColumn("名称", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("快捷键", ImGuiTableColumnFlags.WidthFixed, 132f * scale);
            ImGui.TableHeadersRow();

            foreach (var (key, label) in entries)
            {
                ImGui.PushID($"{kind}_{key}");
                try
                {
                    ImGui.TableNextRow(ImGuiTableRowFlags.None, 36f * scale);
                    ImGui.TableNextColumn();
                    var visible = !HiddenSet(store.Settings, kind).Contains(key);
                    if (ImGui.Checkbox("##visible", ref visible))
                    {
                        store.Update(settings =>
                        {
                            var hidden = HiddenSet(settings, kind);
                            if (visible)
                                hidden.Remove(key);
                            else
                                hidden.Add(key);
                        });
                    }

                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.TextUnformatted(label);

                    ImGui.TableNextColumn();
                    BlmKeyBindingManager.DrawBindingButton(store, kind, key, scale);
                }
                finally
                {
                    ImGui.PopID();
                }
            }
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private static HashSet<string> HiddenSet(BlackMageSettings settings, BlmBindingKind kind)
        => kind == BlmBindingKind.Qt
            ? settings.HiddenQtKeys
            : settings.HiddenHotkeyKeys;
}
