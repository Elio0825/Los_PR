using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Layout;

internal static class LosPageLayout
{
    public static void Draw(
        string id,
        Action mainContent,
        Action familiarRail,
        float scale)
    {
        ArgumentNullException.ThrowIfNull(mainContent);
        ArgumentNullException.ThrowIfNull(familiarRail);
        scale = LosMetrics.NormalizeScale(scale);
        var available = ImGui.GetContentRegionAvail().X;
        if (available < LosMetrics.Scale(LosMetrics.FamiliarRailBreakpoint, scale))
        {
            mainContent();
            ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(8f, scale)));
            familiarRail();
            return;
        }

        var flags = ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.NoSavedSettings
            | ImGuiTableFlags.PadOuterX;
        var rightExtension = LosMetrics.Scale(LosMetrics.FamiliarRailRightExtension, scale);
        ImGui.PushStyleVar(
            ImGuiStyleVar.CellPadding,
            new Vector2(LosMetrics.Scale(9f, scale), 0f));
        try
        {
            if (!ImGui.BeginTable(
                    $"##{id}_page_layout",
                    2,
                    flags,
                    new Vector2(available + rightExtension, 0f)))
                return;

            try
            {
                ImGui.TableSetupColumn("main", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn(
                    "familiar",
                    ImGuiTableColumnFlags.WidthFixed,
                    LosMetrics.Scale(
                        LosMetrics.FamiliarRailWidth + LosMetrics.FamiliarRailRightExtension,
                        scale));
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                mainContent();
                ImGui.TableNextColumn();
                familiarRail();
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
}
