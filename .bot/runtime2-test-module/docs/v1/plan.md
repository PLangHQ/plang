# Docs v1 Plan — testing.md + related coverage

## Context

Branch `runtime2-test-module` ships a full-featured PLang test runner: the `test` module (discover/run/tag/report actions), tag filtering via `[RequiresCapability]`, per-test App isolation, `AssertionError.Variables` snapshots, module/branch coverage, and two report formats. Auditor v2 passes — code is ready to merge. Security v1 pass with 4 low findings (defense-in-depth, no blockers).

Before/after gap scan:
- `docs/modules/` has `assert.md` and `mock.md` but **no `test.md`**. The `test.*` actions users actually run the suite with are undocumented at the user level.
- `docs/modules/index.md` lists `assert` + `mock` under "Events & Testing" but does not list `test`.
- `Documentation/v0.2/building_plang_tests.md` covers the `.goal`-author view of writing tests but never describes how the runner works, what flags configure it, or what the output means.
- No existing doc explains the test lifecycle (discover → run → report), tag filtering, the staleness check, timeouts, parallelism, `.test/` artefacts, or coverage tables.
- XML doc comments on the new handlers and Test.* types are already excellent (I audited discover.cs, run.cs, tag.cs, report.cs, this.cs, TestFile, TestRun, TestStatus, Coverage, RequiresCapabilityAttribute). No XML-doc gaps to fill.

The user explicitly asked: **write testing.md doc specifically about the testing.**

## What I will write

### 1. `docs/modules/testing.md` (NEW — the main deliverable)

User-facing doc for PLang developers running `plang --test`. Structured as:

1. **Overview** — what the test runner does and the discover → run → report flow
2. **Writing a test file** — `.test.goal` naming, a minimal example, assertion idioms linking to `assert.md`
3. **Running tests** — `plang --test`, config dictionary (`timeout`, `parallel`, `include`, `exclude`, `verbose`, `format`)
4. **Tags** — user tags via `tag this test ...`, auto-tags via `[RequiresCapability]`, include/exclude semantics (exclude wins)
5. **Isolation** — one App per test, MemoryStack/FileSystem per test, coverage merged on completion, why this matters
6. **Staleness** — goal hash vs `.pr` hash, `TestStatus.Stale` meaning, what to do about it
7. **Timeouts** — per-test cancellation, how `Status=Timeout` gets set, what the `cts.Token` binding means for sleeping child actions
8. **Failure output** — AssertionError.Expected/Actual, Variables snapshot, sensitive masking ([Sensitive] honored)
9. **Report artefacts** — `.test/results.json` and `.test/junit.xml` at app root, schema for each
10. **Coverage** — module.action table and branch coverage (`condition.if` sites, declared chain ✅/❌ per branch, unreached sites)
11. **Built-in actions** reference table with parameters (discover/run/tag/report)
12. **Limitations** — known low-severity security gaps (ANSI strip covers CSI only, C0 controls in JUnit, variable snapshots may carry user secrets)

Length target: comparable to `file.md` / `http.md` — long enough to be the definitive reference, no fluff.

### 2. `docs/modules/index.md` (UPDATE)

Add `test` to the "Events & Testing" table. Current row:
```
| [assert](assert.md) | Test assertions | ... |
| [mock](mock.md) | Mock actions in tests | intercept, verify, reset |
```
Add between them (or before assert):
```
| [test](testing.md) | Test runner — discovery, execution, reporting | discover, run, tag, report |
```

### 3. `Documentation/v0.2/building_plang_tests.md` (UPDATE — small cross-reference)

Add a "Running the Tests" section pointer near the top: "For how the runner executes, configures, and reports, see [docs/modules/testing.md](../../docs/modules/testing.md)." The v0.2 doc stays focused on authoring; runtime semantics go to the user doc.

### 4. `Documentation/v0.2/good_to_know.md` (UPDATE)

Add a test-module section capturing **cross-cutting, non-obvious** facts a future dev won't see in any one file:
- Per-test App boundary = file boundary. Not per-goal, not per-step.
- Coverage merge is additive + idempotent — parallel-safe.
- `ChildAppCreated` is a test-only event on `run.cs` — do not rely on it in production code.
- The site key format is `"goalPath:stepIndex"` — includes the source path so `Start` steps in different files don't collide.
- `test.discover` seeds declared branch chains so the report can show **unreached** sites (sites that exist in source but no test visits).
- Discovery checks goal.Hash against .pr.Hash — editing a .goal without rebuilding makes the test Stale, not silently out-of-date.
- `[RequiresCapability]` is class-level only, single-instance (AllowMultiple=false); multi-capability actions use `params string[]`.

## What I will NOT write

- **PLang .goal examples beyond the minimal one in the doc** — tester owns the test suite. I flag if more examples are needed, but `Tests/TestModule/**/*.test.goal` already covers the patterns.
- **A separate `Documentation/v0.2/testing.md`** — the v0.2 folder is for PLang Runtime2 internals; the test module is user-facing. The one existing v0.2 test doc is `building_plang_tests.md` which covers authoring. Runtime internals sit alongside the handler source — nothing to add there that isn't already XML-documented.
- **CHANGELOG entry** — no `CHANGELOG.md` exists in this repo (checked); capture user-visible changes in `v1/result.md`.
- **Any .pr file edits or test-code changes** — out of role.

## Verification before finishing

1. Re-read `testing.md` from a new-developer cold-start perspective: can I run the suite, write a test, and understand failure output without reading any source?
2. Cross-check every parameter in the actions table against the C# source (`discover.cs`, `run.cs`, `tag.cs`, `report.cs`).
3. Make sure every claim about behavior (exclude wins, child App boundary, Variables snapshot, sensitive masking) is traceable to the code path that implements it.
4. Lint-check that `docs/modules/index.md` links resolve.

## Outputs

- `docs/modules/testing.md` (new)
- `docs/modules/index.md` (add `test` row)
- `Documentation/v0.2/building_plang_tests.md` (one cross-ref line)
- `Documentation/v0.2/good_to_know.md` (test-module section)
- `.bot/runtime2-test-module/docs/v1/summary.md`
- `.bot/runtime2-test-module/docs/v1/result.md` (user-visible change list)
- `.bot/runtime2-test-module/docs/v1/verdict.json` (pass — gaps filled)
- `.bot/runtime2-test-module/docs-report.json`
- `.bot/runtime2-test-module/docs/summary.md` (cross-session root)
- `.bot/runtime2-test-module/docs/v1/changes.patch`

## Blockers

None. Proceeding on user approval.
