namespace LosPr.BLM.Diagnostics;

public interface IBlmDebugSink
{
    void Publish(BlmDebugEventDraft draft);
}

public interface IBlmDebugViewSource
{
    BlmDebugSnapshot GetSnapshot();

    void ClearView();

    bool TryOpenLogDirectory();
}

public sealed class NullBlmDebugSink : IBlmDebugSink
{
    public static NullBlmDebugSink Instance { get; } = new();

    private NullBlmDebugSink()
    {
    }

    public void Publish(BlmDebugEventDraft draft)
    {
    }
}

public sealed class NullBlmDebugViewSource : IBlmDebugViewSource
{
    public static NullBlmDebugViewSource Instance { get; } = new();

    private NullBlmDebugViewSource()
    {
    }

    public BlmDebugSnapshot GetSnapshot() => BlmDebugSnapshot.Empty;

    public void ClearView()
    {
    }

    public bool TryOpenLogDirectory() => false;
}
