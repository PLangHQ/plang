# PLang Test Module — Architect v1 Plan

**Branch:** `runtime2-test-module`
**Author:** architect
**Date:** 2026-04-17
**Basis:** Tester v2 plan (`.bot/runtime2-test-module/tester/v2/plan.md`) + test-designer review (`.bot/runtime2-test-module/test-designer/test-module-plan-review.md`) + design discussion with Ingi 2026-04-17.

---

## 1. Why this exists

PLang has 142 `*.test.goal` files and a growing signal-to-noise problem. The current runner (`system/test.goal`) is one PLang goal that does `foreach %testFiles%, call RunTest`. When `foreach` hits a runtime bug, 86 tests silently disappear from the results with no error — this is the "silent skip" problem that motivated the whole test-module branch.

Beyond that, the current runner has:

- **No isolation** — tests share state (SQLite, identity, settings) so one test's side effects cascade into the next.
- **No coverage signal** — no way to see which module.actions or which branches were exercised.
- **No diagnostics on failure** — `Expected: "b", Actual: (null)` with no variable dump, no clue where `null` came from.
- **No timeout** — a hung test blocks the whole run.
- **No CI integration** — no JUnit XML, just ad hoc console output.

We want to replace it with a proper test runner that solves all of these. The goal is not to write a test framework competitor — it's to build a tool PLang developers can trust, so improving the 142 failing tests (and adding more) becomes a feedback-driven activity.

---

## 2. Current state (what merged runtime2 gives us)

The ground we build on:

- **`App.@this`** (`PLang/App/this.cs`) — the isolation container. `IAsyncDisposable`. Owns `Modules`, `Providers`, `FileSystem`, `Events`, `Actor`s (with their own SQLite), `Testing`. Fresh App per test is viable.
- **`App.Test.@this`** (`PLang/App/Test/this.cs`) — exists today as a single `bool IsEnabled`. We expand this into the actual runner.
- **`Events.EventType.AfterAction`** — already fires from `Action.RunAsync` through `lifecycle.After.Run(context, EventType.AfterAction)`. The subscription hook test-designer recommended.
- **Modifiers** (`timeout.after`, `cache.on`, `error.handle`) — first-class. Per-test timeout can reuse `timeout.after`.
- **`AssertionError`** (in `App.Errors`) — currently carries `Expected`, `Actual`, `Message`. Needs a `Variables` snapshot.
- **`system/test.goal`** — must be rewritten. No more `foreach`.

---

## 3. Scope locked for v1

**In scope:**

1. `Testing` class upgrade — becomes the runner, owns results/coverage/config.
2. `test.discover` action — scan `*.test.goal`, load `.pr`, freshness check, tag extraction.
3. `test.tag` action — user-declared tags. No-op at runtime, metadata at discovery.
4. `test.run` action — C# main loop: isolate, parallel, timeout, coverage subscription.
5. `test.report` action — console + JSON + JUnit XML + coverage tables.
6. `[RequiresCapability(params string[])]` attribute — on action handlers. Used for auto-tagging (`http.request` → `network`, `llm.ask` → `llm`).
7. `context.Variables.Snapshot()` API + `AssertionError.Variables` field. Assertion handlers capture scope-chain snapshot on failure.
8. `AfterAction` event payload widened to `(Action, Data)`. Minimum surgical change that unlocks module.action coverage + branch tracking.
9. `condition.if` publishes `Properties["branch_index"]` in its returned `Data`. Enables branch coverage for if/elseif/else chains.
10. `system/test.goal` rewritten — no `foreach`. Calls `test.run` once with discovered list. Reporting remains in PLang.
11. Per-test metadata in output — builder version + `.pr` hash for drift correlation.

**Deferred (with rationale):**

