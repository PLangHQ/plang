# Stage 1: Snapshot Foundation

**Goal:** Land `ISnapshotted` and `Snapshot.@this` so every `@this` can declare its own snapshot discipline; provide `App.Snapshot()` / `App.Restore()` as entry points; convert the simple subsystems (Variables, Errors, Providers, Statics, Build, Testing) to use it. Nothing callback-shaped, nothing time-travel, nothing signing.
**Scope:** *Included* — the interface, the payload type, App's two entry points, and per-`@this` Capture/Restore on the six subsystems listed above. *Excluded* — `App.CallStack` (Stage 2), `Variables.SnapshotAt(error)` (Stage 2), `Call` snapshot (Stage 2), Data/signing/serializer work (Stage 3), callback records (Stage 4).
**Deliverables:**
- `ISnapshotted` interface in `PLang/App/Snapshot/` (or wherever the App folder structure puts cross-cutting interfaces)
- `Snapshot.@this` payload type — the typed write/read surface
- `App.Snapshot()` method on `App.@this` (recursive walk of `ISnapshotted` properties)
- `App.Restore(Snapshot.@this s, Context.@this ctx)` dual
- `App.Variables` Capture/Restore (uses existing `Snapshot()` partition rules — skip `!`-prefix, DynamicData, SettingsVariable)
- `App.Errors.Trail` Capture/Restore
- `App.Providers` Capture/Restore (default selections per type + runtime registrations as `(type, name, source)` tuples; hard error on unresolvable name during Restore)
- `App._statics` Capture/Restore (provisional — see todos.md for the goal-backed dynamic property follow-up)
- `App.Build` Capture/Restore (`IsEnabled` only)
- `App.Testing` Capture/Restore (`IsEnabled` only)
- C# tests for each subsystem's round-trip + Providers' hard-error-on-unresolvable-name
**Dependencies:** None.

## Design

`ISnapshotted` is the marker; the type system is the classifier. A subsystem author chooses its bucket by deciding whether to implement the interface — no central registry, no opt-in table. See `plan/snapshotted-system.md` for the three buckets and the inventory; the six subsystems above are the only ones with snapshot-and-restore in this stage.

```csharp
public interface ISnapshotted
{
    void Capture(Snapshot.@this s);
    static abstract void Restore(Snapshot.@this s, Context.@this ctx);
}
```

`Snapshot.@this` owns the typed read/write surface. Capture appends entries; Restore retrieves them. Don't invent `Reader`/`Writer`/`RestoreContext` types — `Context.@this` is the existing actor context; reuse it.

`App.Snapshot()` walks its own `@this` properties (Variables, Errors, Providers, Statics, Build, Testing in this stage), asks each `ISnapshotted` for its capture, and returns a tree mirroring the App's structure. The wire shape *is* the App tree — that's the OBP win the design hangs on. `App.Restore(snapshot, ctx)` is the dual: walk subtrees, dispatch each to the matching `@this.Restore`.

**Key invariants the implementation must honor:**

- *Each `@this` captures values, not graph identity.* References across types are by name (the way PLang already works at runtime), resolved at lookup time. After Restore, no pointer-fixup phase, no inter-type ordering dependency.
- *Providers Restore is two-step:* runtime registrations replay first (load DLLs/sources), then default selections apply. If any registration's source can't be loaded *or* any default selection name doesn't resolve, the resume fails with a referent-integrity error. No silent fallback to system defaults.
- *Variables Capture honors the existing partition* — the rules in the current `App.Variables.Snapshot()` already encode what's safe to carry vs. what's reconstructed from elsewhere (sqlite settings, dynamic data). Don't redesign; preserve.
- *`App.Build` and `App.Testing` are `@this` with an `IsEnabled` property each.* If they don't exist as `@this` today, this stage is the place to make them so. Mode flags are not flat fields on App — they're properties of their own `@this`.

**OBP smells to avoid:**

- Don't write a centralized `SnapshotWalker` class that knows about each subsystem's internals — the walk is reflection or source-generated over `ISnapshotted` properties; each subsystem's serialization stays inside its own `@this`.
- Don't add a `Snapshot` parameter to subsystem constructors so they "know how to restore themselves." Restore is the static factory entry point on `ISnapshotted`; subsystem instances don't need to know the snapshot type at construction.
- Don't expose Capture's appended entries as a public collection on `Snapshot.@this`. The payload type owns its read/write API; consumers use the typed surface, not the underlying storage.

**Why this stage carves where it does:** Variables, Errors, Providers, Statics, Build, Testing are the subsystems that don't depend on temporal data (no diff stream, no frame chain). They round-trip with simple Capture/Restore. `CallStack` is *also* `ISnapshotted` but its Capture walks a frame chain that depends on `Call` having its own snapshot/restore — that's Stage 2's keystone. Splitting them keeps Stage 1 reviewable: ~6 small Capture/Restore implementations against a clean interface, and the Stage 2 reviewer can focus solely on the temporal/positional concerns.

**Test shape:** for each of the six subsystems, write a round-trip test — populate the live `@this`, Capture, construct fresh, Restore, assert deep equality. For Providers, add the hard-error tests (missing source DLL, unresolvable default-selection name). These are not the test-designer's tests — those come later. These are tight C# unit tests that pin the contract.
