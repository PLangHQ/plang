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
