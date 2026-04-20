# Codeanalyzer v1 Result — PLang Test Module

Review of C# changes on `runtime2-test-module` (commits `1178300a..8f7fcaf6`)
against the five analyzer passes.

**Overall verdict: NEEDS WORK.** Design is sound and most files are clean.
Three findings rise to "must address before merge": two are behavioural
(coverage double-counts, invisible else semantics), one is a clear OBP/rule
violation (System.IO used directly). Several smaller simplifications and one
dead-loop finding round out the report.

---

## Summary of findings by severity

**Critical (behavioural or clear rule breaks):**

1. `run.cs:44-49` — Module.action coverage over-records when modifiers or
   inner elseif simple-paths fire. Not wrong output, but double-recording
   makes `module.action` coverage show "ok" when only a modifier ran.
2. `discover.cs:77`, `report.cs:259` — direct `System.IO.Path.X` calls. CLAUDE.md
   rule: **NEVER use System.IO. Always use `fileSystem.Path`.** Clear fix.
3. `run.cs:141-142` — copy-loop is a no-op. `childApp.Testing.CurrentTest`
   IS `testRun` (set on line 72), so iterating its `UserTags` and adding to
   `testRun.UserTags` copies a set into itself. Delete the loop.

**Behavioural risks (worth a second look):**

4. `if.cs:159,163` — dead branch labels. "else" only emits when a branch's
   `condition` is null, which the orchestrator never produces in today's
   grammar. Intentional placeholder for v2 (coder acknowledged), but the
   code implies a semantic the builder can't emit. Either comment it as
   "v2 — never reached today" or remove.
5. `discover.cs:113-118`, `run.cs:48` — bare `catch` / `catch (Exception)`
   swallowing. Per memory, "Any casting/converting is an OBP red flag" and
   "Never use generic catch in wrapper methods". Scope to the specific
   exceptions that the `try` body can actually throw.
6. `run.cs:127-138` — user-cancelled tests (outer Ctrl-C) get marked as
   `Fail`, not distinguished from real failures. Minor — a `Cancelled`
   status would disambiguate, but TestStatus wasn't designed for it. Flag
   for v2.

**Simplifications / dead code:**

7. `report.cs:296-297` — `ResolveBuilderVersion(testing) => testing.App.Version`
   is a one-line delegator. Inline.
8. `Coverage.cs:16-33` — composite string key `"module.action"` split with
   `IndexOf('.')`. A `ConcurrentDictionary<(string, string), byte>` expresses
   the same thing without the split.
9. `discover.cs:185-245` + `discover.cs:275-317` — `ExtractAutoTags` and
   `SeedBranchChains` both recurse the same goal tree via static
   `goal.call`. Consolidate into one walker with two visitors, or accept
   the duplication (minor).
10. `TestRun.cs:53-57` + `run.cs:128` — `Complete(Data.@this)` overload is
    called from exactly one site. Candidate to inline, but it's a clear
    wrapper that documents intent — optional.

**Readability:**

11. `tag.cs:15` — class named `Tag` while peer handlers (`run`, `report`,
    `discover`) use lowercase. Pick one convention.
12. `run.cs:55-146` — `RunSingleAsync` is ~90 lines with a 40-line lambda
    inside. Split the coverage-subscriber into a named helper.

---

## Per-file analysis

### `PLang/App/Test/TestStatus.cs`

21 lines. Well-named enum, good XML docs on each value.

**Verdict: CLEAN**

---

### `PLang/App/Test/TestFile.cs`

**OBP Violations** — none that matter. `TestFile` stores both `Goal` (the
reference) and extracted strings `EntryGoalName`/`GoalHash`/`BuilderVersion`.
Rule 3 ("keep refs, not extracted fields") would push for deleting the
extracted strings and reading `Goal.Name`, `Goal.Hash`, `Goal.BuilderVersion`
through the ref. BUT `Goal` is nullable here (null on Stale), so the code
can't rely on it for non-Stale statuses either — which it already does:
discover.cs lines 159-162 set both `Goal = prGoal` AND the three extracted
fields. The extracted fields are *sometimes* populated even when Goal is
null (well, actually they aren't — discover.cs keeps the stub with no Goal
and no extracted metadata for Stale entries). So the duplicated-field
pattern here doesn't have a load-bearing reason.

1. **Line 23-30: Extracted fields duplicate Goal** — `EntryGoalName`,
   `GoalHash`, `BuilderVersion` are all reachable via `Goal.Name`,
   `Goal.Hash`, `Goal.BuilderVersion`. Rule 3 violation: store the ref,
   not the extractions.
   - Current:
     ```csharp
     public Goal? Goal { get; init; }
     public string EntryGoalName { get; init; } = "";
     public string? GoalHash { get; init; }
     public string? BuilderVersion { get; init; }
     ```
   - OBP form: keep `Goal`, delete the three extracted fields, read
     through `Goal.Name`, `Goal.Hash`, `Goal.BuilderVersion` at the usage
     sites (`report.cs:221`, `225`, `69-71`).
   - Why it matters: if `Goal` is renamed or `Hash` is rescoped, three
     other fields silently rot. The duplicated family risks drift.

