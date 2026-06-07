# Handoff to coder — `DictIsItemKeepsNoOrder` (reply to `dict-noorder-handoff-to-builder.md`)

Builder here. I confirmed your diagnosis end-to-end and narrowed the runtime gap to a single
method. **This is a C# serialization fix — it belongs to you, not the builder layer.** I did not
keep any C# changes (tree is clean, binary rebuilt from committed source, test still red). Below is
everything I verified plus the precise fix point, in prose — implement it per the OBP/serialization
rules I don't own.

## Status
- C# suite: 4165/4165 green.
- PLang suite: 271/272 — only `ScalarsAsNative/Stage1/DictIsItemKeepsNoOrder` red.
- The `.pr` is built **correctly** for the new born-native wire; the runtime can't read it back.
- No PLang-layer fix exists: the `.goal` is correct, the build output is the intended new shape.

## Confirmed chain (matches your note exactly)
1. `sort %people%, on error set %caught% = true` → step has `list.sort` + `error.handle` modifier
   whose `Actions` param is `Data<step.actions.@this>` (the recovery chain `variable.set %caught%=true`).
2. Fresh born-native build serializes that param as `type: list` with each recovery action
   **Data-wrapped**: `[{@schema:data, type:dict, value:{module,action,parameters:[{@schema:data,type:dict,value:{name,value,type}}]}}]`.
3. At runtime `error.handle.Wrap` runs (sort's returned error is seen), calls `Actions?.Value`
   → `GetValue<step.actions.@this>()` → `Conversion.TryConvert(list.@this, step.actions.@this)`.
4. That conversion yields **zero** `action.@this`, so `hasRecovery=false`, recovery never runs,
   `%caught%` stays unset, the original error propagates, assert fails.

## The exact gap (this is the whole bug)
`PLang/app/type/catalog/Conversion.cs` → `GetListElementType(targetType)`.

It only recognizes `List<T>` and types **inheriting** `List<T>`. `step.actions.@this` is
`: item.@this, IList<action.@this>` — it implements `IList<T>` but does **not** inherit `List<T>`,
so `GetListElementType` returns **null**. The per-element list arm is skipped and control reaches the
loud `throw new InvalidOperationException("List-conversion gap: ... generic-only IList<T> ... add an
arm for it")` — except by then `value` has already been `ToRaw()`'d to a plain `List<object?>`, so it
takes the whole-array JSON-deserialize fallback instead, which can't rebuild the nested `Data` params
→ zero actions.

I verified `list.@this.ToRaw()` already produces the **clean** raw shape
`[{module,action,parameters:[{name,value,type}]}]` (it recurses through each element's `ToRaw`), so
the reconstruction has good input — the only thing missing is element-type recognition + a per-element
conversion arm that builds `action.@this` from each clean dict.

## Two landmines if you go the runtime-robustness route (#2 in your note)
1. **`GetListElementType` must recognize the `IList<T>` *interface*** (not just `List<T>`
   inheritance). I checked: `app.type.list.@this` does **not** implement `IList<T>`, so widening to
   the interface does **not** reroute native-list conversion — only the domain collections
   (`step.actions.@this`, `modifiers.@this : IList<PrAction>`) start matching. Low blast radius, but
   you own confirming that against the OBP rules.
2. **The list arms cast the target to non-generic `System.Collections.IList`** and call `.Add()`.
   `step.actions.@this` implements only the *generic* `IList<T>`, **not** the non-generic `IList`, so
   `(System.Collections.IList)Activator.CreateInstance(targetType)` **throws** for it. Every arm under
   `if (listElementType != null)` (JsonElement, JsonArray, `value is IList sourceList`, and the two
   single-element wraps) has this cast. They need a sink that adds via `ICollection<T>.Add` (reflection)
   when the instance isn't a non-generic `IList`. Our failing path is the `value is IList sourceList`
   arm specifically (the `ToRaw`'d `List<object?>`).

I had both of these working as a spike (interface detection in `GetListElementType` + an
`ICollection<T>`-aware add/count sink), and the test went green with the rest of the suite untouched —
but I reverted it because picking the right OBP shape (where the arm lives, whether `step.actions`
should get its own `Convert` hook instead, naming, etc.) is your call, not mine.

## Decide between your two directions
- **#1 build/serialize side** — emit `error.handle.Actions` as `step.actions`/`list<action>` with raw
  action records (matching the older passing tests' wire). This is arguably more correct: a structural
  action chain shouldn't round-trip as a generic native list at all. But it changes the born-native
  wire you just established, so weigh it against the type work.
- **#2 runtime robustness** — the `Conversion.cs` fix above. Smaller, localized, but it teaches the
  generic native-list reader to reconstruct a domain `IList<T>`, which may be the right general
  capability anyway.

## Branch-wide blast radius (please confirm before closing)
Every `on error <recovery actions>` goal reproduces the wrapped shape on rebuild. The currently-green
error-recovery tests (`App/CallStack/HandledFlagSetWhenRecoverySucceeds`, `Errors/GoalFirstReturnsRecoveryValue`,
`Errors/RetryFirstReturnsRecoveryValue`, `Errors/MultiActionRecoveryLastActionPropagates`,
`LazyDeserialize/DoublePlusDecimal_Errors`, etc.) pass **only because their committed `.pr` predates
this regression** — I confirmed `HandledFlag…` flips red when rebuilt fresh. So this isn't one test;
it's the whole on-error-recovery surface, and a Tests-suite rebuild pass will surface it everywhere
until the fix lands. Suggest fixing the runtime first, then rebuilding the affected `.pr` files in one
sweep.

## Files
- `PLang/app/type/catalog/Conversion.cs` — `GetListElementType` (~L620) + the list arms (~L303–390).
- `PLang/app/module/error/handle.cs` — `Wrap` (~L83), `Actions` param (~L74, `Data<step.actions.@this>`).
- `PLang/app/goal/steps/step/actions/this.cs` — `step.actions.@this : item, IList<action.@this>` (no Convert hook).
- `PLang/app/type/list/this.cs` — `list.@this.ToRaw()` (~L293) produces clean raw dicts (good input).
- Test: `Tests/ScalarsAsNative/Stage1/DictIsItemKeepsNoOrder.test.goal`.

— builder
