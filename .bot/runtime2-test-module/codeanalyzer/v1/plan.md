# Codeanalyzer v1 Plan — Review of PLang Test Module

**Branch:** `runtime2-test-module`
**Target:** C# code changes from the coder's v1 implementation (coder summary:
`.bot/runtime2-test-module/coder/v1/summary.md`, commits `1178300a..8f7fcaf6`).

## What I'm reviewing

The test module rewrite: replaces the `foreach`-based PLang test runner with a
C#-driven runner (parallel App isolation, timeouts, AfterAction coverage,
JSON/JUnit output). Coder already landed it — my job is to find OBP violations,
over-complexity, behavioural fragility, and dead lines that can go.

I am **not** reviewing:
- `.goal` / `.pr` test fixtures (test-designer's contract, already reviewed)
- C# test files under `PLang.Tests/App/Testing/` (test-designer's contract)
- Docs in `.bot/` or `Documentation/`

## Scope (code files changed on this branch)

**New Test-module production code** (primary focus):
- `PLang/App/Test/this.cs` — Testing config + state
- `PLang/App/Test/Coverage.cs` — module+branch coverage tracker
- `PLang/App/Test/Results.cs` — thread-safe TestRun collection
- `PLang/App/Test/TestFile.cs` — discovery metadata
- `PLang/App/Test/TestRun.cs` — execution record
- `PLang/App/Test/TestStatus.cs` — enum
- `PLang/App/modules/test/discover.cs` — file walk, freshness, tag extraction
- `PLang/App/modules/test/run.cs` — parallel main loop + coverage subscriber
- `PLang/App/modules/test/report.cs` — console + JSON/JUnit + coverage tables
- `PLang/App/modules/test/tag.cs` — runtime tag accumulator
- `PLang/App/modules/assert/AssertSnapshot.cs` — shared assertion helper
- `PLang/App/modules/condition/BranchChain.cs` — branch-label computation
- `PLang/App/Attributes/RequiresCapabilityAttribute.cs` — capability tag
  attribute

**Modifications touching runtime code paths** (review for regressions):
- `PLang/App/Events/Lifecycle/Bindings/Binding/this.cs` — widened handler
  signature (3-arg)
- `PLang/App/Events/Lifecycle/Bindings/this.cs` — payload routing
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs` —
  post-modifier AfterAction emission
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — AfterAction
  payload
- `PLang/App/modules/condition/if.cs` — branchIndex/Label/Chain publishing
- `PLang/App/modules/assert/*.cs` — AssertSnapshot wrapping (9 handlers)
- `PLang/App/modules/http/{request,download,upload}.cs`,
  `llm/query.cs` — `[RequiresCapability]` attribution
- `PLang/App/modules/event/on.cs`, `mock/action.cs`, `Debug/this.cs` —
  widened handler lambdas
- `PLang/App/Errors/AssertionError.cs` — Variables property
- `PLang/App/Variables/this.cs` — Snapshot() method
- `PLang/Executor.cs` — `--test={...}` routing

## Five analysis passes

I will run the five character passes across every file in scope. For each
finding: file:line, current snippet, OBP/simple form, why it matters.

### Pass 1 — OBP Compliance
- **Rule 1 — Behaviour on owner**: any `foreach` over an externally-owned
  collection? (e.g. run.cs iterating `tests`, report.cs iterating
  `results`, discover.cs iterating `matches`/steps/actions)
- **Rule 2 — Navigate, don't pass**: handlers that accept decomposed params
  instead of walking `this.Context.App` / `Context.App.Testing`.
  Especially check `AssertSnapshot.WithVariables(result, context)` — does
  the snapshot's "owner" belong elsewhere?
- **Rule 3 — Keep refs**: TestFile stores `Directory`/`Path`/`PrPath`/
  `EntryGoalName`/`GoalHash`/`BuilderVersion` — strings extracted from Goal.
  `TestFile.Goal` is kept, so are these duplicated? Are they used by
  consumers who could walk `file.Goal`?
- **Rule 4 — Per-request vs per-object**: `Testing.App` back-reference
  (line 47). Is Testing "per-object" for the App? Yes, but the reverse
  pointer is a smell — check why the reporter can't travel Context→App.
- **Rule 5 — Collections own operations**: `Results` inherits
  `IEnumerable<TestRun>` — does it own the "add/merge/summary" ops or do
  consumers poke at it? Same for `Coverage`.
- **Rule 6 — Data.Value at boundaries only**: `report.cs`, `discover.cs`,
  `run.cs` unwrap `Parameters[n].Value`, `Properties["x"]?.Value`,
  `result.Value` etc. Each unwrap is a potential violation unless the
  receiver is external (JSON, reflection, stdout). Trace every
  `.Value`/`Value as T`.

### Pass 2 — Simplification
- Dead abstractions: `ResolveBuilderVersion(testing) => testing.App.Version`
  is a one-line delegator. Delete?
- `Describe`/`FormatValue`/`FormatPreviewValue`: several ad-hoc value
  formatters duplicated across Debug/report/Assert — is one enough?
- `TestRun.Complete(Data.@this result)` + `Complete(TestStatus)` — two
  overloads. Is the overload used anywhere, or does run.cs always call the
  status form?
- `Coverage.ModuleActions` — yields with a `.IndexOf('.')` split on a
  composite key. Why the composite key at all when we could `ConcurrentBag<
  (string,string)>` or a `ConcurrentDictionary<(string,string), byte>`?
- `Tag` param is `Data.@this<string[]>` — check that string[] matches
  what the parameter builder sends for list literals; arrays vs List<string>
  differences have caused prior drift.
- `SeedBranchChains` plus `ExtractAutoTags` both recurse the goal tree via
  `goal.call`. Same walk, two visitors — candidate for a single pass.
- `BuildJson` anonymous objects plus `ToDictionary(kv => kv.Key.ToString(),
  kv => kv.Value)` — verify this is simpler than a named record.
- Coverage has 4 dictionaries (`_moduleActions`, `_branches`, `_branchLabels`,
  `_branchChains`) — seeding+labels+indices overlap; is there a simpler
  per-site record?

### Pass 3 — Readability
- `@this` aliasing. Confirm each new file follows the `@this` / `this.cs` /
  global-alias convention (CLAUDE.md rule). Spot any drift: e.g.
  `public partial class Tag : IContext` in `tag.cs` — name is `Tag` not
  `@this`, and file is `tag.cs` with no folder. Is this a violation of the
  "folder's primary class is @this in this.cs" rule?
- Method length: `RunSingleAsync` in run.cs is ~90 lines with a nested 40-
  line coverage-subscriber lambda. Split?
- Naming: `UserTags` on `TestRun` vs `File.Tags` — the memory says
  `UserTags → Tags` is a flagged smell (redundant prefix).
- Mixed `App.Test.Results` vs `Results` vs `Test.@this` — verify the
  namespace choices aren't whipsawing.

### Pass 4 — Behavioural Reasoning
- `Testing.Apply` — `TryToInt` has a `double d when d == Math.Truncate(d)`
  cast to int — JSON numeric boxing family. What happens for `3.0` vs
  `3e10`? `(int)3e10` is defined but lossy.
- `ExtractAutoTags` passes `depth=50` cap silently — recursion cycle safety
  depends on `visited` set, so the depth cap is a backup. What happens at
  depth 50? Silent drop of further sub-goals → undertagged tests. Should
  log/warn.
- `ResolveStaticGoalName` JsonElement and IDictionary branches — are these
  actually possible post-load? If GoalMapper always produces typed
  `GoalCall`, these are dead; if not, this is the sharp behavioural edge.
- `RunSingleAsync`'s outer try/catch on `OperationCanceledException` uses
  both `cts.IsCancellationRequested` and `!Context.CancellationToken
  .IsCancellationRequested` — outer cancel path yields which status?
  (Currently: bubble Exception clause marks Fail. Should it be a distinct
  Cancelled?)
- `childApp.Testing.Coverage.RecordModuleAction` fires on EVERY
  AfterAction — including modifiers, events, and the orchestrator's inner
  condition.if re-fires. `IsFirstConditionInStep` filters branch
  recording but NOT module.action recording, so `condition.if` gets
  recorded twice for orchestrate paths. Behavioural.
- `BranchChain.ComputeFor` — single-action step returns `[true,false]`.
  `If.Run` also publishes `[true,false]` in the simple path. Verified
  consistent. But `If.Orchestrate` builds `declaredChain` itself via a
  separate loop — does it match BranchChain.ComputeFor for the same input?
  Two sources of truth → drift risk.
- `AssertSnapshot.WithVariables` — `err.Variables == null` guard. If the
  same AssertionError is returned twice (e.g. via cached Data), the first
  snapshot wins. Intended (architect §4.6) or accidental?
- `AfterAction` widening: the signature change means every existing
  registrant must be updated. Check that `Debug/this.cs`, `event/on.cs`,
  `mock/action.cs` all pass (`_,_,_`) not (`_`). Confirmed above but
  verify no grep-missed sites.
- `Modifiers.RunAsync` fires `AfterAction` for each modifier *after* the
  chain — what if the chain fails? `result` is the failure Data; is it ok
  to emit AfterAction with a failing Data? Existing subscribers assumed
  success? Coverage subscriber ignores success flag → fine. Others?
- `discover.cs` line 98 comment says "the stored Hash and BuilderVersion come
  directly from the built artefact" — but freshness test compares
  `currentGoal.Hash` (re-computed from re-parsed source) vs `prGoal.Hash`
  (stored). If Goal.Parse normalises differently in runtime than the
  builder wrote, every test looks stale. Trace.
- `run.cs` creates `childApp` per test with `new App.@this(test.Directory)`.
  Is this file-system-directory the correct isolation boundary? If two
  tests share a directory, they share file-lock state. Is that intended?
- `report.cs` `ResolveBuilderVersion` returns `testing.App.Version`.
  But test files carry their own `BuilderVersion` (per-test, from .pr).
  Drift report compares per-test vs run-wide. What sets `App.Version`?
  Is it the running builder's own version, or something else? If the
  runner doesn't set it, every test flags as drift.

### Pass 5 — Deletion Test
For every new file, ask "can I delete lines X-Y and have a test fail?":
- `Coverage`'s `Merge` — is it hit by any test? run.cs calls it; a C#
  CoverageTests fixture exists. OK.
- `TestStatus.Ready` is set by TestFile default — is Ready ever observed
  in a Results entry? Not-Ready tests short-circuit in `RunSingleAsync`
  to `skipRun.Complete(test.Status, skipRun.Error)`. Ready tests get
  `TestRun(file)` which copies Ready → TestRun.Status. If the goal never
  completes cleanly this Ready leaks out. Is Ready-as-final-status
  reachable?
- `AssertSnapshot.WithVariables` null-check on `err.Variables` — if the
  Variables snapshot is only attached once, why does every caller wrap
  in `AssertSnapshot.WithVariables`? Could be on the error itself.
- `BranchChain.ComputeFor`'s `chain.Count == 0` fallback to `[true,false]`
  comment says "Orchestrate ran on what looked like a single-action". With
  the `myIndex < actions.Count` invariant and `IsConditionAction(actions[
  myIndex])` at entry, is this branch ever reachable?
- `discover.cs`'s `empty` local (line 44): constructed even when paths pass
  — micro-simplification.
- `report.cs`'s `StripAnsi` regex — does any path feed ANSI bytes into
  `CapturedOutput`? run.cs never sets `CapturedOutput`. If coder never
  wired capture, the whole CapturedOutput branch is dead.
- `TestRun.Complete(Data.@this)` — who calls this? run.cs calls
  `testRun.Complete(result)` which dispatches to the status form. If only
  one call site uses it, consider inlining.

## Output deliverables

Per character spec:

1. **`.bot/runtime2-test-module/codeanalyzer/v1/result.md`** — per-file
   analysis in the character's format (OBP Violations, Simplifications,
   Readability, Verdict). Every file listed above gets a section; clean
   files get a single "Verdict: CLEAN" line.
2. **`.bot/runtime2-test-module/codeanalyzer/v1/summary.md`** — version
   summary in the `v<N>/summary.md` format.
3. **`.bot/runtime2-test-module/codeanalyzer/summary.md`** — bot-root
   one-paragraph cross-session file.
4. **`.bot/runtime2-test-module/codeanalyzer/v1/verdict.json`** — pass/fail
   + one-line summary.
5. **`.bot/runtime2-test-module/codeanalyzer/v1/changes.patch`** — empty
   analysis session, but still a patch of `.bot/` additions.
6. **`.bot/runtime2-test-module/report.json`** — session entry with
   before/plan/actions/after.

When I'm done:
- If CLEAN → suggest **tester** runs next.
- If NEEDS WORK / MAJOR ISSUES → suggest sending back to **coder**.

Then commit `.bot/` and push.

## Estimated effort

Volume: ~1,400 lines of new C# across 13 new files + targeted review of ~7
modified files. Five passes means ~70 "reads". I'll batch-read files, then
write `result.md` file-by-file.

## Blocked?

No. Branch is fetched, everything compiles (per coder summary). Proceeding
after your OK.