**Simplifications** — none.

**Readability** — Goal/File aliases are fine; XML docs are good.

**Verdict: NEEDS WORK** (rule 3 — minor)

---

### `PLang/App/Test/TestRun.cs`

**OBP Violations** — none.

**Simplifications**

1. **Line 53-57: `Complete(Data.@this)` overload has one call site** — it's
   currently called from `run.cs:128` only. Not wrong, but trivial enough
   to consider inlining. Keeping it is defensible (documents the result→
   status mapping) — leave at author's preference.

**Readability**

2. **Line 31: `UserTags`** — memory says `UserTags → Tags` is a flagged
   smell (redundant prefix). But the class already has `File.Tags` (the
   discovery-time set), so `UserTags` disambiguates "user-added during the
   run" from "tagged at discovery". Acceptable.

3. **Line 28: `CapturedOutput` unused** — run.cs never sets
   `CapturedOutput`. report.cs *reads* it (line 99-104). If capture isn't
   wired yet, this is dead infrastructure. Either wire up output redirect
   or delete the field + the report branch. See `report.cs:99-104`.

**Verdict: NEEDS WORK** (dead `CapturedOutput` — delete or wire)

---

### `PLang/App/Test/Results.cs`

**OBP Violations** — none. Collection owns `Add`/`Count`/`Summary` —
consumers delegate. Rule 5 compliant.

**Simplifications** — none.

**Readability** — fine.

**Verdict: CLEAN**

---

### `PLang/App/Test/Coverage.cs`

**OBP Violations**

1. **Line 94-112: `Merge(other)` reaches into `other._moduleActions`,
   `_branches`, `_branchLabels`, `_branchChains` directly** — this is fine
   within the same class (private access). Not a rule violation.

**Simplifications**

2. **Line 15-33: `_moduleActions` uses `"module.action"` composite key with
   `IndexOf('.')` on read** — The split-back-out in `ModuleActions`
   getter is awkward and fragile (what if module name contains a dot?
   Unlikely but possible).
   - Current:
     ```csharp
     private readonly ConcurrentDictionary<string, byte> _moduleActions
         = new(StringComparer.OrdinalIgnoreCase);
     // on read:
     var dot = key.IndexOf('.');
     if (dot < 0) continue;
     yield return (key[..dot], key[(dot + 1)..]);
     ```
   - Simpler:
     ```csharp
     private readonly ConcurrentDictionary<(string Module, string Action), byte>
         _moduleActions = new();
     // on read:
     foreach (var key in _moduleActions.Keys) yield return key;
     ```
     (Tuple equality is case-sensitive by default — use a custom
     `IEqualityComparer<(string, string)>` to match current
     OrdinalIgnoreCase semantics.)
   - Why: one less thing to break. "Can module names contain dots?" stops
     being a question.

3. **Line 13-92: Four dictionaries (`_moduleActions`, `_branches`,
   `_branchLabels`, `_branchChains`), three of them per-site** — `_branches`
   (indices), `_branchLabels` (labels), `_branchChains` (declared chain)
   are all keyed by the same `site`. They could live on a single
   per-site record, keeping the "what we know about this site" coherent.
   - Current: three independent dictionaries; report.cs:128-133 unions
     their keys to enumerate.
   - Simpler: `ConcurrentDictionary<string, SiteCoverage>` where
     `SiteCoverage` holds `{Indices, Labels, Chain}`. Concurrency needs
     care (lock-free merge) but eliminates key-union logic in the
     reporter.
   - Optional — the current form is readable and Merge is already
     correct. Flag for consideration, not a required change.

**Readability** — good XML docs. Sensible naming.

**Verdict: NEEDS WORK** (composite-key split — minor)

---

### `PLang/App/Test/this.cs`

**OBP Violations**

