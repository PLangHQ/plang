# Test Plan v1 — Data Identity Preservation + [VariableName] Migration

Translates `architect/v1/plan.md` (6 phases) into a behavioral test contract. The bulk of the contract is around **identity preservation** — invariants on reference equality of `.Value`, `Properties`, and the three event lists across `As<T>` reads and `Variables.Set` replacements. Get those right and the rest (name propagation, type inference, handler migration) falls into place.

## Coverage map (architect → tests)

| Phase | What changes | Where it gets tested |
|------|---|---|
| 1: events → Lists | `event Action<...>` → `List<Action<...>>` on Data; CopyEventsFrom deleted | `EventListTests.cs` (new) — list shape + multicast + post-add |
| 2a: Name propagation | Full-match resolution propagates the live var's `.Name` | `NamePropagationTests.cs` (new) |
| 2b: Identity rules | Same-type → self; variance → alias; cross-type → wrap; plain Data → canonical | `AsTIdentityTests.cs` (new) — Properties + event-list ref equality |
| 2c: Plang assignability | string-not-iterable carve-out, single-source `IsPlangIterable` | `PlangAssignabilityTests.cs` (new) |
| 2d: AsEnumerable owns conv | `As<IEnumerable>` delegates to `Data.AsEnumerable` | folded into `PlangAssignabilityTests.cs` |
| 3: Variables.Set dumb | Replacement aliases prev's event lists onto dv unconditionally | `SubscriberSurvivalTests.cs` (new) |
| 4: variable.set always-typed | `Set` mints typed Data via GetType + if-chain | `Modules/variable/SetTypeInferenceTests.cs` (new) |
| 5: handler migration | Pattern A (plain Data live ref), B (Data\<string\> name slot), C (Data\<T\> typed value) | spot-check tests on key handlers; existing tests migrated |
| 6: generator + Legacy delete | `[VariableName]` attribute gone; PLNG001 only allows Data\<T\>, Data, [Provider] | `Generator/Diagnostics/Plng001PostMigrationTests.cs` (extend or new) |

End-to-end PLang tests cover the user-visible behavior change: foreach over a string iterates **once**, list.add is visible through the source variable across handler boundaries, set always types correctly, snapshot clone on assign-from-variable.

## Test files (proposed)

**New C# files**
1. `/PLang.Tests/App/DataTests/EventListTests.cs` — Phase 1 — ~6 tests
2. `/PLang.Tests/App/DataTests/AsTIdentityTests.cs` — Phase 2b — ~10 tests
3. `/PLang.Tests/App/DataTests/NamePropagationTests.cs` — Phase 2a — ~6 tests
4. `/PLang.Tests/App/DataTests/PlangAssignabilityTests.cs` — Phase 2c+2d — ~8 tests
5. `/PLang.Tests/App/VariablesTests/SubscriberSurvivalTests.cs` — Phase 3 — ~8 tests
6. `/PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs` — Phase 4 — ~10 tests
7. `/PLang.Tests/App/Modules/list/ListAddIdentityTests.cs` — Phase 5 Pattern A spot-check — ~4 tests
8. `/PLang.Tests/App/Modules/loop/ForeachStringNotIterableTests.cs` — Phase 5 + 2c spot-check — ~3 tests
9. `/PLang.Tests/Generator/Diagnostics/Plng001PostMigrationTests.cs` — Phase 6 — ~5 tests (or extend existing PLNG001 tests)

**New PLang test goals** (under `/Tests/Modules/`)
10. `/Tests/Modules/Variable/Set/TypeInference/TypeInference.test.goal` — set type inference end-to-end
11. `/Tests/Modules/Variable/Set/SnapshotClone/SetSnapshotClone.test.goal` — set %x% = %y%; mutate y; x unchanged
12. `/Tests/Modules/List/Mutation/ListAddVisibleAfterCall.test.goal` — list.add inside called goal visible to caller
13. `/Tests/Modules/Loop/Foreach/StringNotIterable/ForeachStringNotIterable.test.goal` — foreach over string runs exactly once

**Files to migrate** (no new tests, just shape updates as Phase 5 lands)
- `/PLang.Tests/App/Modules/variable/settests.cs` — `Name`/`Type` change shape
- `/PLang.Tests/App/Modules/loop/ForeachTests.cs` — `ItemName`/`KeyName` become `Data<string>`
- `/PLang.Tests/App/Modules/list/*.cs` — `ListName` becomes plain `Data List` (Pattern A) for mutators, `Data<IEnumerable> Collection` for read-only
- Existing `DataAsTResolutionTests.cs` — already passes the resolution contract; verify it still passes under identity-preserving As<T>

## Test batches

I'll present in 8 batches, ~10 tests per batch where feasible.

### Batch 1 — Phase 1 — Event lists (6 tests, all C# in `EventListTests.cs`)

