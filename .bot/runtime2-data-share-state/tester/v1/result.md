# tester v1 — result

Reviewed: coder/v1 + review-response 60b8d1f3, after codeanalyzer/v2 CLEAN at 8e0a419d.

## Test run

### C# (`dotnet run --project PLang.Tests`)

```
total: 2533
failed: 9
succeeded: 2524
skipped: 0
duration: 11s 838ms
```

The 9 failures are exactly the 9 deferred stubs (4 `ListAddIdentityTests` for Phase 5c +
5 `Plng001PostMigrationTests` for Phase 6). All use `Assert.Fail("Not implemented")` —
honest stubs, not silently passing. ✓ Matches coder's claim 2524/2533.

### PLang (`plang --test`)

```
173 total, 170 pass, 2 fail, 0 timeout, 1 stale, 0 skipped
```

- Failed: `tests/modifiers/PerActionErrorScope.test.goal`,
          `tests/modifiers/OnErrorCallGoal.test.goal`
- Stale:  `.bot/runtime2-settings/scaffolder/v1/tests/plang/Start.test.goal`

The 2 PLang failures are NOT caused by coder/v1. They are stale `.pr` files in
the legacy `tests/modifiers/` directory built against an older `error.handle` /
`goal.call` schema (e.g. `Actor: "this"`, `Goal: {...}` instead of
`Actions: [...]`). The maintained equivalents in `Tests/Modules/Modifiers/` PASS:
- `Tests/Modules/Modifiers/PerActionErrorScope.test.goal` ✓
- `Tests/Modules/Modifiers/OnErrorCallGoal.test.goal` ✓

This is pre-existing stale state, not a regression from coder/v1. Flagged as a
**minor** finding for branch hygiene, not blocking.

The 1 stale entry is from a different branch's bot artifact in `.bot/`.

## Coverage on coder/v1's 7 touched production files

| File | Lines | Covered | % |
|---|---|---|---|
| `PLang/App/Data/this.cs` | 449 | 388 | 86.4 |
| `PLang/App/Variables/this.cs` | 329 | 298 | 90.6 |
| `PLang/App/Debug/this.cs` | 418 | 185 | 44.3 (only ~5 new lines) |
| `PLang/App/modules/variable/set.cs` | 82 | 73 | 89.0 |
| `PLang/App/modules/list/add.cs` | 41 | 23 | 56.1 |
| `PLang/App/Utils/Json.cs` | 120 | 65 | 54.2 (only ~5 new lines) |
| `PLang.Generators/Emission/Property/Data/this.cs` | 51 | 42 | 82.4 |

Coverage is high on the lines coder actually touched. The headline gaps (`list/add.cs`
56% and `Debug/this.cs` 44%) are **not** new code — most of the uncovered lines were
uncovered before this branch.

But there ARE new uncovered branches in coder/v1's diff — see findings below.

## Findings

### What's HONEST about the test suite

- `EventListTests` (6) — `OnCreate/OnChange/OnDelete` are real `List<...>` types with
  ref-distinct defaults. Foundation for aliasing tests is solid.
- `AsTIdentityTests` (10) — every aliasing assertion uses
  `ReferenceEquals(...)` for `.Value`, `Properties`, `OnChange`. Same-type fast-path
  pinned with `ReferenceEquals(source, result)`. Cross-type/conversion-failure isolation
  tested with both positive and negative ref-equality. The contract is strong.
- `NamePropagationTests` (6) — covers full-match, literal, partial, unset-var, nested-list,
  chained-resolve. The chained-resolve test (`%slot% → "%a%" → "%b%" → 42` resolves to
  `b`) is the strongest case.
- `PlangAssignabilityTests` (8) — string-not-iterable carve-out tested at 3 layers:
  predicate (`IsPlangIterable`), `As<T>()` consumer, and `AsEnumerable()` consumer. Single
  source of truth pinned at all three call sites.