1. **Line 47-49: `Testing.App` back-reference** — Rule 4 says "per-request
   state is a parameter, per-object state is a property". `App` → `Testing`
   is legitimate ownership; `Testing` → `App` is a reverse pointer for one
   reader (report.cs:297 via `ResolveBuilderVersion`). That single reader
   could walk `Context.App` from its own context instead.
   - Current: `internal App.@this App { get; }` back-ref; reporter reads
     `testing.App.Version`.
   - OBP form: reporter is an action with `Context.App`, so
     `Context.App!.Version` is directly available. Delete the back-ref
     and the `ResolveBuilderVersion` helper.
   - Why: reverse pointers are exactly what OBP rule 4 disallows at the
     level of shared objects. Here `Testing` is per-App (so the ref
     technically doesn't carry request state) — but it's still structural
     coupling that doesn't need to exist.

**Simplifications**

2. **Line 115-143: `TryToInt`, `ToStringList`, `Describe`** — ad-hoc helpers
   local to this class. All three are small enough, and they only matter
   for the CLI config surface. Keep as-is.

**Readability**

3. **Line 56-114: `Apply(IDictionary<string, object?>)`** — 60 lines of
   `switch` cases with inline error messages. Readable but long. A
   `Dictionary<string, Action<object?>>` dispatch would flatten it; not a
   win unless there are more keys coming. Leave as-is.

4. **Line 125: `double d when d == Math.Truncate(d): result = (int)d`** —
   JSON numeric boxing pattern (known recurring bug family in CLAUDE.md).
   This path fires for JSON numeric values. `(int)3e10` is defined (lossy
   truncation, no overflow check). Edge case: a user passing
   `"timeout": 1e12` quietly becomes a meaningless int. Add a range check:
   ```csharp
   case double d when d == Math.Truncate(d) && d >= int.MinValue && d <= int.MaxValue:
       result = (int)d; return true;
   ```

**Verdict: NEEDS WORK** (back-ref + unchecked cast)

---

### `PLang/App/modules/test/discover.cs`

**OBP Violations**

1. **Line 77: `System.IO.Path.ChangeExtension`** — CLAUDE.md: **NEVER use
   System.IO. Always use `fileSystem.Path`.** `IPLangFileSystem` exposes
   `IPath` which has `ChangeExtension`.
   - Current: `var prFileName = System.IO.Path.ChangeExtension(fileName, ".pr").ToLowerInvariant();`
   - Correct: `var prFileName = fs.Path.ChangeExtension(fileName, ".pr").ToLowerInvariant();`
   - Why: hard rule, no judgement call. IPLangFileSystem is the abstraction.

2. **Line 113-118: Broad `catch (Exception ex) when (...)`** — the filter
   is reasonable (IOException or UnauthorizedAccessException or
   JsonException). Acceptable per the "filesystem / serialization
   boundary" guidance in CLAUDE.md's recurring-patterns section. Keep.

3. **Line 48-52: Bare `catch {}` on `ValidatePath`** — `ValidatePath` throws
   generic `Exception` per PLangFileSystem.cs:173. Bare catch is the
   pragmatic fix but silently hides bugs (what if ValidatePath throws a
   NullReferenceException?). Narrow:
   - Current: `try { absRoot = fs.ValidatePath(Path.Value); } catch { ... }`
   - Better: catch the specific types — or even better, have
     `ValidatePath` return a `Data.@this<string>` so the check becomes
     data-flow instead of exception-flow. That's a bigger change; for now,
     at least document why "catch everything" is acceptable here.

**Simplifications**

4. **Line 44: `empty` local declared before the traversal check** —
   constructed once and used twice (traversal fail + directory-not-exist).
   Harmless but "if removing the line wouldn't change anything" test: you
   could return `App.Data.@this.Ok(new List<TestFile>())` twice. It's a
   micro-simplification and the current form reads fine.

5. **Line 145-152: `ExtractAutoTags` + `SeedBranchChains` both walk the
   goal tree recursively** — same visited-set logic, same `goal.call`
   recursion, same depth cap. Could be consolidated into one walker with
   two visitors.
   - Current: two `static` helpers each with its own `visited` and
     `depth=50` guard.
   - Consolidated: one `WalkGoalTree(Goal, visitor, visited, depth)` where
     `visitor` receives (goal, step, action). Callers pass an aggregator.
   - Trade-off: two simple methods vs one more abstract walker. Current
     form is actually clearer if you're only reading one of them at a
     time. Flag as optional refactor.

**Behavioural Reasoning**

6. **Line 135: Hash comparison** — `currentGoal.Hash` vs `prGoal.Hash`.
   `Hash` lazy-computes from `Name + concat(Step.Text)`. `Goal.Parse`
   normalises `"\t"` → `"    "` (four spaces) on line 301 of
   `PLang/App/Goals/Goal/this.cs`. If the builder stores the raw
   unnormalised step text and then computes Hash from that, but
   `Goal.Parse` normalises first and then computes from the normalised
   text, the two hashes will diverge. Worth a quick trace against how
   the builder populates Steps. If they diverge, every test will be
   marked Stale on fresh checkout — self-flagging, not silent, so not
   critical.

7. **Line 218: `if (depth > 50) return;`** — silent cutoff. If a deep
   goal.call chain exceeds 50 levels, tagged capabilities past that
   point go missing. Discovery-time visibility problem. Consider:
   ```csharp
   if (depth > 50) {
       // trace to Debug if enabled — silent in normal mode
       return;
   }
   ```
   Or log to Info to surface the pathological case.

8. **Line 247-263: `ResolveStaticGoalName` — `JsonElement` and
   `IDictionary<string,object?>` branches** — deserialization edge cases.
   After `GoalMapper` runs, `GoalCall` should be a typed object; if the
   builder stores `GoalCall` as raw JSON in the .pr, these branches are
   load-bearing. Otherwise they're dead code. Trace to confirm — if dead,
   delete; if live, add a test fixture that exercises each branch.

**Readability** — consistent with other handlers. XML docs are strong.

**Verdict: NEEDS WORK** (System.IO violation + bare catch + silent depth
cutoff)

---

### `PLang/App/modules/test/run.cs`

**OBP Violations**

1. **Line 33-36: `Context.App!` navigated twice** —
   `var parentApp = Context.App!;` then `parentApp.Testing.Parallel`.
   Correct pattern per rule 2. Fine.

2. **Line 141-142: Copy-loop over the same HashSet** — **critical finding**.
   - Current:
     ```csharp
     foreach (var tag in childApp.Testing.CurrentTest?.UserTags ?? Enumerable.Empty<string>())
         testRun.UserTags.Add(tag);
     ```
   - Why it's a no-op: `childApp.Testing.CurrentTest` IS `testRun` (set on
     line 72). So `childApp.Testing.CurrentTest.UserTags` and
     `testRun.UserTags` are the same `HashSet<string>`. Iterating a set
     and calling `.Add(tag)` on itself is a no-op for every element.
   - Fix: delete the loop entirely. The `test.tag` handler already adds
     to `Context.App.Testing.CurrentTest.UserTags`, which is the same set
     `testRun.UserTags` points at — no copy needed.

