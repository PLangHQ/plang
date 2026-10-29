using System.Diagnostics.Tracing;

namespace Bench.ticks;

/// <summary>The runtime's allocation ticks (GCAllocationTick, one per ~100 KB allocated), summed by type: sampled by
/// bytes, so a type's share is its share of what was allocated.</summary>
internal sealed class @this : EventListener
{
    private readonly Dictionary<string, long> _bytes = new();

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "Microsoft-Windows-DotNETRuntime")
            EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1);
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (e.EventName?.StartsWith("GCAllocationTick") != true || e.Payload == null) return;
        var names = e.PayloadNames!;
        var type = e.Payload[names.IndexOf("TypeName")] as string ?? "?";
        var amount = Convert.ToInt64(e.Payload[names.IndexOf("AllocationAmount64")]);
        lock (_bytes) _bytes[type] = _bytes.GetValueOrDefault(type) + amount;
    }

    /// <summary>Each type's KB per <paramref name="runs"/>, the largest first (`kb type`), after the ticks in flight arrive.</summary>
    internal void Report(int runs)
    {
        Thread.Sleep(2000);
        lock (_bytes)
            foreach (var (type, bytes) in _bytes.OrderByDescending(p => p.Value).Take(60))
                Console.WriteLine(FormattableString.Invariant($"{bytes / 1024.0 / runs:F2} {type}"));
    }
}
