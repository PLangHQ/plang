# Test-Designer v1 Plan — PLang Test Module

**Branch:** `runtime2-test-module`
**Basis:** Architect v1 plan (`.bot/runtime2-test-module/architect/v1/plan.md`, commit `ee5b6166`). Approved by Ingi.
**Author:** test-designer
**Date:** 2026-04-17

---

## 1. What I'm specifying

Tests that define the behavioral contract of the v1 test module. 11 in-scope items from the architect plan plus independent edge-case and security tests I'm adding.

These tests are signatures only — bodies are `Assert.Fail("Not implemented")` (C#) or `throw "not implemented"` (PLang). The coder fills them in. The test text IS the spec.

---

## 2. What I've verified in current code

Grounding facts from reading the codebase:

- `App.Test.@this` today is a stub — `public bool IsEnabled`. Everything else in the architect plan is new.
- `App.@this` is `IAsyncDisposable`; `DisposeAsync()` cancels `_shutdownCts`, disposes actors, modules, providers, channels, keep-alives.
- `AfterAction` fires today as `lifecycle.After.Run(context, EventType.AfterAction)` (Action/this.cs:88) — signature widens to `(context, EventType, action, result)` in v1.
- `Binding.Handler` is `Func<Actor.Context.@this, Task<Data.@this>>` today — subscriber signature will need a sibling form to receive `(Action, Data)`.
- `AssertionError` (Errors/AssertionError.cs) has `Expected`, `Actual`, `UserMessage`. No `Variables`.
- `Variables.@this` has `Save/Restore/Clone/GetAll/ToDictionary` — no `Snapshot()`.
- `condition/if.cs` has `Orchestrate()` with branch index `b` but does not publish it.
- 142 `*.test.goal` files; entry goals are usually named `Start`, not `Test*`.
- No `[RequiresCapability]` attribute exists.
- `system/test.goal` still uses `foreach` (the bug that silently skips 86 tests).

These shape the test fixtures — I'll use MockFileSystem, hand-written provider mocks, and real `App.@this` instances (not mocked).

---

## 3. Batch overview (14 batches, ~108 tests)

Each batch is reviewed with you before I write the next. Every batch mixes happy path, error cases, edge cases, and (where relevant) security.

| # | Batch | Focus | C# | PLang | Total |
|---|---|---|---|---|---|
| 1 | `Testing` class | Config fields live on Testing directly; IsEnabled, Results, Coverage, CurrentTest, timeout/parallel/include/exclude/verbose defaults + JSON apply | 10 | 0 | 10 |
| 2 | `Results` + `Coverage` classes | Aggregation, merging, stats | 10 | 0 | 10 |
| 3 | `[RequiresCapability]` attribute | Reflection, multi-capability, inheritance | 5 | 0 | 5 |
| 4 | `Variables.Snapshot()` | Scope chain, shadowing, thread-safety | 8 | 0 | 8 |
| 5 | `AssertionError.Variables` + assert handlers | Variables captured on failure only | 5 | 1 | 6 |
| 6 | `AfterAction` payload widening | (Action, Data) flows to subscribers, existing subscribers unaffected | 6 | 0 | 6 |
| 7 | `condition.if` branch_index | Simple/elseif/else cases, observed in Properties | 6 | 2 | 8 |
| 8 | `test.discover` action | Filesystem walk, freshness, tag extraction, filters | 10 | 2 | 12 |
| 9 | `test.tag` action | Runtime no-op, discovery-time extraction | 4 | 2 | 6 |
| 10 | `test.run` action | Isolation, parallel, timeout, subscription, cancellation | 10 | 4 | 14 |
| 11 | `test.report` action | Console, JSON, JUnit XML, coverage tables | 7 | 3 | 10 |
| 12 | `system/test.goal` integration | End-to-end: discover→run→report | 0 | 4 | 4 |
| 13 | Per-test metadata | Builder version, .pr hash, drift correlation | 4 | 0 | 4 |
| 14 | Independent edge cases + security | Recursive test.run, path traversal, XML escaping, static state leakage | 7 | 1 | 8 |
| **Total** | | | **90** | **19** | **109** |

---

## 4. Choices I'm making and why

**C# vs PLang split** — Anything internal to the runtime (classes, attributes, reflection, scope chain) is C#. Anything the PLang developer sees (action signatures, error messages, `--test` CLI flag behavior) is PLang. The architect plan is deliberately light on PLang-user-facing tests — I'm adding more.

**Real `App.@this`, not a mock** — `App` is the isolation unit; mocking it defeats the whole point. Tests instantiate real `App.@this` in a temp working directory, using `MockFileSystem` or real filesystem depending on what the test exercises.

**MockFileSystem vs temp directory** — MockFileSystem wins for `test.discover` unit tests (deterministic content, fast, no cleanup). Real temp directory for `test.run` integration tests where actors spin up SQLite and we need actual disposal.

**Isolation tests are explicit, not incidental** — Batch 10 includes tests that write to `%foo%` in one test and verify `%foo%` is not visible in another test. This is the headline feature; it gets direct coverage, not inferred coverage.

**No mocked assertion provider** — Assertion handlers use the real `DefaultAssertProvider`. The failure-diagnostic tests check the full path: handler → provider → `AssertionError` with `Variables` populated.

**Builder non-determinism** — PLang integration tests use real `plang build` and read back the `.pr`. Per memory `feedback_pr_pipeline_testing`, I load from built .pr files, not constructed ones. Per memory `feedback_real_llm_tests`, LLM calls hit real OpenAI (snapshots later).

**What I'm adding beyond the architect plan** — Memory feedback `feedback_think_independently` says roughly 30–40% of tests should be independently-derived. My additions:

| Topic | Source |
|---|---|
| `test.tag` called outside a test — error or no-op? | Independent |
| Recursive execution: a test that calls `test.run` itself | Independent |
| Path traversal: `--test={"path":"../../etc"}` constrained? | Independent (security) |
| XML escaping in JUnit output (test name containing `<&>`) | Independent (security) |
| ANSI injection via captured `output.write` | Independent (security) |
| Static caches leaking across fresh `App` instances (document if so) | Independent |
| Assertion `Variables` snapshot containing a `Data` containing another `Data` | Independent |
| Modifier actions (`timeout.after`, `cache.on`) — do they count toward coverage? | Independent |
| `condition.if` throws during evaluation — does coverage record it? | Independent |
| `--test={"parallel":0}` or negative | Independent |
| `--test={"timeout":0}` — immediate timeout semantics | Independent |
| Mixed languages: Icelandic `.test.goal` — discovery still works | Independent |
| Branch 0 for `if(true)`, branch 1 for `if(false)` — uniform indexing architect specified | Architect |
| Snapshot semantics when variable value is mutated after snapshot (by-ref) | Architect noted it; I test it |

Batch 14 collects the independent items that don't fit elsewhere.

---

## 5. Batch order rationale

Batches 1–7 are **foundation** — the data structures and APIs other batches depend on. They can be implemented first in isolation.

Batches 8–11 are the **actions** — each consumes the foundation.

Batches 12–13 are **integration** — they sit on top.

Batch 14 is **safety net** — runs last to catch things that only show up in integration.

---

## 6. Test naming

**C#** — `MethodOrBehavior_Scenario_ExpectedResult`. TUnit `[Test]`, `async Task`. Per memory: `await Assert.That(x).IsEqualTo(y)`.

**PLang** — goal name starts with `Test`; second line comment states what's verified; body `- throw "not implemented"`. Per my memory rule, ONE goal per file — so batches 8–12 with PLang tests will have multiple `.test.goal` files, one per goal.

---

## 7. File locations

**C#** — `PLang.Tests/App/Testing/` (new subfolder). Mirror structure:

```
PLang.Tests/App/Testing/
    TestingClassTests.cs            # Batch 1 — IsEnabled, config fields, collaborators
    ResultsTests.cs                 # Batch 2
    CoverageTests.cs                # Batch 2
    RequiresCapabilityAttributeTests.cs  # Batch 3
    VariablesSnapshotTests.cs       # Batch 4
    AssertionErrorVariablesTests.cs # Batch 5
    AfterActionPayloadTests.cs      # Batch 6
    ConditionIfBranchIndexTests.cs  # Batch 7
    DiscoverActionTests.cs          # Batch 8
    TagActionTests.cs               # Batch 9
    RunActionTests.cs               # Batch 10
    ReportActionTests.cs            # Batch 11
    TestMetadataTests.cs            # Batch 13
    EdgeCaseTests.cs                # Batch 14
```

**PLang** — `Tests/TestModule/` (new subfolder). One goal per file per memory rule. Filenames:

```
Tests/TestModule/
    Assert/TestAssertFailureSnapshotsVariables.goal            # Batch 5
    Condition/TestConditionIfRecordsBranchIndexTrueBranch.goal # Batch 7
    Condition/TestConditionIfRecordsBranchIndexElseBranch.goal # Batch 7
    Discover/TestDiscoverFindsTestGoals.goal                   # Batch 8
    Discover/TestDiscoverReportsStaleWhenPrMissing.goal        # Batch 8
    Tag/TestTagAccumulatesUserTagsOnRun.goal                   # Batch 9
    Tag/TestTagOutsideTestIsNoOp.goal                          # Batch 9
    Run/TestRunIsolatesMemoryStackBetweenTests.goal            # Batch 10
    Run/TestRunEnforcesTimeout.goal                            # Batch 10
    Run/TestRunReportsAssertionFailure.goal                    # Batch 10
    Run/TestRunSubscribesAfterActionForCoverage.goal           # Batch 10
    Report/TestReportWritesJunitXml.goal                       # Batch 11
    Report/TestReportRendersFailureWithVariables.goal          # Batch 11
    Report/TestReportIncludesCoverageTables.goal               # Batch 11
    Integration/TestSystemTestGoalRunsAllDiscovered.goal       # Batch 12
    Integration/TestSystemTestGoalNoForeach.goal               # Batch 12
    Integration/TestSystemTestGoalRespectsTagFilter.goal       # Batch 12
    Integration/TestSystemTestGoalReportsTimeout.goal          # Batch 12
    EdgeCase/TestDiscoverHandlesIcelandicGoalNames.goal        # Batch 14
```

(Filenames are final when approved — I may rename slightly when writing each batch.)

---

## 8. What I will NOT do

- Implement any tests — bodies only `Assert.Fail("Not implemented")` / `throw "not implemented"`.
- Write any production code.
- Change the architect's scope — if I find something missing, I raise it and you decide.
- Combine multiple goals in one `.test.goal` file (memory rule — they get overwritten by builder).
- Batch-propose more than one batch without your review. I'll show you batch 1, get feedback, move to batch 2, etc.

---

## 9. Decisions (resolved with Ingi 2026-04-17)

1. **`Results` / `Coverage` split** — Separate test files. Two different things.
2. **`Testing.CurrentTest` thread-safety** — My call: skip. Per-App isolation means one test = one App = one Testing = one CurrentTest; zero contention. No thread-safety test in Batch 1.
3. **JUnit XML variant** — Gradle-compatible superset (`testsuites > testsuite > testcase > failure|error|skipped`).
4. **Builder version** — The version of the builder that produced the `.pr`. Purpose: detect/surface version drift (builder upgraded, `.pr` older → notify). Captured at discovery from the `.pr` file's builder-version field; surfaced in the report. Tests in Batch 13 will cover: (a) captured from `.pr`, (b) surfaced in result JSON, (c) mismatch between current builder and `.pr` builder version is flagged in report.
5. **Capability auto-tagging** — Traverses sub-goals. Test file's entry `.pr` + any `goal.call` targets reached statically. This expands Batch 8 — I'll add a test for cross-goal capability propagation.

---

## 10. Next step

If you approve this plan, I start with **Batch 1: `Testing` class + Config** (8 C# tests). I'll show the full signatures with one-line intent comments, you feedback, I iterate, then move to Batch 2.

---

## 11. Deferred (not in my scope)

- Mutation testing (architect deferred).
- Tag negation (architect deferred).
- `.golden.pr` drift detection (architect deferred).
- Per-test timeout overrides (architect deferred to v2).
- Action-level capability overrides (architect deferred).

No tests for these — they're out of scope for v1.
