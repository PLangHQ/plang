# Review of Tester's Plan: PLang Test Module

**Reviewer:** test-designer
**Reviewed:** Tester v1 plan (`.bot/runtime2-test-module/tester/v1/plan.md`, commit `e899aeb3`)
**Date:** 2026-04-16

The plan is strong overall — it fixes the real problem (state leakage, silent skips) with the right architecture (C# orchestration, PLang composition). What follows is grounded in reading the current `App` codebase, not theoretical critique.

---

## Add

### `test.skip` action
Plan shows `SKIP` in output but no mechanism for a test to declare itself skipped. Without a first-class action, "requires network" stays a comment that nobody enforces. Suggest:
- `- skip test "requires network" if no internet`
- Or goal-level annotation the runner reads at discovery time.

### Per-test timeout
Plan has parallel execution but no hung-test guard. One deadlocked test (infinite loop, blocked I/O) starves the parallel pool indefinitely. Need:
- Default budget (e.g. 30s) per test
- Overridable per goal: `- set test timeout 60s`
- Hard kill via `CancellationToken` when exceeded — the test's `App.@this` is `IAsyncDisposable`, dispose cancels everything.

### Tag/filter system
142 `*.test.goal` files already exist in `Tests/`. Selective execution scales painfully without it.
- `plang --test --tag identity`
- `plang --test --tag slow`
- Tags read from goal-level annotation or first-line comment.

### JUnit XML output
In addition to the bespoke JSON. JUnit XML is the lingua franca of CI — GitHub Actions, GitLab CI, Jenkins all parse it natively for free test-result rendering. Bespoke JSON forces every CI to write a custom adapter.

### Setup/Teardown convention
Goals named `Setup`/`Teardown` auto-detected per test file. Without it, every test repeats fixture wiring (seed data, identity creation, mock setup). The pattern is universal in xUnit-family runners for a reason.

---

## Push back / clarify

### Use existing `AfterAction` event for coverage, not a special hook
`Action.@this.RunAsync` already fires lifecycle events:

```
PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:75
  beforeResult = await lifecycle.Before.Run(context, EventType.BeforeAction);
  ...
  afterResult = await lifecycle.After.Run(context, EventType.AfterAction);
```

Coverage tracking should subscribe to `AfterAction`, not patch the dispatcher. This is OBP-aligned (don't reach into the action), zero modification to existing runtime code, and naturally per-test (subscribe on the test's `App.Events`, dispose with the App).

### Isolation unit is the App, not the Actor
Plan says "fresh Engine per test." Be explicit: this means a fresh `App.@this` instance — own `AbsolutePath`, own `Modules` registry, own `Providers`, own `FileSystem`. Disposed via `IAsyncDisposable`.

Resetting only the Actor (`User`/`System`/`Service`) leaks the SQLite/identity/settings state the plan is trying to fix — those live on the App, not the Actor. Worth one explicit sentence so the coder doesn't take a shortcut.

### Variable dump must happen INSIDE the assert handler
Plan implies the runner inspects `engine.Variables` after a failure. Too late: step-scoped variables are popped on step exit. By the time the runner sees the failure result, the relevant frame is gone.

The snapshot has to be taken inside `DefaultAssertProvider.Equals` (and siblings) at the moment the assertion fails, then attached to `AssertionError`. Look at `PLang/App/modules/assert/providers/DefaultAssertProvider.cs:11` — that's where the snapshot belongs.

### Existing 142 test goals don't follow "goal name starts with Test"
Sample inspection:
- `Tests/Error/Mixed/ErrorMixed.test.goal` → goal name is `Start`
- `Tests/Error/Handling/ErrorHandling.test.goal` → goal name is `Start`

Plan says "Each test is a separate goal, name starts with Test". Two reconciliations available:
1. **Codify the new convention** and migrate all 142 — bigger upfront cost, cleaner long-term.
2. **The file is the test; goals inside are sub-tests** — runner executes the file's entry goal (whatever it's named).

Pick one before writing tests. Mixing both creates ambiguity in `test.discover`.

### Discovery + iteration must be C#, only reporting is PLang
The current `system/test.goal` uses `foreach %tests%` — exactly the bug that silently skipped 86 tests. Plan correctly puts `test.discover` in C#, but worth being loud: **the test runner's main loop must not be implemented as a PLang `foreach`**. Otherwise you reintroduce the bug you're fixing. Reporting is fine in PLang.

---

## Trim / defer

### `.pr` snapshot testing belongs to the builder, not the test runner
Catching builder drift is a build-time concern, not a test-time concern. Bundling it into the test runner conflates two pipelines and makes the test runner heavier than it needs to be.

Suggested home: `plang p build --check-stability` reads `*.golden.pr`, semantic-diffs, fails the build on drift. Then `plang --test` stays focused on running tests.

### Mutation testing
Fine to defer to v5 — but the "PLang `.pr` JSON is uniquely cheap to mutate" insight is genuinely good and should not be lost. Worth one paragraph in the plan explaining *why* it's cheap (no source code transformation, mutations are well-defined: swap operator, drop param, change action) so future bots understand the strategic value and don't bury it.

---

## Net opinion

Solid plan. The four **Add** items (skip, timeout, tags, JUnit) are scope gaps in v1 — they're not future work, they're table stakes for a usable test runner.

The **Push back** items are about getting isolation and coverage *correct the first time*. Easy to get wrong, expensive to fix later. Especially the variable-dump-in-assert-handler one — that has to be designed in from day one, not retrofitted.

The **Trim** items keep v1 focused on the actual problem (state leakage, silent skips, no diagnostics) without absorbing adjacent concerns (builder stability, mutation analysis) that deserve their own pipelines.
