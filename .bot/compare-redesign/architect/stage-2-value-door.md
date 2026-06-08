# Stage 2: The value door and the value-as-raw flip

**Goal:** Make the value live in exactly one place — raw CLR inside `Data` — reached through one lazy async door, and turn the per-type classes into views over the Data. This is the foundation the new comparison stands on, and it is the largest stage.
**Scope:** Includes the `Value()` door, the `Peek()` rename, the value slot holding raw CLR, the per-type classes becoming views, the framework-method tripwires, `ToString` degradation, the internal sync read for already-materialised consumers, and migrating the ~990 `.Value` reads. Excludes all comparison logic (Stages 3–4) and consumer comparison wiring (Stage 5).
**Deliverables:**
- `public ValueTask<object?> Value()` on `Data` — the single public value accessor. Sync-complete (zero alloc) when present; async load only when pending.
- Remove the public `.Value` property. Keep a private `_value` field and a `_present` flag.
- An `internal` sync read (e.g. `PresentValue()`, throws if pending) for engine code that already materialised (serializer, the Stage-4 ordering core).
- Rename `ScalarValue` → `Peek()`.
- The value slot holds the **raw CLR** object (`string`, boxed numeric, `Dictionary`, `List`, `byte[]`). The per-type classes (`text`, `number`, date-family, `duration`, `binary`, `dict`, `list`, `bool`, `null`, `choice`) become **views constructed over a `Data`** — `text(data)`, reading the value through it; they stop holding their own backing.
- `GetHashCode` / `Equals(object)` / operators / implicit conversions on the views → **throw** with a message pointing at `await Value()`.
- `ToString()` → never throws, never does I/O: present value if loaded, else `<text pending>` (or the type's pending marker).
- Migrate the ~990 `.Value` reads to `await data.Value()`.
**Dependencies:** None to start, but **coupled with Stages 3–4** — see the coexistence note below.

## Design

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

The `ValueTask` rule to hold: **await once.** Don't store and await twice, don't touch `.Result` before completion. A caller needing the value twice awaits once into a local.

**Lazy is the principle.** A read/fetch holds only the path (source handle) until `Value()` is first awaited — nothing is read before that. `Peek()` is the sync "what's already here, unparsed" read (a json string stays the string); on a *pending* value it has nothing to show, so a passthrough like `write out %file%` still loads (async) first. The three rungs stay: source (path, unread) → `_raw` (read, in memory) → value (parsed). `source→_raw` is the I/O; `_raw→value` is the sync parse.

**The view flip.** Once the value slot holds raw CLR, the per-type class can no longer *be* the stored value — it becomes a view that holds a `Data` pointer and reads `await data.Value()` for behaviour. `item.@this` shifts from "apex of stored values" to "base of the behaviour views." This is the reversal of the stored-wrapper half of scalars-as-native, and it touches every type folder.

**The framework methods, split by consequence.** `GetHashCode`/`Equals`/operators throw because a wrong answer there silently corrupts a dict or `HashSet` — and under the flip, collections key on the **raw materialised value** (a `string`/`int` with its own real CLR hash), not on the view, so these should never be hit. The throws are how every remaining "keyed/compared a view in sync code" site announces itself — that is the migration to-do list surfacing loudly. `ToString` is the exception: it is display-only, so it degrades to `<text pending>` rather than throwing (the debugger and exception messages render via `ToString`; display tolerates not-loaded, a hash cannot). No `#if DEBUG`, no `GetAwaiter().GetResult()`.

**What replaces them — so the throw isn't a dead end:**
- **Equality / operators → `await data.Compare(other) == Equal`** (Stage 4). Value-equality moves to `Compare`, which is async — that is the replacement, and `unique`/`contains`/`indexof` already use it. Nothing is lost; `Equals`/`==` on the view just redirect there.
- **Hashing → nothing.** No value-view is hashed anywhere in the engine — verified by grep: `dict` keys on the entry **name** (`Dictionary<string,int>`), `list.group` keys on the **stringified** key (`Dictionary<string,…>`), `unique` dedupes by pairwise `Compare`. Every hash/keying path keys on a `string` that keeps its own CLR hash. So `GetHashCode` on the view has no caller to replace — the throw is pure migration scaffolding (the old model let `text.@this` be a `HashSet` member; the throw catches any leftover, then is never called again).
- **The names stay as the contract demands.** `GetHashCode`/`Equals`/`==` are names the runtime and compiler call, so the override must keep them to be a tripwire — we don't rename them. The OBP-named API for equality is `Compare`. There is **no `Hash()`**, because nothing needs one. *If* a value-level hash ever becomes necessary (e.g. a fast hash-based `unique` over a large list that must respect coercive equality — `"5"` and `5` colliding), the OBP name is `Hash()`: async (`ValueTask<int>`, awaits `Value()`) and hashing the **canonical coerced form** so it stays consistent with `Compare` (a `Compare`-equal pair must hash equal); `GetHashCode` would remain the throwing tripwire beside it. Do **not** add `Hash()` now — there is no caller, and a coercion-consistent hash is real complexity to carry unused.

(These throws are on the per-type **views** — `text`/`number`/etc. — not on `Data` itself. `Data`'s own `Equals`/`GetHashCode` keep their existing reference/identity behaviour; only the value-views tripwire.)

**Serialization materialises before the sync boundary.** Sync `JsonConverter.Write` / the wire writer cannot await, so a pending value is loaded *before* the sync write (already the codebase pattern — `Materialize` on the way into the sync state). So the wire path only sees present values and never trips a throw.

**Coexistence note (read this before sequencing).** The old compare path (`app.data.Compare` + `ScalarComparer` + the per-type `IEquatableValue`/`IOrderableValue`) still exists until Stage 6. Flipping the value slot to raw CLR degrades that old path for some types — `ScalarComparer` still handles raw `string`/numeric, but raw collections and some cross-type pairs lose the interface dispatch. So the new compare (Stages 3–4) must land close behind this flip. Keep both suites green at this stage's exit: the simplest path is to **keep the per-type views implementing `IEquatableValue`/`IOrderableValue` through Stages 2–5** (so the old mediator still dispatches), and delete those interfaces only in Stage 6. You may also interleave the per-type flip here with the per-type compare in Stage 3 — build order is yours; the invariant is green-at-exit.
