# Coder — compare-redesign — summary (latest state)

**Version:** v8 (continuing slice 2b consumer tail)

## What this is

Stage 9 "born-typed values" + slice 2b/2c consumer tail. Every value is its own
typed instance; `Data` is a thin binding (one typed instance + name/properties/
signature); consumers never branch on raw CLR shape. The typed ask is `T.Create`
(the TARGET constructs itself).

## Latest change — Value<T>() / Create return the INSTANCE, not a Data<T>

Full detail: `v8/value-t-instance-refactor.md`. In short:

- `ICreate<T>.Create(item, asking)` → `TSelf?` (the instance). Pass-through returns
  the *same* instance (zero alloc); decline lands on `asking.Fail` and returns null.
- `Data.Value<T>()` → `T?` = `T.Create(await Value(), this)`. `Data<T>.Value()` is
  just that — the old allocate-a-Data<T>-then-unwrap round-trip is gone.
- `Variable.Create` is **pure pass-through-or-decline** — no `Resolve`, no
  `ToString()` reparse. A variable is born a Variable at the wire boundary
  (`type.Judge` for a `type:variable` param); reparsing-at-ask magic deleted, and
  the `IRawNameResolvable` reflection carve-out in `Value<T>()` deleted.
- The slot stays `Data<T>`, formed once at the dispatch boundary:
  `__d.ShallowClone<T>(await __d.Value<T>())` (the CLR can't re-view base→`Data<T>`
  free). `CloneError<T>` deleted; `ShallowClone<T>(null)` carries the decline error.

## Deep-resolve root — raw-container Lift bridge (temporary)

Nested `%var%` inside a container only deep-resolves when the container is **native**
(`list.@this`/`dict.@this`, door recurses). A raw C# `List<object?>`/`Dictionary`
parked as a `clr` carrier (no-op door) never rendered its holes. Real params are always
native off the wire; raw containers only enter via C# composition (tests) + a few
result/stored-value seams. Per Ingi: no silent global conversion — added a guard at
`Lift` (the throw is kept **commented**, restored when the seams go born-native) and a
**temporary** Lift bridge that converts a raw container to native via
`json.Parse(SerializeToElement(v))`. Marked `TODO(remove)` + `todos.md`.

## Born-Variable test fidelity + UnknownType '' root fix

Making `Variable.Create` pure regressed tests whose hand-built `variable.set` steps
lacked `type:variable` on `Name` (the builder always stamps it). Fixed the seams to
mirror the builder: shared `PrParam.List`/`IsVarNameSlot`, `TestAction.Create`,
`MakeStep` helpers, and the `.pr` fixtures. Then fixed a real pre-existing bug those
tests unmasked: `variable.set` gated its optional `as` type on `typeValue != null`,
but an omitted clause is the non-null `absent` citizen → minted an empty-name type
(`UnknownType ''`). Now gated on `Type.IsEmpty()`.

## Result

C# suite, baseline c026ff245 → now: Modules 230→145, Data 144→105, Wire 60→40,
Types 47→29, Runtime 76→70, Generator 12→7. **Net −173** (−171 net of the 2
documented action-template regressions). Total failing 569 → 396.

Further root fixes after the −104 mark:
- **variable.set no-type path collapsed to one line** (`Context.Variable.Set(name.Name, Value)`)
  — caught a widespread empty-value bug: `ShallowClone(name)` bound to the GENERIC
  `ShallowClone<Variable>` (name is a Variable), cloning the name as the value (~26 tests).
- **text.Value method-group leaks** (`Func→text`) — `text.Value` was deleted; three prod
  Convert sites read it and got the door method group. Fixed to `Clr<string>()`.
- **wire Normalize** — a nested Data now rides AS the dict entry (`Entry` helper), not
  re-boxed (killed the "bare Data stored as value" throws). NOTE: architect is reworking
  Normalize for laziness (drop the eager copy for native containers) — leave it.
- **born-Variable test fidelity, completed** — `PrParam` now covers `variable.set` name +
  `loop.foreach` ItemName/KeyName; modifier/condition inline PrActions + `.pr` fixtures stamped.
- AsT identity tests migrated to the instance contract.

Two action-template tests regressed from the bridge (`DataWrappedActionList_*`) — the
bridge flattens action templates and the stamp walker over-resolves deferred sub-action
`%var%`. Documented in `todos.md`; fixed for free by the template-ownership design
(`v8/template-ownership-proposal.md`, for architect) which removes the walker.

## What's next (remaining tail roots — diverse, each needs fresh investigation)

- **"Expected failure but Data succeeded with value: null"** (~17 in Modules) — diverse
  actions (identity Archive/Create-duplicate, crypto Hash-null, llm Query API-errors)
  whose error paths return Success. Likely several distinct pre-existing roots, not one.
- **lazy/signed wire round-trip** — bare-Data throws gone, but these chain to the next
  wire/signing step (key-BLOB SigningError, signature survival). Architect reworking Normalize.
- `clr`-unwrap (`Lower`/ArrayList/Hashtable, Decompress source.Clr-on-Func),
  `As<T>` CLR reconstruction (RecordWithPositionalCtor, NoParameterlessCtor),
  snapshot-`clr`-wire (Wire), generator `IsNotNull`/sensitive-snapshot.

## Slice-2b structural — progress

- ✅ **`item.ToRaw` DELETED** at every visibility. The CLR edge is `Clr(Type)` alone:
  base defaults to `ClrConvert(Peek(), target)` (every type answers); `dict`/`list`
  own their decompose inline; recurrence pin `GenericToRaw_DoesNotExist_OnItemBase`.
  Exit gate `\.ToRaw()` → 0. Zero regressions.
- ✅ **`IsRef`→`item` virtual** — `Data` no longer does `_type is text t && t.IsRef()`;
  `Data.IsVariable`/`AsCanonical` ask `_type.IsRef(out name)`.
- ⬜ **`Peek()` → `item?`** — stage-deferred (entangled with the `clr` rung-2 carrier).
- ⬜ **`set.cs` `as`-block collapse** — blocked on the "type-entity lift at entry" seam.
- ⬜ **`is/as` exit-gate sweep (~68 left)** — NOT mechanical; it's effectively the
  **stage-7 surface-typing** work. Per category: `(await Value()) as Y` → the real fix
  is `Data<Y>` params (a bare `Value<Y>()` swap flips cast→convert and can turn a
  silent null into an action-failure); `Peek() as X` is sync (no door); `is text
  {Template}` carries a `skipInfrastructure` policy; `EnumerateItems` dict/list ladder
  couples the scalar arm to `Data` identity + `loop.foreach` semantics. Do as a
  dedicated focused pass (per-handler, dispatch-affecting).

## Design hand-offs to architect
- template-ownership (`v8/template-ownership-proposal.md`)
- lazy-streaming wire writer / drop the Normalize eager copy (in discussion)

Design hand-offs to architect: template-ownership (`v8/template-ownership-proposal.md`).
Deferred (todos.md): typed-null slot citizen `@null.@this<T>`; remove the Lift bridge.
