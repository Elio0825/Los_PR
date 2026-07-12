namespace LosPr.BLM.Core;

public interface IBlmClock
{
    long NowMs { get; }
}

public sealed class SystemBlmClock : IBlmClock
{
    public static SystemBlmClock Instance { get; } = new();

    private SystemBlmClock()
    {
    }

    public long NowMs => Environment.TickCount64;
}