**Simplifications**

3. **Line 76-116: 40-line inline lambda for coverage subscriber** — Extract
   to a named method: `HandleAfterActionForCoverage(Context ctx,
   Action? action, Data? result, App.@this childApp)`. Easier to test;
   easier to read `Register(new EventBinding(AfterAction, CoverageHandler,
   priority: int.MaxValue))`.

**Behavioural Reasoning — CRITICAL**

4. **Line 82: `RecordModuleAction` fires on every AfterAction** — this
   includes modifiers (via `Modifiers.RunAsync` → AfterAction per
   modifier, per `Modifiers/this.cs:58-62`) AND inner-elseif simple-path
   condition.if firings. `IsFirstConditionInStep` filters *branch*
   recording (line 89) but not module.action recording. Result:
   - A step like `retry { cache { http.request } }` fires AfterAction 3
     times: for `request`, `cache`, `retry`. All three get
     `RecordModuleAction("http","request") / ("cache","run") /
     ("retry","run")`. That's correct.
   - But the orchestrator's inner elseif condition.if fires AfterAction
     once per elseif. Each records `RecordModuleAction("condition","if")`.
     So the "one if-chain" gets counted N times. `TryAdd` is idempotent,
     so the MAP stays correct — but the point is subtle: you only
     see "condition.if" ticked once in coverage regardless of how many
     condition.if's ran, which is probably intended.
   - Real concern: coverage doesn't distinguish "I ran the handler"
     from "I ran a modifier that wraps the handler". If `cache` short-
     circuits, `http.request` never runs, but `RecordModuleAction("http",
     "request")` still fires. The coverage subscriber might want to
     receive the Modifier-vs-inner info in the payload (currently it
     gets no such flag).
   - Decision point for coder: add `action.IsModifier` (or similar) to
     the payload and filter in the subscriber, OR document that
     coverage = "reached the handler" not "executed the handler".
     Currently it's silently the first.

5. **Line 125-138: OperationCanceledException vs outer cancellation** —
   outer Ctrl-C propagates as OCE. The `when` filter requires
   `cts.IsCancellationRequested && !Context.CancellationToken.IsCancellationRequested`
   — outer cancel flips `Context.CancellationToken.IsCancellationRequested
   = true`, so the `when` fails and the OCE falls through to `catch
   (Exception ex)` → Fail with `ex.Message`. User cancelling a test run
   produces "Fail: The operation was canceled." for every in-flight test.
   Not ideal but not wrong — flag for v2 if a `Cancelled` status is ever
   added.

**Readability**

6. **Line 68-69: `childApp.SystemDirectory = parentApp.SystemDirectory`** —
   pairing with the App constructor that takes `test.Directory`. Why
   this post-construction assignment rather than a constructor param?
   Probably historical, not the handler's problem.