- **Mutation testing** — mechanically cheap (.pr is JSON) but needs its own design pass: mutation catalog, equivalent-mutant handling, runtime cost, report format. Park the insight from tester v1, implement in a dedicated v2.
- **Conditional skip / `test.precondition`** — tags cover the static case. Genuine dynamic cases are rare, and treating them as failures is often correct. Add when real pain emerges.
- **`.golden.pr` drift detection** — builder-pipeline concern, not test-runner concern. Runner logs builder version + `.pr` hash so drift can be correlated, but detection lives elsewhere.
- **Tag negation** — v1 is additive only. Users can add tags with `test.tag`; auto-tags from `[RequiresCapability]` cannot be negated. Add if needed later.
- **Action-level capability overrides** — `[RequiresCapability]` is at the action handler. If someone uses `http.request` against a local mock, it still gets `network` auto-tag. If this pain surfaces, introduce a per-step override. Don't build it pre-emptively.

---

## 4. Architecture

### 4.1 File layout

```
PLang/App/Test/
    this.cs                  # Testing class — runner, config, results, coverage
    Config.cs                # --test={...} shape
    Results.cs               # TestResult, TestRun, per-test status
    Coverage.cs              # ModuleActionCoverage + BranchCoverage trackers

PLang/App/modules/test/
    discover.cs              # test.discover action handler
    tag.cs                   # test.tag action handler (no-op at runtime)
    run.cs                   # test.run action handler (main loop)
    report.cs                # test.report action handler

PLang/App/Attributes/
    RequiresCapabilityAttribute.cs    # [RequiresCapability(params string[])]

PLang/App/Errors/
    AssertionError.cs        # +Variables property (existing file updated)

PLang/App/modules/assert/
    equals.cs, isTrue.cs, ...  # Updated to capture Variables.Snapshot() on failure

PLang/App/Variables/
    this.cs                  # +Snapshot() method

PLang/App/Events/Lifecycle/Bindings/
    Binding/this.cs          # Updated — AfterAction payload (Action, Data)

PLang/App/modules/condition/
    if.cs                    # +Properties["branch_index"] in result

PLang/App/modules/http/
    request.cs, download.cs, upload.cs  # +[RequiresCapability("network")]

PLang/App/modules/llm/
    ask.cs                   # +[RequiresCapability("llm")]

system/
    test.goal                # Rewritten — no foreach
```

### 4.2 `Testing` class shape

```csharp
namespace App.Test;

public sealed class @this  // Testing
{
    public bool IsEnabled { get; set; }
    public Config Config { get; set; } = new();
    public Results Results { get; } = new();
    public Coverage Coverage { get; } = new();

    // Per-test state during execution — set by test.run for the current test in-flight
    internal TestRun? CurrentTest { get; set; }

    public @this(App.@this app) { }
}
```

`Config` captures `--test={...}`:
- `TimeoutSeconds` (default 30)
- `Parallel` (default `Environment.ProcessorCount`)
- `Include: string[]` (tag filter, empty = all)
- `Exclude: string[]` (tag filter, applied after include)
- `Verbose: bool` (live output vs captured-on-failure)

### 4.3 The actions

**`test.discover`** — C# handler, pure filesystem + `.pr` parsing.

```
- discover tests in 'Tests/' recursive, write to %tests%
```

Inputs: `Path` (default `.`), `Pattern` (default `*.test.goal`).

Behavior:
1. Walk the directory via `FileSystem.Directory.EnumerateFiles(path, "*.test.goal", recursive)`.
2. For each file, locate the `.pr` (`.build/<goalname>.pr`). If missing → `TestStatus.Stale` with reason "no .pr".
3. Freshness check: compute hash of `.goal` text, compare against `pr.SourceHash`. If mismatch → `TestStatus.Stale` with reason "rebuild needed".
4. Extract user tags: scan actions for `test.tag`, collect `Tags` parameters.
5. Extract auto-tags: for each action in the `.pr`, resolve the handler via `App.Modules.GetCodeGenerated(action)`. Reflect on `[RequiresCapability]`. Union the capability strings into the test's tag set.
6. Apply `Config.Include` / `Config.Exclude` filter. Filtered-out tests are recorded as `TestStatus.Skipped` in results.
7. Return `List<TestFile>` — file path, entry goal name, `.pr` path, tags, status.

