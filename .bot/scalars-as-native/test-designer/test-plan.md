# Test plan — `scalars-as-native`

Two layers (per architect's strategy):

- **C# unit** — `PLang.Tests/App/ScalarsAsNative/`. Each wrapper in isolation; `item`'s universal contract; coercion mediator over wrappers; the constraint + double-wrap negative-compile.
- **PLang integration** — `Tests/ScalarsAsNative/Stage{1..7}/`. Born-native + sweep + load-bearing proofs end-to-end. Real LLM not needed here (no LLM surface).

## v1 — the load-bearing failing test (this commit)

`Tests/ScalarsAsNative/Stage3/`:
- `DateIsDateNotDatetime.test.goal` — the architect's anchor: `%d% is date` true, `is datetime` false. Today `ScalarComparer.Name()` classifies `DateOnly` as `"datetime"` (file `data/ScalarComparer.cs:62-82`), so this **fails before Stage 3 and passes after**.
- `TimeIsTimeNotUnhandled.test.goal` — sister proof: `time` is its own type (today `ScalarComparer` has no `TimeOnly` arm at all).

## v2 — full skeleton batch (next commit)

### C# unit (~70 tests, file-per-surface)

`PLang.Tests/App/ScalarsAsNative/`

| File | ~Count | What it pins |
|---|---|---|
| `ItemApexTests.cs` | 5 | `item` carries truthiness + lazy narrow; `dict : item` keeps no order; `Compare.Order(dict)` still throws; un-narrowed `item(kind=json)` narrows on touch; `item` does **not** implement `IOrderableValue` |
| `NumberRegressionTests.cs` | 3 | arithmetic / compare / truthiness / `→ returns int` unchanged after `: item` rewire |
| `TextWrapperTests.cs` | 9 | length / case / contains / substring / split / trim ops; ordinal compare; value-equality + `GetHashCode` (`text("a")` equal in `HashSet`, in list-element dedup); empty falsy; raw-`string` ↔ `text` aliasing guard in `HashSet`/list-element (mid-migration hazard, coder #3); atomicity (not `IEnumerable`); `Owns string` |
| `DateTimeWrapperTests.cs` | 6 | accepts CLR `DateTime` on ctor; offset compare; ISO bare-serialize; truthiness; parts; value-equality |
| `DateWrapperTests.cs` | 5 | backed by `DateOnly`; **distinct from datetime**; compare within type; bare ISO; equality; parts |
| `TimeWrapperTests.cs` | 4 | backed by `TimeOnly`; compare within type; bare ISO; equality |
| `DurationWrapperTests.cs` | 6 | `TimeSpan` backing; parts; compare; equal durations value-equal; documented zero-truthiness policy; bare serialize |
| `BoolWrapperTests.cs` | 5 | wraps raw `bool`; `IBooleanResolvable` bottoms out; equality + hash; bare `true`/`false`; `Owns bool` |
| `NullWrapperTests.cs` | 6 | singleton instance identity; always falsy; `null == null`; sorts last; bare `null`; `Data.Null()` stamps the singleton |
| `CoercionMediatorTests.cs` | 6 | `"5" == 5`; numeric widening (int↔decimal); enum ↔ string; date-vs-datetime is a coercion outcome **not** silent-equal; inspects wrapper types not raw CLR; `bool`/`null` route through the mediator's equality |
| `ItemConstraintTests.cs` | 5 | `where T : item` compiles; `Data<int>` must **not** compile (negative-compile via guarded reflection); `Data<data.@this>` must **not** compile (double-wrap kill); `Variable : item` satisfies the slot + still `IRawNameResolvable`; `Data<Ask>` / `Data<snapshot>` / `Data<path>` compile under the constraint |
| `ScalarComparerCollapseTests.cs` | 4 | `ScalarComparer.Name()` switch gone; `IsDateTime`/`ToOffset` arms gone; `Compare.Order(text)` routes via `IOrderableValue`; raw-scalar arms unreachable (perimeter-only) |
| `ConstructionBornNativeTests.cs` | 6 | `UnwrapJsonElement` String → `text.@this`, Number → `number.@this`, True/False → `bool.@this`, Null → `Data.Null()` (singleton); no raw scalar escapes; `UnwrapNewtonsoftToken` deleted |

### PLang integration (~37 tests, one concern per `.test.goal`)

`Tests/ScalarsAsNative/Stage{1..7}/`

| Stage | File | What it proves |
|---|---|---|
| 1 | `DictIsItemKeepsNoOrder.test.goal` | dict still throws on sort under `: item` |
| 1 | `ListIsItemSortsAsBefore.test.goal` | list still sorts under `: item` |
| 1 | `JsonReadIsItemUntilTouch.test.goal` | `read file.json, %x%` is item-kind-json, narrows to dict on `%x.field%` |
| 1 | `NumberArithmeticUnchanged.test.goal` | number flow unchanged after rewire |
| 2 | `TextBornNative.test.goal` | `set %s% = "Hello"` → `%s.type%` is `text` |
| 2 | `TextLengthOp.test.goal` | `%s.length%` works through wrapper |
| 2 | `TextForEachDoesNotIterateChars.test.goal` | `foreach %s%` does **not** char-iterate |
| 2 | `TextEmptyIsFalsy.test.goal` | `if %s%` with empty text is falsy |
| 2 | `TextSignedPlangRoundtrip.test.goal` | signed text survives `.plang` |
| 2 | `TextBareOnJson.test.goal` | `.json` is bare `"Hello"` |
| 2 | `TextReturnsString.test.goal` | `→ returns string` reconstructs |
| 3 | `DateIsDateNotDatetime.test.goal` | **(load-bearing — shipped in v1)** |
| 3 | `TimeIsTimeNotUnhandled.test.goal` | **(shipped in v1)** |
| 3 | `DatetimeBornNative.test.goal` | `set %dt% = 2026-01-01T12:00:00Z` → wrapper |
| 3 | `DatetimeAcceptsClrDateTime.test.goal` | `DateTime` input lands as `datetime` |
| 3 | `DateReturnsDateOnly.test.goal` | `→ returns DateOnly` reconstructs |
| 3 | `DatetimeReturnsDateTimeOffset.test.goal` | `→ returns DateTimeOffset` reconstructs |
| 3 | `TimeReturnsTimeOnly.test.goal` | `→ returns TimeOnly` reconstructs |
| 3 | `DateDatetimeSortFamiliesSeparate.test.goal` | each sorts within its own type |
| 3 | `DateBareIsoOnJson.test.goal` | bare ISO `.json`; signed `.plang` |
| 4 | `DurationBornNative.test.goal` | duration literal → wrapper |
| 4 | `DurationPartsAndCompare.test.goal` | parts + ordering |
| 4 | `DurationReturnsTimeSpan.test.goal` | `→ returns TimeSpan` reconstructs |
| 4 | `DurationBareOnJson.test.goal` | bare on `.json` |
| 5 | `BoolBornNative.test.goal` | JSON `true` → `bool.@this` |
| 5 | `IfBoolTruthy.test.goal` | `if %b%` + `if !%b%` |
| 5 | `BoolReturnsBool.test.goal` | `→ returns bool` reconstructs |
| 5 | `AssertIsTrueOnBoolResult.test.goal` | condition action reads via `IBooleanResolvable` |
| 5 | `BoolBareOnJson.test.goal` | bare `true`/`false` |
| 6 | `NullValueIsSingleton.test.goal` | `set %x% = null` → falsy + `%x% == null` |
| 6 | `MissingVarIsNotNullValue.test.goal` | **(guard)** missing var → `NotFound`, **not** `null.@this` |
| 6 | `NullsSortLast.test.goal` | nulls trail in mixed-list sort |
| 6 | `NullBareOnJson.test.goal` | bare `null` on `.json` |
| 7 | `FiveEqualsFiveStillCoerces.test.goal` | `"5" == 5` via mediator over wrappers |
| 7 | `NumberWideningCrossesKinds.test.goal` | int↔decimal widens via mediator |
| 7 | `DictStillThrowsOnSort.test.goal` | regression — `dict` order still throws after constraint |

## Coverage vs. architect's matrix

Every row in `plan/test-coverage.md` mapped:

| Coverage row | File(s) |
|---|---|
| `item` universal contract | `ItemApexTests.cs` |
| `dict : item` keeps no order | `ItemApexTests.cs` + `DictIsItemKeepsNoOrder.test.goal` + `DictStillThrowsOnSort.test.goal` |
| `object` folds into `item` | `JsonReadIsItemUntilTouch.test.goal` |
| `number` unchanged | `NumberRegressionTests.cs` + `NumberArithmeticUnchanged.test.goal` |
| `text` ops | `TextWrapperTests.cs` |
| `text` value-equality (HashSet / list-element + raw aliasing guard) | `TextWrapperTests.cs` |
| `text` truthiness | `TextWrapperTests.cs` + `TextEmptyIsFalsy.test.goal` |
| `text` atomicity | `TextForEachDoesNotIterateChars.test.goal` |
| `text` born native | `TextBornNative.test.goal` + `TextReturnsString.test.goal` |
| `text` serialize | `TextSignedPlangRoundtrip.test.goal` + `TextBareOnJson.test.goal` |
| `date` ≠ `datetime` | `DateIsDateNotDatetime.test.goal` (load-bearing v1) |
| `time` own type | `TimeIsTimeNotUnhandled.test.goal` |
| `datetime` accepts `DateTime` | `DateTimeWrapperTests.cs` + `DatetimeAcceptsClrDateTime.test.goal` |
| datetime/date/time compare | `DateTimeWrapperTests.cs` / `DateWrapperTests.cs` / `TimeWrapperTests.cs` + `DateDatetimeSortFamiliesSeparate.test.goal` |
| `duration` parts + compare | `DurationWrapperTests.cs` + `DurationPartsAndCompare.test.goal` |
| `duration` truthiness | `DurationWrapperTests.cs` |
| `bool` truthiness primitive | `BoolWrapperTests.cs` |
| `bool` in a condition | `IfBoolTruthy.test.goal` + `AssertIsTrueOnBoolResult.test.goal` |
| `bool` born native | `BoolBornNative.test.goal` + `BoolReturnsBool.test.goal` |
| `null` singleton + truthiness + sorts last | `NullWrapperTests.cs` |
| `null` value vs absent | `NullValueIsSingleton.test.goal` + `MissingVarIsNotNullValue.test.goal` (guard) |
| coercion mediator over wrappers | `CoercionMediatorTests.cs` + `FiveEqualsFiveStillCoerces.test.goal` |
| `Variable : item` | `ItemConstraintTests.cs` |
| `where T : item` compiles | `ItemConstraintTests.cs` (positive + `Data<int>` negative-compile) |
| double-wrap impossible | `ItemConstraintTests.cs` (`Data<data.@this>` negative-compile) |
| `Ask` / `snapshot` / `path` `: item` | `ItemConstraintTests.cs` |
| `ScalarComparer` collapsed | `ScalarComparerCollapseTests.cs` |

## Independently-derived coverage (beyond the architect's matrix)

Per `test_design_principles.md`, second pass for edge cases the architect didn't list:

- **Construction-seam born-native pinning** (`ConstructionBornNativeTests.cs`) — the architect names the seam but tests only sample it through downstream consumers. A direct unit test on `UnwrapJsonElement` per `JsonValueKind` catches a regression there in isolation.
- **`Data.Null()` factory stamps the singleton** — guards against a future "allocate-per-null" regression that integration tests would only catch by accident.
- **Raw-`string` vs `text("a")` aliasing in a HashSet** — the architect calls out the hazard; the test goes one level beyond `Equals` to `GetHashCode` because the implicit operator compiles but does not hash-equal.
- **`time` returns `TimeOnly` round-trip** — architect lists `DateOnly`/`DateTimeOffset` but skipped `TimeOnly`; round-tripping it is the other half of "time is its own type."
- **`UnwrapNewtonsoftToken` is deleted** — architect calls for it; a build-time absence test (a `[Test]` that does `typeof(...).GetMethod("UnwrapNewtonsoftToken").Should().BeNull()`) keeps it from coming back.

## Mutation checkpoints (architect-specified)

1. After Stage 3 lands, revert the `ScalarComparer` date fix → `DateIsDateNotDatetime` goes red.
2. After Stage 7 lands, reintroduce one raw-scalar construction arm (e.g. `UnwrapJsonElement` emits raw `string`) → constraint compile error or a body test red.

Both announced per CLAUDE.md before mutating.

## Sequencing

- **v1 (this commit)** — `DateIsDateNotDatetime` + `TimeIsTimeNotUnhandled` ship now, failing today by analysis (`ScalarComparer.cs:62-82`). Anchor.
- **v2 (next commit)** — full C# + integration skeletons; bodies are `Assert.Fail("Not implemented")` (C#) and `- throw "not implemented"` (PLang). Coder takes them stage-by-stage.

## Test budget (final)

- C# unit: **~70** across 13 files.
- PLang integration: **~37** across 7 stage folders (2 already in v1).

Within the architect's ~65–80 / ~30–40 bands.
