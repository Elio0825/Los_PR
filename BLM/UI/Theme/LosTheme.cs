using System.Numerics;
using Dalamud.Bindings.ImGui;
using LosPr.BLM.Data;

namespace LosPr.BLM.UI.Theme;

internal static class LosTheme
{
    private const int WindowColorCount = 35;
    private const int WindowVarCount = 11;
    private const int ContentColorCount = 2;
    private const int ContentVarCount = 3;
    private const int CardColorCount = 2;
    private const int CardVarCount = 3;
    private static readonly Stack<float> BackgroundOpacityStack = new();
    private static float _backgroundOpacity = 1f;

    public static void PushWindowStyle(
        float scale = 1f,
        bool reduceMotion = false,
        float backgroundOpacity = 1f,
        BlmUiThemeStyle themeStyle = BlmUiThemeStyle.MoonlitCat)
    {
        scale = LosMetrics.NormalizeScale(scale);
        LosPalette.ApplyStyle(themeStyle);
        BackgroundOpacityStack.Push(_backgroundOpacity);
        _backgroundOpacity = Math.Clamp(
            float.IsFinite(backgroundOpacity) ? backgroundOpacity : 1f,
            0f,
            1f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, ApplyBackgroundOpacity(LosPalette.Background));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ApplyBackgroundOpacity(LosPalette.Content));
        ImGui.PushStyleColor(ImGuiCol.PopupBg, ApplyBackgroundOpacity(LosPalette.Elevated));
        ImGui.PushStyleColor(ImGuiCol.Border, LosPalette.Border);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, LosPalette.Input);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, LosPalette.InputHover);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, LosPalette.InputActive);
        ImGui.PushStyleColor(ImGuiCol.TitleBg, LosPalette.Surface);
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, LosPalette.Surface);
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, LosPalette.Background);
        ImGui.PushStyleColor(ImGuiCol.MenuBarBg, LosPalette.Surface);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, LosPalette.Background);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, LosPalette.BorderStrong);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, LosPalette.TextMuted);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, LosPalette.Cyan);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, LosPalette.Primary);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, LosPalette.Cyan);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, LosPalette.CyanHover);
        ImGui.PushStyleColor(ImGuiCol.Button, LosPalette.Button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, LosPalette.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, LosPalette.ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Header, LosPalette.PrimaryMuted);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, LosPalette.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, LosPalette.PrimaryMuted);
        ImGui.PushStyleColor(ImGuiCol.Separator, LosPalette.Separator);
        ImGui.PushStyleColor(ImGuiCol.SeparatorHovered, LosPalette.BorderStrong);
        ImGui.PushStyleColor(ImGuiCol.SeparatorActive, LosPalette.Cyan);
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, LosPalette.Cyan);
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, LosPalette.Primary);
        ImGui.PushStyleColor(ImGuiCol.Tab, LosPalette.Surface);
        ImGui.PushStyleColor(ImGuiCol.TabHovered, LosPalette.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.Text, LosPalette.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, LosPalette.TextDisabled);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, LosMetrics.Scale(LosMetrics.WindowRounding, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, LosMetrics.Scale(LosMetrics.FrameRounding, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, LosMetrics.Scale(LosMetrics.CardRounding, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, LosMetrics.Scale(LosMetrics.PopupRounding, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, LosMetrics.Scale(LosMetrics.ScrollbarRounding, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, LosMetrics.Scale(LosMetrics.ScrollbarSize, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, LosMetrics.Scale(new Vector2(8f, 7f), scale));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, LosMetrics.Scale(new Vector2(8f, 4f), scale));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, LosMetrics.Scale(new Vector2(13f, 6f), scale));
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, LosMetrics.Scale(LosMetrics.TabRounding, scale));

        _ = reduceMotion;
    }

    public static void PopWindowStyle()
    {
        ImGui.PopStyleVar(WindowVarCount);
        ImGui.PopStyleColor(WindowColorCount);
        _backgroundOpacity = BackgroundOpacityStack.Count > 0
            ? BackgroundOpacityStack.Pop()
            : 1f;
    }

    public static void PushContentStyle(float scale = 1f)
    {
        scale = LosMetrics.NormalizeScale(scale);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ApplyBackgroundOpacity(LosPalette.Content));
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowPadding,
            new Vector2(LosMetrics.Scale(LosMetrics.ContentPadding, scale)));
    }

    public static void PopContentStyle()
    {
        ImGui.PopStyleVar(ContentVarCount);
        ImGui.PopStyleColor(ContentColorCount);
    }

    public static void PushCardStyle(float scale = 1f, bool border = true)
    {
        scale = LosMetrics.NormalizeScale(scale);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ApplyBackgroundOpacity(LosPalette.Card));
        ImGui.PushStyleColor(ImGuiCol.Border, border ? LosPalette.Border : Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, LosMetrics.Scale(LosMetrics.CardRounding, scale));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, border ? 1f : 0f);
        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowPadding,
            new Vector2(LosMetrics.Scale(LosMetrics.CardPadding, scale)));
    }

    public static void PopCardStyle()
    {
        ImGui.PopStyleVar(CardVarCount);
        ImGui.PopStyleColor(CardColorCount);
    }

    public static Vector4 ApplyBackgroundOpacity(Vector4 color)
        => LosPalette.WithAlpha(color, color.W * _backgroundOpacity);
}
