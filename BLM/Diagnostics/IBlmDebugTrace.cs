namespace LosPr.BLM.Diagnostics;

internal interface IBlmDebugSink
{
    void Publish(BlmDebugEventDraft draft);
}

internal interface IBlmDebugViewSource
{
    BlmDebugSnapshot GetSnapshot();

    void ClearView();

    bool TryOpenLogDirectory();
}

internal sealed class NullBlmDebugSink : IBlmDebugSink
{
    public static NullBlmDebugSink Instance { get; } = new();

    private NullBlmDebugSink()
    {
    }

    public void Publish(BlmDebugEventDraft draft)
    {
    }
}

internal sealed class NullBlmDebugViewSource : IBlmDebugViewSource
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
