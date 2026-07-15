namespace LosPr.BLM.Core;

internal interface IBlmClock
{
    long NowMs { get; }
}

internal sealed class SystemBlmClock : IBlmClock
{
    public static SystemBlmClock Instance { get; } = new();

    private SystemBlmClock()
    {
    }

    public long NowMs => Environment.TickCount64;
}
