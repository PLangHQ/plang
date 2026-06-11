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

## Result

C# suite, baseline c026ff245 → this change: Modules 230→213, Wire 60→53,
Generator 12→11, Runtime/Types/Data parity. **Net −25, zero regressions.**

## What's next

The deep-resolve / nested-`%var%` family is the next consumer-tail root
(Data ~144, Generator 11 failing — same root). Plus `UnknownType ''` (FileHandler
Integration) and the snapshot-`clr`-wire family (Wire). All pre-existing, separate
roots from this change.

Deferred (todos.md): typed-null slot citizen `@null.@this<T>` (richer absent).
