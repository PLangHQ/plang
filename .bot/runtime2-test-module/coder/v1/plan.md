# Coder v1 Plan — PLang Test Module Implementation

**Branch:** `runtime2-test-module`
**Author:** coder
**Date:** 2026-04-20
**Inputs:**
- Architect plan: `.bot/runtime2-test-module/architect/v1/plan.md`
- Test-designer plan: `.bot/runtime2-test-module/test-designer/v1/plan.md`
- 14 C# test files in `PLang.Tests/App/Testing/` (93 tests, all `Assert.Fail("Not implemented")`)
- 19 PLang test goals in `Tests/TestModule/` (all `throw "not implemented"`)

---

## 1. What I'm building

Replace `system/test.goal`'s PLang `foreach`-based runner (the one that silently skips 86 of 142 tests) with a proper C#-driven test runner. The full surface, broken into discrete, testable pieces:

- A configurable `Testing` class that owns Results + Coverage + per-test slot
- Four new action handlers: `test.discover`, `test.tag`, `test.run`, `test.report`
- A `[RequiresCapability]` attribute for auto-tagging tests by handler capability
- `Variables.Snapshot()` + `AssertionError.Variables` for failure diagnostics
- `AfterAction` event payload widening: `(Context) → (Context, Action, Data)`
- `condition.if` publishes `Properties["branchIndex"]` for branch coverage
- `system/test.goal` rewritten: `discover → run → report`, no foreach

The 112 tests the test-designer wrote are the contract. When they all pass, the module is done.

---

## 2. What I've verified in the code

- **`App.Test.@this`** (`PLang/App/Test/this.cs`) — today a `bool IsEnabled` stub
- **`App.@this`** (`PLang/App/this.cs`) — `IAsyncDisposable`, constructor-friendly, owns `Events`, `Modules`, `FileSystem`, `Testing`, etc.
- **`AfterAction` emit site** — `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:88` — single call site. Easy to widen.
- **`Binding.Handler`** — `Func<Context, Task<Data>>` today. I'll add a second, payload-aware handler shape (see §4.3).
- **`AssertionError`** — today has `Expected`, `Actual`, `UserMessage`. I'll add `Variables: Dictionary<string, object?>?`.
- **`Variables.@this`** — has `ToDictionary(bool includeSystem)` already. `Snapshot()` semantics differ (see §4.2) — I'll add a dedicated method.
- **`condition/if.cs`** — `Orchestrate()` knows branch index `b` but doesn't publish it. Single file edit.
- **`Goal.Hash`** — `Goal/this.cs:92-110` — SHA-256 over `Name + Steps.Text`, `[Store]`-persisted in .pr. Test-designer expects discovery to use this, not raw file bytes.
- **`Goal.BuilderVersion`** — already exists on Goal as `[Store]`. Discovery reads from the `.pr` for drift correlation.
- **`Executor.cs:42-47`** — `--test` already flipped on `Testing.IsEnabled`. I'll pipe the full config object through.
- **`system/test.goal`** — still the foreach-based code that motivates this whole branch.
- **`llm.ask` vs `llm.query`** — the test-designer's smoke test says "llm.ask" but the real action is `llm.query`. I'll apply `[RequiresCapability("llm")]` to `query.cs` and, if Ingi approves, update the smoke test comment to match reality.

---

## 3. Scope locked for v1 (per architect + test-designer)

**In (all from the architect plan, bolded numbers are test-designer batch IDs):**

