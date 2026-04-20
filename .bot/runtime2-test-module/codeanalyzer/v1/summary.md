# v1 Summary — Codeanalyzer: PLang Test Module Review

## What this is

Post-implementation code review of the coder's v1 test-module rewrite
(`.bot/runtime2-test-module/coder/v1/summary.md`, commits
`1178300a..8f7fcaf6`). Replaces the `foreach`-based PLang runner with a
C#-driven one: per-test App isolation, semaphore-throttled parallelism,
timeout via CancellationToken, AfterAction-subscribed coverage,
JSON/JUnit output, drift detection via builder version + Goal.Hash.

Scope: 13 new C# files (~1,400 lines) + ~15 modified runtime files.
Out of scope: `.goal` test fixtures, `.pr` output, PLang.Tests/* test files
(test-designer's contract).

Five passes per character spec: OBP compliance → Simplification →
Readability → Behavioural reasoning → Deletion test.

## What was done

Read every file in scope end-to-end, grounded each finding in a file:line
snippet, and graded each file CLEAN / NEEDS WORK / MAJOR ISSUES.

**Verdict: NEEDS WORK** (fail). Design is sound; four must-fix findings
(three single-site, one OBP pattern), several smaller simplifications
rounded out the report.

### Must-fix

1. **`discover.cs:77`, `report.cs:259` — `System.IO.Path.X` used directly.**
   CLAUDE.md: *"NEVER use System.IO. Always use `fileSystem.Path`."* Both
   calls (`ChangeExtension`, `GetDirectoryName`) are pure string ops, but
   the rule is absolute. Quick swap.

2. **`run.cs:141-142` — copy-loop is a no-op.**
   `childApp.Testing.CurrentTest` IS `testRun` (set on line 72), so
   `testRun.UserTags` and `childApp.Testing.CurrentTest.UserTags` are the
   same `HashSet<string>`. Iterating and adding-to-self is dead code.
   Delete the loop.

3. **`if.cs:160-165` — duplicated declared-chain logic.**
   `If.Orchestrate` builds `declaredChain` inline; `BranchChain.ComputeFor`
   builds it too. Two sources of truth for the same derived chain — classic
   drift-risk pattern (CLAUDE.md's recurring-bug-patterns §Clone/Copy
   Family). Use `BranchChain.ComputeFor(actions, myIndex)` and delete the
   inline loop.

4. **OBP rule 1 + rule 5 — outside-iteration cluster (six new
   instances in this branch).** See "OBP outside-iteration cluster"
   in `result.md`. Handlers and utilities iterate
   `Goal.Steps` / `Step.Actions` / `Action.Parameters` from outside
   the owner:
   - `discover.cs` triple walk (`ExtractUserTags`, `ExtractAutoTags`,
     `SeedBranchChains`) — same foreach-step-foreach-action skeleton
     copied three times.
   - `BranchChain.cs` static utility taking `StepActions` as parameter —
     textbook rule 5 anti-pattern; belongs on `Actions.@this`.
   - `if.cs Orchestrate` — three outside loops: finding self via
     `ReferenceEquals` scan, splitting actions into branches,
     disabling indented sub-steps.
   All should move to instance methods on `Goal` / `Steps` / `Actions`
   (visitor or domain ops, author's choice). Pre-existing instances in
   `mock/action.cs`, `DefaultBuilderProvider.cs`, `GoalCall.cs` are
   out of scope for this branch.

### Should-fix

4. `TestFile.cs:24,27,30` — extracted fields (`EntryGoalName`, `GoalHash`,
   `BuilderVersion`) duplicate `Goal.Name/Hash/BuilderVersion`. Rule 3
   violation ("keep object references, not extracted fields").
5. `Test/this.cs:125` — `double → int` cast without range check. JSON
   numeric boxing family.
6. `discover.cs:48-52` — bare `catch` on `ValidatePath`. Narrow the
   scope.
7. `Test/this.cs:47-49` — `Testing.App` back-reference only used by one
   caller that has `Context.App` available directly. Remove the back-ref.

### Consider

8. `Coverage.cs` — composite `"module.action"` key + `IndexOf('.')` split
   → tuple key.
9. `report.cs:296-297` — `ResolveBuilderVersion` one-line delegator; inline.
10. `BranchChain.cs:33-34` — unreachable fallback; delete.
11. `tag.cs` class naming — `Tag` vs peer `run`/`discover`/`report`.
12. `CapturedOutput` field — wire stdout capture or delete the dead field
    + report-rendering branch + `StripAnsi` regex.

### v2 follow-ups (flagged, not this review)

- True `else` branch semantics — coder's v1 summary already flagged this.
- `Cancelled` TestStatus distinct from `Fail` on outer cancellation.
- Capability tags for `db.*`, `signing.*`, `code.*`.

## Code example — the pattern

The duplicated declared-chain logic in `if.cs:160-165` illustrates the
"Clone/Copy Family" drift risk from CLAUDE.md:

```csharp
// if.cs Orchestrate — BUILDS declaredChain INLINE
var declaredChain = new List<string>();
for (int di = 0; di < branches.Count; di++)
{
    var (dc, _) = branches[di];
    declaredChain.Add(di == 0 ? "if" : dc == null ? "else" : $"elseif[{di}]");
}
lastResult.Properties.Set("branchChain", declaredChain);

// BranchChain.cs ComputeFor — does almost the same thing, different shape
public static List<string> ComputeFor(StepActions actions, int myIndex)
{
    if (actions.Count == 1) return new List<string> { "true", "false" };
    var chain = new List<string>();
    for (int i = myIndex; i < actions.Count; i++)
        if (IsConditionAction(actions[i]))
            chain.Add(chain.Count == 0 ? "if" : $"elseif[{chain.Count}]");
    return chain;  // NEVER emits "else" — the comment explains why
}
```

Two places that must agree to keep coverage coherent. If someone adds a
new branch type and forgets to update both, coverage reports silently
lie.

OBP form: `var declaredChain = BranchChain.ComputeFor(actions, myIndex);`
— one function, one truth, Orchestrate uses the same data the
discover-time seeder uses.

## Deliverables

- `v1/plan.md` — what I planned, what I scoped out, what I expected to
  find.
- `v1/result.md` — per-file findings with file:line, snippet, OBP form,
  why-it-matters. Every file in scope has a section; clean files say so.
- `v1/verdict.json` — `{"status":"fail", ...}`.
- `v1/summary.md` — this file.
- `v1/changes.patch` — git diff of `.bot/` (only .bot/ changes this
  session).
- `.bot/runtime2-test-module/codeanalyzer/summary.md` — bot-root one-
  paragraph cross-session summary.
- `.bot/runtime2-test-module/report.json` — session entry appended with
  before/plan/actions/after.

## What to do next

**Suggested: send back to coder** for fixes. The three must-fix items are
all small-surface changes. The should-fix items are also small. Once
addressed, re-review (v2) should be quick, then tester.

If the coder disagrees with any finding, this review is a starting point
for discussion, not a ruling — especially items 4, 7, 8, 11, 12 which are
taste/trade-off calls.
