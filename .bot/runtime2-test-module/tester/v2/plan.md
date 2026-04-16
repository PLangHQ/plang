# PLang Test Module — v2 Plan (Revised After Test-Designer Review)

## Responses to Review

### Accepted — Adding to v1

**1. `test.skip` action** — Agreed. A test that can't declare "I need network" just gets counted as a failure in CI, which trains people to ignore failures. Implementation: `test.skip` action with a `reason` parameter. The runner records it as SKIP with the reason in the report. No conditional-skip mechanism yet (the `if no internet` part) — that requires a `test.precondition` concept. For v1, just the declaration: `- skip test "requires network"`.

**2. Per-test timeout** — Agreed, this is a correctness issue, not a feature. One hung test blocks the entire parallel pool. Implementation: default 30s budget per test. `App.@this` is `IAsyncDisposable` — create a `CancellationTokenSource` per test, cancel after timeout, dispose the App. The test result records "TIMEOUT" as the failure reason. No per-goal override in v1 — just the global default. Override can come later as `- set test timeout 60s` → `test.setTimeout` action.

**3. Tag/filter system** — Deferred to v2. 142 tests is manageable without tags today. The filter adds discovery complexity (scan .pr files for tag actions, or parse comments) that's not needed to ship the core runner. When we add it: `- tag test "identity"` → `test.tag` action, scanned at discovery time. CLI: `plang --test --tag identity`.

**4. JUnit XML output** — Agreed. Adding alongside JSON. JUnit XML is ~50 lines of string formatting for the standard schema. No library needed. `test.report` handler writes both formats. CI gets free test-result rendering.

### Accepted — Corrections to v1 Plan

**5. AfterAction event for coverage** — Correct and important. My plan said "hook `Action.RunAsync`" which implies patching the dispatcher. The OBP-aligned approach: subscribe to `AfterAction` lifecycle event on the test's `App.Events`. Zero modification to runtime code. Coverage tracking becomes an event listener that logs `(module, action)` pairs. Subscribe on test App creation, dispose with the App. Fixed.

**6. Isolation unit is App, not Actor** — The v1 plan was ambiguous. To be explicit: each test creates a fresh `App.@this(tempDir)` instance. This gives own `AbsolutePath`, own `Modules` registry, own `Providers`, own `FileSystem`, own SQLite database. Resetting only the Actor would leak identity/settings state across tests — exactly the bug we're fixing. The `App.@this` IS the isolation boundary.

**7. Variable dump inside assert handler** — This is the sharpest correction. My plan had the runner inspecting `engine.Variables` after failure — but step-scoped variables are popped on step exit. By the time the runner processes the failure result, the relevant variable frame is gone.

The fix: snapshot inside `DefaultAssertProvider.Equals` (and all assertion methods) at the moment of failure. The `AssertionError` already has `Expected` and `Actual` — we add a `Variables` dictionary snapshot. The assert handler has access to the action's context (via `IContext`), which has `context.Variables`. Capture at failure time, attach to `AssertionError`, the runner reads it from the error object.

One concern: `DefaultAssertProvider` methods currently take only the action record (e.g., `Equals action`), not the context. The provider needs access to the variable stack. Options:
- (a) Pass context to the provider methods — cleanest but changes the `IAssertProvider` interface
- (b) Assert handlers (the `equals.cs`, `isTrue.cs` etc.) capture the snapshot before calling the provider, attach it to the error after
- Option (b) is safer — doesn't change the provider contract, assert handlers already have context via `IContext`

Going with (b): assert handlers capture `context.Variables` snapshot on failure, attach to `AssertionError` before returning.

**8. Goal naming convention** — Going with "file is the test, entry goal is whatever it's named." This matches all 142 existing tests (they use `Start`). The runner discovers `*.test.goal` files, runs each file's entry goal. No migration needed. If a file has multiple goals, they're sub-goals called by the entry goal — same as how PLang apps work.

**9. C# main loop** — Already in v1 plan (`test.run` is a C# action that iterates), but the test-designer is right to be loud about it. Explicitly: `test.run` receives the discovered test list, iterates in C# (`Parallel.ForEachAsync`), creates App per test, runs goal, collects results. The PLang `.goal` file calls `test.run` once — it does NOT use `foreach %tests%`. This is the fix for the 86-test-skip bug.

### Accepted — Deferred

**10. `.pr` snapshot testing → builder** — Agreed, moving this out of the test runner entirely. It's a build-time concern. The insight is preserved in the plan but flagged as "builder feature, not test runner feature." Future: `plang build --check-stability`.

**11. Mutation testing** — Already deferred in v1. Preserving the key insight: PLang `.pr` files are structured JSON, mutations are well-defined (swap operator, drop param, change action, flip boolean), no source code transformation needed. This makes PLang-level mutation testing ~10x cheaper than traditional mutation testing.

---

## Revised Architecture

```
test.discover  →  C# handler: scan *.test.goal files, read .pr for dependencies, return test list
test.run       →  C# handler: parallel execution, fresh App per test, AfterAction coverage, timeout guard
test.skip      →  C# handler: marks current test as SKIP with reason (NEW)
test.report    →  C# handler: console output + JSON + JUnit XML (UPDATED)
test.dependency → C# handler: declares dependency on another test file
```

PLang orchestration:
```
RunTests
- discover tests in 'Tests/' recursive, write to %tests%
- run tests %tests%, write to %results%
- write test report %results%
```

The `test.run` handler internally:
1. Builds dependency DAG from discovery data
2. Creates `CancellationTokenSource` per test (30s default timeout)
3. Runs independent tests via `Parallel.ForEachAsync`
4. Per test: `new App.@this(tempDir)` → subscribe AfterAction for coverage → run entry goal → collect result → dispose App
5. Dependent tests wait for their dependencies to complete
6. Timed-out tests get their App disposed (cancels everything)

## Assertion Diagnostics (Revised)

Assert handlers (`equals.cs`, `isTrue.cs`, etc.) — on failure:
1. Capture `context.Variables` snapshot (all names → values in current scope)
2. Attach snapshot to `AssertionError` as a `Dictionary<string, object?>`
3. Runner reads `AssertionError.Variables` for diagnostic output

No changes to `IAssertProvider` interface. No changes to `DefaultAssertProvider`. The snapshot is captured in the handler layer, not the provider layer.

## Implementation Priority (Revised)

1. **`test.run` + `test.discover`** — C# iteration, App isolation, parallel, 30s timeout. Core.
2. **AfterAction coverage hook** — subscribe to lifecycle events, log module.action pairs.
3. **`test.skip`** — simple action, SKIP result type.
4. **Assertion variable dump** — snapshot in assert handlers, attach to AssertionError.
5. **`test.report` with JUnit XML** — console + JSON + JUnit.
6. **`test.dependency`** — DAG from .pr scan, execution ordering.

## Open Questions

1. **Timeout override mechanism** — Default 30s is fine for v1. But some tests legitimately need longer (LLM tests, network tests). Per-test override via `test.setTimeout` action? Or per-file convention (a step at the top of the test goal)?

2. **Parallel pool size** — What's the right default? `Environment.ProcessorCount`? Each test gets its own App + SQLite, so memory is the constraint, not CPU. Start with 4, make configurable via `--parallel N`?

3. **Test output during execution** — Should `output.write` inside a test go to stdout? Or should it be captured and only shown on failure? Captured-on-failure is cleaner for parallel output, but makes debugging harder. Default: capture, show on failure. Flag: `--verbose` shows live output.
