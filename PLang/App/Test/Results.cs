using System.Collections;
using System.Collections.Concurrent;

namespace App.Test;

/// <summary>
/// Run-wide collection of TestRun entries. Per-test Apps add their TestRun here
/// concurrently during parallel execution — thread-safety is required.
/// </summary>
public sealed class Results : IEnumerable<TestRun>
{
    private readonly ConcurrentQueue<TestRun> _items = new();

    /// <summary>Number of TestRun entries recorded.</summary>
    public int Count => _items.Count;

    /// <summary>Appends a TestRun. Safe to call from multiple threads.</summary>
    public void Add(TestRun run) => _items.Enqueue(run);

    public IEnumerator<TestRun> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Per-status counts across all recorded TestRuns. Every status key is present, even with count 0.</summary>
    public Dictionary<TestStatus, int> Summary()
    {
        var summary = new Dictionary<TestStatus, int>();
        foreach (TestStatus status in Enum.GetValues<TestStatus>())
            summary[status] = 0;
        foreach (var run in _items)
            summary[run.Status]++;
        return summary;
    }
}
