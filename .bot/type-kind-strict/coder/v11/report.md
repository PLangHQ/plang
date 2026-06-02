# Coder v11 — snapshot↔disk wire serializer (durable-execution replay)

Answers the builder handoff (`.bot/type-kind-strict/builder/v2/coder-handoff.md`): the
deterministic-replay loop was blocked on **piece 1 — a snapshot→disk serializer** (`Data.Snapshot`
is `[JsonIgnore]`; the snapshot tree stores entries as `object?`, so it couldn't round-trip).
Built that plus **piece 2** (load + resume-from-file entry). Full fidelity — all 7 sections.

## Design: "sections self-serialize" (Ingi's call)
Each `ISnapshot` subsystem owns its wire shape — it alone knows the concrete CLR type of each
entry it captured. Added to `ISnapshot`:

```
static abstract void Write(@this section, Io io);   // section → bytes
static abstract void Read(Io io, @this section);     // bytes → section
```

Both static (parallel to `Restore`'s factory) — they touch only the section, no live state.
`App.SnapshotToWire`/`SnapshotFromWire` dispatch per-section by the **same hardcoded list/order
as `App.Restore`**. The element-owns-itself rule held the line on CallStack: `call.@this` owns
its frame's scalar keys (`WriteFrame`/`ReadFrame`); CallStack owns only the list structure.

## Reuse, not reinvent
The value slot rides the existing `app.data.Wire` STJ converter — `data.@this` round-trips for
free. The snapshot options reuse the canonical **Store** recipe (camelCase + Wire + path +
transport modifier) — one source of truth — varying one axis:

- **Non-signing.** Added a `sign` flag to `Wire` (default `true`, all existing behavior intact).
  The snapshot wire is `Wire(View.Store, sign: false)`: a snapshot is internal in-process state
  replayed into the same actor, so signing it is wrong (mutates captured Data) **and** fails
  headless (no writable identity) — which is exactly where the builder harness runs. This was the
  first test failure and the key correctness find.

## IError full fidelity (the "full fidelity now" add)
`app.error.ErrorWire` — one polymorphic `JsonConverter<IError>`, lives **only** in the snapshot
options (never changes any other wire). ~10 subclasses carry no own state (differ only by default
Key/StatusCode), so one flattened shape covers the trail: `$type` + common content + recursive
`ErrorChain`, with the two stateful subclasses' extras (`AskError.Table/DataKey`,
`PermissionDenied.Permission`). Dropped the live object graph that can't round-trip (Exception,
Step, Goal, CallFrames) — the CallStack section already carries the chain. Added a private
restore ctor + `Error.Restore(...)` factory so **Id and CreatedUtc survive** for the base case.

## Files
New: `app/snapshot/Io.cs`, `app/this.SnapshotWire.cs`, `app/error/IError.Wire.cs`,
`PLang.Tests/App/SnapshotTests/SnapshotWireTests.cs`.
Modified: `ISnapshot.cs`, `data/Wire.cs`, `channel/serializer/plang/this.cs`, `error/Error.cs`,
and the 7 subsystem `this.Snapshot.cs` (variables, errors+trail, providers, statics, build,
testing, callstack + callstack/call).

## Entry points for the builder
- Capture verb (piece 0, builder's): `App.Snapshot()` → `App.SnapshotToWire(snap)` → write string.
- Resume verb (piece 2): read `.snapshot` via path verbs → `App.ResumeFromWire(json, context)`
  (= `SnapshotFromWire` + `Restore` + `snapshot.Resume`). File reading stays in PLang so
  System.IO never enters the engine.

## Verification
- PLang lib, PLang.Tests, PlangConsole all build clean (0 errors; PLNG002 / Console gates pass).
- 5 new disk round-trip tests pass (variables w/ value+type, build/testing bits, errors trail
  w/ content + Id preservation, callstack frame int-typing, empty-app valid JSON).
- All pre-existing snapshot/restore suites still pass.
- One unrelated pre-existing failure on this branch: `CompileLlm_Kernel_ContainsTypeHintRule`
  (compile-prompt prose — outside this diff; confirmed not caused here).

## Design conversation — what landed vs. deferred (Ingi, 2026-06-02)

The serializer is OBP-relocated onto the object: `snapshot.@this.Serialize` /
`Deserialize` / `FromWire` (`app/snapshot/this.Wire.cs`) — the snapshot owns its
own wire shape; `App.SnapshotToWire`/`FromWire` are thin wrappers threading the
actor context.

Two follow-ups were discussed and **deferred to `Documentation/v0.2/todos.md`**:
- **Format-agnostic rework.** `Serialize` currently ends in `root.ToJsonString` and
  the `Io` layer is JSON-coupled (wraps `JsonObject` + STJ). Ingi: a value should
  write to `IWriter` and not know the format. Left as-is for now; todo captures the
  full `Io`→`IWriter` rework + the dependent PLang surface (renderer for
  channel-write, `Data<Snapshot>` lazy-convert seam, `resume` verb, `INavigator`
  for `%snap.variable.x%`, `.snapshot` extension, the `.test.goal`). A draft of the
  `TryConvert` FromWire hook was written and **reverted** to keep this diff focused.
- **`Data.Snapshot` removal.** Redundant for the error-callback path (Value already
  is the snapshot), still load-bearing for ask-suspend. Todo'd for the ask-module pass.

The deterministic core is proven, including an end-to-end C# test
(`EndToEnd_SuspendedState_SurvivesDisk_AndResumesToSuccess`): suspend → serialize
to string → read back → Resume → success.

## Piece 3 note
`App.Snapshot()` captures current live Variables; since `error/Error.cs` snapshots at throw-time
synchronously, the in-scope LLM value (`%plan%`/`%compileResult%`, ordinary non-`!` user vars) is
captured and round-trips. If strict throw-time *projection* is ever wanted in capture itself,
swap `Variable.Capture` → `SnapshotAt` in `App.Snapshot()` — orthogonal to the wire.
