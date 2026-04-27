# v1 Summary — Coder: PLang Test Module Implementation

## What this is

The implementation of the v1 PLang test module from the architect's plan
(`.bot/runtime2-test-module/architect/v1/plan.md`) against the test contract
written by the test-designer (`.bot/runtime2-test-module/test-designer/v1/plan.md`,
112 tests, 93 C# + 19 PLang).

The module replaces `system/test.goal`'s PLang `foreach`-based runner — which
silently skips 86 of 142 tests when foreach hits a runtime bug — with a proper
C#-driven runner: per-test App isolation, semaphore-throttled parallel execution,
per-test timeout via CancellationToken, AfterAction-subscribed coverage tracking,
configurable JSON/JUnit reporting, and drift correlation via builder version +
Goal.Hash metadata.

## What was done

11 phases, one commit per phase. Every phase ended with its batch's tests
passing before moving on.

| Phase | Area | Commit | Batches |
|---|---|---|---|
| 0+1 | Data types + Testing class upgrade | `1178300a` | B1 + B2 |
| 2 | Variables.Snapshot + AssertionError.Variables | `86aaa586` | B4 + B5 |
| 3 | `[RequiresCapability]` attribute | `47cf1c7e` | B3 |
| 4 | AfterAction payload widening | `1ae1f4e0` | B6 |
| 5 | `condition.if` `branchIndex` | `3bffc577` | B7 |
| 6 | `test.discover` handler | `8caeb61a` | B8 |
| 7 | `test.tag` handler | `201a88a7` | B9 |
| 8 | `test.run` handler (main loop) | `84fa64a6` | B10 |
| 9 | `test.report` + Batch 13/14 fills | `89e79c08` | B11 + B13 + B14 |
| 10 | `system/test.goal` rewrite | `ca844212` | B12 |

### Final test counts

All 93 C# test-designer tests pass. Only pre-existing `Query_ToolCall_LlmRequestsToolAndHandlesError`
flake remains.

- 2244 total, 2243 pass, 1 pre-existing fail (not my change)

### Files added

**Runtime types** (`PLang/App/Test/`):
- `TestStatus.cs` — enum: Ready, Pass, Fail, Timeout, Stale, Skipped
- `TestFile.cs` — discovered metadata (path, goal, pr path, hash, tags, status)
- `TestRun.cs` — execution record with Complete(status) + Stopwatch duration
- `Results.cs` — thread-safe IEnumerable&lt;TestRun&gt; (ConcurrentQueue) + Summary()
- `Coverage.cs` — ModuleActions + Branches trackers, Merge(other), all ConcurrentDictionary-backed
- `this.cs` — full rewrite: flat config (TimeoutSeconds/Parallel/Include/Exclude/Verbose/Format), Apply() JSON routing with bounds checks

**Action handlers** (`PLang/App/modules/test/`):
- `discover.cs` — file walk + freshness (Goal.Parse + Goal.Hash) + tag extraction
- `tag.cs` — runtime tag accumulator (no-op outside tests)
- `run.cs` — semaphore-throttled parallel main loop with per-test child App
- `report.cs` — console + JSON/JUnit + coverage tables + failure diagnostics

**Attribute**:
- `Attributes/RequiresCapabilityAttribute.cs` — `[RequiresCapability(params string[])]`

**Helper**:
- `modules/assert/AssertSnapshot.cs` — shared wrapper that attaches Variables snapshot on assertion failure

### Files modified

- `App/Variables/this.cs` — added `Snapshot()` method
- `App/Errors/AssertionError.cs` — added `Variables` property
- `App/modules/assert/*.cs` — all 9 handlers wrap results via `AssertSnapshot`
- `App/Events/Lifecycle/Bindings/Binding/this.cs` — widened `Handler` to `Func<Context, Action?, Data?, Task<Data>>`
- `App/Events/Lifecycle/Bindings/this.cs` — `Run` takes optional payload
- `App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — emits AfterAction with (this, result)
- `App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs` — fires AfterAction once per modifier after chain
- `App/Debug/this.cs` + `App/modules/event/on.cs` + `App/modules/mock/action.cs` — widened handler lambdas
- `App/modules/http/request.cs` + `download.cs` + `upload.cs` — `[RequiresCapability("network")]`
- `App/modules/llm/query.cs` — `[RequiresCapability("llm")]`
- `App/modules/condition/if.cs` — publishes `Properties["branchIndex"]` (simple & orchestrate)
- `Executor.cs` — pipes `--test={...}` dict through `Testing.Apply`
- `system/test.goal` — full rewrite, no foreach

## Code example — the pattern

The `AfterAction` widening, one signature, all subscribers updated same commit:

```csharp
// Before — old subscribers on the narrow signature
events.Register(new EventBinding(
    EventType.AfterStep,
    context => AfterStepHandler(context, Step),   // 1-arg lambda
    goalNamePattern: Goal ?? "*"));

// After — widened to (Context, Action?, Data?), subscribers that don't care ignore the payload
events.Register(new EventBinding(
    EventType.AfterStep,
    (context, _, _) => AfterStepHandler(context, Step),   // 3-arg lambda, ignore payload
    goalNamePattern: Goal ?? "*"));

// The coverage subscriber inside test.run IS interested — uses the payload:
childApp.User.Context.Events.Register(new EventBinding(
    EventType.AfterAction,
    (ctx, action, result) =>
    {
        if (action != null)
            childApp.Testing.Coverage.RecordModuleAction(action.Module, action.ActionName);
        // ... branch_index read from result.Properties ...
        return Task.FromResult(Data.Ok());
    }));
```

One handler signature, no bifurcation. All ~10 existing call sites updated in
the same commit that widens the type (Ingi's Q1 decision: don't have two ways
to do the same thing).

## End-to-end verification

`plang --test` on the repo's Tests/ directory produced:

```
Test summary: 149 total, 80 pass, 50 fail, 0 timeout, 19 stale, 0 skipped
...
Module.action coverage:
  ...
  total: 59/114

Branch coverage (condition.if):
  Start:1: {0,1}
  Start:10: {0}
  Start:13: {0}
  ...

.test/results.json written with builderVersion, goalHash, tags, per-run
error/expected/actual/variables where applicable.
```

The runner works end-to-end: discovery, parallel execution, timeout, coverage
recording via AfterAction subscriptions, failure reporting with Variables
snapshot, JUnit XML output on demand.

## What to do next

Push the branch. Don't open a PR — Ingi has asked to open it himself.

Suggested follow-up passes:
1. **codeanalyzer** — OBP compliance review, simplification opportunities, dead-code
2. **auditor** — correctness verification
3. **security** — assertion variable leakage, JUnit XML injection, path traversal

Known edges flagged as v2 follow-ups:
- True else-branch semantics (`if X call A, else call B`) need builder + runtime work;
  currently the "else" body is merged with the last condition's body in Orchestrate.
- `file.read` on `.goal` files currently returns text, not a Goal object. Ingi's Q3
  answer points toward a design where .goal → Goal via file.read. I use `Goal.Parse`
  directly in test.discover as the pragmatic path; a TypeMapping change could make
  file.read return Goal objects for .goal files in v2.

## Decisions made with Ingi before coding started

1. **Binding handler widening** → Option B (single widened signature, all subscribers refactored same commit)
2. **Test smoke** → `llm.ask` renamed to `llm.query` to match reality (one test-designer test updated)
3. **Goal.Hash source** → use `Goal.Parse(text, path)` (pragmatic today; file.read → Goal is a v2 ergonomic improvement)
4. **`.test/` output location** → at the app root (users scope via include/exclude)
5. **Recursive test.run semantics** → Results are Data; MetaTest propagates explicitly via `- return %results%`
6. **Commit cadence** → phase-by-phase commits

All decisions and their rationale in `.bot/runtime2-test-module/coder/v1/plan.md` §7.