- `SubscriberSurvivalTests` (8) — pivoted correctly to the new "dumb storage" semantics.
  `Set_Replace_DoesNotAliasPrevOnChangeOntoDv` and `Set_Replace_DoesNotAliasAnyEventList`
  pin the OPPOSITE of the original architect plan — exactly what the user-corrected design
  requires. `Remove_FiresOnDelete_OnRemovedData` covers the new behavior at
  `Variables/this.cs:357`.
- `ForeachStringNotIterableTests` (3) — checks `loopResult!.itemCount == 1` with
  collection="hello". Deletion test: if `IsPlangIterable` were removed, count would be 5
  and the test would fail. Strong assertion.
- 9 stub tests in `ListAddIdentityTests` and `Plng001PostMigrationTests` use
  `Assert.Fail("Not implemented")` — they fail honestly.

### Findings

#### 1. (major) `MintTyped` cold types in if-chain are uncovered

**File:** `PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs`
**Code:** `PLang/App/modules/variable/set.cs:111–116, 119`

The `MintTyped` switch-expression has hot branches for string, bool, int, long, double,
**decimal, float, DateTimeOffset, Guid, byte[]**, List, Dict, plus a reflection cold
fallback (`_ => ConstructDataOfT`). Coverage shows lines 111 (decimal), 112 (float),
114 (DateTimeOffset), 115 (Guid), 116 (byte[]) all uncovered — none of the 12 tests
in `SetTypeInferenceTests` exercises them. The reflection cold path (line 119) is also
not directly hit.

**Impact:** A regression in any of those 5 type arms (e.g. `byte[]` → `Data<List<byte>>`
because byte[] is also IList) would not be caught. The reflection fallback's
`Activator.CreateInstance` call is the most fragile spot — uncovered.

**Suggestion:** add 5 tests to `SetTypeInferenceTests`:
```csharp
Set_DecimalValue_MintsDataOfDecimal       // value = 12.34m
Set_FloatValue_MintsDataOfFloat           // value = 12.34f
Set_DateTimeOffsetValue_MintsDataOfDateTimeOffset
Set_GuidValue_MintsDataOfGuid
Set_ByteArrayValue_MintsDataOfByteArray   // value = new byte[] { 1, 2, 3 }
```
Plus one test that flows through reflection (e.g. a custom record type). The pattern
is identical to the existing `Set_DateTimeValue_MintsDataOfDateTime`.

#### 2. (major) `list.add` complex-snapshot path is fully uncovered

**File:** `PLang.Tests/App/Modules/list/ListTests.cs`
**Code:** `PLang/App/modules/list/add.cs:53–66`

The new SnapshotClone-via-Data.SnapshotClone path (lines 53–66) is uncovered. All
existing tests in `ListTests.cs` use string values like `"first"`, `"c"` which take the
cheap-clone primitive branch (line 50). No test passes a Dictionary, custom POCO, or
deeply-nested list to `list.add`.

This is **exactly** the path codeanalyzer/v2 flagged (the behavior unification — list
entries now go through `UnwrapJsonElement` after JSON-roundtrip). If this path
regressed back to leaving `JsonElement` graphs, no test would fail.

The catch fallback (lines 59–65, JsonException/NotSupportedException → alias-mode) is
also uncovered. The 4 `ListAddIdentityTests` stubs would cover this when implemented,
but they're deferred to Phase 5c.

**Impact:** The codeanalyzer/v2 behavioral unification (a quiet improvement) is unproven
by tests. A regression to `JsonElement` leaks downstream goes silent.

