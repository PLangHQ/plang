# Stage 23: `callstack-restoredframe-rename`

**Read first:**
- `plan/principles.md` — OBP discipline.
- `results.md` deviation #5 — original brief from stage 10 deferred this rename when stage 10 focused on the App.Run reduction headliner.

**Goal:** Rename `App/CallStack/RestoredFrame.cs` → `App/CallStack/Call/Position.cs`. Type `App.CallStack.RestoredFrame` → `App.CallStack.Call.Position`. The record body stays exactly as-is — pure file move + type rename + caller sweep. No behaviour change.

**Why this rename:** the type is a snapshot surrogate of one Call frame, used to identify the *position* a callback resumes at. The property on `ICallback`, `AskCallback`, and `ErrorCallback` is already named `Position` — only the type was named `RestoredFrame`. The rename brings the type name in line with the property and puts the snapshot record next to its live counterpart (`Call/this.cs`). Nothing about the data or behaviour changes; only the name and its folder location.

## Scope

**Included:**
- File move: `PLang/App/CallStack/RestoredFrame.cs` → `PLang/App/CallStack/Call/Position.cs`.
- Namespace change: `App.CallStack` → `App.CallStack.Call`.
- Type rename: `RestoredFrame` → `Position` (record).
- Caller sweep across PLang/ and PLang.Tests/. Today: 18 hits in 11 files (production + tests).

**Excluded:**
- Anything inside the record body — the 5 fields (`Action, Goal, StepIndex, ActionIndex, Id`) stay as-is.
- Any `Call/this.cs` change.
- The CallStack scope question (per-context vs shared) — that's a Bucket C item, deferred.

## Deliverables

### File move + namespace + type rename

```
App/CallStack/RestoredFrame.cs     →  App/CallStack/Call/Position.cs

namespace App.CallStack;            →  namespace App.CallStack.Call;

public sealed record RestoredFrame(...)  →  public sealed record Position(...)
```

The record body (5 fields, no methods) stays exactly the same. The XML doc comment on the record stays — only the `<see cref="Call.@this"/>` line currently reads naturally; once the record sits alongside `Call/this.cs`, that reference resolves locally.

### Caller propagation (18 sites in 11 files)

Production:

- `PLang/App/CallStack/this.Snapshot.cs` — 6 hits (`_restoredChain` field type, `RestoredChain` property, `BottomFrame` property type, two `new RestoredFrame(...)` calls, `FrameFromLive` return type). All in this file because Snapshot owns the materialization path.
- `PLang/App/Callback/ICallback.cs:17` — `RestoredFrame? Position { get; }` interface property — the most-loaded site since every ICallback implementer pins to it.
- `PLang/App/Callback/AskCallback.cs` — 4 hits: `Position` field, `PositionWire.From(RestoredFrame f)` factory, `Resolve(...)` return type, `new RestoredFrame(...)` inside Resolve.
- `PLang/App/Callback/ErrorCallback.cs` — 2 hits: `_position` field, `Position` property.

Tests:

- `PLang.Tests/App/CallbackTests/AskCallbackTests.cs` — 6 hits (test fixtures constructing `new RestoredFrame(...)`).
- `PLang.Tests/App/CallbackTests/ICallbackPositionTests.cs:22` — 1 hit.
- `PLang.Tests/App/CallbackTests/FailureMatrixTests.cs:190` — 1 hit (test stub).
- `PLang.Tests/App/CallbackTests/CallbackRunActionTests.cs:16` — 1 hit (test stub).
- `PLang.Tests/App/DataTests/DataContextWiringTests.cs:9` — 1 hit (test stub).
- `PLang.Tests/App/DataTests/DataLazySignatureTests.cs:9` — 1 hit (test stub).
- `PLang.Tests/App/Serializers/PlangDataSerializerRoundTripTests.cs:11` — 1 hit (test stub).