**`test.tag`** — declarative metadata.

```
- set test tag 'http', 'fast'
```

Parameter: `Tags: string[]`. At runtime, the action simply writes to `Testing.CurrentTest?.UserTags` and returns `Data.Ok()`. The real work happens at discovery (scan of `.pr`).

**`test.run`** — the main loop. C# handler — this is the one that must not be a PLang `foreach`.

```
- run tests %tests%, write to %results%
```

Parameters: `Tests: List<TestFile>`, `Parallel: int?` (override config), `Timeout: int?` (override config).

Behavior (pseudo-code):

```csharp
var semaphore = new SemaphoreSlim(parallel);
var tasks = tests.Where(t => t.Status == TestStatus.Ready).Select(async test =>
{
    await semaphore.WaitAsync();
    try
    {
        await using var testApp = new App.@this(test.Directory);
        testApp.SystemDirectory = context.App.SystemDirectory;
        testApp.Testing.IsEnabled = true;
        testApp.Testing.CurrentTest = new TestRun(test);

        // Subscribe AfterAction for coverage
        var coverageSub = SubscribeAfterAction(testApp, testApp.Testing);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
        try
        {
            var goalCall = new GoalCall { PrPath = test.PrPath };
            var result = await testApp.RunGoalAsync(goalCall, testApp.User.Context, cts.Token);
            testApp.Testing.CurrentTest.Complete(result, assertionVariables: ExtractAssertionVariables(result));
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            testApp.Testing.CurrentTest.Complete(TestStatus.Timeout);
        }

        context.App.Testing.Results.Add(testApp.Testing.CurrentTest);
    }
    finally { semaphore.Release(); }
});
await Task.WhenAll(tasks);
```

Returns: `Data<TestRun[]>` — the full results collection.

**`test.report`** — formats output.

```
- write test report %results%
```

Writes:
- Console (always): summary table + per-test status + failure details (with variable snapshot).
- JSON file (`.test/results.json`): structured data for tooling.
- JUnit XML (`.test/junit.xml`): CI integration.
- Coverage tables:
  - **Module.action coverage**: all `App.Modules.All` handlers vs exercised set.
  - **Branch coverage**: per `condition.if` site — `{then, else}` observed.

### 4.4 `AfterAction` event payload change

Today:
```csharp
var afterResult = await lifecycle.After.Run(context, EventType.AfterAction);
```

After:
```csharp
var afterResult = await lifecycle.After.Run(context, EventType.AfterAction, this, result);
```

Where `this` is the `Action` and `result` is the `Data` returned. The binding layer passes both to subscribers.

This is the surgical change. Subscribers not interested in action/result ignore them. The coverage subscriber uses both:
- `action.Module`, `action.ActionName` → module.action coverage
- `action.Module == "condition" && action.ActionName == "if"` → read `result.Properties["branch_index"]` → branch coverage

### 4.5 `[RequiresCapability]` attribute

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresCapabilityAttribute : Attribute
{
    public string[] Capabilities { get; }
    public RequiresCapabilityAttribute(params string[] capabilities)
        => Capabilities = capabilities;
}
```

Applied to action handler classes. v1 applications:
- `http.request`, `http.download`, `http.upload` → `[RequiresCapability("network")]`
- `llm.ask` → `[RequiresCapability("llm")]`

Test discovery reflects on handlers referenced in a test's `.pr` and unions the capabilities into the test's tag set. Users can add more tags via `test.tag`; auto-tags cannot be removed in v1.

### 4.6 Assertion variable snapshot

`Variables.Snapshot()` returns `Dictionary<string, object?>` — all variables visible from the current scope chain (walks frames from innermost outward, deduplicating by name, innermost wins).

Each assertion handler on failure:
```csharp
public Task<Data.@this> Run()
{
    var result = Assert.Equals(this);
    if (!result.Success && result.Error is AssertionError err)
        err.Variables = Context.Variables.Snapshot();
    return Task.FromResult(result);
}
```

`AssertionError` gains:
```csharp
public Dictionary<string, object?>? Variables { get; set; }
```

Runner reads `AssertionError.Variables` to render the failure diagnostic:

```
FAIL: assert %result% equals "b"
  Expected: "b"
  Actual:   (null)
  Variables:
    %idx%      = 1
    %items%    = ["a","b","c"]
    %result%   = (unset)
