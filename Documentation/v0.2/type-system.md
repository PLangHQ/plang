# Type System Notes

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## `IExitsGoal.ShouldExit()` — Value-side opt-out for resolved sentinels

`IExitsGoal` is the marker the engine queries via `result.ShouldExit()` to decide "stop here, capture a Snapshot, return through the channel". `ShouldExit()` is **virtual with default `true`** — the marker alone is enough for a type that always means "suspend".

A type that rides both states on one record (suspending **and** resolved) overrides:

```csharp
public sealed class Ask : IExitsGoal
{
    public string? Answer { get; init; }
    public bool ShouldExit() => Answer == null; // resolved Ask flows through
}
```

`output.ask` returns `Data<Ask>`. On the suspend path `Answer == null` → `ShouldExit()` returns true, the step loop short-circuits, the Snapshot rides as `Data.Snapshot`. On the resume path the channel has pre-bound the answer, `Answer != null` → `ShouldExit()` returns false, the step loop continues and the trailing `variable.set` binds the Ask. Callers read `%name.Answer%` for the structured form; `Ask.ToString() => Answer ?? ""` covers `%name% equals "Alice"` string-context comparisons.

The carve-out: `Data` with only `Type` set (no Value) still triggers the **Type-side** exit check. The Value-side `ShouldExit()` only fires when a Value is present.

Pattern to copy when adding another resolved-sentinel type: implement `IExitsGoal`, expose a nullable "answer-like" field, override `ShouldExit()` to return false when that field is bound. Don't reach for a separate "ResolvedAsk" subclass — one record, two states, the override carries the semantics.

## Typed values — `app/type/<name>/`, per-(type, format) renderers, `type` + `kind` as separate fields

Higher-level PLang values (`number`, `image`, `code`, `path`, `datetime`, `duration`, …) live as folders under `PLang/app/type/<name>/`. Each owns a small contract: `this.cs` (the value + `[PlangType]` + `IBooleanResolvable` truthiness), `this.Parse.cs` (`static Resolve(value, context)` — runtime construction), `this.Build.cs` (`static Build(value) → kind` — build-time kind derivation), and a `serializer/` subfolder.

**`type` + `kind` are separate `.pr` fields.** Every value carries a high-level `type` (the routing key) and an optional `kind` refinement, stored separately — never as a `type:kind` string. The `kind` is stamped at build by the type's own `Build(value)` method, the build-time sibling of `Resolve`. So **`int`/`decimal`/`double` are kinds of `number`**, `jpg`/`png` are kinds of `image`, `csharp`/`python` are kinds of `code`, `http`/`file` are kinds of `path`. Number isn't special; the LLM is shown a type's kinds only when developer-meaningful (number's precision) — otherwise `Build()` derives the kind silently.

**Per-(type, format) serializer dispatch.** Each type owns `serializer/<format>.cs` files — one `Default.cs` (uniform rendering) plus a file per format that genuinely differs. `image/serializer/text.cs` renders a path placeholder, `image/serializer/protobuf.cs` raw bytes, `image/serializer/Default.cs` base64. The filename **is** the format selector, the folder name **is** the type. The source generator emits a `(typeName, formatToken) → Write` table; the writer carries its `Format` token and looks up. No `IWireWritable` interface on the value; no mime switch inside any method.

**Two `Build`s, kept distinct.** The **action** `IClass.Build()` decides an *action's* return type when it's dynamic (e.g., `file.read.Build()` reads the extension and resolves it to `image`). The **type** `Build(value)` decides a *value's* `kind`. They cooperate: `read photo.jpg` → action's Build sets `type = "image"`, type's Build sets `kind = "jpg"`.

**Multi-faceted values compose, not union.** A file-backed `image` carries a `Path` property of type `path` (nullable when constructed from raw bytes). `%photo.Path.Exists%` navigates through the typed-property catalog. No `path|image` union — the routing key stays single.

**Runtime DLL loading extends the catalog.** `code.load` scans the loaded assembly for `[PlangType]` classes and `ITypeRenderer` implementations as well as `ICode`. Runtime registrations outrank generator-emitted ones at resolution + rendering, but cannot rewrite what the source generator already baked into compiled handler slots and shipped `.pr` stamps. Five names are **sealed** against shadowing (`identity`, `signature`, `signedoperation`, `callback`, `channel`) because their bodies are signing- or transport-load-bearing — attempting to register one fails with `TypeLoadCollision`. `Loader.SealedNames` enforces this at every register site.