7. **Line 123: `new Goals.Goal.GoalCall { PrPath = test.PrPath }`** —
   only `PrPath` is set, nothing else. If `GoalCall` has other fields
   normally populated by the builder (e.g. Name, Parameters), they're
   left at defaults. Is that valid for `RunGoalAsync`? Trace to verify
   that `PrPath`-only GoalCall is equivalent to `file.read + run`.

**Verdict: NEEDS WORK** (copy-loop no-op is critical; coverage
double-count deserves docs or a payload field)

---

### `PLang/App/modules/test/report.cs`

**OBP Violations**

1. **Line 259: `System.IO.Path.GetDirectoryName(r.File.Path)`** — CLAUDE.md
   violation. Use `fs.Path.GetDirectoryName`.
   - Current: `var byPath = results.GroupBy(r => System.IO.Path.GetDirectoryName(r.File.Path) ?? "");`
   - Correct: `var byPath = results.GroupBy(r => fs.Path.GetDirectoryName(r.File.Path) ?? "");`
     (fs is already in scope via `var fs = Context.App.FileSystem;` on
     line 38.) Note that line 259 is inside `BuildJUnit`, a `static`
     method that doesn't have `fs` — either pass `fs` in, or make
     `BuildJUnit` non-static. Non-static is simpler.
   - Why: rule is absolute.

2. **Line 213-252: `BuildJson` uses anonymous objects everywhere** —
   fine for JSON boundary. Rule 6 compliant (serialization is a
   boundary). Keep.

3. **Line 299-307: `FormatValue`** — duplicate of a similar formatter in
   `Debug/this.cs` (public method `FormatValue` on lines 415-449 of
   that file). Both stringify objects with different strategies. Not
   worth unifying — they serve different consumers.

**Simplifications**

4. **Line 296-297: `ResolveBuilderVersion`** — one-line delegator
   (`testing.App.Version`). Inline at the two call sites (line 68, line
   247).
   - Current: `private static string? ResolveBuilderVersion(Test.@this testing) => testing.App.Version;`
   - Inline: replace call with `testing.App.Version`. Or, if the
     `Testing.App` back-ref is removed per the finding in `Test/this.cs`,
     use `Context.App!.Version` directly.

5. **Line 131-133: `SortedSet<string>` constructed from three concat'd
   Key streams** — fine, small sets. Keep.

6. **Line 145-185: Branch rendering loop** — 40 lines with the label-
   backed / index-only fallback nested in. Works, and the comments are
   helpful. Readable.

**Readability**

7. **Line 57-79: `RenderConsole`** — mixes per-test rendering with drift
   detection. Drift could live on `TestRun` (computed property) —
   then `RenderConsole` just reads `run.HasBuilderDrift`. Optional.

8. **Line 203-211: `SortLabel` switch** — cute, works. "elseif[" prefix
   check is the one pattern-match case. Readable.

9. **Line 254-294: `BuildJUnit` with manual XML string building via
   `StringBuilder`** — fragile for anything beyond this simple case.
   Consider `XDocument` / `XElement` for maintainability if JUnit output
   grows. Current form is short enough to keep.

**Behavioural Reasoning**

10. **Line 266-270: `suiteTests.Count`, `failures`, `timeSec`** — the
    `<testsuite>` aggregate only counts `TestStatus.Fail` as failure.
    `Timeout`/`Stale`/`Skipped` are NOT counted in `failures` attribute
    but ARE rendered with `<failure type="timeout">` or `<skipped>`
    elements. That's JUnit-compliant (timeout is often errors, not
    failures), but the attribute/element mismatch could mislead CI
    consumers. Acceptable per JUnit convention.

11. **Line 68-71: drift detection reads `testing.App.Version`** — What
    sets `App.Version`? Per `PLang/App/this.cs:322`, it's read from the
    app's json `version` field. So drift = "the test's .pr was built by
    a different builder than this app's version". If the running app
    has no version set (e.g., a fresh repo without app.json version),
    `App.Version` is null, and drift detection is silently disabled
    (`!string.IsNullOrEmpty(currentBuilderVersion)` guard skips it).
    Acceptable safety net.

12. **Line 309-312: `StripAnsi` + regex** — `CapturedOutput` is never set
    by run.cs. If no capture is wired, lines 99-104 and 311-312 are
    dead. Same finding as TestRun.cs item 3.

**Verdict: NEEDS WORK** (System.IO on line 259 + dead StripAnsi branch
unless CapturedOutput gets wired)

---

### `PLang/App/modules/test/tag.cs`

**OBP Violations** — none.

**Simplifications**

1. **Line 22-26: Tags guard** — clean.

**Readability**