```

### 4.7 `condition.if` branch index

Today `condition.if.Run()` returns `Data(true)` or `Data(false)` for simple form, and `Orchestrate()`'s final branch result for multi-branch. In multi-branch mode, the index `b` inside `Orchestrate` knows which branch fired but doesn't publish it.

Change: before returning the chosen branch's result, set `result.Properties["branch_index"] = b` (int). For the simple non-orchestrating path, set `branch_index = 0` for true, `1` for false — uniform representation.

Coverage subscriber observes per-site `branch_index` values. A site that only ever fires index `0` has an untested else branch; the report flags it.

### 4.8 `system/test.goal` rewrite

Before (today):
```
Test
- set default %path% = '.'
- find files '*.test.goal' in %path% recursive, write to %testFiles%
- write out 'Found %testFiles.Length% test files'
- foreach %testFiles%, call RunTest, item=%testFile%
- write out %!test.summary%
```

After:
```
Test
- set default %path% = '.'
- discover tests in %path% recursive, write to %tests%
- run tests %tests%, write to %results%
- write test report %results%
```

No `foreach`. No per-test goal call in PLang. The iteration is inside `test.run` (C#), immune to the silent-skip bug.

---

## 5. Behavior details and edge cases

### 5.1 Isolation boundary

**File boundary = App boundary.** Each `.test.goal` file runs in a fresh `App.@this`. Sub-goals within the file and calls to goals outside the file share that App instance — this allows shared setup files (e.g., `Tests/_fixtures/Setup.goal` called from the test's entry goal).

A test's entry goal is whatever goal appears first in the `.goal` file, matching today's convention (most existing tests use `Start`).

### 5.2 Timeout handling

Default 30 seconds per test file. Implemented via `CancellationTokenSource(TimeSpan.FromSeconds(timeout))` passed to `App.RunGoalAsync`. On timeout, `App.DisposeAsync()` cancels everything — actors, channels, providers, keep-alives.

Override via `--test={"timeout":60}` globally. Per-test overrides deferred to v2 (would use a `test.setTimeout` action or goal-level metadata).

### 5.3 Parallel pool sizing

Default `Environment.ProcessorCount`. Each test creates its own App + SQLite — memory is the constraint, not CPU. `--test={"parallel":N}` overrides. If the constraint turns out to be memory, we add a measured default later.

### 5.4 Output capture

Default: capture `output.write` per test, show on failure only (`Config.Verbose = false`). On `--test={"verbose":true}`, live-stream to stdout prefixed with test name.

### 5.5 Freshness check

At discovery, for each test file:
1. Hash the `.goal` text (SHA-256 of UTF-8 bytes).
2. Load the `.pr`, read `pr.SourceHash` (if present) or compute from the `.pr`'s own source field.
3. If hashes differ → `TestStatus.Stale`, reason "rebuild needed". Test is not run. Reported as STALE, not FAIL.

This catches the "I edited the .goal, forgot to rebuild" class of confusion.

### 5.6 Coverage: what counts as an action

Inventory: `App.Modules.All` — every registered `ICodeGenerated`. Modifiers (`timeout.after`, `cache.on`, `error.handle`) count. The coverage report cross-references the inventory against the set of `(module, action)` pairs observed in `AfterAction` events across all tests.

Branch coverage: restricted to `condition.if` in v1. The same pattern extends to other branching actions later (e.g., `error.handle` firing vs not firing).

### 5.7 Assertion snapshot semantics

`Variables.Snapshot()` walks the scope chain inside the current `Context.Variables`. Inner frames shadow outer. Returns a flat `Dictionary<string, object?>`. Variable values are captured by reference — no deep clone. If a test mutates a list after the snapshot, the rendered failure diagnostic will reflect the *current* state, not the failure-time state.

That's acceptable in v1 because by the time the runner renders the diagnostic, the test App is about to be disposed — no more mutations. If we later surface snapshots across step boundaries, we'll need deep-clone semantics.

---

## 6. Risks and tripwires

**R1. App bootstrap cost.** Each test spins up providers, types, navigators, event bindings. If this is slow, 142 parallel-App bootstraps become painful wall-clock-wise. Measurement is part of v1: report the total and per-test bootstrap time. If >2s overhead per test, we consider a warm pool or selective registration.

**R2. SQLite contention.** Each App gets its own SQLite via Actor's settings store. 142 parallel SQLite connections in different temp directories should be fine. If not, tests run sequentially in fallback mode.

**R3. Shared process state.** Some things are process-wide regardless of App isolation: loaded assemblies, static caches in third-party libraries, environment variables. A test that mutates `System.Environment` bleeds. We don't solve this in v1 — we document it and treat env-mutation as a test smell.

**R4. `AfterAction` payload change risk.** Widening the event payload breaks any existing subscribers. Current subscribers are few and internal. We update all call sites in the same commit.

**R5. Builder non-determinism.** A test that passed yesterday may fail today because the builder emitted a different `.pr`. The freshness check catches "edited .goal without rebuild" but not "same .goal, different `.pr`." The per-test builder version + `.pr` hash in the report is our feedback loop — when drift happens, we see it in the run logs.

**R6. `Variables.Snapshot()` performance.** Called on every assertion failure. Cheap if we only snapshot on failure (which is the design). Guard: no snapshot on success.

**R7. Multi-branch coverage gaps.** v1 handles `condition.if` via `branch_index`. Other branching patterns (`error.handle` fired, `loop.foreach` per-item success) are not tracked. Acceptable for v1 — the precedent is set, extension is mechanical.

---

## 7. What v1 is *not*

- Not a mutation-testing framework.
- Not a runtime debugger (use `plang --debug`).
- Not a `.goal` editor.
- Not a coverage-enforcing gate (it reports; it doesn't fail the run based on coverage).
- Not a test-discovery mechanism for arbitrary non-PLang test artifacts.

---

## 8. Next step — handoff to test-designer

Once this plan is approved, the natural next step is the **test-designer** agent. Its job: read this plan, design the test suites that prove v1 works. Expected test surface:

- Unit tests for `Testing` class — config parsing, result aggregation, coverage merging.
- Unit tests for `test.discover` — freshness check, tag extraction, auto-tag via attribute reflection.
- Unit tests for `Variables.Snapshot()` — scope chain, shadowing.
- Integration tests for `test.run` — isolation (two tests don't leak state), timeout (hung test cancels), parallel (no ordering dependencies).
- Integration tests for `test.report` — JUnit XML well-formed, JSON schema stable.
- Regression tests for `AfterAction` payload change — existing subscribers still work.
- PLang integration tests — rewrite a representative subset of the 142 to exercise the new runner end-to-end.

After test-designer produces the test plan, the coder picks it up with a concrete implementation brief.

---

## 9. Approval checklist

Before I hand off, Ingi please confirm:

- [ ] File layout (`PLang/App/Test/` + `PLang/App/modules/test/`) is correct.
- [ ] `AfterAction` event payload widening is acceptable (small blast radius, all call sites updated in the same commit).
- [ ] `[RequiresCapability]` at action-handler level (not module level).
- [ ] `system/test.goal` rewrite is fine as part of v1.
- [ ] Deferred list is correct (mutation, conditional skip, `.golden.pr`, tag negation, action-level capability overrides).
- [ ] No v1-scope items missing.

On approval, I'll commit this plan and the session record, then hand off to test-designer.
