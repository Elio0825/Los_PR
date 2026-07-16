namespace LosPr.BLM.Core;

internal sealed class BlmActionEffectPacketFilter
{
    private const int Capacity = 256;

    private readonly HashSet<PacketKey> _seen = new();
    private readonly Queue<PacketKey> _order = new();

    public bool TryAccept(LogSystemActionEffectEvent actionEffect, uint globalSequence)
    {
        ArgumentNullException.ThrowIfNull(actionEffect);

        var key = globalSequence != 0
            ? new PacketKey(actionEffect.SourceId, globalSequence, 0, 0)
            : new PacketKey(
                actionEffect.SourceId,
                0,
                actionEffect.Timestamp.Ticks,
                actionEffect.ActionId);
        if (!_seen.Add(key))
        {
            return false;
        }

        _order.Enqueue(key);
        while (_order.Count > Capacity)
        {
            _seen.Remove(_order.Dequeue());
        }

        return true;
    }

    public void Clear()
    {
        _seen.Clear();
        _order.Clear();
    }

    private readonly record struct PacketKey(
        ulong SourceId,
        uint GlobalSequence,
        long TimestampTicks,
        uint ActionId);
}