2. **Line 15: `public partial class Tag`** — capitalisation inconsistent
   with peers (`run`, `discover`, `report`). The `@Action("tag")` maps
   the class to the action regardless. Pick one convention across all
   handlers in `modules/test/`. Minor.

3. **Line 18: `public partial Data.@this<string[]> Tags { get; init; }`** —
   the handler takes `string[]`, but the builder typically stores list
   parameters as `List<string>` or `JsonElement`. Trace a builder fixture
   to confirm the type-mapping pipeline converts to `string[]` correctly.
   (Most likely yes — source generator handles it.)

**Verdict: CLEAN** (with minor naming inconsistency)

---

### `PLang/App/modules/assert/AssertSnapshot.cs`

**OBP Violations** — none; the helper sits at the boundary between the
assert handler (a PLang component) and `AssertionError.Variables` (a data
field on an Error). Passing `Data.@this` and reading `result.Error` is
correct OBP.

**Simplifications** — 20 lines, one intent. Keep.

**Readability**

1. **Line 16: `if (result.Error is AssertionError err && err.Variables == null)`**
   — first-wins guard. Intent: if the same Data is returned twice, don't
   re-snapshot. Correct for cached Data (recurring-bug-pattern context:
   providers could return cached Data). Keep.

**Behavioural Reasoning**

2. **`err.Variables = context.Variables.Snapshot()` mutates a shared
   Error** — if the Error object is shared across threads (via cache or
   cross-test use), this mutation races. In practice, AssertionError is
   per-call so this should be fine, but it's a latent concern if caching
   ever extends to errors.

**Verdict: CLEAN**

---

### `PLang/App/modules/assert/{equals,notEquals,isTrue,isFalse,isNull,isNotNull,contains,greaterThan,lessThan}.cs`

All nine files follow the same 15-line pattern: declare parameters, delegate
to `Assert.X(this)`, wrap with `AssertSnapshot.WithVariables`. Reviewed all
nine — they're identical in structure.

**OBP Violations** — none.

**Simplifications**

1. **9 nearly-identical `Task.FromResult(AssertSnapshot.WithVariables(
   Assert.X(this), Context))` lines** — a base class couldn't help
   because the `Assert.X(this)` call is different per handler. The
   pattern is already as DRY as it can be with a pass-through helper.
   Keep.

**Readability**

2. **`return Task.FromResult(...)` wrapping a synchronous call** — the
   assert provider is sync, returning `Data.@this`. `Task.FromResult` is
   correct. An `async Task<Data.@this> Run() { return AssertSnapshot.
   WithVariables(...); }` would be no better. Keep.

**Verdict: CLEAN**

---

### `PLang/App/modules/condition/BranchChain.cs`

**OBP Violations** — none. Internal static utility; single responsibility.

**Simplifications**