The test stubs all declare `RestoredFrame? Position => null;` to satisfy ICallback — straight find-and-replace.

After the sweep:

```bash
grep -rn "\bRestoredFrame\b" PLang/ PLang.Tests/ Tests/ --include='*.cs'
```

returns zero hits.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `find PLang/App/CallStack/RestoredFrame.cs` — not present.
- `find PLang/App/CallStack/Call/Position.cs` — present.
- `grep -rn "RestoredFrame" PLang/ PLang.Tests/ Tests/ --include='*.cs'` — zero hits.

**Dependencies:** None. Independent of every other Tier 5 stage.

## Design

### The smell this closes

Two minor smells, both naming:

1. **Type and property names disagree.** The property on `ICallback` is `Position`; the type was `RestoredFrame`. Reading `callback.Position` returns a `RestoredFrame?` — a small cognitive bump every time. Aligning the type to the property removes it.

2. **Snapshot and live counterparts live at different folder depths.** `Call/this.cs` is the live execution scope (lives at `CallStack/Call/`); `RestoredFrame.cs` is its snapshot surrogate (lives at `CallStack/`, one level shallower). Moving the snapshot next to its live counterpart makes the relationship visible from the folder tree. After the move, `Call/this.cs` and `Call/Position.cs` are siblings — same shape as `Diff.cs` sitting next to `this.cs` for the per-mutation snapshot.

Neither smell is severe; this is hygiene cleanup carried over from stage 10.

### Why not collapse Position into Call.@this as a property

`Call/Position.cs` is a separate record because it's a *snapshot* — pulled by serializers, captured into callbacks, restored from wire. Mutating `Call.@this`'s shape to embed Position would entangle the live frame with its snapshot surrogate. Keep them as siblings; the snapshot stays a small immutable record.

## Risk + dependencies

**Risk: low.** Mechanical rename with build coverage. Compiler catches every miss.

Possible failure modes:
1. The grep on caller sites missed something — build break; `dotnet build` will identify.
2. A test stub somewhere implements `ICallback` with a hand-rolled `RestoredFrame? Position => null;` line that doesn't show in the grep above (case sensitivity, partial match, etc.) — same outcome: build break, fix at point.

**Dependencies: none.** No interaction with stages 24–28.

## Watch for (coder eyes-on)

- **The using directive at the top of the file** — `RestoredFrame.cs` uses two type aliases (`ActionEntity`, `GoalEntity`). Carry them through the move. They're file-local; no global change needed.
- **The `<see cref="Call.@this"/>` reference in the XML doc** — currently resolves through the `App.CallStack.Call.@this` global namespace. After the move, the namespace is `App.CallStack.Call` so the cref might want adjusting to `<see cref="@this"/>` (sibling) or stay as-is (fully-qualified). Either compiles; pick the simpler form.
- **`PositionWire.From(RestoredFrame f)` static factory in AskCallback.cs:134** — the parameter type changes; the wire-format struct itself doesn't change. Just type rename in the signature.

## Out of scope

- Any change to the record's fields, methods, or behaviour.
- Reorganising `Call/this.cs` itself.
- Introducing a `Position` interface or factory pattern — record stays plain.
- The CallStack scope question (per-context vs shared) — Bucket C, deferred.

## Commit plan

```
runtime2-cleanup stage 23: RestoredFrame → Call/Position rename

Snapshot record renamed and relocated next to its live counterpart.
Property on ICallback was already named Position; aligning the type
removes the cognitive bump and puts the snapshot/live pair at the
same folder level.

File: App/CallStack/RestoredFrame.cs → App/CallStack/Call/Position.cs
Namespace: App.CallStack → App.CallStack.Call
Type: RestoredFrame → Position (record)

11 files swept (production + tests); 18 reference sites updated.
Pure rename; record body unchanged; no behaviour change.

Cleanup leftover from stage 10 (which focused on the App.Run reduction
headliner). Tier 5 stage 23.
```
