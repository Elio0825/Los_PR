using System;
using System.Numerics;
using LosPr.BLM.Data;

namespace LosPr.BLM.UI.Theme;

public static class LosMetrics
{
    public static readonly Vector2 DefaultWindowSize = new(
        BlackMageSettings.DefaultWindowWidth,
        BlackMageSettings.DefaultWindowHeight);
    public static readonly Vector2 MinWindowSize = new(
        BlackMageSettings.MinimumWindowWidth,
        BlackMageSettings.MinimumWindowHeight);

    public const float MinScale = BlackMageSettings.MinimumUiScale;
    public const float MaxScale = BlackMageSettings.MaximumUiScale;
    public const float WindowRounding = 8f;
    public const float CardRounding = 7f;
    public const float FrameRounding = 5f;
    public const float PopupRounding = 7f;
    public const float TabRounding = 5f;
    public const float ScrollbarRounding = 4f;
    public const float ScrollbarSize = 10f;

    public const float HeaderHeight = 72f;
    public const float TabBarHeight = 42f;
    public const float ContentPadding = 14f;
    public const float CardPadding = 14f;
    public const float CardSpacing = 10f;
    public const float DefaultCardHeight = 120f;
    public const float SectionHeaderHeight = 46f;
    public const float SectionSpacing = 14f;
    public const float RowHeight = 28f;

    public const float ButtonHeight = 34f;
    public const float CommandButtonWidth = 132f;
    public const float IconButtonSize = 34f;
    public const float SegmentHeight = 32f;
    public const float ToggleWidth = 40f;
    public const float ToggleHeight = 22f;
    public const float SliderWidth = 240f;
    public const float StatusPillWidth = 112f;
    public const float ProgressHeight = 18f;
    public const float TooltipMaxWidth = 320f;
    public const float CompactBreakpoint = 820f;

    public static float NormalizeScale(float scale)
        => Math.Clamp(float.IsFinite(scale) ? scale : 1f, MinScale, MaxScale);

    public static float Scale(float value, float scale)
        => value * NormalizeScale(scale);

    public static Vector2 Scale(Vector2 value, float scale)
        => value * NormalizeScale(scale);

    public static float ResponsiveScale(Vector2 availableSize, float requestedScale = 1f)
    {
        if (availableSize.X <= 0f || availableSize.Y <= 0f)
            return NormalizeScale(requestedScale);

        var fit = MathF.Min(
            availableSize.X / DefaultWindowSize.X,
            availableSize.Y / DefaultWindowSize.Y);
        fit = Math.Clamp(fit, MinScale, 1f);
        return NormalizeScale(requestedScale * fit);
    }

    public static bool IsCompact(float availableWidth, float scale = 1f)
        => availableWidth < Scale(CompactBreakpoint, scale);

    public static float MotionStep(bool reduceMotion, float speed = 12f)
        => reduceMotion ? 1f : Math.Clamp(speed, 1f, 40f);
}