Anchors the contract that subscribers are mutable lists, not immutable multicast delegates. If these don't hold, the alias-on-replacement story in Phase 3 can't work.

```
OnCreate_IsListType_NotEvent
  // OnCreate is exposed as List<Action<Data>>, mutable from outside.

OnChange_IsListType_NotEvent
  // OnChange is exposed as List<Action<Data,Data>>, mutable from outside.

OnDelete_IsListType_NotEvent
  // OnDelete is exposed as List<Action<Data>>, mutable from outside.

FireOnChange_InvokesAllSubscribersInOrder
  // Two subscribers added; FireOnChange invokes both, in insertion order, with (this, newData).

FireOnCreate_SubscriberAddedAfterInit_StillFires
  // Subscriber added after construction sees a subsequent FireOnCreate.

EventLists_TwoDataInstances_HoldDistinctListsByDefault
  // Without aliasing, two fresh Data instances have different OnChange list refs (precondition for the aliasing tests).
```

### Batch 2 — Phase 2b — As\<T\> identity preservation (10 tests, `AsTIdentityTests.cs`)

The core of the architect's redesign. Reference-equality is the contract.

```
AsT_SameType_ReturnsSourceInstance
  // As<int>() on Data<int> returns this — no allocation, ReferenceEquals.

AsT_SameType_PreservesProperties
  // The instance returned IS the source, so Properties is trivially the same ref.

AsT_Variance_ListToIEnumerable_ValueRefShared
  // Data<List<int>>.As<IEnumerable>() — wrapped.Value (IList<int>) is the SAME ref as source.Value.

AsT_Variance_PropertiesAliased
  // Cross-type wrap (variance fast path): wrapped.Properties === source.Properties (ref equality).

AsT_Variance_OnChangeAliased_FireOnSourceVisibleThroughWrapped
  // Subscribe to source.OnChange; fire via wrapped → subscriber sees it (same list ref).
  // Inverse: subscribe to wrapped.OnChange; fire via source → subscriber sees it.

AsT_Variance_PostWrapSubscribe_VisibleThroughBothRefs
  // After wrap, add a subscriber via wrapped.OnChange. Source.OnChange contains it.
  // Proves the lists are aliased, not copied at wrap time.

AsT_CrossType_ConversionWraps_PropertiesAliased
  // Data<int>(42).As<string>() — converted Value="42" (or whatever TypeMapping produces),
  // wrapped is a NEW Data<string>, but Properties + event lists alias source.

AsT_CrossType_ConversionFailure_ReturnsFromError_NoAlias
  // Conversion failure returns Data<T>.FromError(error). No aliasing, no leaked subscribers.
  // Existing __resolutionError post-Run capture catches it; test verifies the no-alias shape.

AsT_PlainDataTarget_LiteralParameter_ReturnsParameterDataAsIs
  // For plain Data property: literal parameter Data flows through as the canonical — no wrap, no copy.
  // Returned == parameter Data (same ref).

AsT_PlainDataTarget_VarReference_ReturnsLiveVariableData
  // For plain Data property: when value is "%var%", returned Data IS the live variable Data — same ref.
  // Mutations to .Value via the returned Data are visible through Variables.Get(name).
```

### Batch 3 — Phase 2a — Name propagation (6 tests, `NamePropagationTests.cs`)

Param Data carries a slot name; full-match resolution swaps in the live variable's name. Partial / literal / unset cases keep the slot name.

```
Name_FullVarMatch_PropagatesLiveVariableName
  // Param Data { Name="List", Value="%products%" } — Variables.Set("products", [...]); As<T> → result.Name == "products".

Name_LiteralValue_KeepsSlotName
  // Param Data { Name="Variable", Value="user" } (no %, literal string) — As<T> → result.Name == "Variable".

Name_PartialInterpolation_KeepsSlotName
  // Param Data { Name="Greeting", Value="hello %name%!" } — partial doesn't propagate; result.Name == "Greeting".

Name_UnsetVariable_PropagatesVarName_NotInitialized
  // Param Data { Name="X", Value="%missing%" } — variable doesn't exist.
  // result.Name == "missing" (still propagates), result.IsInitialized == false (or .Value == null per architect).

Name_NestedListResolution_PreservesSlotName
  // Param Data { Name="Items", Value=["a","%b%","c"] } — list resolved, result.Name == "Items" (no full-match).

Name_ChainedFullMatch_PropagatesFinalName
  // %a% → "%b%", %b% → 42; Param Data { Name="Slot", Value="%a%" } resolves through to b.
  // result.Name == "b" (the variable that owns the value).
```

### Batch 4 — Phase 2c+2d — Plang assignability + AsEnumerable single source (8 tests, `PlangAssignabilityTests.cs`)

The string-not-iterable rule must hold in three places (As\<T\> variance check, AsEnumerable, EnumerateItems) by routing through one predicate.

