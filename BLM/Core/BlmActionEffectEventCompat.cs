namespace LosPr.BLM.Core;

internal static class BlmActionEffectEventCompat
{
    private static readonly Func<LogSystemActionEffectEvent, uint> GlobalSequenceGetter =
        CreateGlobalSequenceGetter();

    public static uint GetGlobalSequence(LogSystemActionEffectEvent actionEffect)
    {
        ArgumentNullException.ThrowIfNull(actionEffect);
        return GlobalSequenceGetter(actionEffect);
    }

    private static Func<LogSystemActionEffectEvent, uint> CreateGlobalSequenceGetter()
    {
        var getter = typeof(LogSystemActionEffectEvent)
            .GetProperty("GlobalSequence")?
            .GetMethod;
        if (getter is null || getter.ReturnType != typeof(uint))
        {
            return static _ => 0;
        }

        return (Func<LogSystemActionEffectEvent, uint>)getter.CreateDelegate(
            typeof(Func<LogSystemActionEffectEvent, uint>));
    }
}
