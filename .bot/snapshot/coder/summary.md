# snapshot — coder summary

**Version:** v1 (stage 3, stage 4's variables half, stage 1)

## What this is

A run's exact state saved (to disk, a db) and started later. Stage 1 replaces the app's hand-made list of snapshot
owners and their named sections with a walk: `ISnapshot` is `Store`/`Start`, and the snapshot stores every part the app
holds at its member path (`Code`, `actor.list.User.Context.Variable`, `actor.list.User.Context.call`, …).

## What was done

- **Stage 3** (4611e3ef4): one door from text to a snapshot (`snapshot.Create`, the snapshot's own reader); a file save
  writes its own form; the wire helpers are gone.
- **Stage 4, half** (0aa445ada): a variable holding a `sensitive` value is never saved. The `[Sensitive]` member half
  waits on `View.Snapshot` (Ingi).
- **Stage 1** (this commit):
  - `PLang/app/snapshot/ISnapshot.cs`: `data Store(snapshot, context)` and `Task<data> Start(snapshot, context)`, both
    defaulting to nothing (a holder). Store is sync: every part's state is in memory, and two callers store from sync code
    (`error.Callback`'s getter, a message channel's `Ask`).
  - `PLang/app/snapshot/this.Store.cs`: the walk — public members whose declared type is an `ISnapshot`, depth first, in
    declaration order; a part reached twice stays where it was first stored; an empty node is left out.
  - Holders (Ingi, snapshot 4): `type.node`, `actor.list`, `actor`, `actor.context`.
  - `PLang/app/this.Snapshot.cs`: the app is the root part (its mode, its kept); `Snapshot(context)` and the error's
    throw-time `Snapshot(error, context)` answer `data<snapshot>`; a save inside a task is refused (`SnapshotInTask`);
    `app.Restore` is gone (`snapshot.Start(app, context)`, which `Resume` calls).
  - Parts: the variables (explicit members), Code's providers (the provider kind's plang name, not a C# assembly-qualified
    name; the DLL load is awaited; failures answered), the call stack (explicit members forwarding to its public
    `Capture`/`Restore`, which stay until 1b). `kept.list` is no part: the app writes its kept and starts it through
    `kept.Replace`.
  - Gone: `snapshot.Section`/`HasSection`/`SectionNames`/`Sections`, `ProviderRestoreException`, the parts' `Section`.
- **Tests:** `PLang.Tests/Shared/SnapshotPathExtensions.cs` (a node by its path), `SnapshotSaveExtensions.cs` (`Saved()`,
  `app.Restore(snapshot, context)` through `snapshot.Start`); the snapshot tests moved onto paths and answered errors; new:
  the member paths, the task refusal, the kind's plang name, an unknown kind.

## Next / blocked

- 1b (call.list's Store/Start), 1c (the frames' ICreate), 1d, View.Snapshot, loops, tasks, the verb: with Ingi.
- Stage 2 after 1b–1c.

## Code example

```csharp
// the walk: a part, then each part it holds at a node named for the member that holds it
var own = (standIns.GetValueOrDefault(part) ?? part).Store(this, context);
foreach (var (name, child) in Parts(part))
{
    if (stored.Contains(child)) continue;
    var node = new @this(Context);
    var result = node.Store(child, standIns, stored, context);
    if (!result.Success) return result;
    if (node.Entries.IsTruthy()) Entries.Set(new global::app.data.@this(name, node, context: Context));
}
```
