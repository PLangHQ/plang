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

## Result

C# suite, baseline c026ff245 → now: Modules 230→186, Wire 60→47, Generator 12→7,
Data 144→123, Runtime 76→72, Types 47→44. **Net −90.**

Two action-template tests regressed from the bridge (`DataWrappedActionList_*`) — the
bridge flattens action templates and the stamp walker over-resolves deferred sub-action
`%var%`. Documented in `todos.md`; fixed for free by the template-ownership design
(`v8/template-ownership-proposal.md`, for architect) which removes the walker.

## What's next

Remaining tail roots (pre-existing): `clr`-unwrap (`Lower`/ArrayList/Hashtable),
`UnknownType ''` (FileHandler Integration), snapshot-`clr`-wire (Wire), and the
`IsNotNull`/sensitive-snapshot generator cases.

Design hand-offs to architect: template-ownership (`v8/template-ownership-proposal.md`).
Deferred (todos.md): typed-null slot citizen `@null.@this<T>`; remove the Lift bridge.
