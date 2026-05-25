using System.Collections;

namespace app.tester;

/// <summary>
/// Per-step wall-clock record for a Run, in source order. Scoped to a
/// single Run (one thread of execution within its child App), so no
/// concurrency surface is needed — unlike <see cref="Results"/> or
/// <see cref="Coverage"/>, which span parallel child Apps.
/// Records only top-level steps of the entry goal; nested sub-goal steps
/// roll up into the calling step's duration naturally (AfterStep on the
/// caller doesn't fire until the call returns).
/// </summary>
public sealed class Timings : IEnumerable<Timing>
{
    private readonly List<Timing> _items = new();

    /// <summary>Number of step timings recorded.</summary>
    public int Count => _items.Count;

    /// <summary>Appends a timing entry. Caller is the test-runner subscriber.</summary>
    public void Add(int stepIndex, double ms) => _items.Add(new Timing(stepIndex, ms));

    public IEnumerator<Timing> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
