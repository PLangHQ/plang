# What I expect for stage 1 (written before the coder's shape, for comparison; not sent)

## ISnapshot — two members (replacing Section / Capture / Restore, `snapshot/ISnapshot.cs:13–29`)

```csharp
namespace app.snapshot;

/// <summary>A part of a run that takes part in a snapshot: it writes itself into one, and comes back from what it
/// wrote. The snapshot knows nothing of who takes part.</summary>
public interface ISnapshot
{
    /// <summary>Writes this part into <paramref name="snapshot"/>, the node at its own place in the saved tree.</summary>
    Task<data.@this> Store(snapshot.@this snapshot, actor.context.@this context);

    /// <summary>Comes back from what <paramref name="snapshot"/> holds at its place.</summary>
    Task<data.@this> Start(snapshot.@this snapshot, actor.context.@this context);
}
```

- Both answer Data (the error rule): a part that can't store or start answers why, never a throw.
- No `Section` member: a part's place is its path from `app`, given by the walk, never a name the part picks.

## One participant, Kept (`kept/list/this.cs:38–52` today)

```csharp
// Store: its members, as they are, into its own node
public async Task<data.@this> Store(snapshot.@this snapshot, actor.context.@this context)
    => snapshot.Set("kept", this, context);          // NEW on snapshot: a value at a key of its node (a plang value, not Write<T>'s any-CLR)

// Start: its members back, whole
public async Task<data.@this> Start(snapshot.@this snapshot, actor.context.@this context)
{
    var kept = await snapshot.Get<kept.list.@this>("kept", context);   // NEW: typed read of its node's key
    if (!kept.Success) return kept;
    Replace(await kept.Value());                     // its own, as Restore's clear-and-refill does today
    return context.Ok();
}
```

## The walk: `snapshot.save`

- Starts at `app` and goes down the live tree the way navigation goes (`%!app…%`): each node's children are the nodes it holds (`app.type`, `app.goal`, `app.module`, `app.actor`, the run's `call`, its `variable`, its `kept`).
- At each node that is an `ISnapshot`, it makes the node's place in the snapshot (its path) and asks `Store`. A node that isn't one is passed through to its children.
- No list on the app (`app/this.Snapshot.cs:7–9` goes), and no type switch: "is it an ISnapshot" is the interface's own question.
- The open part: how a node names its children for the walk without reflection. I expect the coder to find an existing door (the node's own members as navigation already reads them, `node/this.cs` `Own` / `Named`). If none can serve, it's a shape to bring.

## Starting

- `resume %snapshot%` walks the saved tree from its root, and at each place asks the live node at the same path to `Start` from it. A saved place with no live node is a hard error naming the path.