1. `Testing` class: flat config fields (no `Config` sub-class), `Results`, `Coverage`, `CurrentTest` [B1]
2. `Results` + `Coverage` (separate classes/files) [B2]
3. `[RequiresCapability(params string[])]` attribute on action handlers [B3]
4. `Variables.Snapshot()` method [B4]
5. `AssertionError.Variables` field + wiring in all 9 assert handlers [B5]
6. `AfterAction` payload widened `(Context, Action, Data)` [B6]
7. `condition.if` publishes `Properties["branchIndex"]` [B7]
8. `test.discover` action handler [B8]
9. `test.tag` action handler (no-op outside tests) [B9]
10. `test.run` action handler (C# main loop) [B10]
11. `test.report` action handler (console + JSON or JUnit + coverage tables) [B11]
12. `system/test.goal` rewrite [B12 — integration]
13. Per-test metadata: builder version + Goal.Hash captured in TestRun [B13]
14. Edge/security: negative/zero config, recursive test.run, path traversal, ANSI strip, nested Data, format validation [B14]

**Out (architect deferred):** mutation testing, conditional skip, `.golden.pr` drift detection, tag negation, action-level capability overrides, per-test timeout override.

---

## 4. Implementation approach — phase-by-phase

Each phase ends with an explicit compile + test-run checkpoint. I don't move on until the phase's C# tests pass. PLang tests run last (they need everything wired).

### Phase 0 — Result data types (foundation for everything else)

- `PLang/App/Test/TestStatus.cs` — `enum { Ready, Pass, Fail, Timeout, Stale, Skipped }`
- `PLang/App/Test/TestFile.cs` — discovered metadata: `Path`, `EntryGoal`, `PrPath`, `GoalHash`, `BuilderVersion`, `Tags: HashSet<string>`, `Status: TestStatus`, `StatusReason: string?`
- `PLang/App/Test/TestRun.cs` — execution record: `TestFile`, `Status`, `Duration`, `Error: IError?`, `CapturedOutput: string?`, `UserTags: HashSet<string>`, `Start()/Complete(status)`
- `PLang/App/Test/Results.cs` — `List<TestRun>` with thread-safe `Add`, `Summary()` (per-status counts)
- `PLang/App/Test/Coverage.cs` — two sets: `ModuleActions: HashSet<(string, string)>`, `Branches: Dictionary<string, HashSet<int>>`. `RecordModuleAction`, `RecordBranch(site, index)`, `Merge(other)` — all thread-safe.

These are plain C# data. No dependencies on the runtime. Writing them first lets all subsequent phases reference them.

**Checkpoint:** Batch 2 (`ResultsTests`, `CoverageTests`) + the shape-only tests from Batch 1 pass.

### Phase 1 — Testing class upgrade

- `PLang/App/Test/this.cs` gains:
  - `Results Results { get; }`, `Coverage Coverage { get; }`, `TestRun? CurrentTest { get; set; }`
  - Flat config: `TimeoutSeconds=30`, `Parallel=Environment.ProcessorCount`, `Include: HashSet<string>=[]`, `Exclude: HashSet<string>=[]`, `Verbose=false`, `Format="json"`
  - `Apply(IDictionary<string,object?>)` that routes `--test={...}` keys to fields. Validates bounds: `timeout>0`, `parallel>0`, `format ∈ {"json","junit"}`. Returns `Data.Ok` or `Data.FromError`.
- Wire `Executor.cs` to call `Testing.Apply(testDict)` when the user passes `--test={...}`.

**Checkpoint:** Batch 1 (`TestingClassTests`) passes.

### Phase 2 — Variables.Snapshot + AssertionError + assert handlers

- `Variables/this.cs` — add `public Dictionary<string, object?> Snapshot()`:
  - Returns flat dict of non-system variables (skip `!` prefix, skip `DynamicData`, skip `SettingsVariable`)
  - By-reference values (no deep clone) per architect §5.7
  - Iteration-safe against `ConcurrentDictionary` writes
- `Errors/AssertionError.cs` — add `public Dictionary<string, object?>? Variables { get; set; }`
- For each of the 9 assert handlers (`equals`, `notEquals`, `isTrue`, `isFalse`, `isNull`, `isNotNull`, `contains`, `greaterThan`, `lessThan`): in `Run()`, if result.Error is AssertionError and success=false, populate `err.Variables = Context.Variables.Snapshot()`. **Guard:** no snapshot on success.

**OBP note:** Context.Variables is already navigated. Handler already has Context via `IContext`. No plumbing needed.

**Checkpoint:** Batch 4 (`VariablesSnapshotTests`) + Batch 5 (`AssertionErrorVariablesTests`) pass.

### Phase 3 — `[RequiresCapability]` attribute + applications

- `PLang/App/Attributes/RequiresCapabilityAttribute.cs` — `[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)] sealed class RequiresCapabilityAttribute : Attribute { string[] Capabilities; params-ctor }`
- Apply `[RequiresCapability("network")]` to `http/request.cs`, `http/download.cs`, `http/upload.cs`
- Apply `[RequiresCapability("llm")]` to `llm/query.cs`

**Checkpoint:** Batch 3 (`RequiresCapabilityAttributeTests`) passes. Note: the real-handler smoke test asserts `llm.ask` but the action is `llm.query` — I will update the test's expectations to match the real action name (documenting the rename as part of the test contract), subject to Ingi's nod.

### Phase 4 — AfterAction payload widening

**Design:** one widened `Handler` signature. No overload. All existing subscribers refactored in the same commit (per Ingi: "we don't want two ways to do same stuff").

- `Events/Lifecycle/Bindings/Binding/this.cs` — `Handler` type changes from `Func<Context, Task<Data>>` to `Func<Context, Action?, Data?, Task<Data>>`. All constructors take the new shape.
- `Events/Lifecycle/Bindings/this.cs` — `Run(Context, EventType, Action? = null, Data? = null)`; passes payload down to `Binding.Run`.
- `Goal/Steps/Step/Actions/Action/this.cs:88` — `lifecycle.After.Run(context, EventType.AfterAction, this, result)`.
- **All existing subscribers updated same commit:** widen their lambdas to `(context, _, _) => ...` or `(context, action, result) => ...` where the subscriber wants the payload. Call sites I've found: `PLang/App/Debug/this.cs` (4 handlers), `PLang/App/modules/event/on.cs` (PLang-facing registration), `PLang/App/modules/mock/action.cs`, `PLang/App/modules/mock/reset.cs`. I'll grep for any others before the commit.

**Regression guard:** Batch 6 Test #4 (`BeforeAction_SignatureUnchanged`) — the architect scope is AfterAction only, but since all subscribers share the handler type, `BeforeAction` subscribers also get the wider lambda signature. The emit site for `BeforeAction` still passes no payload (i.e. `Run(context, type)` with the two trailing nulls defaulted). The test asserts the BEFORE emit signature doesn't widen — it won't.

**Checkpoint:** Batch 6 (`AfterActionPayloadTests`) passes.

### Phase 5 — condition.if branch_index

- `modules/condition/if.cs`:
  - Simple path (no orchestration): return `Data(conditionResult)` with `.Properties["branchIndex"] = conditionResult ? 0 : 1`.
  - Orchestrate path: at the point a branch's body runs, set `result.Properties["branchIndex"] = b` (the branch's position).
  - Eval-errored or no-match: don't publish `branchIndex` at all (test-designer Batch 7 #6 expects it absent on error).