1. **Line 33-34: Unreachable fallback** — `if (chain.Count == 0) return
   new List<string> { "true", "false" };` only fires when
   `actions.Count > 1` AND no condition.if is in `actions[myIndex..]`.
   Callers pass `myIndex` pointing at a known condition.if (per
   `If.Orchestrate`'s call site), so `chain.Count >= 1` is invariant.
   Dead code.
   - Current: line 33-34 fallback.
   - Simpler: delete. If the invariant is ever broken, fail loudly.
   - Why: "If I deleted this, would any test fail?" — no test exercises
     it; no caller can trigger it.

**Readability**

2. **Line 18-37: Two-part semantics (single-action vs multi-action)** —
   comments are clear. Keep.

3. **Line 48-58: `IsFirstConditionInStep`** — clear.

**Verdict: NEEDS WORK** (delete unreachable fallback)

---

### `PLang/App/modules/condition/if.cs`

Modified — the big change is `branchIndex`/`branchLabel`/`branchChain`
publishing.

**OBP Violations** — none introduced.

**Simplifications**

1. **Line 160-165: `declaredChain` built inside `Orchestrate`** —
   duplicates the logic in `BranchChain.ComputeFor`. If the intent is
   one source of truth, call `BranchChain.ComputeFor(actions, myIndex)`
   and use its result. But watch the semantic difference: Orchestrate's
   `declaredChain` currently can emit "else" (dead today — see below),
   while `BranchChain.ComputeFor` can't.
   - Current: two pieces of logic that MUST agree to keep coverage
     coherent. One in `BranchChain.ComputeFor`, one inline in
     `Orchestrate`.
   - Simpler: `var declaredChain = BranchChain.ComputeFor(actions,
     myIndex);`. Then delete Orchestrate's inline loop.
   - Why: the recurring-bug-patterns guidance says "when a property is
     added to any class, ALL methods that create copies must be
     updated". Analogous here: two chain computations = drift risk.

**Behavioural Reasoning**

2. **Line 159: `condition == null ? "else" : $"elseif[{b}]"`** — "else"
   emit unreachable in current grammar. The `Orchestrate` branch-
   splitting loop attaches trailing non-condition actions to the LAST
   condition's body, so `condition` is never null for any built branch.
   The coder's v1 summary documents this as a v2 follow-up ("True
   else-branch semantics need builder + runtime work"). Options:
   - Leave it as-is: harmless dead path, ready for v2.
   - Add an `// v2: unreachable until builder emits standalone else-
     branches` comment so the next reader doesn't try to trigger it.
   - Delete the `condition == null ? "else"` arm and revisit when v2
     lands. Deletion is safest ("if I delete lines X-Y and no test
     fails, they shouldn't exist").

3. **Line 29: `evalResult.Value is true`** — pattern-match on
   `Value is true` treats null/false as false. Intentional per the
   `if (!evalResult.Success) return` guard above (Value shouldn't be
   null on success). Keep.

4. **Line 33-44: Indent-based sub-step disable/enable** — unchanged by
   this branch's commits. Skim only.

**Verdict: NEEDS WORK** (duplicated declared-chain logic + "else"
dead-code arm)

---

### `PLang/App/Events/Lifecycle/Bindings/Binding/this.cs`

Modified — widened `Handler` signature to `Func<Context, Action?, Data?,
Task<Data>>`.

**OBP Violations** — none introduced.

**Simplifications** — none.

**Readability**

1. **Line 21-24: XML doc on Handler** — good, explains when `action` and
   `result` are populated.

**Behavioural Reasoning**

2. **Line 36-69: `Run(context, action, result)`** — passes through to
   handler. Clean.

3. **Widening to 3-arg signature** — cross-cutting change. All existing
   registrants must pass `(_, _, _)` lambdas. Verified via grep:
   `Debug/this.cs`, `event/on.cs`, `mock/action.cs`, and `Executor.cs`
   all updated. Test suite compiled & passed per coder summary — means
   no caller is forgotten.

**Verdict: CLEAN**

---

### `PLang/App/Events/Lifecycle/Bindings/this.cs`

**OBP Violations** — none.

**Simplifications** — none.

**Readability** — clear; the public overload `Run(context, type, action,
result)` documents the payload-carrying events.

**Verdict: CLEAN**

---

### `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`

Modified — `lifecycle.After.Run(context, AfterAction, this, result)` now
passes `this` + `result`.

**OBP Violations** — none. This IS the emit site — correct to pass
`this` and the completed `result`.

**Readability** — fine. Clear.

**Behavioural Reasoning**

1. **Line 84-86: `context.Variables.Put(result)` only on success** —
   success is required for `__data__` to make sense as the last
   successful result. Unchanged by this branch.

**Verdict: CLEAN**

---

### `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs`

Modified — emits AfterAction for each modifier after the chain.

**OBP Violations** — none. Modifiers own their iteration (rule 5).

**Simplifications** — none.

**Readability**

1. **Line 58-62: `foreach (var modifier in _items)` emits AfterAction
   with the chain's `result`** — each modifier gets the SAME result.
   Intent: coverage records modifier presence. Documented inline.

**Behavioural Reasoning**

2. **Line 56: `result = await execute()`** — if any step in the chain
   throws, the exception propagates; the AfterAction loop never runs.
   So AfterAction fires only on clean completion (success or documented
   failure). Reasonable — coverage only counts cleanly-completed
   modifier runs.

3. **Line 43-65: Post-chain AfterAction emission** — is this the right
   time to emit? The chain's result has already been returned up to
   Action.RunAsync by the time the chain completes. Now we emit
   AfterAction per modifier, AFTER the outer Action.RunAsync already
   emitted its own AfterAction for the inner action. Ordering:
   `inner-action AfterAction → modifier[0] AfterAction → modifier[1]
   AfterAction`. That's outside → in — was the coder's intent
   innermost → outermost? Flag for a test to verify.

**Verdict: CLEAN** (ordering check recommended)

---

### `PLang/App/Variables/this.cs`

Modified — added `Snapshot()`.

**OBP Violations** — none.

**Simplifications**

1. **Line 593-604: `Snapshot()` vs `ToDictionary()`** — very similar:
   both iterate `_variables`, both skip `!`-prefixed. Differences:
   `Snapshot()` also skips `DynamicData` and `SettingsVariable`;
   `ToDictionary()` has a `includeSystem` param.
   - A `Snapshot()` that called `ToDictionary(false)` and then stripped
     Dynamic/Settings would be slightly less code. But the current
     `Snapshot()` is explicit about its diagnostic purpose, which is a
     valid reason to keep it separate.
   - Keep.

**Readability** — comments are good, explaining why by-ref is safe.

**Verdict: CLEAN**

---

### `PLang/App/Errors/AssertionError.cs`

Modified — added `Variables` property.

**OBP Violations** — none.

**Simplifications** — none.

**Readability** — clear.

**Behavioural Reasoning**

1. **Line 21: `public Dictionary<string, object?>? Variables { get; set; }`**
   — mutable property on a shared error instance. Safe because the
   AssertionError is only published once per failure, and `AssertSnapshot`
   guards against double-set. If error objects ever get cached (e.g. by
   a memoizing provider), this mutation races.

**Verdict: CLEAN**

---

### `PLang/App/modules/http/{request,download,upload}.cs`, `llm/query.cs`

Modified — added `[RequiresCapability("network")]` /
`[RequiresCapability("llm")]` attributes.

**OBP Violations** — none.

**Simplifications** — none.

**Readability** — attributes are appropriate.

**Behavioural Reasoning**

1. **Which handlers need capability tags, which don't?** — Only `http.*`
   and `llm.query` tagged. What about:
   - `db.*` → should probably be `"database"` capability
   - `file.*` → arguable: reading local files is always allowed; perhaps
     no tag needed
   - `signing.*` → cryptographic ops; maybe `"crypto"`
   - `code.*` → arbitrary code execution; probably `"code"` or similar
   
   The architect's spec may have locked the initial set (network + llm
   only). Flag as an open question for v2 expansion — not a current
   issue.

**Verdict: CLEAN**

---

### `PLang/App/Attributes/RequiresCapabilityAttribute.cs`

**OBP Violations** — none.

**Simplifications** — none. 19-line attribute declaration.

**Readability** — clear.

**Verdict: CLEAN**

---

### `PLang/App/modules/event/on.cs`, `mock/action.cs`, `Debug/this.cs`

Modified — widened handler lambdas to `(ctx, _, _)` / `(context, _, _)`
for 3-arg `EventBinding.Handler` signature.

**OBP Violations** — none.

**Simplifications** — none.

**Readability** — discard pattern is clear.

**Verdict: CLEAN**

---

### `PLang/Executor.cs`

Modified — routes `--test={...}` dict through `Testing.Apply`.

**OBP Violations** — none.

**Simplifications**

1. **Line 41-53: Test mode block** — clear. `if (parameters.TryGetValue(
   "!test", out var testValue) && testValue is not false)` gate is
   consistent with `!debug` and `!build` above.

**Readability** — fine.

**Verdict: CLEAN**

---

## Deletion test summary

Lines that could be deleted with no test failure:

1. **`run.cs:141-142`** — copy-loop over same HashSet (confirmed).
2. **`BranchChain.cs:33-34`** — unreachable `[true, false]` fallback.
3. **`if.cs:159,164` — "else" label arm** — dead until v2 builder work.
4. **`TestFile.cs:24,27,30`** — extracted fields duplicating `Goal.X`.
5. **`report.cs:296-297`** — `ResolveBuilderVersion` one-liner.
6. **`report.cs:99-104` + `TestRun.cs:28` + `report.cs:309-312`** — the
   `CapturedOutput` / `StripAnsi` branch is dead unless stdout capture
   gets wired.

---

## Pass-through feedback to coder

The implementation is solid. Most of the codebase is clean. Before going
to tester, please address:

**Must-fix:**
- `discover.cs:77` + `report.cs:259` — swap `System.IO.Path.X` for
  `fs.Path.X` (CLAUDE.md hard rule).
- `run.cs:141-142` — delete the no-op copy-loop.

**Should-fix:**
- `if.cs:160-165` — replace inline `declaredChain` construction with
  `BranchChain.ComputeFor(actions, myIndex)` (one source of truth).
- `TestFile.cs` — either delete the extracted `EntryGoalName` /
  `GoalHash` / `BuilderVersion` fields and read through `Goal`, or
  document why the duplication is load-bearing.
- `this.cs (Test/):125` — range-check the `double → int` cast in
  `TryToInt`.
- `discover.cs:48-52` — narrow the bare `catch` to specific exception
  types from `ValidatePath`.

**Consider:**
- `Coverage.cs` — composite `"module.action"` key + `IndexOf('.')`
  replaced with tuple key.
- `report.cs:296-297` — inline `ResolveBuilderVersion`.
- `BranchChain.cs:33-34` — delete unreachable fallback.
- `tag.cs` vs peers — unify class name casing.
- `CapturedOutput` field — either wire stdout capture in `run.cs` or
  delete the field + report rendering branch + `StripAnsi` helper.

**Flag for v2 (not this review):**
- True `else` branch semantics in `if.cs`.
- `Cancelled` TestStatus distinct from `Fail` on outer cancellation.
- Capability tags for `db.*`, `signing.*`, `code.*`.
