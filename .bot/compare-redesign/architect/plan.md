# Comparison redesign — plan

This supersedes the rules in `.bot/compare-redesign/coder/compare-redesign-plan.md`. Where the two disagree, this file wins. Settled with Ingi on the whiteboard; verified against the real code on this branch (`compare-redesign`, cut from `scalars-as-native`).

## Why

Comparison today is spread across five pieces: a static `app.data.Compare` mediator, `ScalarComparer`, `Operator.NormalizeTypes`, the `IEquatableValue`/`IOrderableValue` interface pair, and a per-type `AreEqual`/`Order` on each value class. On top of that the value is stored inconsistently — sometimes a raw CLR scalar, sometimes a `text.@this`/`number.@this` wrapper — so the mediator has to handle both forms and coercion lives in a separate pass. Comparison should be owned by the value's type, over a value that lives in exactly one place, reached through exactly one door, so `if a > b`, `sort`, `contains`, and `assert` can't drift apart. The same redesign makes **lazy I/O** real: a value backed by a file (or http, or any I/O source) holds only the path until something actually needs it, and is read on first touch — never before.

## The model (settled)

The value lives once, in `Data`, as the raw CLR object. The type is behavior — a view built over the Data that reads the value through it. The value is reached through one async door, `await data.Value()`, which loads lazily on first touch. Comparison is owned by the type, compares only its own kind, reconciles cross-type pairs by a per-type rank, and runs its ordering math synchronously on values the caller already awaited.

