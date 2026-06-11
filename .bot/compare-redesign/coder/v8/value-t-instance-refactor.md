# Value<T>() returns the instance; Create returns the instance; Variable is pure pass-through

## What changed (design, approved by Ingi)

The typed ask `Data.Value<T>()` used to return a fresh `Data<T>` binding (via
`ShallowClone`/`CloneError`). That allocation was wasted: the binding identity is
already `this`, and `Data<T>.Value()` immediately unwrapped the `Data<T>` to read
the instance back out. So:

- **`ICreate<T>.Create(item, asking)` now returns `TSelf?`** (the value instance),
  not `Data<TSelf>`. Pass-through returns the *same* instance (zero allocation).
  A decline lands its reason on `asking` via `asking.Fail(...)` and returns null —
  the error always belonged to the binding the caller already holds.
- **`Data.Value<T>()` returns `ValueTask<T?>`** = `T.Create(await Value(), this)`.
  No allocation, no `ShallowClone` on the read path.
- **`Data<T>.Value()` is now just `Value<T>()`** — the allocate-then-unwrap
  round-trip is gone.
- **`Variable.Create` is pure pass-through-or-decline** — no `Resolve`, no
  `value.ToString()`. A variable NAMES a thing; it is born a Variable at the wire
  boundary (`type.Judge` → `Variable.Resolve` for a `type:variable` param), never
  reparsed from a value at the ask. The reflection carve-out in `Value<T>()`
  (`if (IRawNameResolvable) Create(_type,…)`) is deleted — uniform path.
- **`CloneError<T>` deleted** (its job — landing the decline error — is now
  `asking.Fail` + `ShallowClone` carrying the source error).
- **`ShallowClone<T>(answer)`** now carries the source binding's error onto the
  typed view when `answer == null`, so the dispatch's `.Success` check still works.

## The slot is still `Data<T>` — formed once, at the boundary

A handler's typed slot stays `Data<T>`. The CLR can't re-view a base `Data` as
`Data<T>` without allocating (it's a subclass with zero extra state), so the
*one* legitimate allocation is forming the slot at the dispatch boundary:

```csharp
// generator dispatch (Emission/Property/Data/this.cs)
var __d = __ResolveData("path");
__Path_backing = __d.ShallowClone<text>(await __d.Value<text>());
if (!__Path_backing.Success) __resolutionError = __Path_backing;
```

`await __d.Value<text>()` is the instance ask; `ShallowClone` wraps it under the
slot's binding identity. Same idiom in `channel/serializer/Text.DeserializeAsync<T>`.

## Born-Variable in the real pipeline

The builder stamps `type:{name:variable}` on raw-name params (verified in
`Tests/BuilderSanity/.build/finalize.pr`), so `Judge` births the Variable at
.pr load and `Variable.Create` is pure pass-through. Hand-built test goals must
stamp `type:variable` too (done for FileHandler Integration tests).

## Files

Production: `PLang/app/type/item/ICreate.cs`, `PLang/app/data/this.cs`
(`Value<T>`, `Data<T>.Value`, `ShallowClone<T>`, delete `CloneError<T>`),
`PLang/app/variable/this.cs` (`Create`), `PLang/app/channel/serializer/Text.cs`,
`PLang.Generators/Emission/Property/Data/this.cs` (dispatch).

Tests migrated to the new contract (~90 sites): the `= await x.Value<T>()` slot
form became `x.ShallowClone<T>(await x.Value<T>())` (faithful to the dispatch);
`VariableResolveTests` slot tests now birth Variables; `Read_UnregisteredSchemePath`
runs through `RunGoalAsync` so the dispatch guard surfaces `SchemeNotRegistered`;
`GeneratorValidationTests` + `ResolutionTests` snapshot/concurrency asserts updated.

## Result (C# suite, baseline c026ff245 → this change)

Modules 230→213, Wire 60→53, Generator 12→11, Runtime/Types/Data parity.
**Net −25 failures, zero regressions.** Remaining red is the pre-existing
deep-resolve / clr-unwrap / UnknownType / snapshot-wire families (separate roots).

## Open / deferred

- Typed-null slot citizen `@null.@this<T>` (richer absent) — `todos.md`. The
  Create boundary must stay `T?` (a typed-null can't satisfy a `T` return).
- The deep-resolve / nested-`%var%` family (Data ~144, Generator 11) is the next
  consumer-tail root, untouched here.