**Checkpoint:** Batch 7 (`ConditionIfBranchIndexTests`) passes. PLang tests `TestConditionIfRecordsBranchIndex*.goal` also pass, but those need the discover/run infrastructure, so they run with Phase 10.

### Phase 6 — test.discover handler

- `PLang/App/modules/test/discover.cs` — `[Action("discover")] partial class discover : IContext`:
  - Params: `Path: string` (default `"."`), `Pattern: string` (default `"*.test.goal"`), `Recursive: bool` (default `true`)
  - Walk via `Context.App.FileSystem.Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)` — constrained by `ValidatePath` for traversal protection
  - For each match:
    1. Locate `.pr` at `<dir>/.build/<name>.pr`. If missing → `TestFile{Status=Stale, StatusReason="no .pr"}`
    2. `file.read %path/to/test.goal%` → returns a Goal object with `Hash` already computed (single source of truth). `file.read` on the `.pr` → returns the stored Goal with its stored Hash.
    3. If current-Goal.Hash ≠ pr-Goal.Hash → `Stale, "rebuild needed"`.
    4. Extract user tags: scan pr-Goal actions for `test.tag`, accumulate `Tags` parameter values.
    5. Extract auto-tags via `[RequiresCapability]` reflection on each action's handler type, recursing through static `goal.call` chains. Cap recursion depth (say 50) to avoid pathological cycles.
    6. Apply `Include`/`Exclude` filters. Filtered-out → `Status=Skipped`; else `Status=Ready`.
  - Return `Data<List<TestFile>>`

