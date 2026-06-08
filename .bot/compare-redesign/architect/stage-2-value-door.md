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

**Part A — the async value source (do this first; it is the bulk, and it is net-new).** The door's `await _source.ReadAndParse()` below reads as a wiring change but isn't: there is no `_source` and no Data-level async read today. What exists is three *separate* load mechanisms, all sync or per-type:
- `_valueFactory` — a **sync** `Func<object?>` (`this.cs:28`; used by `SetValue`, clone, `RawUntouched`).
- `_raw` + a **sync** `Materialize()` (`this.cs:218`; `MaterializeCount` probe at `:294`).
- `ILoadable.LoadAsync()` — a **per-type** async load for reference fundamentals (image/audio/video), tied to the serializer's STJ wall, idempotent (`ILoadable.cs`). Not a Data-level source.

Part A is to design the one source these collapse into — a value the door awaits: `ValueTask<object?>`, **sync-complete** for everything that's already in memory or pure CPU, **async only** when it must read. Most of it is sync; the async is narrow. The current mechanisms become source *shapes*:
- **recompute-each-read (sync)** — `DynamicData` (`!app`, `MyIdentity`): re-runs its `Func<object?>` every read, never caches (`Value => _valueFactory()`, `this.cs:1566`). Sync, no I/O. The door must support a "don't cache, recompute" source — this is the live reason the override seam below exists. **Note: `DynamicData` has no async and never will** — it's purely the recompute case.
- **parse-once-and-cache (sync)** — `_raw` + `Materialize()`: parses the in-memory source form via the reader registry on first read, caches. Sync (CPU), no I/O.
- **load-once-async (async)** — a file/http value, and the reference fundamentals behind `ILoadable.LoadAsync()` (image bytes): reads on first touch, then caches. This is the only shape that awaits real I/O.
- an authored value (`set %x% = 5`) has **no source** — born present.

**The 2↔3 convergence (your point).** Today `_raw`+`Materialize` (read-eager-at-the-channel, parse-lazy, sync) and `ILoadable` (read-lazy, async) are two paths because the channel reads eagerly. Under the lazy door the channel stops pre-reading and holds the source, so both collapse into **one chain**: `reference → read (async, lazy) → raw → parse (sync) → value`. `_raw`+parse is that chain minus the read (had it eagerly); `ILoadable` is that chain minus the parse (bytes need none). So Part A is literally "make both walk the whole chain."

**Decide how `ILoadable` folds:** the natural home is that a reference fundamental's source delegates to its `LoadAsync()` (the per-type load logic stays on the type; the source just drives it), and `Data.Load()` (the serialize-time walk) drives the door across the graph instead of calling `LoadAsync()` directly. Settle that here — it is the one real fork in Part A. This is the largest chunk of work on the branch; treat it as such (you may commit it as "2a" — the green gate is the 2→4 boundary regardless).

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
