using System;
using System.Numerics;
using LosPr.BLM.Data;

namespace LosPr.BLM.UI.Theme;

/// <summary>
/// Ink-dark palettes for the Black Mage familiar console.
/// </summary>
internal static class LosPalette
{
    private static BlmUiThemeStyle _style = BlmUiThemeStyle.MoonlitCat;

    public static Vector4 Background => _style switch
    {
        BlmUiThemeStyle.AmethystCat => new(0.055f, 0.050f, 0.130f, 0.985f),
        BlmUiThemeStyle.AstralFamiliar => new(0.025f, 0.040f, 0.060f, 0.985f),
        BlmUiThemeStyle.EmberFamiliar => new(0.055f, 0.029f, 0.030f, 0.985f),
        _ => new(0.025f, 0.030f, 0.047f, 0.985f),
    };

    public static Vector4 Content => _style switch
    {
        BlmUiThemeStyle.AmethystCat => new(0.075f, 0.068f, 0.170f, 1f),
        BlmUiThemeStyle.AstralFamiliar => new(0.035f, 0.055f, 0.078f, 1f),
        BlmUiThemeStyle.EmberFamiliar => new(0.074f, 0.039f, 0.039f, 1f),
        _ => new(0.038f, 0.044f, 0.067f, 1f),
    };

    public static Vector4 Surface => _style switch
    {
        BlmUiThemeStyle.AmethystCat => new(0.090f, 0.082f, 0.195f, 1f),
        BlmUiThemeStyle.AstralFamiliar => new(0.050f, 0.072f, 0.098f, 1f),
        BlmUiThemeStyle.EmberFamiliar => new(0.094f, 0.052f, 0.049f, 1f),
        _ => new(0.055f, 0.061f, 0.088f, 1f),
    };

    public static Vector4 Card => _style switch
    {
        BlmUiThemeStyle.AmethystCat => new(0.125f, 0.115f, 0.255f, 1f),
        BlmUiThemeStyle.AstralFamiliar => new(0.060f, 0.083f, 0.108f, 1f),
        BlmUiThemeStyle.EmberFamiliar => new(0.105f, 0.061f, 0.056f, 1f),
        _ => new(0.067f, 0.073f, 0.102f, 1f),
    };

    public static Vector4 CardHover => Lerp(Card, TextPrimary, 0.07f);
    public static Vector4 Elevated => Lerp(Card, TextPrimary, 0.11f);

    public static readonly Vector4 TextPrimary = new(0.970f, 0.950f, 1.000f, 1f);
    public static readonly Vector4 TextSecondary = new(0.715f, 0.690f, 0.800f, 1f);
    public static readonly Vector4 TextMuted = new(0.515f, 0.490f, 0.625f, 1f);
    public static readonly Vector4 TextDisabled = new(0.370f, 0.350f, 0.455f, 1f);

    public static Vector4 Primary => _style switch
    {
        BlmUiThemeStyle.AmethystCat => new(0.655f, 0.390f, 1.000f, 1f),
        BlmUiThemeStyle.AstralFamiliar => new(0.315f, 0.710f, 0.920f, 1f),
        BlmUiThemeStyle.EmberFamiliar => new(0.940f, 0.405f, 0.220f, 1f),
        _ => new(0.925f, 0.690f, 0.285f, 1f),
    };

    public static Vector4 PrimaryHover => Lerp(Primary, Vector4.One, 0.16f);
    public static Vector4 PrimaryActive => Lerp(Primary, Vector4.Zero, 0.16f);
    public static Vector4 PrimaryMuted => WithAlpha(Primary, 0.16f);

    public static Vector4 Cyan => _style switch
    {
        BlmUiThemeStyle.AmethystCat => new(0.875f, 0.715f, 1.000f, 1f),
        BlmUiThemeStyle.AstralFamiliar => new(0.610f, 0.470f, 0.930f, 1f),
        BlmUiThemeStyle.EmberFamiliar => new(0.945f, 0.695f, 0.315f, 1f),
        _ => new(0.410f, 0.690f, 0.920f, 1f),
    };

    public static Vector4 CyanHover => Lerp(Cyan, Vector4.One, 0.16f);
    public static Vector4 CyanMuted => WithAlpha(Cyan, 0.14f);
    public static Vector4 Accent => Cyan;
    public static Vector4 Info => Cyan;
    public static Vector4 Arcane => new(0.620f, 0.430f, 0.900f, 1f);
    public static Vector4 MoonGold => new(0.950f, 0.760f, 0.350f, 1f);

    public static readonly Vector4 Success = new(0.315f, 0.745f, 0.490f, 1f);
    public static readonly Vector4 SuccessMuted = new(0.315f, 0.745f, 0.490f, 0.14f);
    public static readonly Vector4 Warning = new(0.930f, 0.710f, 0.270f, 1f);
    public static readonly Vector4 WarningMuted = new(0.930f, 0.710f, 0.270f, 0.14f);
    public static readonly Vector4 Danger = new(0.875f, 0.330f, 0.365f, 1f);
    public static readonly Vector4 DangerHover = new(0.935f, 0.405f, 0.435f, 1f);
    public static readonly Vector4 DangerActive = new(0.775f, 0.255f, 0.295f, 1f);
    public static readonly Vector4 DangerMuted = new(0.875f, 0.330f, 0.365f, 0.14f);

    public static Vector4 Border => WithAlpha(Lerp(Card, Primary, 0.26f), 0.70f);
    public static Vector4 BorderStrong => WithAlpha(Lerp(Card, Primary, 0.42f), 0.88f);
    public static Vector4 Separator => WithAlpha(Lerp(Card, TextMuted, 0.28f), 0.74f);
    public static Vector4 Input => Lerp(Background, Card, 0.28f);
    public static Vector4 InputHover => Lerp(Input, Primary, 0.10f);
    public static Vector4 InputActive => Lerp(Input, Primary, 0.18f);
    public static Vector4 Track => WithAlpha(Background, 0.94f);
    public static Vector4 Button => Lerp(Card, TextPrimary, 0.08f);
    public static Vector4 ButtonHover => Lerp(Card, Primary, 0.18f);
    public static Vector4 ButtonActive => Lerp(Card, Primary, 0.10f);

    public static BlmUiThemeStyle Style => _style;

    public static void ApplyStyle(BlmUiThemeStyle style)
        => _style = Enum.IsDefined(style) ? style : BlmUiThemeStyle.AmethystCat;

    public static uint ToUInt(Vector4 color)
    {
        var r = (uint)(Math.Clamp(color.X, 0f, 1f) * 255f + 0.5f);
        var g = (uint)(Math.Clamp(color.Y, 0f, 1f) * 255f + 0.5f);
        var b = (uint)(Math.Clamp(color.Z, 0f, 1f) * 255f + 0.5f);
        var a = (uint)(Math.Clamp(color.W, 0f, 1f) * 255f + 0.5f);
        return r | (g << 8) | (b << 16) | (a << 24);
    }

    public static Vector4 WithAlpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, Math.Clamp(alpha, 0f, 1f));

    public static Vector4 Lerp(Vector4 from, Vector4 to, float amount)
        => Vector4.Lerp(from, to, Math.Clamp(amount, 0f, 1f));
}
