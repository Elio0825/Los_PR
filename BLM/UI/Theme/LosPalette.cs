using System;
using System.Numerics;

namespace LosPr.BLM.UI.Theme;

/// <summary>
/// Neutral charcoal palette for the standalone BLM console.
/// Purple and cyan are reserved for focus and information accents.
/// </summary>
public static class LosPalette
{
    public static readonly Vector4 Background = new(0.045f, 0.049f, 0.055f, 0.98f);
    public static readonly Vector4 Content = new(0.059f, 0.064f, 0.071f, 1f);
    public static readonly Vector4 Surface = new(0.075f, 0.081f, 0.090f, 1f);
    public static readonly Vector4 Card = new(0.088f, 0.095f, 0.105f, 1f);
    public static readonly Vector4 CardHover = new(0.108f, 0.116f, 0.128f, 1f);
    public static readonly Vector4 Elevated = new(0.118f, 0.126f, 0.139f, 1f);

    public static readonly Vector4 TextPrimary = new(0.930f, 0.936f, 0.946f, 1f);
    public static readonly Vector4 TextSecondary = new(0.690f, 0.710f, 0.735f, 1f);
    public static readonly Vector4 TextMuted = new(0.465f, 0.490f, 0.520f, 1f);
    public static readonly Vector4 TextDisabled = new(0.330f, 0.350f, 0.375f, 1f);

    public static readonly Vector4 Primary = new(0.615f, 0.455f, 0.890f, 1f);
    public static readonly Vector4 PrimaryHover = new(0.690f, 0.555f, 0.940f, 1f);
    public static readonly Vector4 PrimaryActive = new(0.520f, 0.365f, 0.800f, 1f);
    public static readonly Vector4 PrimaryMuted = new(0.615f, 0.455f, 0.890f, 0.16f);

    public static readonly Vector4 Cyan = new(0.255f, 0.735f, 0.785f, 1f);
    public static readonly Vector4 CyanHover = new(0.345f, 0.820f, 0.860f, 1f);
    public static readonly Vector4 CyanMuted = new(0.255f, 0.735f, 0.785f, 0.14f);
    public static readonly Vector4 Accent = Cyan;
    public static readonly Vector4 Info = Cyan;

    public static readonly Vector4 Success = new(0.315f, 0.745f, 0.490f, 1f);
    public static readonly Vector4 SuccessMuted = new(0.315f, 0.745f, 0.490f, 0.14f);
    public static readonly Vector4 Warning = new(0.930f, 0.710f, 0.270f, 1f);
    public static readonly Vector4 WarningMuted = new(0.930f, 0.710f, 0.270f, 0.14f);
    public static readonly Vector4 Danger = new(0.875f, 0.330f, 0.365f, 1f);
    public static readonly Vector4 DangerHover = new(0.935f, 0.405f, 0.435f, 1f);
    public static readonly Vector4 DangerActive = new(0.775f, 0.255f, 0.295f, 1f);
    public static readonly Vector4 DangerMuted = new(0.875f, 0.330f, 0.365f, 0.14f);

    public static readonly Vector4 Border = new(0.255f, 0.275f, 0.305f, 0.62f);
    public static readonly Vector4 BorderStrong = new(0.355f, 0.380f, 0.415f, 0.78f);
    public static readonly Vector4 Separator = new(0.220f, 0.240f, 0.270f, 0.72f);
    public static readonly Vector4 Input = new(0.055f, 0.060f, 0.068f, 1f);
    public static readonly Vector4 InputHover = new(0.085f, 0.092f, 0.102f, 1f);
    public static readonly Vector4 InputActive = new(0.105f, 0.112f, 0.125f, 1f);
    public static readonly Vector4 Track = new(0.045f, 0.049f, 0.055f, 0.92f);
    public static readonly Vector4 Button = new(0.130f, 0.140f, 0.155f, 1f);
    public static readonly Vector4 ButtonHover = new(0.175f, 0.188f, 0.208f, 1f);
    public static readonly Vector4 ButtonActive = new(0.105f, 0.114f, 0.128f, 1f);

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
