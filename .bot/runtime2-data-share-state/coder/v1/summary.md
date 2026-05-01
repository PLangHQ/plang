# coder v1 — Data identity preservation

## What this is

Architect/v1 redesigned the `Data` identity model to fix a silent class
of bugs: today's `Data.As<T>()` always allocates a fresh wrapper via
`ConvertAndWrap` even on the same-type fast path, dropping the source's
`Properties`, event subscribers, and memory identity. That violates the
"every plang variable IS Data" principle.

The architect's fix: cross-type views (`As<T>`) become LIVE windows into
the same variable — `Properties` and the three event lists share by
reference; only `Type` and the converted `.Value` differ. `variable.set`
becomes the **sole binding-mint site** with explicit clone semantics.

The 6-phase plan touched ~30 files. This branch lands phases 1, 2, 3, 4
and the spot-check tests for phase 5a — the foundation. Phases 5b, 5c,
and 6 (handler migrations off `[VariableName]` and the Legacy emission
delete) are documented as deferred — they need a rebuilt `.pr` set,
which requires LLM access not available to me.

## What was done

### Phase 1+2 — Data foundation (commit `46b327c5`)

`PLang/App/Data/this.cs`:
- Events from C# `event Action<...>` to `public List<Action<...>>` so cross-type wraps can share list refs.
- `FireOn{Change,Create,Delete}` iterate the list; `CopyEventsFrom` deleted.
- `Properties` field initializer (not constructor body) so object-initializer assignment wins for identity-preserving wraps.
- `IsPlangIterable(value)` and `IsPlangAssignable(target, source)` — single source of truth for the string-not-iterable rule. `AsEnumerable` and `EnumerateItems` route through `IsPlangIterable`.
- `TryFullVarMatch(value, out varName)` — shared regex helper for `^%([^%]+)%$` (used by both `AsT_Impl` and `AsCanonical`).
- `AsT_Impl<T>` full-match recursion now invokes `resolved.AsT_Impl<T>(...)` so `this` inside the recursion IS the live variable. `Name` propagates from canonical.
- `WrapAs<T>(value, ctx)` — the four identity rules:
  1. Same-type: returns `this` (no allocation).
  2. Variance fast path: `IsPlangAssignable(typeof(T), value.GetType())` → new `Data<T>` with cast-only `.Value`, state aliased from `this`.
  3. Cross-type: `T == IEnumerable` delegates to `AsEnumerable` (Phase 2d); else `TypeMapping.TryConvertTo`. State aliased.
  4. Conversion failure: `FromError` sentinel, no aliasing.
- `AsCanonical(ctx)` — new method for plain-Data slots (Phase 2 Rule 4). Full-match `%var%` returns the LIVE variable Data; literal returns `this`; partial wraps the resolved string with slot Name + aliased state.

`PLang.Generators/Emission/Property/Data/this.cs`:
- Plain-Data slot emission switches from `As<object>(Context)` to `AsCanonical(Context)`.

`PLang/App/Debug/this.cs`:
- Four debug subscribe sites switch from `+=` to `.Add(...)` for the new `List<Action<...>>` shape.

`PLang/App/Variables/this.cs`:
- Drop the dead `CopyEventsFrom` call. Comment cleanup.

### Phase 3 — Variables.Set is dumb storage (commit `db0c7ecc`)

Per Ingi: clone semantics for `set %x% = %y%` live in the **handler**
(Phase 4); `Variables.Set` itself does NOT merge prev's state onto dv on
replacement. The architect's "alias prev events" idea was dropped.

`PLang/App/Variables/this.cs`:
- `Remove(name)` now fires `OnDelete` on the removed Data so debug subscribers see the deletion.
- `Set` replacement comment clarified as dumb-storage shape (no event merging).

### Phase 4 — variable.set is the sole binding-mint site (commit `1a55b0ff`)

`PLang/App/modules/variable/set.cs`:
- Rewrote `Run()` per architect/v1 §Phase 4.
- Type inference via `MintTyped(name, raw, ctx)` — switch on runtime type. Hot types (string, int, long, double, bool, decimal, float, DateTime, DateTimeOffset, Guid, byte[], List, Dict) take the if-chain; cold types fall through to reflection (`typeof(Data<>).MakeGenericType`).
- Mutable refs (`List<object?>`, `Dictionary<string,object?>`) snapshot-cloned via JSON roundtrip.
- **Fixed a JsonElement regression** discovered during this work: `JsonSerializer.Deserialize<List<object?>>(...)` produces `List<JsonElement>` (not primitives). Routed through `Data.UnwrapJsonElement` to recursively normalize.
- Forced `[Type]`: explicit conversion via `TryConvertTo`. Failure → `Data.FromError`.
- Source-clone semantics: when value comes from a Data source, `CarryStateFromSource` copies `Properties` (deep clone) and event subscribers (shallow clone of delegate lists) onto the new dv. Independent from source after mint.

