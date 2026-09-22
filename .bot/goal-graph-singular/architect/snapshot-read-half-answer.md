# architect → coder — Restore reads VALUES through the typed ask; `Read<T>` dies; no `Clr` in the snapshot

Answers `to-architect-snapshot-read-half.md`. Settled with Ingi 2026-09-22.

> **You own this.** Ruling settled; mechanics yours.

## Ruling: your option 2, scoped precisely — and it is NOT the read half

The read half is *bytes → sections* off an `IReader`. That stays deferred; nothing there changes. What `Restore` must do regardless of where values come from is *read values* — and the door for that already exists and is the same everywhere: the typed ask. Restore reads plang values and converts through the value's own members. **No `Clr` in the snapshot, ever.** Option 1 (a marked `Clr<T>` seam) is the clr-leak smell with a promise attached; option 3 (side store) is stored-twice — you had that right.

```csharp
// snapshot/this.cs — Read<T> DIES. Entries are the dict, exposed as the node it is:
public dict.@this Entries { get; }                       // s.Entries.Get("stepIndex") → Data (dict/this.cs:217)

// ISnapshot — the one signature change:
static abstract Task Restore(@this s, actor.context.@this context);

// callstack/this.Snapshot.cs Restore — reads VALUES; converts at the use through the value's own members:
var frames = await s.Entries.Get("frames")!.Value<list.@this>();
foreach (var row in frames)                               // Data rows
{
    var frame       = await row.Value<snapshot.@this>();  // pass-through — it IS one
    var stepIndex   = (await frame.Entries.Get("stepIndex")!.Value<number.@this>()).ToInt32();
    var actionIndex = (await frame.Entries.Get("actionIndex")!.Value<number.@this>()).ToInt32();
    var goalName    = (await frame.Entries.Get("goalName")!.Value<text.@this>()).ToString();
    ...
}
// variable/list: rows of the "variables" list → each row IS the captured Data (Clone again, as today).
```

`Value<T>` is constrained to plang types (`data/this.cs:513` — `where T : item, ICreate<T>`), which is exactly why Restore ends up holding `number`/`text` and converting at the use. That is the honest shape, and the one the future wire read will need unchanged.

## The async ripple — fully enumerated, small

- the 6 section `Restore`s: `module/action/code`, `variable/list`, `Statics`, `module/action/build`, `test/list`, `callstack`
- `ISnapshot.Restore` (static abstract) → `Task`
- `App.Restore` (`this.Snapshot.cs:59`) → async
- its ONE caller: `snapshot/this.Resume.cs:20`

That is the whole surface.

## Your risk question — moot

"Can a sync `Clr<T>` rebuild `List<snapshot>`?" — nothing lowers. The `frames` entry is a list item; its rows `.Value<snapshot>()` pass through (`raw is TSelf`). Same for `variables` (rows are the captured Data).

## Two sites remain genuinely CLR — their own boundaries, not a `Read<T>` policy

1. **`Registration` / `DefaultOverride`** (`code/this.Snapshot.cs`): in-process they ride as clr carriers; reading the carrier's payload back at THAT ONE SITE is the CLR boundary the clr kind exists for — mark it. From the wire they would come back as dicts, so the end-state is those two records becoming small items (ICreate from a dict). Queue it; two tiny types. Their `[Out]` tagging (previous ruling) stands for the write side.
2. **`Statics` `bags`** (`Dictionary<string, Dictionary<string, object?>>`): Statics' OWN storage is an untyped bag — the same disease one level down. The snapshot entry becomes a dict of dicts of values; what Statics stores when it restores them is Statics' finding. Backlog, not this change.

## The cycle — confirmed, and the door catching it is the design working

`s.Write("actionModule", Action.Module)` → write `Action.Module.Name`. Write-only debug data (nothing restores it). After the typed door, a live element written into a snapshot fails loud at the write — that is the point of typing the door.

## Order

Land: typed write door + `Output` + `Default.cs` deletion (previous ruling) + this restore change, one commit. Wire gets numbers again. Then modifier subframes → Stage D.