1. **Value lives once, raw, in Data.** The value is the CLR object — `5`, `"hello"`, a `Dictionary`, a `List`, `byte[]`. Never a type wrapper. The wrapper-stored-in-the-value-slot shape goes away.
2. **The type is a view over the Data.** The per-type class (`text`, `number`, …) is constructed with the Data instance and reads the value through it. It holds a pointer to the one home, not a copy. `text(data)`, not `text(string)`.
3. **One door: `await data.Value()`, returning `ValueTask<object?>`.** It loads lazily — if the Data is a not-yet-read file/http source it reads on this first touch; if the value is already present it completes synchronously. There is **no public `.Value` property** — the value is reached one way. Details in *The value door* below.
4. **Lazy is the principle.** Nothing is read until `Value()` is first awaited. A read/fetch holds only the path (or source handle) until then. This holds for files, http, and any I/O-backed value.
5. **`_raw` stays; `ScalarValue` is renamed to `Peek()`.** Three rungs: source (path, unread) → `_raw` (read bytes / json text, in memory) → value (parsed). The source→`_raw` step is I/O (async, inside `Value()`); the `_raw`→value step is a sync parse. `_raw` earns its place in the uses that stop before parsing — verbatim write-back (no lossy round-trip), byte length, binary, upload, http pre-dispatch. **`Peek()`** (was `ScalarValue` — "scalar" pointed at the wrong axis: it's about unparsed-vs-parsed, not single-vs-collection) is the sync read that hands back what is already present without forcing a parse: a json string stays the string, never built into a `dict`. It is the cheap "look at what's here" against `await Value()`'s "load and parse." Caveat: `Peek()` on a *pending* value has nothing to show (`_raw` is empty until first load), so a passthrough like `write out %file%` still loads (async) before there is anything to peek at — `Peek()` is the sync, no-load read, not a way to dodge the file read.
6. **Comparison is owned by the type and compares only its own kind.** A type never compares a foreign kind. Cross-type is resolved first by coercion: the higher-ranked of the two operands' types wins, coerces the loser into its own kind, then compares two of its own.
7. **Cross-type direction is decided by a per-type rank, and the rank lives on the type — this is load-bearing.** Each type owns its rank (specificity): `number > text`, the date family `> text`, with `text` as the floor. Data does not compare ranks — it asks `this.Type.Rank(other)`, passing the whole other operand (never `other.Type`), and the type returns the winner. The higher-ranked type drives regardless of operand order, which guarantees antisymmetry: `text"10"` vs `number 9` lets `number` drive in both directions, so both compare numerically and agree. Distributing coercion as "the left operand's type coerces the right" breaks this — the two directions disagree and `sort` corrupts. The rank makes explicit what `NormalizeTypes` bakes in today ("always coerce text→number").
8. **Reading is async; the ordering math is sync.** `await data.Compare(other)` awaits both operands through the one door, then runs the sync ordering core on the two materialised values + their types. Conditions call it from their already-async evaluator.
9. **Sort (and any I/O-bearing key) is async, two-phase, no sync-over-async.** See *Sort and the async boundary* below.
10. **`Comparison` enum** — see *The Comparison enum* below.
11. **Dispatch reuses the one routing that already exists.** The type entity already routes name → family behavior (`type.Convert` via `App.Type.Conversions`, the registry resolving `App.Type[Name].ClrType`). Comparison is one more method on the behavior that routing produces — no new compare registry, no `Type.Name == "..."` switch anywhere. The type entity carries `Context` (stamped at `Variables.Set` / `Action.RunAsync`), so the routing is reachable; compare always runs on a stamped type.

## The value door

This is the heart of the change, so it gets its own section.

**Public surface: `public ValueTask<object?> Value()`.** It is the only way to get the value. `ValueTask`, not `Task`, because this is the system's hottest accessor and the common case (value already in memory) must allocate nothing — `Task.FromResult(...)` allocates a `Task` on every call even with no `async` keyword, and there are ~990 read sites, many in loops. `ValueTask` completes synchronously with zero allocation when the value is present, and allocates only on the genuinely-pending I/O path.

```csharp
// Data
private object? _value;
private bool _present;           // true once loaded — so a legitimate null value still counts as present

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

Consumers look identical whether the await completes sync or async — that is the point:

```csharp
var raw = await data.Value();   // present -> returns synchronously, no alloc; pending -> awaits the read, once
```

The one `ValueTask` rule to teach: **await it once.** Don't store it and await twice, don't touch `.Result` before completion. For an accessor awaited inline at each use this is the normal pattern; a caller that needs the value twice awaits once into a local.

**No public `.Value` property — just the private `_value` field.** The two engine consumers that hold an already-present value don't need a property: the sync ordering core receives the awaited values as arguments (`Compare` awaits both and passes the raw objects in), and the serializer works from the values its async pre-materialise pass produced. If an engine path genuinely must read a present value back from a Data in sync code, that is an `internal` method (`PresentValue()`, throws if pending), never a public property.

**The sync framework methods that can't be async and can't be deleted — split by consequence:**

- **`GetHashCode` / `Equals` / operators / implicit conversions → throw**, with a message pointing at `await Value()`. A wrong answer here silently corrupts a dict or a `HashSet`, so failing loud is correct. Under this model these shouldn't be hit anyway — collections key on the **raw materialised value** (a `string`/`int` with its own real CLR hash and equality), not on the wrapper. The throws surface, loudly, every site that still keys/compares a wrapper in sync code — that is the migration to-do list announcing itself.
- **`ToString()` → never throws, never does I/O.** It shows the value if it is already present, else a pending marker: `_present ? _value?.ToString() : "<text pending>"`. The debugger, exception messages, and logs render values by calling `ToString()`; display tolerates "not loaded yet" gracefully (a hash cannot), so `ToString` degrades instead of throwing. No `#if DEBUG`, no `GetAwaiter().GetResult()`.

**Serialization materialises before the sync boundary.** Sync serialization (STJ `JsonConverter.Write`, the wire writer) cannot await, so any pending value must be loaded *before* the sync write runs. This is already the pattern in the codebase: `Materialize` is called on the way into the sync serialization state. So the serializer only ever sees present values, and the throwing framework methods are never tripped from the wire path.

## Dispatch, concretely

```
// inside Data
public async ValueTask<Comparison> Compare(Data other)
{
    // Rank lives on the type, not on Data. Ask this.Type, passing the WHOLE other Data
    // (never other.Type) — the type owns the rank comparison and returns the winning operand.
    var winner = this.Type.Rank(other);
    var loser  = ReferenceEquals(winner, this) ? other : this;

    var wv = await winner.Value();   // one door; loads lazily if pending; sync if present
    var lv = await loser.Value();

    // The winner's type coerces the loser into its own kind and orders two of its own.
    return winner.Type.Order(wv, lv);
}
```

`this.Type.Rank(other)` is the only place the winner is decided — Data never compares ranks and never reaches into `other.Type`. The type owns its rank and, given the whole other operand, returns the winner (always one of the two operands; no new type is minted in a compare — minting only happens in `set %x% = "5" as number`, which is conversion). `winner.Type.Order(...)` then routes through the existing name→family path to the winner's compare, which coerces the loser into its own kind and compares two of its own. Signatures are the shape, not the final names — you own those.

## The Comparison enum

`{ Less, Equal, Greater, NotEqual, Incomparable }` — no sign-bearing numbers (a magic `-2` would satisfy `< 0` and corrupt sort). `==` is `Equal`; `<`/`>` read `Less`/`Greater`.

- **`Incomparable` is ordering-only.** `<`/`>` across types that can't be ordered → a PLang error at the boundary.
- **Equality across types:** a coercible pair (`%count% == "5"`) coerces then compares; a non-coercible **non-null** pair (`dict == number`) → error (the developer is comparing things that can't be compared).
- **null is always comparable for equality** — `%x% == null` / `%x% != null` never errors, for any type.
- **`nulls last`** in ordering.
- The value never throws; the errors surface at the operator/sort/assert boundary.

## Sort and the async boundary

Sort is async, two-phase, and never does sync-over-async:

- **Phase 1 — materialise keys (async).** All I/O lives here. `sort %files% by size`: `var p = (path)await item.Value();` then `await p.Size()` (an explicit async `path` verb — `Size()`/`ReadText()` do the I/O). Collect `(key, item)` pairs.
- **Phase 2 — order (sync).** Order the in-hand keys with the sync ordering core inside `List.Sort` — no await in the comparator, because every key was already awaited in phase 1.

`GetAwaiter().GetResult()` appears nowhere. The invariant that protects this: the ordering math is sync over values already pulled through the door, and all I/O is hoisted to phase-1 key materialisation. A type's **default** compare must stay sync (e.g. `path` by name); anything that needs I/O to compare is expressed as `sort by <key>`, so the read lands in phase 1, never inside the comparator.

## What this replaces

- `app.data.Compare` (static mediator), `ScalarComparer`, `Operator.NormalizeTypes`, `IEquatableValue`, `IOrderableValue`, and every per-type `AreEqual`/`Order` in the current shape.
- The stored-wrapper shape: the value slot stops holding `text.@this`/`number.@this`; those classes become views over a Data, and their backing-holding constructors (`text(string)`, the boxed numeric on `number`) move to reading through the Data.
- The public sync `.Value` property — replaced by the single `await data.Value()` (`ValueTask`).
- The golden-diff method on `Data` (`this.Compare.cs`) is renamed to `Diff` (rename the file too) so the value-`Compare` owns the name. 18 `.Compare(` call sites, ~14 in tests, no production callers.

## Consumers to move

- **condition operators** (`==`, `!=`, `<`, `>`, `<=`, `>=`, `contains`, `in`) — the registry is already async; wire each to `await data.Compare(other)` reading the `Comparison` enum.
- **assert** (`Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `Contains`, `NotContains`) — await `Compare`.
- **sort** — the two-phase shape above; drop the `Comparer<object>.Default` sync comparator on raw values.
- **list ops** (`contains`, `indexof`, `unique`) — await `Compare` per element.
- **the ~990 `.Value` reads** become `await data.Value()`. Most are inside async `Run` bodies and convert mechanically; the genuinely-stuck ones are the framework-contract methods above, which throw (or, for `ToString`, degrade). The throws make every remaining site loud.

## Execution order

A rough order, not stage files. Re-read this plan before each step; push after every step.

0. Confirm green from clean (C# + PLang) on this branch.
1. Add the `Comparison` enum.
2. The value door: `ValueTask<object?> Value()` (sync-complete when present, async load when pending); remove the public sync `.Value`; keep `_raw` and the parse step; rename `ScalarValue` → `Peek()`. Make the value slot hold the raw CLR value; turn the per-type classes into views over the Data. Make `GetHashCode`/`Equals`/operators throw with guidance; make `ToString` degrade to `<text pending>`.
3. Per-type rank + each type's coerce-and-compare-own-kind, with the sync ordering core. Prove `text`, `number`, and the `text`↔`number` cross-pair end to end before replicating across `bool` / `null` / date-family / `duration` / `binary` / `dict` / `list`.
4. `data.Compare(other)` wiring the rank + the door + the sync core, through the existing name→family routing.
5. Move the consumers (conditions, assert, sort, list ops, and the `.Value` reads) to the async door / `Compare`.
6. Delete the old mediator, `ScalarComparer`, `NormalizeTypes`, the two interfaces, the old per-type `AreEqual`/`Order`. Rename golden-diff `Compare` → `Diff`.
7. Green both suites from clean. Triage residual; flag any real semantic bug rather than papering over it.

## You own this

The code shapes, signatures, and names here are suggestions to make the design concrete — not a spec to copy. You own the final shape: method names, how rank is represented on each type (a number/static the type owns is the natural fit — the *call shape* `this.Type.Rank(other)→winner` is the fixed part, not the storage), how the sync ordering core is factored, how the view is constructed, the exact wording of the tripwire throws. The parts that are **not** yours to change without coming back: the value lives once in Data (raw, one async door, lazy); cross-type direction is decided by rank **owned by the type** — Data never compares ranks, it asks `this.Type.Rank(other)` (whole other operand, never `other.Type`) — so antisymmetry holds; the ordering math is sync and all I/O is hoisted so nothing does `GetAwaiter().GetResult()`; the value door is `ValueTask` (no public sync `.Value` property); the sync framework methods throw rather than read; no `Type.Name` switch and no second compare registry. If implementing forces one of those to bend, stop and flag it.
