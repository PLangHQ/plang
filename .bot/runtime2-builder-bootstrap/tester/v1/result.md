# Tester v1 — Result

## Test run summary

| Suite | Total | Pass | Fail | Stale | Skip |
|---|---:|---:|---:|---:|---:|
| C# (TUnit, `dotnet run --project PLang.Tests`) | 2289 | 2281 | **8** | – | 0 |
| PLang (`/Tests/` via `plang --test`) | 161 | 132 | **25** | 4 | 0 |
| PLang (`/tests/` via `plang --test`) | 9 | 8 | **1** | 0 | 0 |
| **Combined** | **2459** | **2421** | **34** | **4** | **0** |

Coverage (C# only): **36.5% line / 21% branch global.** Per-changed-file detail in `coverage.json`.

## Critical: BuildingGuard regression (NEW finding, missed by 4 rounds of code review)

`PLang.Tests/App/Modules/builder/BuildingGuardTests.cs` — all 8 tests fail. They assert: when `engine.Building.IsEnabled == false`, every builder action (goals, GetActions, validate, goalsSave, app, appSave, merge, types) returns an error with message "Building is not enabled".

**This guard existed in `runtime2` (the base branch) and was deleted by the v2 builder squash on this branch.** Verified by checking out `runtime2:PLang/App/modules/builder/providers/DefaultBuilderProvider.cs`:

```csharp
// runtime2 baseline (lines 18-23)
private static Data.@this? BuildingGuard(IContext action)
{
    if (!action.Context.App.Building.IsEnabled)
        return Data.@this.FromError(new Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));
    return null;
}
// Called from Actions, Goals, GoalsSave, Validate, App, AppSave, Merge, Types.
```

Current `runtime2-builder-bootstrap` provider has zero references to `BuildingGuard` or `Building.IsEnabled`. The test file is byte-identical to runtime2 — which means the tests survived the squash but the production guard didn't. Codeanalyzer's 4 rounds reviewed code-only and never ran the test suite, so this regression slipped through.

This is **not** a false-green or a test-quality issue. It's a production regression the existing tests honestly catch. Either the guard must be restored in `DefaultBuilderProvider` (one helper + 8 call sites, mechanical) or the tests must be deleted with explicit justification (e.g., the guard moved to a different layer — but I found no such layer).

## v4 carryovers — confirmed

### F1. Locale-format asymmetry has zero coverage

`TypeConverter.cs:325` parses with `CultureInfo.InvariantCulture` (the v3→v4 fix). The three FORMAT sites still use `Thread.CurrentCulture`:

- `PLang/App/Catalog/ExampleRenderer.cs:103` — `sb.Append(value)` for `IConvertible`.
- `PLang/App/modules/ui/providers/FluidProvider.cs:140` — `v.ToString()` for `IConvertible`.
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:FormatValue` — same shape.

I greped `PLang.Tests/` for any test setting `Thread.CurrentCulture` to a non-Invariant culture. Result: zero. The only culture-aware test is `Phase05_CultureInfo_DefaultsToInvariant` which asserts `engine.Culture == InvariantCulture` — but `App.Culture` is **never read by any production code** (verified by grep across `PLang/App/`). It's a vestigial property suggesting the design intended renderers to consult it; the wiring never landed.

**Impact:** an Italian/Spanish/German user building a goal with a numeric `@known` value will produce a `.pr` with `"3,14"` strings the InvariantCulture parse cannot read back. The fix that v4 escalated is *worse* than no fix on European locales because before v4 both ends used current culture and round-tripped consistently.

### F2. `promoteGroups` and `enrichResponse` have 0% coverage

Coverage shows 0 lines hit for both `PLang/App/modules/builder/promoteGroups.cs` and `PLang/App/modules/builder/enrichResponse.cs`. Grep confirms no `.cs` test, no `.goal` test, and no `Build.goal` / `BuildGoal.goal` step references either action.

The `ActionError("PromoteGroupsImmutableStep")` codeanalyzer v4 traced as "structurally correct" therefore has zero verification. Same for the entire enrichResponse code path.

Either delete both modules or write a goal-test that exercises each at least once. Without a routing goal, the LLM has nothing to map "promote groups" or "enrich response" intent to. `enrichResponse` is referenced from `os/system/builder/ApplyStep.goal` — let me reconcile that.

→ Actual: the system goals invoke them at build time but `--test` doesn't exercise the build path. So coverage data confirms my point: under the test suite, neither runs. They're built-time-only.

**Impact:** the 0% coverage means any regression to either action ships silent. Recommend either:
- Add a unit test that builds a fixture goal hitting both actions, or
- Add a PLang `.test.goal` that exercises a minimal builder loop end-to-end so these are smoke-covered.

## PLang test cluster — 25 fails, 4 stale, 161 total

I'm not the one to fix these but they are signals about the production-side health of the branch. Sampling a few:

### Loop / Foreach / Conditions — production regressions
- `Modules/Loop/Loop.test.goal` — Expected `3`, Actual `"0 + 1 + 1 + 1"`. Sum operation not happening; string concatenation instead.
- `Modules/Loop/Foreach/Dictionary/ForeachDictionary.test.goal` — fails.
- `Modules/Condition/Compound/Mixed/ConditionCompound.test.goal` — fails.

### Signing — multi-test cluster
9 `Modules/Signing/*` tests fail with various keys ("Contract mismatch", "File not found: .build/sign.pr", "Action 'timeout.after.after' not found"). These look like a mix of identity/GPG environment, missing builds, and one outright bug (`'timeout.after.after'` is the action mis-routed via an extra `.after` segment).

### Variable resolution
- `Modules/Identity/Unarchive/IdentityUnarchive.test.goal` — Actual is the literal string `"%__data__"` (unresolved). Variable resolution path is dropping the trailing `%` somewhere or skipping resolution entirely.
- `Modules/Event/Priority/EventPriority.test.goal` — Expected `"high,low"`, Actual `""`. Empty string suggests the join/concat path returned no result; the assert against a pre-set string is honest here.

### Builder type-conversion — directly tied to v4 carryover #1
- `Modules/Builder/ValidateValid/BuilderValidateValid.test.goal` fails with **a flood of conversion errors**: `"Cannot convert 'int = 1' (String) to Int32: The input string 'int = 1' was not in a correct format"` for ~80+ parameters across the entire module catalog. This is the @known annotation string (e.g. the literal `"int = 1"`) being routed through the parameter-coercion path instead of being unwrapped first. This isn't directly the locale carryover but it's adjacent — both surface in the build pipeline's type coercion.

### Tests with intent-vs-implementation mismatches
- `Modules/Test/Discover/TestDiscoverReportsStaleWhenPrMissing.test.goal` fails: `Expected: True, Actual: False, %hasMissingPr% = False`. The intent is "if .pr is missing, status=Stale". The discover code does set Stale on missing .pr (verified at `discover.cs:91-95`). Either the test fixture isn't producing a missing-.pr state correctly, or the discovery isn't seeing the directory. Need a coder eye on this.

## Missing-coverage findings

### F3. `file.read` ResolveVariables has zero tests
The coder handover (Gap 2) listed 3 test cases. None were written. Grep across `PLang.Tests/`, `Tests/`, and `tests/` for `ResolveVariables` returned zero hits. Coverage on `file/read.cs` is 62.5% (10/16 lines), and the uncovered range is exactly the resolve-vars branch.

### F4. `TypeMapping`/`TypeConverter` single→list auto-wrap has zero tests
The coder handover (Gap 3) was implemented at `TypeConverter.cs:156-168` (the `listElementType.IsAssignableFrom(sourceType)` branch and its convert-then-wrap fallback). No test in `TypeMappingTests.cs` or `TypeMappingDictConversionTests.cs` exercises a single value → `List<T>` conversion. Coverage on `TypeConverter.cs` is only 50.4%.

This is one of the three gaps the original handover described — the other two (AsDefault, ResolveVariables) have partial/missing coverage too. The whole point of the bootstrap branch was these three changes; they should each have direct unit tests.

## Weak-assertion findings (would not catch subtle bugs)

### F5. ErrorHandle retry tests don't verify retry count
`PLang.Tests/App/Modules/modifier/ErrorHandleTests.cs`:

```csharp
public async Task Handle_RetryFirst_PersistentFailure_AllRetriesFail()
{
    var action = Throw("always fails",
        modifiers: new ActionModifiers { ErrorHandler(("retryCount", 3)) });
    var result = await action.RunAsync(Ctx);
    await Assert.That(result.Success).IsFalse();   // ← only assertion
}
```

If the retry loop body were `for (int i = 0; i < 0; i++)` (i.e., never retried), `error.throw` still fails on first attempt and `Success.IsFalse()` still passes. Same shape on `Handle_GoalFirst_NoGoal_ExhaustsRetriesAndFails`. **Deletion test fails.**

Fix: increment a side-effect counter from the action under test (e.g., a mock that increments a variable on each call) and assert the counter == retryCount + 1.

### F6. `If_OrchestratedBranchAction_ReturnsError_PropagatesThroughStep` doesn't pin error identity
```csharp
await Assert.That(result.Success).IsFalse();
await Assert.That(result.Error).IsNotNull();
```

The intent is "404 from goal.call must surface, not be swallowed by Handled flag". The test confirms an error came out, not that it was the 404. If an unrelated error path also surfaced a non-null Error.Success=false, this still passes. Add `result.Error!.StatusCode == 404` or `Error.Key == "NotFound"`.

### F7. `ValidateResponseTests.NullInputs_ReturnsError` doesn't pin the message
Checks `Error.Key == "ValidationError"`. The validateResponse handler emits multiple distinct ValidationError variants (null inputs, step-count mismatch, gap in indexes, Keep without prior). Conflating them as one Key with no Message check means a null-inputs regression that swapped to "step count" message would still show Key=ValidationError and pass.

### F8. ListTests, QueryCallback, ForeachErrorPropagation each have `Success.IsFalse()` without Error.Key
3 weak assertions across these files. Lower-impact than F5/F6 because the modules are more localized, but the same shape.

## What's NOT a finding (verified honest)

- **AsDefault tests on `variable.set`** are reasonable. The deletion test on `if (existing.IsInitialized)` → `if (false)` makes `Set_AsDefault_DoesNotOverwriteExisting` fail; → `if (true)` makes `Set_AsDefault_SetsWhenNotExists` fail. Both edges are covered. Could be more explicit about a non-null-but-uninitialized Data, but the practical paths through `Variables.Get` (NotFound → IsInitialized=false) are exercised. Coverage 96.6%.
- **Catalog tests** verify shape (record/enum kinds, Fields/Values fields) and JSON serialization. The renderer's locale-asymmetry would not be caught here because the tests don't pass numeric values, but the shape contract is tight.
- **`AsDefault` end-to-end via PLang** — `set default %path% = '.'` is exercised by the system test runner (`os/system/test.goal:5`). It runs.

## Recommendations (priority order)

| # | Severity | Action |
|---|---|---|
| 1 | **CRITICAL** | Restore `BuildingGuard` in `DefaultBuilderProvider` (or delete `BuildingGuardTests.cs` with justification). 8 honest red tests. |
| 2 | **MAJOR** | Diagnose the `BuilderValidateValid` cluster — `'int = 1'` strings shouldn't reach `Convert.ChangeType`. This is the @known unwrap path for build validation; whatever does the unwrap is missing a step. |
| 3 | **MAJOR** | Diagnose the Loop/Foreach/Condition production failures (8+ PLang tests). `Loop` returning `"0 + 1 + 1 + 1"` instead of `3` is a hard regression. |
| 4 | **MAJOR** | Diagnose Signing cluster (9 tests). The `'timeout.after.after'` action lookup is at minimum a routing bug. |
| 5 | MEDIUM | Wire `App.Culture` into `FormatValue` / `FormatFormalValue` / `ExampleRenderer.RenderValueFormal`, OR pass `CultureInfo.InvariantCulture` directly. Add a test that flips Thread.CurrentCulture to it-IT, formats `3.14` via FormatFormalValue, and asserts `"3.14"` (not `"3,14"`). |
| 6 | MEDIUM | Decide `promoteGroups` / `enrichResponse` fate — wire from a current goal or delete. Both are 0%. |
| 7 | MEDIUM | Fix variable resolution leaving literal `"%__data__"` in `IdentityUnarchive.test`. |
| 8 | MEDIUM | Add unit tests for the three coder-handover gaps: AsDefault uninitialized-Data state, ResolveVariables with/without `%var%`, single→list auto-wrap. |
| 9 | MINOR | Tighten F5/F6/F7/F8 weak assertions. Each is a small fix; combined they harden the tests against regressions. |

## Verdict

`needs-fixes`. Not because the test design is bad — most tests are honest — but because they surface a real production regression (BuildingGuard) that 4 rounds of code review missed, plus a sea of secondary failures the PLang tests are catching. The existing test suite is doing its job; the response should be to treat these reds as findings, not to hide them.