**Suggestion:** add at least one test that calls `list.add` with a `Dictionary<string,
object?>` value and asserts the stored list entry's `.Value` is `Dictionary<string,
object?>` (NOT `JsonElement`). Example:
```csharp
[Test]
public async Task Add_DictValue_SnapshotCloneUnwrapsJsonElement()
{
    var (context, memory) = CreateContext();
    var dict = new Dictionary<string, object?> { ["nested"] = new Dictionary<string, object?> { ["k"] = 1 } };
    var action = new Add { Context = context, ListName = "items",
        Value = new global::App.Data.@this("", dict) };
    await action.Run();
    var list = memory.GetValue("items") as List<object?>;
    var entry = list![0] as global::App.Data.@this;
    var entryValue = entry!.Value;
    await Assert.That(entryValue).IsTypeOf<Dictionary<string, object?>>();
    var nested = ((Dictionary<string, object?>)entryValue!)["nested"];
    await Assert.That(nested).IsTypeOf<Dictionary<string, object?>>();  // NOT JsonElement
}
```

#### 3. (major) `Variables.Set` dot-path JsonElement-shape regression unprotected

**File:** `PLang.Tests/App/VariablesTests/VariablesTests.cs`
**Code:** `PLang/App/Variables/this.cs:150–172` (now uses `Data.@this.SnapshotClone`)

Same coverage gap as #2 but on the Variables.Set dot-path. The existing
`Set_DotPath_ConvertsListOfObject_ToTypedList` (line 300) does pass a dict-list as
input, but only verifies that `holder.Items[0].Name == "Alice"` — that works under
BOTH the old (JsonElement leak via TypeMapping conversion) and new (UnwrapJsonElement
to Dictionary) behaviors.

**Impact:** Regression to `JsonElement` graph at the Variables.Set dot-path is
undetectable by current tests.

**Suggestion:** add a test that asserts the converted intermediate is `Dictionary`:
```csharp
[Test]
public async Task Set_DotPath_ListOfDicts_NoJsonElementLeak()
{
    var stack = new Variables();
    var holder = new TestItemHolder();
    stack.Set("holder", holder);
    var items = new List<object> { new Dictionary<string, object?> { ["Name"] = "X", ["Score"] = 1 } };
    stack.Set("holder.Items", items);
    // Side-channel: holder.Items now contains TestItem; original `items[0]` shouldn't
    // be a JsonElement-graph view either. The simpler check is the type of the list.
    await Assert.That(holder.Items[0].Name).IsEqualTo("X");
    // Critical: the snapshot clone produced a Dictionary, not JsonElement.
}
```
Implementing this correctly requires an introspection point — possibly easier to test
at the `Data.SnapshotClone(...)` direct level rather than through the dot-path. A unit
test like `SnapshotClone_OfDict_UnwrapsJsonElementsAtAllDepths` would suffice and be
faster.

#### 4. (major) Set.ValidateBuild error-message branch + Run() Unknown-type path uncovered

**File:** `PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs`
**Code:** `PLang/App/modules/variable/set.cs:36–37, 67–70`

- Line 37 returns the validation error string for forced-type conversion failure at
  build time — uncovered. `Set_ForcedType_ConversionFailure_ReturnsError` tests the
  RUNTIME path (line 76–77) but not the build-time validate path.
- Lines 68–70 return `Data.FromError(new ServiceError("Unknown type 'X'", "UnknownType",
  400))` for unknown forced types — uncovered. No test sets `Type="bogus"` to trigger
  this.

**Impact:** Two distinct error paths (build-time validation message + runtime
Unknown-type service error) have no test. A breaking change to the error key
("UnknownType") or message format would slip through.

**Suggestion:** two tests:
```csharp
Set_ForcedType_UnknownTypeName_ReturnsServiceError  // type="bogus" → Error.Key=="UnknownType"
ValidateBuild_ForcedTypeConversionFailure_ReturnsErrorString  // unit test for ValidateBuild
```
Per memory `false_green_techniques.md` — `Success == false` is weak; check `Error.Key`
and `StatusCode` explicitly. The existing `Set_ForcedType_ConversionFailure_ReturnsError`
only asserts `result.Success` is false; doesn't pin the error key. Worth widening that
test too.

#### 5. (minor) `Set_IntValue_MintsDataOfInt` accepts Data<int> OR Data<long>

**File:** `PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs:38–51`

The test sets value=`42` and accepts EITHER `Data<int>` or `Data<long>` because the
JSON pipeline normalizes integers to long. The test name says "MintsDataOfInt" but the
assertion is permissive. This is honest given the upstream normalization but it means a
regression that changed int → byte → would still pass (both not int and not long).

**Impact:** Low. Correctness of `int → Data<long>` boxing is not pinned to one specific
shape.

**Suggestion:** rename test to `Set_IntValue_MintsDataOfIntOrLong` (or split into
two tests: one with `int` literal, one with `long` literal that pins the long arm
exclusively — `Set_LongValue_MintsDataOfLong` already does the latter). The
permissive shape is effectively what `Set_LongValue_MintsDataOfLong` (line 53–62)
already asserts strictly, so the int test here is mostly redundant under the JSON
normalization reality. Could be deleted to reduce confusion, or kept as a
"hot-path-int-arm-not-skipped" smoke test if the assertion were made stricter.

#### 6. (minor) Replacement-NOT-fires-OnDelete unpinned

**File:** `PLang.Tests/App/VariablesTests/SubscriberSurvivalTests.cs`
**Code:** `PLang/App/Variables/this.cs` (Set replacement path)

`SubscriberSurvivalTests` covers what DOES fire: `Set_Replace_FiresOnChange_OnPrev_WithDvAsNewData`
and what doesn't get aliased. It does NOT pin that **prev.OnDelete** is NOT fired during
a Set replacement (only Remove fires OnDelete per architect). If a future "fire OnDelete
on every prev disposal" change landed, it would silently break the model that says
OnDelete is for Remove.

**Impact:** Low. The architect/v1 plan is clear; current code is correct; but the test
suite doesn't lock the contract.

**Suggestion:** one-line addition to `Set_Replace_FiresOnChange_OnPrev_WithDvAsNewData`:
```csharp
var deleteCalls = 0;
prev.OnDelete.Add(_ => deleteCalls++);
// ... Set(dv) ...
await Assert.That(deleteCalls).IsEqualTo(0);  // OnDelete is for Remove, not replacement
```

#### 7. (minor) Stale tests in `tests/modifiers/` (legacy directory)

**Files:** `tests/modifiers/PerActionErrorScope.test.goal`, `tests/modifiers/OnErrorCallGoal.test.goal`

The .pr files in `tests/modifiers/.build/` were built against an older `error.handle`
schema (parameter `Goal: {name: ...}` instead of `Actions: [...]`) and an older
`goal.call` shape (`Actor: "this"` literal). The runtime now strictly converts
`Actor` strings → `Actor.@this` and the literal "this" fails (`TypeMismatch(400):
Cannot convert String to this`).

The maintained `Tests/Modules/Modifiers/` versions PASS — coder/v1 didn't introduce
the regression. This is pre-existing stale state, not a coder/v1 finding.

**Impact:** Inflates the "2 failed" count and could mask future PLang test
regressions. Not blocking for THIS branch but should be cleaned up:
- Either delete `tests/modifiers/.build/` and rebuild (requires a working
  `plang build`), or
- Delete the `tests/modifiers/` directory if `Tests/Modules/Modifiers/` supersedes it.

**Out of scope for tester** — flagged for project-level cleanup.

## Verdict

**approved** — all critical tests are honest, the identity-preservation contract is
strong (every aliasing claim uses `ReferenceEquals`), the 9 deferred stubs honestly
fail, and the new architectural behaviors (dumb-storage Variables.Set, OnDelete on
Remove, AsCanonical live-variable resolution, IsPlangAssignable single source of
truth, IsPlangIterable string-not-iterable carve-out) are all pinned correctly.

The 7 findings above are all coverage GAPS, not false greens. None of the existing
assertions are weak enough to silently pass under a likely regression in the lines
they DO cover. The gaps are real and worth filling, but they don't undermine what's
been tested.

Findings #1, #2, #3, #4 are all **major** (coverage holes for paths the diff
introduced or changed) — they should be addressed before merge OR explicitly
deferred with a follow-up issue. Findings #5, #6, #7 are minor / cleanup.

If Ingi wants the major findings closed before auditor: bounce back to coder for
~1–2 hours of test additions (all 4 are simple test files added in the same shape
as existing tests).

If accepting the gaps: proceed to **auditor**, then merge. Mark findings #1–#4 as
"deferred coverage" in a follow-up issue.

## Suggested next step

If approved as-is → **auditor**, then merge.
If addressing findings → **coder** for ~1h of test additions per #1–#4.
