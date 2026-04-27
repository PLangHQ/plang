# Tester v3 Plan — Test-Quality Review of Shipped Test Module

## Context

Previous tester v1 and v2 were plan refinement only (no implementation, no testing). The coder has since shipped v1 (11 phases, ~112-test contract) and codeanalyzer has reviewed and approved (v3 CLEAN). This v3 is the **first actual testing pass** on the shipped code.

## What I'm reviewing

Everything shipped in commits `1178300a..d05c138d`:

- `PLang/App/Test/*` — TestStatus, TestFile, TestRun, Results, Coverage, Testing (@this)
- `PLang/App/modules/test/*` — discover, tag, run, report handlers
- `PLang/App/modules/assert/AssertSnapshot.cs` + 9 assert handlers
- `PLang/App/Variables/this.cs` — new `Snapshot()` method
- `PLang/App/Errors/AssertionError.cs` — added `Variables` property
- `PLang/App/Events/Lifecycle/Bindings/*` — `AfterAction` payload widening
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — emits AfterAction w/ result
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs` — AfterAction per chain
- `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs` — SplitAtConditions indexer fix (d05c138d)
- `PLang/App/modules/condition/if.cs` — branchIndex property publishing
- `PLang/App/modules/http/*` + `llm/query.cs` — `[RequiresCapability]` attribute
- `PLang/App/Attributes/RequiresCapabilityAttribute.cs`
- `system/test.goal` — rewrite

## Workflow

### Phase 1 — Build & run the full suite

1. Build PLang.sln. Record warnings/errors.
2. Run `dotnet run --project PLang.Tests` on net10.0. Record pass/fail/skip.
3. Confirm the known `Query_ToolCall_LlmRequestsToolAndHandlesError` flake is the only expected failure.
4. Run `plang --test` from repo root on `Tests/` to exercise the PLang pipeline end-to-end.

### Phase 2 — Coverage

1. Run `dotnet-coverage collect` or TUnit's built-in coverage on the changed files.
2. Save as `v3/coverage.json`.
3. Note which files changed in this branch have 0% or partial coverage — flag each.

### Phase 3 — False-green hunt (core tester job)

For each changed file, ask the four questions from my memory:

1. **Intent vs implementation**: does the test verify *behaviour* (file deleted, variable stored) or mechanics (method returns `Data.Ok()`)?
2. **Deletion test**: if I delete the changed lines, does any test fail?
3. **Assertion strength**: `IsFalse(r.Success)` without `Error.Key` check is weak. Flag.
4. **Mocks hiding real behaviour**: mock returns fixed data regardless of input → finding.

Priority hunt sites (highest risk first — see memory feedback_new_code_highest_risk):

- **AssertSnapshot wrapper** — new helper; every assert handler calls it. Does any test verify `Variables` snapshot survives serialization round-trip? Does `Variables.Snapshot()` deep-copy or hold references? (If references, a later mutation corrupts the snapshot.)
- **AfterAction payload widening** — signature change touched ~10 call sites. A subscriber that ignores the payload (3-arg lambda with `_`) is easy to write — is there a test that verifies a subscriber actually *receives* the payload? (Registration-only tests, per memory `feedback_registration_vs_execution_tests`, are insufficient.)
- **`condition.if` branchIndex property** — new Properties write. Is there a test that asserts `result.Properties["branchIndex"]` is the **correct** index after a multi-elseif chain? Especially after d05c138d, when inner branch matches after earlier false ones.
- **Coverage.Merge** — ConcurrentDictionary merge. Overlap keys, concurrent calls.
- **test.run** — semaphore parallelism, timeout cancellation, child App disposal. Does any test verify timeout actually disposes the App (not just cancels)?
- **test.discover** — Goal.Hash freshness. Does a test exist where a .goal file is edited between two discover runs and the staleness check catches it?
- **[RequiresCapability]** — is this enforced anywhere? A decorator that no one reads is dead metadata.
- **SplitAtConditions indexer fix** (d05c138d) — codeanalyzer v3 explicitly flagged no existing test catches this cluster. Confirm, and propose the test they described.

### Phase 4 — Check PLang tests alongside C# tests

My notes from memory: `every new module/action should have a PLang .goal test`. Walk through the new modules (`test.discover`, `test.tag`, `test.run`, `test.report`) and confirm each has a PLang test goal. Read each .pr file and verify `actions[0].module.action` semantically matches the step text.

### Phase 5 — Write deliverables

- `v3/coverage.json` — raw coverage output
- `v3/test-report.json` — findings, severity, structured report (branch-root: `.bot/<branch>/test-report.json`)
- `v3/verdict.json` — pass or needs-fixes
- `v3/summary.md` — human summary
- `v3/result.md` — detailed findings
- Update `.bot/<branch>/tester/summary.md` with a v3 line
- Update `report.json` with final after + timestamp_end

### Phase 6 — Commit & push

Everything in `.bot/` committed together, then push. (Memory: push-after-report — don't wait.)

## Key false-green suspects I'm NOT going to miss

From my memory of recurring patterns on this codebase:

1. **Default-path handlers least tested** — test.report's default console-only path, test.discover's default tag-extraction path.
2. **Cache/mock divergence** — if any test mocks Testing or Coverage, the mock won't exercise real serialization.
3. **Absence ≠ presence** — verifying `coverage.ModuleActions.Count > 0` is not the same as verifying the *right* module.action key is recorded.
4. **Callback invocation unverifiable** — if a test relies on an AfterAction subscriber firing, but has no way to observe it fired, it's meaningless.

## Codeanalyzer's flagged test gap

Codeanalyzer v3 explicitly asked for a test that:

- Builds a multi-action orchestrate step (outer if + elseif + elseif)
- Attaches the production coverage subscriber (no ReferenceEquals filter that skips inner actions)
- Asserts no `"?:?"` site is recorded
- Asserts indented sub-steps execute when any branch matches

I will verify this test is missing, and **propose** the test (not write it — test writing is the coder's job per my role discipline, but I will describe exactly what to assert and why).

## Not in scope

- Production code bug-hunting (my role discipline: I validate tests, I don't chase production bugs).
- Writing new test code (I recommend, the coder writes).
- Security review (that's the security analyst's next pass).

## Estimated time

Suite build + run: ~5 min. Coverage: ~5 min. False-green hunt across ~20 files: ~30-45 min. Reporting: ~15 min. Total: ~60-90 min.
