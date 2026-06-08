# Stage 2: The value door and the value-as-raw flip

**Goal:** Make the value live in exactly one place — raw CLR inside `Data` — reached through one lazy async door, and turn the per-type classes into views over the Data. This is the foundation the new comparison stands on, and it is the largest stage.
**Scope:** Includes the `Value()` door, the `Peek()` rename, the value slot holding raw CLR, the per-type classes becoming views, the framework-method tripwires, `ToString` degradation, the internal sync read for already-materialised consumers, and migrating the ~990 `.Value` reads. Excludes all comparison logic (Stages 3–4) and consumer comparison wiring (Stage 5).
**Deliverables:**
- **The async value source (net-new — the largest single piece on the branch; see Part A).** A source abstraction the door awaits, into which today's three load mechanisms collapse: the sync `_valueFactory`, `_raw` + the sync `Materialize()`, and the per-type `ILoadable.LoadAsync()`. This is design, not wiring — `_source` does not exist today.
- `public ValueTask<object?> Value()` on `Data` — the single public value accessor. Sync-complete (zero alloc) when present; async load only when pending.
- Remove the public `.Value` property. Keep a private `_value` field and a `_present` flag.
- An `internal` sync read (e.g. `PresentValue()`, throws if pending) for engine code that already materialised (serializer, the Stage-4 ordering core).
- Rename `ScalarValue` → `Peek()`.
- The value slot holds the **raw CLR** object (`string`, boxed numeric, `Dictionary`, `List`, `byte[]`). The per-type classes (`text`, `number`, date-family, `duration`, `binary`, `dict`, `list`, `bool`, `null`, `choice`) become **views constructed over a `Data`** — `text(data)`. A view's value-touching methods read the data's **present** value (the same sync `PresentValue()` read), because views are only built mid-operation after the value is materialised; they stop holding their own backing. **Views keep a sync `.Value`** (= the present-value read) — see the migration-scope note.
- `GetHashCode` / `Equals(object)` / operators / implicit conversions on the views → **throw** with a message pointing at `await Value()` — **shipped per type, together with that type's raw-flip** (see per-type sequencing).
- `ToString()` → never throws, never does I/O: present value if loaded, else `<text pending>` (or the type's pending marker).
- Migrate the **`Data`-receiver** `.Value` reads to `await data.Value()` — *not* a blind 990-site find-replace (see migration scope).
**Dependencies:** None to start. **Stages 2–4 are one green unit** — the per-stage green gate applies at the 2→4 boundary, not within 2 or 3 (the staging is for review structure; flipping the value to raw degrades the old mediator until the new compare lands, so 2–4 realistically land green only at the end of 4). See the coexistence note.

## Design

**Part A — the lazy value source (do this first; it is the bulk, and it is net-new).** The door's `await _source.ReadAndParse()` below reads as a wiring change but isn't: there is no `_source` and no Data-level async read today. Build **one** source — every way a value loads becomes a case of it. There is no longer a "fold `ILoadable` in or keep it separate" decision; it folds in.

**The one chain.** A non-authored value is produced by walking, at most, two steps — each skippable when already done:

```
reference (path / http handle)  ──read (ASYNC, I/O)──►  raw (bytes / text in memory)  ──parse (SYNC, CPU)──►  value
```

- **read** — turn a reference into raw bytes/text. The only async step; the only one that does I/O. Skipped when the raw is already in memory.
- **parse** — turn raw into the structured value via the reader registry (`App.Type.Readers.Of(Name, Kind)`). Sync. Skipped when there is nothing to parse (bytes *are* the content).
- the result is cached (so the second read is free), unless the source is a recompute source (below).

**Every existing mechanism is a position on that one chain:**
- `_raw` + `Materialize()` (today: `this.cs:218`) is the chain **minus the read** — the channel already read the bytes eagerly, so it starts at *raw* and only parses. → becomes the parse step.
- `ILoadable.LoadAsync()` (today: per-type, image/audio/video, `image/this.cs:139` → `BytesAsync()`) is the chain **minus the parse** — it reads bytes that need no parse. → becomes the read step (a reference fundamental's source drives its own `LoadAsync()`).
- an authored value (`set %x% = 5`) skips **both** — born present, no source.
- `DynamicData` (`!app`, `MyIdentity`, `this.cs:1566`) is the degenerate case: a **recompute** source — sync, no read, no parse, and it does **not** cache (re-runs every read). It has no async and never will; it's only here because the door must support "don't cache, recompute." This is why the override seam exists (below).

So flows 2 and 3 are one mechanism, not two: a source whose `Load()` is `read?-then-parse?`, with each `?` a no-op when that step isn't needed. The async is narrow — it lives only in the read step.

**The serialize walk drives this door.** Sync serialization can't `await`, so `Data.Load()` (`this.Load.cs`) stays as the pre-serialization gateway — but it now drives `await Value()` across the value graph (instead of calling `LoadAsync()` directly), so everything is materialised before the sync write.

*(How a Data is born holding a source — the channel/wire `FromRaw` birth point — is the next thing to settle; this stage takes "it has a source" as given.)*

This source is the largest chunk of work on the branch; treat it as such (you may commit it as "2a" — the green gate is the 2→4 boundary regardless).

**The override seam moves to the source.** `Value` is `virtual` today with at least one override (`this.cs:1566`, `public override object? Value => _valueFactory()`). Turning the property into a method removes that polymorphic seam — so the new variation point is the **source** (a subclass that recomputes or loads specially supplies its own source, or overrides a protected `Load()`/`LoadCore()` hook), not an overridden `Value()`. `DynamicData` is the live case: its seam is "recompute every read, never cache" — sync, not async. Name that hook in Part A so the subclass that overrides `Value` today has somewhere to go.

**The door.** `ValueTask`, not `Task`, because this is the system's hottest accessor (~990 reads) and the common case is a value already in memory — `Task.FromResult` allocates on every call, `ValueTask` allocates nothing when it completes synchronously:

```csharp
private object? _value;
private bool _present;          // true once loaded — a legitimate null still counts as present

public ValueTask<object?> Value()
{
    if (_present) return new ValueTask<object?>(_value);   // in memory: synchronous, zero alloc
    return Load();                                          // pending: real async, allocates only here
}
private async ValueTask<object?> Load()
{
    _value = await _source.ReadAndParse();   // the lazy I/O — file read / http fetch, then parse
    _present = true;
    return _value;
}
```

The `ValueTask` rule to hold: **await once.** Don't store and await twice, don't touch `.Result` before completion. A caller needing the value twice awaits once into a local. At hundreds of call sites this is easy to violate in a loop or a LINQ projection — back it with a guard, not just this prose: a Roslyn analyzer flagging a stored/double-awaited `Value()`, or at minimum a grep gate in the stage (`\.Value\(\)` not immediately preceded by `await`).

**Lazy is the principle.** A read/fetch holds only the path (source handle) until `Value()` is first awaited — nothing is read before that. `Peek()` is the sync "what's already here, unparsed" read (a json string stays the string); on a *pending* value it has nothing to show, so a passthrough like `write out %file%` still loads (async) first. The three rungs stay: source (path, unread) → `_raw` (read, in memory) → value (parsed). `source→_raw` is the I/O; `_raw→value` is the sync parse.

**The view flip.** Once the value slot holds raw CLR, the per-type class can no longer *be* the stored value — it becomes a view that holds a `Data` pointer. A view is only built mid-operation, *after* the value is materialised (the consumer awaited the door, or it came from `Compare` which awaited both operands), so the view reads the **present** value synchronously — the same `PresentValue()` read, no `await`, no I/O. `item.@this` shifts from "apex of stored values" to "base of the behaviour views." This is the reversal of the stored-wrapper half of scalars-as-native, and it touches every type folder.

**Migration scope — not a 990-site find-replace.** `grep '\.Value\b'` is 990, but `.Value` is overloaded and most hits must be left alone: ~74 are `Lazy<T>.Value` / `KeyValuePair.Value` / `Nullable<T>.Value` / `JsonElement.Value`, and the **views own a `.Value` too** (`text.@this.Value`, `number.@this.Value`, `choice.Value`, `TString.Value`). Only the **`Data`-receiver public `.Value`** reads migrate to `await data.Value()`. The view `.Value` stays — it's the present-value sync read (views run post-materialisation). So the stopping rule is by **receiver type**: `Data` → `await Value()`; view or framework type → leave. Re-scope the estimate as "the `Data`-receiver subset, each needing a per-receiver type check" — a few hundred judgements, not a mechanical sweep. A find-replace across all 990 rewrites the wrong receivers and breaks the views.

**Per-type sequencing — the throw and the raw-flip ship together, per type.** `GetHashCode`/`Equals` on the views back live CLR keying *today* (`TString.cs:104,109`; `choice/this.cs`), so "collections key on the raw value, not the view" is true only *after* a given type's value-slot is flipped to raw. So for each type, flip-to-raw and throw-the-framework-methods land in the **same step** — if the throw precedes the flip for any type, dict/set keys explode mid-migration. This is a different axis from the `IEquatableValue` coexistence below (that's about the *mediator*; this is about *CLR collection keying*).

**The framework methods, split by consequence.** `GetHashCode`/`Equals`/operators throw because a wrong answer there silently corrupts a dict or `HashSet` — and under the flip, collections key on the **raw materialised value** (a `string`/`int` with its own real CLR hash), not on the view, so these should never be hit. The throws are how every remaining "keyed/compared a view in sync code" site announces itself — that is the migration to-do list surfacing loudly. `ToString` is the exception: it is display-only, so it degrades to `<text pending>` rather than throwing (the debugger and exception messages render via `ToString`; display tolerates not-loaded, a hash cannot). No `#if DEBUG`, no `GetAwaiter().GetResult()`.

**What replaces them — so the throw isn't a dead end:**
- **Equality / operators → `await data.Compare(other) == Equal`** (Stage 4). Value-equality moves to `Compare`, which is async — that is the replacement, and `unique`/`contains`/`indexof` already use it. Nothing is lost; `Equals`/`==` on the view just redirect there.
- **Hashing → nothing.** No value-view is hashed anywhere in the engine — verified by grep: `dict` keys on the entry **name** (`Dictionary<string,int>`), `list.group` keys on the **stringified** key (`Dictionary<string,…>`), `unique` dedupes by pairwise `Compare`. Every hash/keying path keys on a `string` that keeps its own CLR hash. So `GetHashCode` on the view has no caller to replace — the throw is pure migration scaffolding (the old model let `text.@this` be a `HashSet` member; the throw catches any leftover, then is never called again).
- **The names stay as the contract demands.** `GetHashCode`/`Equals`/`==` are names the runtime and compiler call, so the override must keep them to be a tripwire — we don't rename them. The OBP-named API for equality is `Compare`. There is **no `Hash()`**, because nothing needs one. *If* a value-level hash ever becomes necessary (e.g. a fast hash-based `unique` over a large list that must respect coercive equality — `"5"` and `5` colliding), the OBP name is `Hash()`: async (`ValueTask<int>`, awaits `Value()`) and hashing the **canonical coerced form** so it stays consistent with `Compare` (a `Compare`-equal pair must hash equal); `GetHashCode` would remain the throwing tripwire beside it. Do **not** add `Hash()` now — there is no caller, and a coercion-consistent hash is real complexity to carry unused.

(These throws are on the per-type **views** — `text`/`number`/etc. — not on `Data` itself. `Data`'s own `Equals`/`GetHashCode` keep their existing reference/identity behaviour; only the value-views tripwire.)

**Serialization materialises before the sync boundary.** Sync `JsonConverter.Write` / the wire writer cannot await, so a pending value is loaded *before* the sync write (already the codebase pattern — `Materialize` on the way into the sync state). So the wire path only sees present values and never trips a throw.

**Coexistence note (read this before sequencing).** The old compare path (`app.data.Compare` + `ScalarComparer` + the per-type `IEquatableValue`/`IOrderableValue`) still exists until Stage 6. Flipping the value slot to raw CLR degrades that old path for some types — `ScalarComparer` still handles raw `string`/numeric, but raw collections and some cross-type pairs lose the interface dispatch. So the new compare (Stages 3–4) must land close behind this flip. Keep both suites green at this stage's exit: the simplest path is to **keep the per-type views implementing `IEquatableValue`/`IOrderableValue` through Stages 2–5** (so the old mediator still dispatches), and delete those interfaces only in Stage 6. You may also interleave the per-type flip here with the per-type compare in Stage 3 — build order is yours; the invariant is green-at-exit.