```
IsPlangIterable_String_ReturnsFalse
  // string is IEnumerable<char> in C#, but plang treats it atomic.

IsPlangIterable_List_ReturnsTrue

IsPlangIterable_Null_ReturnsFalse

IsPlangAssignable_StringToIEnumerable_ReturnsFalse
  // Variance fast path SHOULD NOT apply to string→IEnumerable. Conversion path takes over.

IsPlangAssignable_ListToIEnumerable_ReturnsTrue
  // Variance fast path applies — wrap shares .Value ref.

AsT_StringToIEnumerable_WrapsAsSingleElementList
  // Data<string>("hello").As<IEnumerable>() — Value is a one-element array containing "hello",
  // NOT a char-by-char enumeration. Goes through ConvertAndWrap, not variance fast path.

AsT_IntToIEnumerable_WrapsAsSingleElementList
  // Data<int>(42).As<IEnumerable>() → Value enumerates as [42].

AsEnumerable_DelegatesToSharedPredicate_StringNotIterable
  // Data with string value: AsEnumerable() yields the string itself (one element), not its chars.
  // Same predicate result as IsPlangIterable("hello") == false.
```

### Batch 5 — Phase 3 — Variables.Set dumb storage + subscriber survival (8 tests, `SubscriberSurvivalTests.cs`)

Replacement-on-Set must alias prev's three event lists onto the new Data. This is what the `--debug={"variables":[…]}` path depends on.

```
Set_NewVariable_FiresOnCreate_NotOnChange
  // First Set: dv.OnCreate fires, dv.OnChange does not.

Set_Replace_FiresOnChange_OnPrev_WithDvAsNewData
  // Subscribe pre-replacement to prev.OnChange. Replace via Set(newDv). Subscriber receives (prev, newDv).

Set_Replace_AliasesPrevOnChangeOntoDv
  // After replacement: dv.OnChange ref-equals what prev.OnChange was before — subscriber list survives.

Set_Replace_AliasesAllThreeEventLists
  // OnCreate, OnChange, OnDelete all alias from prev to dv on replacement.

Set_PostReplacement_SubscribeViaNewRef_VisibleThroughOldRef
  // After replacement, subscribe via dv.OnChange. Verify the subscriber is also reachable through prev.OnChange (ref-shared).

Set_SameInstance_NoFire_NoAlias
  // Set the same Data twice in a row (ReferenceEquals dv to existing) — no FireOnChange, no relisted alias work.

Set_NonDataValue_WrapsAndFiresOnCreate
  // Set("name", 42) (not a Data) — Variables wraps in Data, fires OnCreate; behavior preserved post-refactor.
  // (Or: if the architect's "drop the wrap-overload" call lands, this becomes Set_NonDataValue_RemovedAfterPhase4 — tbd)

Set_PropertiesNotAliased_NewBindingHasOwnProperties
  // Replacement aliases event lists but NOT Properties (architect's open question default).
  // dv.Properties is its own bag — prev's metadata doesn't carry over.
  // Note: this test pins the architect's stated default. If the design call lands the other way, we update this one test.
```

### Batch 6 — Phase 4 — variable.set type inference (10 tests, `SetTypeInferenceTests.cs`)

`variable.set` is the **sole binding-mint site**. Hot types take the if-chain; uncommon types fall through to reflection.

```
Set_StringValue_MintsDataOfString
  // Stored variable is Data<string> (typed wrapper, not plain Data).

Set_IntValue_MintsDataOfInt
  // ...Data<int>.

Set_LongValue_MintsDataOfLong

Set_DoubleValue_MintsDataOfDouble

Set_BoolValue_MintsDataOfBool

Set_DateTimeValue_MintsDataOfDateTime

Set_ListValue_MintsDataOfListAndSnapshotClones
  // Stored variable is Data<List<object?>>.
  // AND: x.Value is NOT the same reference as the list we passed in — snapshot-cloned.

Set_DictValue_MintsDataOfDictionary
  // Same — Data<Dictionary<string,object?>>, snapshot-cloned.

Set_ForcedType_String_ConvertsAndMintsDataOfString
  // set %n% = "42" with Type="string" → Data<string> "42". Type=int branch tested separately.

Set_ForcedType_ConversionFailure_ReturnsError
  // set %n% = "abc" with Type="int" — conversion fails, handler returns Data with Error (Success=false).

Set_NullValue_MintsPlainDataNotGeneric
  // null value can't be type-inferred; stored as plain Data (or Data<object?> per architect's null arm).

Set_AsDefault_ExistingInitialized_DoesNotReplace
  // Already covered in current SetTests; reaffirm here so the new code path doesn't regress.
```

(Goes slightly over 10 — null + AsDefault are short, both worth pinning.)

### Batch 7 — Phase 5 — Handler migration spot-checks (~7 tests across 2 files)

