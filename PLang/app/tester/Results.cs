using System.Collections;
using System.Collections.Concurrent;
using app.Attributes;

namespace app.tester;

/// <summary>
/// Run-wide collection of Run entries. Per-test Apps add their Run here
/// concurrently during parallel execution — thread-safety is required.
/// </summary>
[PlangType("results")]
public sealed class Results : IEnumerable<Run>
{
    private readonly ConcurrentQueue<Run> _items = new();

    /// <summary>Number of Run entries recorded.</summary>
    [LlmBuilder] public int Count => _items.Count;

    /// <summary>Appends a Run. Safe to call from multiple threads.</summary>
    public void Add(Run run) => _items.Enqueue(run);

    public IEnumerator<Run> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Per-status counts across all recorded TestRuns. Every status key is present, even with count 0.</summary>
    public Dictionary<Status, int> Summary()
    {
        var summary = new Dictionary<Status, int>();
        foreach (Status status in Enum.GetValues<Status>())
            summary[status] = 0;
        foreach (var run in _items)
            summary[run.Status]++;
        return summary;
    }
}