**Couriers never read `.Value`.** Variable memory, callstack, channel routing, signing, the wire envelope all key on `Data.Type` (and `Data.Kind` when relevant). Only **leaf actions** (handlers declaring `Data<T>` parameters) and **leaf serializers** (the per-(type, format) renderer files) get to dereference `Data.Value`. This is [OBP Rule #9](object_pattern_formal.md#9-only-leaves-touch-datavalue) — and the seventh entry in `/CLAUDE.md`'s OBP Smell Checklist.

Full design (movie, build-vs-runtime trace, dispatch mechanism): the architect plan on the `plang-types` branch — `.bot/plang-types/architect/plan.md` and the seven stage files alongside.

## Strict kind — `as image/gif strict` validates wherever the bytes appear

`as <type>/<kind> strict` says "the value must really be a `<kind>`, not just declared as one". A PNG written to disk as `photo.gif` and bound with `as image/gif strict` must fail — but the failure can happen at three genuinely different moments depending on when the bytes are available. Two interfaces on `app.data` carry the discipline, and `image.@this` is the sole implementor today (audio/video planned).

```csharp
public interface IKindValidatable                              // stateless sniffer
{
    (bool ok, string? actualKind) ValidateKind(object value, string requiredKind);
}

public interface IStrictKindEnforcer                           // imprint that rides with the value
{
    void RequireStrictKind(string kind);
    (bool ok, string? actualKind)? CheckStrictKind();          // null = not loaded yet → defer
}

public sealed class StrictKindMismatchException : Exception { ... }
```

**Two interfaces because validation and enforcement are different jobs.** `IKindValidatable` is a pure sniffer — "do these bytes match this kind?" — usable without state. `IStrictKindEnforcer` is the imprint that travels with a path-backed value so its own load seam (`image.BytesAsync`) can throw on mismatch the `set` never saw. The markers live next to `Data` (the dispatcher), not on the concrete value type, so strict's machinery depends only on the marker, not on `image` specifically.

**Why both `text.@this` and `image.@this` don't implement these the same way.** `text` doesn't implement `IKindValidatable` — there is no probe that distinguishes plain text from markdown by content, so `as text/md strict` degrades to "kind name accepted". `image` implements both: magic-byte sniffing answers "are these bytes really a GIF?", and the imprint defers the check to load time.

**Three enforcement gates in `variable.set` — not redundant, each catches a case the others can't.** The chain at `variable/set.cs:131-274` is annotated in-source with exactly this rationale:

1. **Build time** — `ValidateBuild` rejects a literal value when its spelling can be checked at compile (`set %img% = "real.png" as image/gif strict` with a literal path the builder reads).
2. **Run time, `IKindValidatable` probe** — `set.cs:181-195`. A `%var%` value resolved at run time is fed through `TryInstantiateValidator` (constructs a probe from raw `byte[]` when the type's primary ctor accepts them) and rejected before the binding mints.
3. **Materialization time, `IStrictKindEnforcer` imprint** — `set.cs:264-274`. The `RequireStrictKind` imprint rides with the value; for a path-backed image where bytes aren't in memory yet, `CheckStrictKind()` returns `null` (defer) and `image.BytesAsync` throws `StrictKindMismatchException` when bytes finally load.

**Strict×lazy at the chokepoint — handled by `Data.Load()`.** A path-backed strict image never throws at `set` (the bytes aren't loaded yet); without intervention, every consumer reads sync `image.Bytes` → `Array.Empty<byte>()` and strict never fires. The fix is an async pre-pass run at the serialize chokepoint — see [`Data.Load()` in wire-serialization](wire-serialization.md#dataload--async-pre-materialization-at-the-serialize-chokepoint).

**Receiver-side discipline.** The `Strict=true` flag stamped on the wire does **not** auto-impose strict on the receiver without an explicit `as ... strict` clause. Signing is the trust boundary; strict is a developer ergonomic for the side that owns the binding. (Security audit: `.bot/type-kind-strict/security/v1/result.md` F2.)

**Forward-looking discipline for future implementors.** Any new `IKindValidatable` ctor is auto-invoked on user data by `TryInstantiateValidator` — **public ctors of `IKindValidatable` types must be side-effect-free**. Anything that opens a connection, writes a file, or otherwise touches the world from a constructor breaks the strict-probe contract.

## Reader registry — `app.type.reader.@this`, the read-side mirror

The renderer writes; the reader reads. `app/type/reader/this.cs` (`app.type.reader.@this`) mirrors `app.type.renderer.@this` exactly — same dispatch shape, same precedence order, same wildcard token. It exists so the read half is one symmetric registry instead of the fragmented set it replaced (`type.Convert`, the per-family `Convert` hook, `FromWire`/`WireReader`, `path.JsonConverter`, per-type `JsonConverter<T>`, and three separate parse moments across `file.read` / `http.get` / `channel.read`).

```csharp
public sealed class @this
{
    public const string AnyKind = "*";   // mirror of renderer.AnyFormat

    public delegate object? Read(object raw, string? kind, ReadContext ctx);

    public Read? Of(string typeName, string? kind);
    public void  Register(string typeName, string kind, Read read);   // code.load seam
}
```

**Discovery and file shape mirror the renderer.** A type ships its read by adding a `public static class` in `app/type/<name>/serializer/<kind>.cs` with `public static object? Read(object raw, string? kind, ReadContext ctx)`. `Default.cs` is the wildcard (`AnyKind`); a kind that reads specially gets a per-kind file. **Same file holds both halves** of a type's serialization (`Read` next to `Write`) — not a parallel `reader/` tree (that would split a type's two halves across folders).

**Precedence: runtime-exact → generated-exact → runtime-`*` → generated-`*`.** Runtime registrations (`code.load`) shadow generator-discovered entries. An exact `(type, kind)` match wins over the wildcard at the same level. Identical to the renderer.

**Two layers, both halves symmetric with write.**

```
write                                 read
──────────────────────────────────   ──────────────────────────────────
type/renderer/this.cs              →  type/reader/this.cs
channel/serializer/IWriter.cs      →  channel/serializer/IReader.cs
channel/serializer/json/writer.cs  →  channel/serializer/json/reader.cs
<family>/serializer/Default.cs Write → <family>/serializer/Default.cs Read
```

On write, the channel serializer owns the format (`IWriter`, `json/writer.cs`) and the type owns its value (`Write(value, IWriter)`). Read mirrors that: the channel serializer decodes bytes → a structure (`IReader`, `json/reader.cs`), and the type builds its value from that (`Read(raw, kind, ctx)`). Format-decode and value-materialize are different layers — same split the write side already has.

**Dispatch key is `(type, kind)` where `type` is the data's *shape* and `kind` is the *encoding within that shape*.** `object` is hierarchical/tree data, `table` is a grid of rows and columns, `number` is a scalar — not how it was encoded. `kind` is the encoding within that shape: `json`/`xml`/`yaml` for `object`, `csv`/`xlsx` for `table`, `png`/`jpg` for `image`, `int`/`uint` for `number`. So `config.json` → `(object, json)`, `report.csv` → `(table, csv)`. Stamping the type does **not** parse — `type=object` is a promise about the shape on materialization, not an instruction to produce it now. See [`Data` lazy materialization](data-internals.md) for the touch-time read.

**Distributed `OwnerOf`.** The old `clr → (family, kind)` switch in `app/type/convert/this.cs` is being distributed onto each family — `number` declares the numeric CLR types it owns, `text` declares `string`, `path` declares its subclasses. The central `if u == typeof(int) …` ladder dies; adding a new CLR-backed kind becomes an edit to one family.

**What mid-graph fields do.** Payload-level reads are keyed by `(type, kind)` — but STJ also hits domain-typed fields *mid-graph* (e.g. a `path` nested three levels down inside an `As<T>` target). A payload-level registry can't serve those. The json layer owns **one** converter — `app/channel/serializer/json/converter.cs` — that talks STJ on one side and the reader registry on the other. Built per-actor with context. Three legacy converters stay specialized rather than folding in: `TimeSpanIso8601` (site-dependent — STJ option-bag form differs from wire `IWriter.TimeSpan`), `ErrorWire` (polymorphic over `IError` with a `$type` discriminator; snapshot-only), and `HashDataConverter` (object-shaped, signing-only). None is a *value type owning a format-named converter* — that was the actual smell `path.JsonConverter` exhibited, and only `path.JsonConverter` is deleted.

**Errors stay in `Data`, never throw at the courier.** A `Read` that fails (malformed json, wrong shape) produces a `Data.Error` rather than throwing — materialization failures surface through `As<T>()` / navigation, which already carry an `Error`. See [navigation seams](data-internals.md) for how `MaterializeFailed` gets surfaced past `GetChildValue` / `SetValueOnObjectByPath` instead of being silently dropped as `NotFound`.

## `app.X` is the collection node — `[name]` / `.list` / `.current`

Across the `singular-namespaces` refactor every plural `App<Plural>`
wrapper alias (`AppGoals`, `AppChannels`, `AppEvents`, `AppModules`) was
deleted. The replacement is the **collection-node convention**: every
concept `X` in the PLang vocabulary exposes its collection at `app.X`
(type `app.X.list.@this`, folder `X/list/this.cs`), owned once by the
singleton `app` (or by `actor` for `channel`). Selection, enumeration,
and "what we're currently inside" are all on the node:

```csharp
app.Goal["main"]            // select one by name/key — throws on miss
app.Goal.list               // enumerate (IEnumerable<goal.@this>)
app.Goal.current            // the goal execution is currently inside
                            // (reads CallStack.Current.Action.Step.Goal)
```

**`.current` exists only where execution flows through.** A concept that
nothing is ever *inside* — `type`, `channel`, `event`, `module`, `format`
— has **no** `.current`. Reach for the registry on the node, not for a
fictional current.

**Registry = selection + lifecycle; behavior lives on the element.** A
type-switch (`is X.subtype` or `switch (registry[name])`) inside a
registry method is misplaced behavior — push it onto the element as a
virtual member. The collection never lives on the element, and the
collection is never a flat property on `app` either (the deleted
`App<Plural>` aliases were exactly that smell).

**`module` is a no-`.current` service.** Action modules are dispatched,
not navigated. `app/module/this.cs` (= `app.module.@this`, the type) is
the action registry; the property surface is `app.Module` (PascalCase).
`app.Module.Describe()` walks the registered handlers for the catalog;
`app.Module.Schema.Build()` produces the LLM action catalog snapshot.

The full token map for the rename (plural → singular) lives in
`app-tree.md`; the canonical OBP rule (Rule #1 — Public mutable
collection with rules enforced from outside) is what was being fixed.

## Producer-stamping invariant — `Data.Type` propagation

`Data.Context` is **non-null end-to-end** post-`singular-namespaces`.
But `Data`'s constructor sets the internal `_type` field directly
(bypassing the setter), which means `type.@this.Context` is **not**
populated at construction. It is propagated **only** by the
`Data.Context` setter — when a downstream owner stamps the Data with a
context, the setter also writes through to `_type.Context`.

The invariant:

> A `type.@this` entity that fronts a `Data` is stamped *by the Data*,
> not at its own construction. Code that reads `type.Fields` /
> `type.Values` / `type.Example` (the catalog-fold properties) **before**
> the carrying Data has been stamped will throw
> `InvalidOperationException` via `type.Promote()`.

Two carve-outs from the throw:

- **Primitive entities** — constructed via the 2-arg ctor that flips
  `_foldLoaded = true`. `app.Type["string"].Example` is reachable
  without any App or stamped Data.
- **`ClrType` reads** — the chain
  `_clrType ?? Context?.App.Type.Resolve(name)?.ClrType ?? GetPrimitiveOrMime(name)`
  falls off to `null` instead of throwing. Tests pin both branches:

```csharp
TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard       // Promote() fires
TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext  // _foldLoaded=true
ClrType_OnUnstampedDomainType_ReturnsNull             // silent null, no Promote
```

(All three in `PLang.Tests/App/SingularNamespaces/NullabilityTests/NonNullInvariantTests.cs`.)

**Why this matters for module authors.** A new module that constructs a
`type.@this` itself (instead of `app.Type["name"]`) and reads fold
properties on it before stamping the Data will hit the throw. The fix
is always to either (a) route through `app.Type[...]` to get a stamped
primitive entity, or (b) stamp the carrying Data with a context before
reading. Don't add `?.` to the fold-property accessors — the throw is
the contract; the dot-chain is defense-in-depth on top.

## `type.@this.Null` — non-null sentinel on `Data.Type`

`Data.Type` is non-null end-to-end. A `Data` with no declared type
returns the singleton `type.@this.Null` (`IsNull = true`,
`ClrType = typeof(object)`) instead of literal null. Three concrete
consequences:

- **Call sites don't null-check `Data.Type`.** Copying
  `dest.Type = source.Type` is unconditional — the setter recognises
  `Null` by `ReferenceEquals(this, Null)` and clears `_type` so the
  sentinel doesn't propagate as a real assignment.
- **Wire skips emission.** `Wire.Write` does not emit a `"type"` field
  for `Data` whose type is `Null` — the on-wire shape matches the
  pre-rename "no type stamped" case, so callbacks signed under the old
  shape still verify.
- **Don't construct `new type.@this("null")` to mean Null.** That gives
  you a real entity named "null", not the sentinel. The codeanalyzer v4
  F1 note (auditor v1 F2) tracks the latent footgun where
  `IsNull => Value == "null"` is string-magic instead of
  `ReferenceEquals`; a future coder pass will tighten this. Today, the
  only correct way to get the sentinel is `type.@this.Null`.

## `dict.@this` and `list.@this` — native PLang collection types

`dict.@this` (`PLang/app/type/dict/this.cs`) and `list.@this`
(`PLang/app/type/list/this.cs`) are the native PLang value types for
objects and arrays. They are symmetric peers:

- **`dict`** — an ordered key→Data map (`_entries` list + `_index`
  dictionary for O(1) case-insensitive lookup). Implements
  `IBooleanResolvable` (empty = falsy) and `IEquatableValue` (structural,
  order-insensitive). Does **not** implement `IOrderableValue` — dict is
  equality-only; `Compare.Order` throws for it.
- **`list`** — an indexed sequence of Data rows. Implements
  `IBooleanResolvable`, `IEquatableValue` (element-by-element), and
  `IOrderableValue` (lexicographic). Also implements `IListLeaf` (see
  below).

**Collections hold Data end-to-end.** An element stored inside a dict or
list keeps its own type-tag and signature; nothing is decomposed to a raw
CLR scalar on entry. Nested access (`%obj.key%`, `%list[0]%`) retrieves
the full Data, so type and signing round-trip through nested structures.

**`[JsonConverter]` governs the raw STJ view only.** The `application/json`
channel, snapshot-clone round-trips, and debug display use the converter;
the `application/plang` wire never does — there, values ride through
`Data.Normalize` → the json.Writer's dict/list arm. The converter is not
a violation of "domain types ride the wire as property bags" — it is the
raw-json view only, not the wire path. Without it, STJ would reflect the
C# `_entries`/`_items` surface instead of the PLang key/element layout.

**`dict.ToRaw()` / `list.ToRaw()`** unwrap to `Dictionary<string, object?>`
and `List<object?>` at the typed-conversion boundary (record construction,
`set ... type=json`, wire-shape reconstruction). The in-memory graph stays
Data-keyed; `ToRaw()` is the read-out form only, not a mutation path.

## `Compare` — single typed-compare mediator

`app.data.Compare` (`PLang/app/data/Compare.cs`) is the one place where
two values are ordered or tested for equality. Both the condition operators
(`>`, `<`, `==` via `app.module.condition.Operator`) and `list.sort` route
through it — so `if a.age > b.age` and `sort by "age"` can never drift.

The mediator owns three things only:

1. **Null policy** — both null = equal; null sorts last.
2. **Coercion** — `Operator.NormalizeTypes` (numeric widening,
   string↔number) before hitting the scalar leaf.
3. **Dispatch** — if the left value implements `IEquatableValue` or
   `IOrderableValue`, call it; otherwise forward to `ScalarComparer`.

`ScalarComparer` (`PLang/app/data/ScalarComparer.cs`) is the one legal
type-switch over raw CLR scalars (number, string, datetime, duration, bool).
It is internal; only `Compare` reaches it. A value type that owns its own
ordering/equality implements the interface and recurses **back through
`Compare`** for its children — so a nested number inside a list still
widens, and nested text still compares case-insensitively.

**`Compare.NotOrderableException`** is raised for an equality-only type
(dict has no natural ordering) or two genuinely different value types. It
derives from `ArgumentException` so the condition evaluator surfaces it as
a clean `EvaluationError` rather than an unhandled exception.

**Don't add type-specific cases to `Compare` itself.** A new value type
that has a natural order implements `IOrderableValue`; the mediator
dispatches automatically. Adding an `is MyNewType` arm to `Compare` is
the smell — it violates OBP Rule #9 (behavior belongs on the value, not
a dispatcher).

## List chunk/row model and `IListLeaf`

`list.@this` stores elements as **rows** (`_items: List<Data>`), but
exposes a **flattened** public surface (`Count`, `Items`, `At`, `Locate`).
A row whose value implements `IListLeaf` (currently only `list.@this`
itself) contributes its leaves to the flat view; a scalar, dict, or table
row is one item at weight 1.

`Add` is O(1) — it appends a new row without reading the existing rows.
The flat view is computed on demand by walking rows. This means:

```
add [1,2,3] to %x%   // one row, dissolves → Count = 3 in flat view
add {a:1} to %x%     // one row, stays whole → Count +1
```

**`IListLeaf`** (`PLang/app/data/IListLeaf.cs`) is the interface a value
implements to say "I dissolve into my container list." The only implementer
is `list.@this` — adding a nested list to a list flattens it; adding a
dict stays whole. The container never asks `is list.@this` — it dispatches
to `IListLeaf.LeafCount` / `IListLeaf.Leaves`. A `table`, `dict`, or any
future collection type that wants to stay whole simply does not implement
the interface.

**List-in-list aliasing prevention (`CopyStructure`).** When `add %src%
to %target%` would store a nested list, `list.@this.CopyStructure` makes a
shallow copy (deep only for nested lists, not nested dicts). This prevents
the most common write-through: mutating the source list after adding it to
a container. Dict elements **share by reference** inside the copy — `set
%d.x% = 5` after `add %d% to %list%` mutates the shared dict. This is
consistent with JS/Python object reference semantics and is documented as
intentional; a future copy-on-write pass (branch `collections-are-data`
todo, auditor O1) will close it uniformly.

## `datetime` navigable members — `%Now.Date%`, `.TimeOfDay`, `.Offset`, `.Ticks`

`datetime.@this` exposes a set of properties that navigate to their own PLang types via dot-notation:

| Member | PLang type | C# backing |
|--------|-----------|------------|
| `.Date` | `date` | `DateOnly.FromDateTime(Value.Date)` |
| `.TimeOfDay` | `time` | `TimeOnly.FromTimeSpan(Value.TimeOfDay)` |
| `.Offset` | `duration` | `Value.Offset` |
| `.Ticks` | `number` (long) | `Value.Ticks` |
| `.Millisecond` | `number` (int) | `Value.Millisecond` |
| `.DayOfYear` | `number` (int) | `Value.DayOfYear` |
| `.DayOfWeek` | `number` (DayOfWeek enum) | `Value.DayOfWeek` |

These are C# properties on `datetime.@this`, so PLang's dot-navigation (`%Now.Date%`, `%Now.TimeOfDay%`, `%Now.Ticks%`) reaches them through the normal `Data.GetChild()` path. Each returns a born-typed Data wrapping the corresponding PLang type — `%Now.Date%` is a `date`, `%Now.TimeOfDay%` is a `time`, `%Now.Offset%` is a `duration`. Scalar members (`.Ticks`, `.Millisecond`, `.DayOfYear`, `.DayOfWeek`) return a `number`.

**`Data.Clr<T>(fallback)`.** Added in this branch as an async companion to `item.Clr<T>()`. Used by system-variable reads and handlers that need a typed CLR value out of a `Data` without crashing on absent entries: `await data.Clr<int>(0)` returns `0` if the slot is null/absent, or the extracted CLR `int` otherwise. The sync `item.Clr<T>()` / `item.Clr(Type)` family (base throws, each type hands its backing through `ClrConvert`) remains the leaf door; `Data.Clr<T>(fallback)` is a convenience wrapper that peeks the underlying item and falls back to the provided default.