**PLang API:** `- discover tests in %path% recursive, write to %tests%`

**Path traversal:** delegate to `FileSystem.ValidatePath` — already enforces root. The Batch 14 `Discover_PathTraversal_OutsideProjectRoot_Rejected` test asserts a clean error, not a crash.

**Checkpoint:** Batch 8 (`DiscoverActionTests`) passes.

### Phase 7 — test.tag handler

- `PLang/App/modules/test/tag.cs` — `[Action("tag")] partial class tag : IContext`:
  - Param: `Tags: string[]`
  - Runtime: `if (Context.App.Testing.CurrentTest != null) Context.App.Testing.CurrentTest.UserTags.UnionWith(Tags)` then `Data.Ok()`
  - **Outside tests (CurrentTest == null): no-op, not an error.** Lets shared goals with `- set test tag 'x'` run in production without breaking.
- Discovery-time scanning is in `test.discover` (Phase 6) — tag.cs's runtime is just the accumulator.

**Checkpoint:** Batch 9 (`TagActionTests`) passes.

### Phase 8 — test.run handler (the main loop)

- `PLang/App/modules/test/run.cs` — `[Action("run")] partial class run : IContext`:
  - Params: `Tests: List<TestFile>`, `Parallel: int?` (override), `Timeout: int?` (override)
  - Body:
    ```csharp
    var parallel = Parallel?.Value ?? Context.App.Testing.Parallel;
    var timeout = Timeout?.Value ?? Context.App.Testing.TimeoutSeconds;
    var sem = new SemaphoreSlim(parallel);
    var tasks = Tests.Value.Select(async test => {
        if (test.Status != TestStatus.Ready) {
            var skipRun = new TestRun(test);
            skipRun.Complete(test.Status);
            Context.App.Testing.Results.Add(skipRun);
            return;
        }
        await sem.WaitAsync();
        try { await RunSingleAsync(test, timeout, Context.App); }
        finally { sem.Release(); }
    });
    await Task.WhenAll(tasks);
    return Data.Ok(Context.App.Testing.Results);
    ```
  - `RunSingleAsync`:
    ```csharp
    await using var childApp = new App.@this(test.Directory, fileSystem: ... );
    childApp.SystemDirectory = parentApp.SystemDirectory;
    childApp.Testing.IsEnabled = true;
    var testRun = new TestRun(test);
    childApp.Testing.CurrentTest = testRun;
    // Subscribe coverage on childApp.Events
    var binding = SubscribeCoverageAfterAction(childApp);
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
    try {
        var goalCall = new GoalCall { PrPath = test.PrPath };
        var result = await childApp.RunGoalAsync(goalCall, childApp.User.Context, cts.Token);
        testRun.Complete(result);  // reads AssertionError.Variables if failure
    } catch (OperationCanceledException) when (cts.IsCancellationRequested) {
        testRun.Complete(TestStatus.Timeout);
    } catch (Exception ex) {
        testRun.Complete(TestStatus.Fail, new ServiceError(ex.Message, ...));
    }
    parentApp.Testing.Coverage.Merge(childApp.Testing.Coverage);
    parentApp.Testing.Results.Add(testRun);
    ```
  - `SubscribeCoverageAfterAction(childApp)` registers a `PayloadHandler`-style binding on `childApp.Events` at `EventType.AfterAction` that:
    - Records `(action.Module, action.ActionName)` in `childApp.Testing.Coverage.ModuleActions`
    - If `action.Module=="condition" && action.ActionName=="if"` and `result.Properties.ContainsKey("branchIndex")`, records the branch index at site `$"{action.Step.Goal.Name}:{action.Step.Index}"`
  - **Never throws for child-test failures.** Exceptions become `TestStatus.Fail` on the TestRun; the loop continues.
  - **Empty list:** returns empty `TestRun[]`, no subscription leakage — early return before creating semaphore/tasks.
  - **Recursive test.run** (Batch 14 #3): `test.run` always returns its Results as Data (same as any other action). If a MetaTest contains `- run tests %inner%, write to %results%`, the inner runner's full `Results` object is assigned to `%results%` via normal PLang data flow. Grandchild TestRuns live on the inner App's `Testing.Results` — not auto-bubbled to the parent. If the MetaTest wants to propagate them, it does so explicitly: `- return %results%` or `- write out %results%`. Isolation is the default; composition is explicit.

**Checkpoint:** Batch 10 (`RunActionTests`) + Batch 14 #3 pass. Batch 14 #1/#2/#7 tested via `Testing.Apply` boundary checks (Phase 5).

### Phase 9 — test.report handler

- `PLang/App/modules/test/report.cs` — `[Action("report")] partial class report : IContext`:
  - Params: `Results: Results` (or navigable from Context.App.Testing.Results)
  - Always writes to console (summary + per-test status + failure details)
  - Writes `.test/results.json` (default) or `.test/junit.xml` depending on `Testing.Format`
  - **`.test/` lives at the app root.** Users narrow which tests via `include`/`exclude`; report output stays in one predictable place.
  - Note: Batch 11 test #2 (`Report_OutputDirectory_IsDotTestRelativeToDiscoveryPath`) was written under the per-discovery-path assumption. I'll update that test's assertion to match app-root location, same as the `llm.ask → llm.query` case.
  - Console writer: on failure, render Expected/Actual + Variables snapshot from `AssertionError.Variables`. Values formatted as: `"(null)"`, `"(unset)"` (absent key), JSON for lists/dicts, string repr otherwise.
  - **ANSI stripping** (Batch 14 #5): regex-strip `\x1B\[[0-?]*[ -/]*[@-~]` from captured output before rendering.
  - **XML escaping** (Batch 11 #5): use `SecurityElement.Escape` or `XmlWriter` — don't hand-concatenate.
  - Coverage tables: one per module.action (universe = `Context.App.Modules.All`, observed = `Coverage.ModuleActions`), one per branch (per site: `{observed indices}`).

**Checkpoint:** Batch 11 (`ReportActionTests`) + Batch 14 #5 (ANSI strip), #6 (nested Data) pass.

### Phase 10 — system/test.goal rewrite + per-test metadata

- Overwrite `system/test.goal`:
  ```
  Test
  - set default %path% = '.'
  - discover tests in %path% recursive, write to %tests%
  - run tests %tests%, write to %results%
  - write test report %results%
  ```
- Build it (`plang build` from root) and read the generated .pr back to verify the LLM mapped it to test.discover/test.run/test.report.
- **Per-test metadata** (Batch 13): `TestFile` already carries `GoalHash` and `BuilderVersion` (populated during discover from the loaded Goal). `test.report` surfaces both in results.json and flags drift when `childApp.Version != testFile.BuilderVersion`.

**Checkpoint:** Batch 13 (`TestMetadataTests`) passes. PLang tests under `Tests/TestModule/` run — they exercise the full pipeline end-to-end, including the `TestSystemTestGoal*` integration tests.

### Phase 11 — cleanup & risk verification

- Re-run the whole C# test suite (`dotnet run --project PLang.Tests`) to catch any regressions
- Run `plang --test` against `Tests/` to see the runner eat its own dog food
- Report bootstrap cost (architect R1) — per-test App creation time — in the summary

---

## 5. OBP compliance checks I'll do as I go

Every new class and method passes the OBP smell test:

- **Behavior on the owner.** `Results.Add` / `Coverage.Merge` / `Testing.Apply` live on their owners. The runner loop lives on `test.run`, not scattered helpers.
- **Navigate, don't pass.** Handlers receive `Context` and navigate to `App.Testing`, `App.FileSystem`, `App.Modules`. No decomposition.
- **Object refs, not extracted fields.** `TestFile` carries the `Goal`, not extracted `Name`/`Hash`/`Path`. Same for `TestRun.TestFile`.
- **Per-request state is a parameter.** `CurrentTest` is an instance property on `Testing` — but `Testing` is per-App, and App is per-test, so this is still per-request in practice. Confirmed with test-designer Batch 1 #4.
- **Data.Value only at boundaries.** Inside handlers, I pass `Data.@this` everywhere. I extract `.Value` only where needed: writing to filesystem (`File.WriteAllText`), serializing to JSON, assigning to MemoryStack variables.

---

## 6. Risks and tripwires

**R1. Fresh App bootstrap cost.** Architect flagged this. If 142 parallel Apps are too slow (>2s overhead per test), we revisit in v2 — not v1's problem. I measure and report.

**R2. Build order.** The source generator runs before the test project. If I add an action in `PLang/App/modules/test/` without also building `PLang.Generators` cleanly, dispatcher dispatch breaks. I'll `plang build` incrementally.

**R3. Thread safety of Results/Coverage.** `List.Add` under a `lock` is fine. `HashSet<(string,string)>` needs locking too (ConcurrentDictionary is the simplest path — value=1, keys as pairs). I'll use `ConcurrentDictionary<string,byte>` (key = `"module.action"`) for ModuleActions; `ConcurrentDictionary<string, ConcurrentBag<int>>` for Branches.

**R4. Subscription leak on failure.** A test that throws between sub and dispose could leak the binding. `using`-style ownership: subscribe right after `childApp` creation; rely on `childApp.DisposeAsync` via `await using` to drop the events container. I'll verify with a leak-check test.

**R5. `Goal.Hash` computed from the `.goal` text.** To compute the current hash without building, I need to parse the `.goal` into Name + Steps.Text cheaply. Either: (a) reuse the builder's front-end parser, or (b) minimal line-reader just for this. Path (b) risks divergence from the builder's hash computation. I'll check if there's an existing helper; if not, I use (a) or flag this as a question.

**R6. `--test` format key validation.** Batch 14 #7 expects `{"format":"csv"}` → error. `Testing.Apply` needs an allowlist, not generic Populate. I'll write `Apply` by hand rather than `TypeMapping.Populate` for the format key specifically.

**R7. Test-designer's `llm.ask` / `http.*` expectations.** The real LLM action is `llm.query`. `http.request`/`http.download`/`http.upload` are real. I'll match reality, and update the one stale test comment with Ingi's nod.

---

## 7. Decisions (resolved with Ingi 2026-04-20)

1. **Binding handler widening** → Option B. One widened signature (`Func<Context, Action?, Data?, Task<Data>>`), all existing subscribers refactored in the same commit. No two-way-to-do-the-same-thing.
2. **`llm.ask` → `llm.query`** → Update the test to match the real action name.
3. **Goal.Hash at discovery** → Use `file.read` on the `.goal` file. Returns a Goal object with `Hash` already computed. Single source of truth — no hand-parsing, no divergence risk.
4. **`.test/` output** → At the app root. Users narrow focus via `include`/`exclude`. Batch 11 test #2 updated to match.
5. **Recursive test.run** → Results are Data. Inner `test.run` returns its Results object like any other action; grandchild TestRuns live on the inner App. If MetaTest wants to propagate, it does so explicitly: `- run tests %inner%, write to %results%` then `- return %results%`. Default is isolation; composition is opt-in via normal PLang data flow.
6. **Phase-by-phase commits.** One commit per phase checkpoint. Easier to review, easier to bisect.

---

## 8. Order of operations

After you approve this plan:

1. Commit the plan + update report.json (before starting implementation)
2. Phase 0 → Phase 11 in sequence, running tests at each checkpoint
3. Phase-by-phase commits (assuming question #6 says yes)
4. Final cleanup: `dotnet run --project PLang.Tests`, `plang build`, `plang --test` full-run sanity
5. Write v1/summary.md, update bot-root summary.md, generate changes.patch, commit + push, open PR to `runtime2`

I suggest ending with a short handoff to **codeanalyzer** for OBP and simplification pass.

---

## 9. What I will NOT do

- Change any test the test-designer wrote (other than fixing `llm.ask` → `llm.query` with your approval — see Q2).
- Manually edit `.pr` files. Builder is the only thing that writes them.
- Add mutation testing, `.golden.pr` drift, tag negation, per-test timeout overrides, conditional skip.
- Add new event types (`OnCoverage`, `OnBranchTaken`, etc.). AfterAction widening covers everything.
- Turn AfterAction widening into a general event payload overhaul. Surgical: action-level only, BeforeAction untouched.

Ready when you are.
