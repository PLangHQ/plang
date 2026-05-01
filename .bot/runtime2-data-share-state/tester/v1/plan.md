# tester v1 — plan

## What I'm reviewing

`runtime2-data-share-state` after:
- `coder/v1` (commits e28b6f69..463aa537) — Phases 1+2+3+4+5a of Data identity-preservation
- `codeanalyzer/v1` review at b4dff2c5 (NEEDS WORK, 4 cleanups)
- `coder/v1 review-response` at 60b8d1f3 (closed all 4)
- `codeanalyzer/v2` re-review at 8e0a419d (CLEAN — pass)

The branch is now ready for tester. After me, suggested next is auditor → merge.

## Source-of-truth files I'll be testing

These are the files coder/v1 actually touched (production code):

- `PLang/App/Data/this.cs` — events→Lists, `WrapAs<T>`, `AsCanonical`, `IsPlangIterable`/`IsPlangAssignable`, `TryFullVarMatch`, `AsT_Impl` rewrite, `SnapshotClone(object)` helper.
- `PLang/App/Variables/this.cs` — `Set` is dumb storage; `Remove` fires `OnDelete`.
- `PLang/App/Debug/this.cs` — `+=` → `.Add(...)` for new event-list shape.
- `PLang/App/modules/variable/set.cs` — `MintTyped`, `CarryStateFromSource`, JSON-roundtrip clone.
- `PLang/App/modules/list/add.cs` — uses `Data.@this.SnapshotClone`.
- `PLang/App/Utils/Json.cs` — `Json.SnapshotClone` serializer options.
- `PLang.Generators/Emission/Property/Data/this.cs` — plain-Data slot uses `AsCanonical`.

And the test files coder/v1 wrote:

- `PLang.Tests/App/DataTests/EventListTests.cs` (6)
- `PLang.Tests/App/DataTests/AsTIdentityTests.cs` (10)
- `PLang.Tests/App/DataTests/NamePropagationTests.cs` (6)
- `PLang.Tests/App/DataTests/PlangAssignabilityTests.cs` (8)
- `PLang.Tests/App/VariablesTests/SubscriberSurvivalTests.cs` (8)
- `PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs` (12)
- `PLang.Tests/App/Modules/loop/ForeachStringNotIterableTests.cs` (3)
- `PLang.Tests/App/Modules/list/ListAddIdentityTests.cs` (4 — STUB, deferred)
- `PLang.Tests/Generator/Diagnostics/Plng001PostMigrationTests.cs` (5 — STUB, deferred)

PLang `.test.goal` files brought back online:
- `Tests/Modules/Loop/Foreach/Dictionary/ForeachDictionary.test.goal`
- `Tests/Modules/Test/Discover/TestDiscoverReportsStaleWhenPrMissing.test.goal`
- `Tests/Modules/Test/Integration/*.test.goal` (4)
- `Tests/Modules/Test/Report/*.test.goal` (5)

## Workflow

1. **Build**: `dotnet build PLang.Tests/PLang.Tests.csproj` and `dotnet build TestFixtures/*/`.
2. **Run C# tests**: `dotnet run --project PLang.Tests` — record total/passed/failed/skipped.
3. **Run coverage**: `dotnet run --project PLang.Tests --coverage --coverage-output /tmp/cov/coverage.cobertura.xml --coverage-output-format cobertura`.
4. **Run PLang tests**: `plang build` from project root, then `plang --test`.
5. **Read every test file coder added/changed** — apply the deletion test mechanically.
6. **Confirm stubs are honestly stubbed** — `Plng001PostMigrationTests` (5) and `ListAddIdentityTests` (4). They must `Assert.Fail(...)` not silently pass.
7. **Trace the codeanalyzer/v2 behavioral concern** — extraction of `SnapshotClone` unified `UnwrapJsonElement` across three call sites. Find tests that assert the now-Dictionary list-entry shape (vs raw `JsonElement`). If none → finding.
8. **Trace coverage on the seven production files** — flag uncovered branches.

## Things I will explicitly hunt for

### Identity-preservation contract — ref-equality vs value-equality

The whole architect plan rests on `ReferenceEquals(wrapped.Properties, source.Properties)`,
`ReferenceEquals(wrapped.OnChange, source.OnChange)`, `ReferenceEquals(wrapped.Value, source.Value)`.
A test that mistakenly checks value-equality (`Assert.AreEqual` or `wrapped.Value == source.Value`)
would still pass when the wrap re-allocates state — silent regression. I'll grep the new
test files for `Equal(` on `Properties`/`OnChange`/`OnCreate`/`OnDelete`/`Value` and confirm
each assertion is `ReferenceEquals` or `Assert.That(..., Is.SameAs(...))`.