Pattern A (live var) and the foreach string-not-iterable case carry the most contract weight.

`/PLang.Tests/App/Modules/list/ListAddIdentityTests.cs` — 4 tests
```
ListAdd_PlainDataList_MutatesLiveVariableValueDirectly
  // After list.add, Variables.Get("products").Value contains the appended item.
  // No Variables.Set call needed inside list.add — the ref is shared.

ListAdd_ReturnsLiveVariableData_NotNewData
  // Handler return Data IS the live variable's Data (ReferenceEquals).

ListAdd_ItemAsLiveVarRef_AppendsCurrentValue
  // %item% resolves to its current Variables.Get; list contains that value.

ListAdd_AfterReplacement_HandlerSeesNewValue
  // Replace %products% via Set; subsequent list.add appends to the new list, not the stale one.
```

`/PLang.Tests/App/Modules/loop/ForeachStringNotIterableTests.cs` — 3 tests
```
Foreach_StringCollection_RunsBodyExactlyOnce
  // Collection="hello"; body increments counter; counter == 1 after loop.
  // Without the carve-out it would be 5.

Foreach_StringCollection_BodyReceivesWholeString
  // Inside body, %item% == "hello" (whole string), not 'h'.

Foreach_NumberCollection_RunsBodyOnceWithNumber
  // Collection=42 (single int); body runs once with item=42. (sanity for non-iterable scalar fallback)
```

### Batch 8 — Generator (Phase 6) + end-to-end PLang tests (~5 + 4)

`/PLang.Tests/Generator/Diagnostics/Plng001PostMigrationTests.cs` — 5 tests
```
PLNG001_DataT_Property_NoDiagnostic
  // public partial Data<int> Count → no diagnostic.

PLNG001_PlainData_Property_NoDiagnostic
  // public partial Data List → no diagnostic.

PLNG001_ProviderProperty_NoDiagnostic

PLNG001_VariableNameAttribute_NowReportsDiagnostic
  // After Phase 6, [VariableName] string is no longer a valid shape.
  // Generator emits PLNG001 — was previously allowed.

PLNG001_RawScalar_StillReportsDiagnostic
  // public partial int Count (no Data wrap, no Provider) → PLNG001.
```

PLang `.test.goal` files — 4 tests, one per file
```
TypeInference.test.goal
  / set %s% = "hello"; assert type of %s% is string. set %n% = 42; assert type of %n% is long (or int).
  // End-to-end check that variable.set's type inference reaches user-observable type queries.

SetSnapshotClone.test.goal
  / set %a% = ["x"]; set %b% = %a%; add "y" to %a%; assert %b% does not contain "y".
  // End-to-end snapshot clone — b's list is independent of a's after assignment.

ListAddVisibleAfterCall.test.goal
  / set %products% = []; call AddProduct; assert count of %products% is 1.
  / AddProduct: add "apple" to %products%
  // Live-ref pattern A: list mutation inside a called goal is visible to the caller.

ForeachStringNotIterable.test.goal
  / set %s% = "hello"; set %count% = 0; foreach %s%, call Inc; assert %count% equals 1.
  / Inc: set %count% = %count% + 1
  // foreach over a string runs exactly once, not five times.
```

## Risks / open items

- **Phase 3 open question** (architect/v1/plan.md:284,290): does Set replacement also alias `Properties`? My batch 5 pins the architect's stated default (no — Properties is per-binding metadata; events are per-name). If the design call lands the other way, that test gets flipped.
- **PLang assertion of "type of"** — assert with type queries needs to be expressible. If there isn't a clean PLang assertion for "type is X", the TypeInference end-to-end may need to fall back to behavioral checks (compute on it as if string vs int) or be replaced by a C# integration test.
- **ListAddVisibleAfterCall** depends on goal call passing the same Variables instance (which it does, per current architecture). Worth one C# spot test that the Pattern A live-ref behavior doesn't regress if scoping changes.
- I am NOT writing tests that pin "exactly N allocations" — that ties the implementation. Tests assert ref-equality on Properties/event-lists/.Value where appropriate, and inequality where the architect requires a fresh wrapper.
- Existing tests under `DataAsTResolutionTests`, `VariablesTests`, `ListSetTests`, `ForeachTests` will need shape migration (handler property types) once Phase 5 lands — those changes are in the coder's scope, not new tests here.

## Output check

- All C# tests use TUnit (`[Test]`, `await Assert.That(...).IsEqualTo(...)`).
- Bodies stub with `Assert.Fail("Not implemented");`.
- PLang tests use `- throw "not implemented"` placeholder bodies; comments above each step describe the intent (the spec).
- Test names: `MethodOrBehavior_Scenario_ExpectedResult`.

## Approval gate

I will present each batch one at a time for approval, take feedback, then write all approved test files at the end.
