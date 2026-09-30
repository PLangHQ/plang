namespace app.actor.context.trace;

/// <summary>
/// Per-context trace identity. Born with the Context, shared across every goal,
/// step, and LLM call that runs inside that Context. Used to correlate runtime
/// activity with diagnostic output written under <c>.build/traces/{Id}/...</c>.
///
/// Sub-goals do not get their own Trace — they share the parent Context's Trace.
/// Each new Context (e.g. a forked actor) gets its own.
///
/// Accessible from PLang as <c>%!trace.id%</c>; a plang value, written as its id and start.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    /// <summary>
    /// Sortable + unique identifier: <c>{ticks}_{guid8}</c>.
    /// Ticks make file listings sort by build order; the guid suffix prevents
    /// collisions when two contexts are constructed in the same tick.
    /// </summary>
    [global::app.Out, global::app.Debug]
    public global::app.type.item.text.@this Id { get; }

    /// <summary>
    /// When this Trace was created (= when its Context was constructed).
    /// </summary>
    [global::app.Out, global::app.Debug]
    public global::app.type.item.datetime.@this Started { get; }

    public @this()
    {
        var started = DateTimeOffset.UtcNow;
        Started = new(started);
        Id = $"{started.Ticks}_{Guid.NewGuid().ToString("N")[..8]}";
    }
}