### Variance fast-path completeness

The architect's contract: `IsPlangAssignable(typeof(T), value.GetType())` → variance fast-path,
state aliased. If only the same-type fast-path is tested but the variance fast-path isn't,
the path most users hit (`Data<List<int>>` → `As<IEnumerable>`) is undefined.
I'll check that `AsTIdentityTests` covers BOTH: same-type return-self AND variance new-wrapper-but-aliased-state.

### Replacement-survival of subscribers — the "alias prev's lists" pivot

Architect originally said `Set` should alias prev's event lists onto dv on every replacement;
Ingi changed that mid-flight to "Set is dumb storage; clone semantics live in variable.set".
The `SubscriberSurvivalTests` was rewritten to pin the new behavior. I'll confirm the
test now asserts the OPPOSITE of what it originally asserted (no aliasing on dv directly;
aliasing happens only when value is minted by variable.set via `CarryStateFromSource`).

### Variables.Remove fires OnDelete

This is a behavior CHANGE — old `Remove` did NOT fire `OnDelete`. New does. There must be
a test (`Remove_FiresOnDelete_OnRemovedData`) and it must verify the subscriber count goes
up by 1 and the callback fires with the removed Data.

### Snapshot-clone behavior unification (codeanalyzer/v2 concern)

`Variables.Set` dot-path and `list.add` previously left raw `JsonElement` graphs after
JSON-roundtrip; now they go through `Data.SnapshotClone` which `UnwrapJsonElement`s.
Tests must assert that downstream `Value` is `Dictionary<string, object?>` / `List<object?>`,
NOT `JsonElement`. If tests only check property-by-property navigation works (which it does
for both shapes via `TypeMapping`), they won't catch a regression to `JsonElement`.

### Deferred stubs must FAIL, not silently pass

`Plng001PostMigrationTests.cs` (5 tests) and `ListAddIdentityTests.cs` (4 tests) are
"forward-looking" stubs for Phases 5b/5c/6 — DO NOT MERGE if they're silently passing.
I'll grep for `Assert.Fail("Not implemented")` and confirm the test runner reports them as
failures (not passes, not skipped). The coder's claim "C# 2524/2533 (9 stubs)" must match.

### foreach string-not-iterable — three tests

`ForeachStringNotIterableTests.cs` (3 tests) covers the `IsPlangIterable` carve-out. The
deletion test: if the carve-out were removed, would each test fail? I'll trace what each
asserts.

### variable.set type inference — 12 tests

`SetTypeInferenceTests.cs` (12 tests) covers the `MintTyped` if-chain. Hot path: string,
int, long, double, bool, decimal, float, DateTime, DateTimeOffset, Guid, byte[], List, Dict.
Cold path: reflection. I need to check both paths are tested.

### Plain-Data slot via AsCanonical

The generator emission switched plain-Data slots from `As<object>(Context)` to
`AsCanonical(Context)`. There must be a test verifying full-match `%var%` returns the LIVE
variable Data (not a fresh wrapper). The architect's example is in coder's summary:
`var canonical = paramData.AsCanonical(); ((List<int>)canonical.Value!).Add(4); // mutates source.Value`.

### Edge cases

- Same-type self-return on `Data` of type `null`?
- Empty Properties dict on cross-type wrap — does ref still alias?
- Variance from `Data<int[]>` to `As<IEnumerable<int>>`?
- Conversion failure path: `FromError` sentinel — is state explicitly NOT aliased? (Architect's Rule 4.)
- `IsPlangIterable("string")` returns false; `IsPlangIterable(byte[])` — what's the contract?
- `EnumerateItems` on null or non-iterable scalar — single-element shape?

## Output

- `v1/coverage.json` — extracted Cobertura summary for the 7 production files.
- `v1/result.md` — full deletion-test analysis per test file.
- `v1/summary.md` — one-pager.
- `v1/verdict.json` — pass/fail.
- `.bot/runtime2-data-share-state/test-report.json` — branch-shared report.

## Verdict criteria

- **approved**: All identity assertions use ref-equality. Stubs honestly fail. Replacement-survival behavior is pinned correctly. Snapshot-clone unification has at least one test asserting the Dictionary-not-JsonElement shape. Variables.Remove fires OnDelete is tested. AsCanonical live-variable behavior is tested.
- **needs-fixes**: Any of the above missing. False-green tests count as findings.

## Notes

- I will not review production code for correctness — that's auditor's role. My job is whether the tests are honest.
- Per memory: push after report.
