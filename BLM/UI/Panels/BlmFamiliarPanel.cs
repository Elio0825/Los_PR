using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using LosPr.BLM.Diagnostics;
using LosPr.BLM.UI.Components;
using LosPr.BLM.UI.Layout;
using LosPr.BLM.UI.Navigation;
using LosPr.BLM.UI.Theme;

namespace LosPr.BLM.UI.Panels;

internal static class BlmFamiliarPanel
{
    private static BlmConsoleTab? _lastNoteTab;
    private static int _noteIndex;

    public static void Draw(
        IDalamudTextureWrap? texture,
        BlmConsoleTab tab,
        BlackMageSettingsStore store,
        BlmUiSnapshot snapshot,
        BlmDebugSnapshot debug,
        float scale,
        bool reduceMotion)
    {
        var settings = store.Settings;
        LosCard.Draw(
            $"familiar_{tab}",
            () =>
            {
                DrawImage(texture, scale);
                LosComponents.StatusPill(
                    $"当前页 · {BlmConsoleTabInfo.Labels[(int)tab]}",
                    LosStatusTone.Info,
                    scale,
                    MathF.Min(
                        ImGui.GetContentRegionAvail().X,
                        LosMetrics.Scale(178f, scale)));
                ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(10f, scale)));
                DrawSummary(tab, settings, snapshot, debug, scale);
                BlmPanelPrimitives.DrawDivider(scale);
                ImGui.PushStyleColor(ImGuiCol.Text, LosPalette.Cyan);
                ImGui.TextUnformatted("黑猫留言");
                ImGui.PopStyleColor();
                BlmPanelPrimitives.DrawMuted(NoteFor(tab));

                if (tab == BlmConsoleTab.Debug)
                {
                    BlmPanelPrimitives.DrawDivider(scale);
                    BlmDebugPanel.DrawCompactControls(store, scale, reduceMotion);
                }
            },
            "黑猫值班台",
            "BLACK CAT ON DUTY",
            height: 610f,
            scale: scale);
    }

    private static void DrawImage(IDalamudTextureWrap? texture, float scale)
    {
        var available = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        var side = MathF.Min(available, LosMetrics.Scale(250f, scale));
        var offset = MathF.Max(0f, (available - side) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        var position = ImGui.GetCursorScreenPos();
        var size = new Vector2(side);
        var drawList = ImGui.GetWindowDrawList();
        if (texture is not null)
        {
            drawList.AddImageRounded(
                texture.Handle,
                position,
                position + size,
                Vector2.Zero,
                Vector2.One,
                uint.MaxValue,
                LosMetrics.Scale(18f, scale),
                ImDrawFlags.None);
        }
        else
        {
            drawList.AddRectFilled(
                position,
                position + size,
                LosPalette.ToUInt(LosPalette.Input),
                LosMetrics.Scale(18f, scale));
            var label = "猫使魔载入中";
            var textSize = ImGui.CalcTextSize(label);
            drawList.AddText(
                position + ((size - textSize) * 0.5f),
                LosPalette.ToUInt(LosPalette.TextMuted),
                label);
        }

        drawList.AddRect(
            position,
            position + size,
            LosPalette.ToUInt(LosPalette.WithAlpha(LosPalette.Cyan, 0.55f)),
            LosMetrics.Scale(18f, scale),
            ImDrawFlags.None,
            1f);
        ImGui.Dummy(size);
        ImGui.Dummy(new Vector2(0f, LosMetrics.Scale(10f, scale)));
    }

    private static void DrawSummary(
        BlmConsoleTab tab,
        BlackMageSettings settings,
        BlmUiSnapshot snapshot,
        BlmDebugSnapshot debug,
        float scale)
    {
        switch (tab)
        {
            case BlmConsoleTab.Overview:
                LosComponents.KeyValueRow("状态", snapshot.InCombat ? "运行中" : "待命", scale,
                    snapshot.InCombat ? LosPalette.Success : LosPalette.Cyan);
                LosComponents.KeyValueRow("预设", ModeLabel(settings.CombatMode), scale);
                LosComponents.KeyValueRow("阶段", snapshot.PhaseLabel, scale);
                LosComponents.KeyValueRow("目标", snapshot.CanAttackTarget ? "可攻击" : "等待目标", scale);
                break;
            case BlmConsoleTab.Battle:
                LosComponents.KeyValueRow("预设", ModeLabel(settings.CombatMode), scale);
                LosComponents.KeyValueRow("起手", OpenerLabel(settings.OpenerSelection), scale);
                LosComponents.KeyValueRow("爆发药", settings.OpenerPotionEnabled ? "开启" : "关闭", scale,
                    settings.OpenerPotionEnabled ? LosPalette.Success : LosPalette.TextSecondary);
                LosComponents.KeyValueRow("不三连", settings.OpenerNoTriplecast ? "开启" : "关闭", scale);
                break;
            case BlmConsoleTab.Style:
                LosComponents.KeyValueRow("主题", ThemeLabel(settings.UiThemeStyle), scale, LosPalette.Cyan);
                LosComponents.KeyValueRow("控制台", $"{settings.UiScale:F2} x", scale);
                LosComponents.KeyValueRow(
                    "浮窗",
                    $"QT {settings.QtPanelScale:F2} / HK {settings.HotkeyPanelScale:F2}",
                    scale);
                LosComponents.KeyValueRow("透明度", settings.WindowOpacity.ToString("F2"), scale);
                break;
            case BlmConsoleTab.Hotkeys:
                LosComponents.KeyValueRow(
                    "QT 显示",
                    $"{BlackMageRotation.QtList.Keys.Count(key => !settings.HiddenQtKeys.Contains(key))} / {BlackMageRotation.QtList.Count}",
                    scale);
                LosComponents.KeyValueRow(
                    "技能显示",
                    $"{BlmHotkeyCatalog.Entries.Count(entry => !settings.HiddenHotkeyKeys.Contains(entry.Key))} / {BlmHotkeyCatalog.Entries.Count}",
                    scale);
                LosComponents.KeyValueRow("QT 绑定", settings.QtBindings.Count.ToString(), scale);
                LosComponents.KeyValueRow("技能绑定", settings.HotkeyBindings.Count.ToString(), scale);
                break;
            case BlmConsoleTab.Debug:
                LosComponents.KeyValueRow("Writer", debug.WriterHealthy ? "正常" : "异常", scale,
                    debug.WriterHealthy ? LosPalette.Success : LosPalette.Danger);
                LosComponents.KeyValueRow("Pending", snapshot.HasPendingIssuedAction ? "等待 Ack" : "无", scale);
                LosComponents.KeyValueRow("Gauge", snapshot.PendingGaugeReconcile ? "等待" : "已同步", scale);
                LosComponents.KeyValueRow("Dropped", debug.DroppedCount.ToString(), scale,
                    debug.DroppedCount == 0 ? LosPalette.TextPrimary : LosPalette.Danger);
                break;
        }
    }

    private static string NoteFor(BlmConsoleTab tab)
    {
        var notes = tab switch
        {
            BlmConsoleTab.Overview => new[]
            {
                "冰火阶段、通晓和目标状态都在这里，开打前瞄一眼就好。",
                "没有可攻击目标时，黑猫不会替你乱丢法术。",
                "通晓快满了喵，别让异言一直压在书页里。",
            },
            BlmConsoleTab.Battle => new[]
            {
                "我要放黑魔纹了喵，站稳以后再交给我。",
                "需要连续走位时，留一层通晓会更从容。",
                "高难预设会收紧自动位移和黑魔纹，请按副本需要调整。",
            },
            BlmConsoleTab.Style => new[]
            {
                "QT 和技能栏可以分开缩放，摆在顺眼的位置就好。",
                "窗口太抢眼时，先调透明度，不必把文字一起缩小。",
                "黑猫喜欢紫晶色，但其他配色也不会生气喵。",
            },
            BlmConsoleTab.Hotkeys => new[]
            {
                "常用开关绑到顺手的位置，临场就不用找鼠标了。",
                "隐藏图标不会取消快捷键，想要清爽布局可以放心藏。",
                "爆发药和能力技会继续走各自的安全队列。",
            },
            _ => new[]
            {
                "排查问题时保留这一页的日志，战斗结束后再慢慢看。",
                "候选技能不代表实际释放，服务器回执才算数。",
                "如果 Writer 变红，先打开日志目录看看最后错误。",
            },
        };

        if (_lastNoteTab != tab)
        {
            _lastNoteTab = tab;
            _noteIndex = Random.Shared.Next(notes.Length);
        }

        return notes[Math.Clamp(_noteIndex, 0, notes.Length - 1)];
    }

    private static string ModeLabel(BlmConsoleMode mode)
        => mode == BlmConsoleMode.HighEnd ? "高难" : "日常";

    private static string OpenerLabel(BlmOpenerSelection selection)
        => selection switch
        {
            BlmOpenerSelection.Level70 => "Lv.70",
            BlmOpenerSelection.Level80 => "Lv.80",
            BlmOpenerSelection.Level90 => "Lv.90",
            BlmOpenerSelection.Standard57 => "标准 5+7",
            BlmOpenerSelection.Flare => "核爆",
            _ => "关闭",
        };

    private static string ThemeLabel(BlmUiThemeStyle style)
        => style switch
        {
            BlmUiThemeStyle.AmethystCat => "紫晶黑猫",
            BlmUiThemeStyle.AstralFamiliar => "星界使魔",
            BlmUiThemeStyle.EmberFamiliar => "余烬魔猫",
            _ => "月影黑猫",
        };
}
