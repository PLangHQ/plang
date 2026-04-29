namespace App.Actor.Context.Trace;

/// <summary>
/// Per-context trace identity. Born with the Context, shared across every goal,
/// step, and LLM call that runs inside that Context. Used to correlate runtime
/// activity with diagnostic output written under <c>.build/traces/{Id}/...</c>.
///
/// Sub-goals do not get their own Trace — they share the parent Context's Trace.
/// Each new Context (e.g. a forked actor) gets its own.
///
/// Accessible from PLang as <c>%!trace.id%</c>.
/// </summary>
public sealed class @this
{
    /// <summary>
    /// Sortable + unique identifier: <c>{ticks}_{guid8}</c>.
    /// Ticks make file listings sort by build order; the guid suffix prevents
    /// collisions when two contexts are constructed in the same tick.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// When this Trace was created (= when its Context was constructed).
    /// </summary>
    public DateTimeOffset Started { get; }

    public @this()
    {
        Started = DateTimeOffset.UtcNow;
        Id = $"{Started.Ticks}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}
