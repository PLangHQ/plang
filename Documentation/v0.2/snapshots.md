# Snapshots

A snapshot is a typed capture of the App tree's current state — enough to suspend execution, ship the bytes through a wire (or a file), reconstruct a fresh App on the other side, and resume from where it stopped. It's the foundation `ErrorCallback` rides on; `AskCallback` does not use the full snapshot but uses the same `Snapshot.@this` shape for its slim wire format.

The design is OBP through-and-through: each subsystem captures into its own subtree and reconstructs from that subtree on Restore. References across subsystems are by name, never by pointer. Restore order is data-driven (the section names in the snapshot), not hard-coded.

## The classifier — `ISnapshotted`

`PLang/app/snapshot/ISnapshotted.cs`:

```csharp
public interface ISnapshotted
{
    void Capture(@this s);
    static abstract void Restore(@this s, actor.context.@this ctx);
}
```

The type system is the bucket assignment. Subsystems that implement `ISnapshotted` participate in snapshot/restore. Subsystems that don't are reconstructed on App build instead — Modules, Goals, Channels, Cache, the action registry. There is no third bucket and no per-call opt-in.

Implementers in v1: `app.@this`, `app.callstack.@this`, `app.callstack.call.@this`, `app.variables.@this`, `app.errors.@this`, `app.errors.trail.@this`, `app.modules.code.@this`, `app.statics.@this`, `app.modules.builder.@this`, `app.tester.@this`.

## The container — `Snapshot.@this`

`PLang/app/snapshot/this.cs` is the typed read/write surface. It's a tree of named sections; each section is itself a `@this` so a subsystem with nested `ISnapshotted` properties can give each child its own subtree without leaking storage to the children.

```csharp
public sealed class @this
{
    @this Section(string name);             // get-or-create a subsection
    bool   HasSection(string name);
    IReadOnlyCollection<string> SectionNames;
    void   Write<T>(string key, T value);
    T?     Read<T>(string key);
    bool   Has(string key);
}
```

A subsystem captures into the subtree it was given:

```csharp
public partial class @this : ISnapshotted   // app.variables.@this
{
    public void Capture(Snapshot.@this s)
    {
        foreach (var v in _variables.Values)
            s.Write($"var:{v.Name}", v.Value);
    }
}
```

And on Restore reads from the same subtree:

```csharp
public static void Restore(Snapshot.@this s, actor.context.@this ctx)
{
    foreach (var key in s.Keys.Where(k => k.StartsWith("var:")))
        ctx.App.Variables.Set(key.Substring(4), s.Read<object>(key));
}
```

Each subsystem owns the wire shape of its own subtree. Callers never inspect the entries.

## The orchestrator — `app.@this.Snapshot()` / `app.@this.Restore()`

`PLang/app/this.Snapshot.cs` is App's partial. It walks the type-implementing-`ISnapshotted` properties on App, calls `Capture` on each into a named subsection, and returns the full tree. Restore is the inverse: it walks `SectionNames` on the captured tree and dispatches each name to the right subsystem's `Restore`. **Section presence is the gate** — if a section isn't in the snapshot, the corresponding subsystem stays at its build-time default. This is what lets `ErrorCallback`'s narrow wire (CallStack + Variables only) round-trip cleanly: missing sections are simply absent.

## Referent integrity

Restore is **strict**. No silent fallback. Every name resolution that fails surfaces a typed error before the run can continue:

- Goal stub doesn't resolve in the live app.goals registry → hard error.
- Captured goal hash differs from the live goal's hash → `CallbackGoalHashMismatch`.
- Source file backing a captured goal is missing → hard error.

The discipline is "names not refs": entries store goal/action/identity *names*, not CLR object references. On Restore the live App resolves names against its current registry. If the live App has drifted from the capture point (renamed goal, altered hash, deleted source) the restore fails loudly rather than running against half-resolved state.

## Per-subsystem subtrees (v1)

Each `*.Snapshot.cs` partial in `PLang/app/` owns one subsystem's subtree. Quick reference:

| Subsystem | File | Subtree contents |
|---|---|---|
| App | `app/this.Snapshot.cs` | dispatch root — walks ISnapshotted properties |
| CallStack | `app/callstack/this.Snapshot.cs` | per-frame entries (each frame's Capture writes goal/step/action triple + Variables snapshot) |
| Call | `app/callstack/call/this.Snapshot.cs` | one frame: position triple + Errors slice + frame-local Variables |
| Variables | `app/variables/this.Snapshot.cs` | name → value, plus `Variables.SnapshotAt` for time-travel reads at a captured point |
| Errors | `app/errors/this.Snapshot.cs` | current error + Errors.Trail subtree |
| Errors.Trail | `app/errors/trail/this.Snapshot.cs` | append-only error history |
| Code | `app/modules/code/this.Snapshot.cs` | named code registrations (by name + type assembly-qualified) |
| Statics | `app/statics/this.Snapshot.cs` | per-goal static bag |
| Builder | `app/modules/builder/this.Snapshot.cs` | build-time provenance (hashes, source map) |
| Tester | `app/tester/this.Snapshot.cs` | test-runner mode bag |

`Variables.SnapshotAt` deserves a callout: it's a *time-travel* read, not a separate subsystem. Given a captured `Variables` subtree it answers "what was variable %x% at this point in the run?" — used by `ErrorCallback` to reconstruct local variables visible at error issue time.

## What's NOT in the snapshot

- **Channels** — reconstructed at App build. Wire surface is per-process.
- **Modules / action registry** — reconstructed at App build. Code identity, not run state.
- **Goals** — reconstructed by re-loading source. Goal identity is the source file + hash, captured by name in CallStack.
- **Cache** — intentionally not restored; cache hits across a callback boundary would silently mask drift.
- **FileSystem handle** — sandbox is per-App-instance; the path roots are reconstructed at build.

If a subsystem needs to round-trip across a callback, it implements `ISnapshotted`. The mechanism is uniform; there is no escape hatch.

## Wire shape choices

- `ErrorCallback` writes the full `Snapshot.@this` but **only the CallStack and Variables sections** today. Other sections are present in-process and dropped at the wire boundary. Adding a section to the wire shape means extending `ErrorCallback.Serialize` and `Deserialize` symmetrically — `app.Restore` already gates on section presence, so missing sections stay missing on the receiving side.
- `AskCallback` does not write a `Snapshot.@this`. Its wire is `{ ActorName, Position, Variables }` — slim by design. The resumed run boots a fresh App and only the developer-named state crosses the suspend.

The choice between full-snapshot and slim-shape is a per-callback design call, not a framework decision. A new callback type that needs (say) five surviving variables should not write the whole snapshot just to carry them.

## See also

- `Documentation/v0.2/callbacks.md` — the two ICallback impls that ride on this machinery.
- `PLang/app/snapshot/{ISnapshotted,this}.cs` — the classifier and the container.
- `PLang/app/this.Snapshot.cs` — App-level orchestrator.
- `PLang/app/variables/this.SnapshotAt.cs` — time-travel reads.
- `PLang/app/callstack/RestoredFrame.cs` — the position surrogate (not a Call.@this).