### Phase 5a — foreach (commit `a5786f09`)

The `loop/foreach.cs` handler **didn't need code changes**: Phase 2c's
`IsPlangIterable` carve-out (already in `EnumerateItems`) gives foreach
over strings/scalars its single-iteration shape. Three new tests pin the
behavior. The architect's Pattern B migration for `ItemName`/`KeyName`
(`[VariableName] string` → `Data<string>`) needs rebuilt `.pr` files —
deferred.

Brought back online (and confirmed green):
- `Modules/Loop/Foreach/Dictionary/ForeachDictionary.test.goal`
- `Modules/Test/Discover/TestDiscoverReportsStaleWhenPrMissing.test.goal` (both Modules/* and TestModule/* paths)
- `Modules/Test/Integration/*` (4 tests, both paths)
- `Modules/Test/Report/*` (5 tests, both paths)

The two TestDiscoverReportsStaleWhenPrMissing variants needed their
fixture `_stale/.build/unbuilt.fixture.pr` removed — the test asserts
that fixture has NO `.pr` file, but a `.pr` had been built and committed
previously.

## Tests written / filled

### C# (62 stubs from test-designer became real tests)

| File | Tests | Status |
|------|-------|--------|
| `App/DataTests/EventListTests.cs` | 6 | ✅ |
| `App/DataTests/AsTIdentityTests.cs` | 10 | ✅ |
| `App/DataTests/NamePropagationTests.cs` | 6 | ✅ |
| `App/DataTests/PlangAssignabilityTests.cs` | 8 | ✅ |
| `App/VariablesTests/SubscriberSurvivalTests.cs` | 8 | ✅ — REWRITTEN per Ingi (no event-list aliasing on replacement; pins dumb-storage behavior; `Remove_FiresOnDelete` added) |
| `App/Modules/variable/SetTypeInferenceTests.cs` | 12 | ✅ |
| `App/Modules/loop/ForeachStringNotIterableTests.cs` | 3 | ✅ |
| `App/Modules/list/ListAddIdentityTests.cs` | 4 | ❌ STUB — needs Phase 5c (deferred) |
| `Generator/Diagnostics/Plng001PostMigrationTests.cs` | 5 | ❌ STUB — needs Phase 6 (deferred) |

### plang `.test.goal`

C# baseline: **2524/2533** (9 remaining are forward-looking stubs).
plang baseline: **166/166 green** (started at 145; brought back 21 tests).

## Code example — the As<T> identity-preserving wrap

The contract (architect/v1 §Phase 2):

```csharp
// Source: Data<List<int>> with Properties + subscribers.
var source = new Data<List<int>>("nums", new List<int> { 1, 2, 3 });
source.Properties.Set("annot", "labeled");
source.OnChange.Add((o, n) => Log(n));

// As<IEnumerable> — variance fast path. New wrapper instance, but state aliased.
var wrapped = source.As<IEnumerable>();
Assert.True(ReferenceEquals(wrapped.Value, source.Value));        // ref shared
Assert.True(ReferenceEquals(wrapped.Properties, source.Properties)); // ref shared
Assert.True(ReferenceEquals(wrapped.OnChange, source.OnChange));     // ref shared

// Same-type fast path — returns source as-is, no allocation.
var same = source.As<List<int>>();
Assert.True(ReferenceEquals(source, same));

// Plain Data slot bypass — AsCanonical returns the live variable Data
// for full-match %var%, parameter Data for literals.
var paramData = new Data("Slot", "%nums%");  // Variables.Set(source) elsewhere
var canonical = paramData.AsCanonical();
Assert.True(ReferenceEquals(canonical, source));  // live variable
((List<int>)canonical.Value!).Add(4);             // mutates source.Value
```

## Why Phases 5b+5c+6 deferred

The architect's Pattern B migration replaces:
```csharp
[VariableName] public partial string ListName { get; init; }   // OLD
```
with:
```csharp
public partial Data.@this<string> Name { get; init; }   // NEW Pattern B
```

Two blockers:

1. **Property name → `.pr` param name.** The generator does
   `ParamName = Name.ToLowerInvariant()`. Today's `.pr` has
   `"name": "ListName"` for `list.add`. The new shape would name it
   `"name"`. Existing `.pr` files break.

2. **Pattern B value semantics.** The slot value is the BARE NAME
   string — the architect's plan: ".pr stores the bare name (no
   percents); LLM examples teach the bare-name convention." But
   existing `.pr` files store `"%products%"`. Reading via
   `Data<string>.As<string>(Context)` would try to RESOLVE `%products%`
   as a variable lookup, not return the literal name. Either rebuild
   the `.pr` files (LLM) or special-case the resolution to strip `%`.

   Plus: per Ingi (2026-04-30), the property name `Name` on `variable.set`
   stays as-is — don't rename to `Variable`. So at minimum the variable.set
   handler keeps the `Name` slot under the new shape.

Phase 6 (delete `[VariableName]` + Legacy emission + PLNG001 gate
update) is gated on Phase 5b+5c — `[VariableName]` is still in active
use across ~25 handlers.

A follow-up branch with builder/LLM access can do the rebuild as a
single coordinated commit:
- Update LLM example syntax to bare-name convention.
- Run `plang p build` across all goals.
- Migrate handlers to Pattern A/B in code.
- Delete Legacy emission + `[VariableName]` + `__StripPercent` + `__Resolve<T>`.
- Update PLNG001 gate to allow only `Data<T>`, plain `Data`, `[Provider] T`.

## Tests still sidelined (.goal2)

43 remain. Per Ingi:
- **Out of scope (stays sidelined)**: Modules/Signing/* (10), Modules/Event/* (6), Modules/Identity/* + Identity/* (5), Modules/Crypto/HashBcryptVerify, Modules/Cache/*, Modules/Condition/Compound + Files/*, Modules/Error/*, Modules/Goal/*, Modules/Http/DownloadFile, Modules/Ui/*, Modules/Builder/ValidateValid, App/SetupGoal/, Builder/ForeachCallsGoalPerItem.
- **In scope but deferred**:
  - `Modules/Variable/Set/SnapshotClone.test.goal2` (stub; needs LLM build + the C# `Set_ListValue_MintsDataOfListAndSnapshotClones` test covers same contract)
  - `Modules/Variable/Set/TypeInference.test.goal2` (stub; needs `assert.type` action which doesn't exist + LLM build)
  - `Modules/Variable/ContextVars/Basic/ContextVars.test.goal2` (test references `%!engine.Name%` — no `!engine` system variable exists in code; appears to be testing a feature that was never implemented)
  - `Modules/Loop/Foreach/StringNotIterable.test.goal2` (stub; C# `ForeachStringNotIterableTests` covers same contract)
  - `Modules/List/Mutation/ListAddVisibleAfterCall.test.goal2` (stub; needs Phase 5c)

## Key files modified

- `PLang/App/Data/this.cs` — events→Lists, `WrapAs<T>`, `AsCanonical`, `IsPlangIterable`/`IsPlangAssignable`, `TryFullVarMatch`, `AsT_Impl` rewrite for identity-preservation.
- `PLang/App/Variables/this.cs` — Set comment cleanup; `Remove` fires OnDelete.
- `PLang/App/Debug/this.cs` — `+=` → `.Add(...)`.
- `PLang/App/modules/variable/set.cs` — full rewrite. `MintTyped` if-chain + reflection fallback, `CarryStateFromSource` for clone semantics, `SnapshotClone` via JSON roundtrip with `UnwrapJsonElement`.
- `PLang.Generators/Emission/Property/Data/this.cs` — plain-Data emission uses `AsCanonical`.

## Reviewer-bot suggestion

**codeanalyzer next** — review the `Data.cs` rewrite for OBP compliance
(WrapAs, AsCanonical, ConstructWrap method placement and naming) and
the `variable.set` rewrite (the if-chain switch is a lot of new surface).
Double-check the `JsonElement` snapshot-clone fix isn't masking a deeper
issue elsewhere.

After codeanalyzer: **tester** to confirm the 9 deferred-stub C# tests
are properly stubbed (not silently passing) and the brought-back plang
tests really exercise the new identity-preservation paths.
