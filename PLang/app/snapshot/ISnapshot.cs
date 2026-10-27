namespace app.snapshot;

/// <summary>
/// A part of a run that takes part in a snapshot: it writes itself into the node at its place, and comes back from what
/// it wrote. The type system is the classifier — a public member whose declared type is an <see cref="ISnapshot"/> is
/// a part (<see cref="@this.Store"/> walks them from the app); any other member is never
/// read, and rebuilds from the app's own files (modules, goals, channels, cache, …).
///
/// <para>A part's place is its member path from the app (<c>actor.list.User.Context.Variable</c>), never a name it picks.
/// A part that only holds parts (a concept's node, the actors, an actor, its context) writes nothing of its own: both
/// members default to nothing. References across parts are by name — a start never depends on pointer fixup.</para>
/// </summary>
public interface ISnapshot
{
    /// <summary>Writes this part's own state into <paramref name="snapshot"/>, the node at its place — its entries, never
    /// its parts (the walk stores those at their own places). A state it can't write is answered, never thrown.</summary>
    data.@this Store(@this snapshot, actor.context.@this context) => context.Ok();

    /// <summary>Comes back from what <paramref name="snapshot"/> holds at its place. What can't come back (a goal gone,
    /// a source that won't load) is answered, with no fallback.</summary>
    System.Threading.Tasks.Task<data.@this> Start(@this snapshot, actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult(context.Ok());
}
