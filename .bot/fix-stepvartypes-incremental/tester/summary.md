# tester — fix-stepvartypes-incremental

**Version:** v6
**Verdict:** PASS

## What this is

v5 found one regression (5 C# tests failing on a stale template path after the
builder bot's restructure). Coder commit `dfd7429a7` made the two-line fix.
v6 verifies the fix landed clean AND runs proper false-green analysis on the
substantive code changes from v4→v6 — because a green suite is only the
starting point of the tester's job.

## What was done

1. Clean rebuild PlangConsole → 0 errors.
2. `cd Tests && plang --test` → **208/208 pass.**
3. `dotnet run --project PLang.Tests` → **3036/3036 pass.**
4. **False-green hunting** on each substantive change since my v4 PASS.

## False-green analysis (the real job)

### 1. `condition/code/Default.cs` — EvaluateOperator extract (commit 0943e5fda)

Three identical 9-line bodies in `Evaluate(If)`, `Evaluate(Elseif)`,
`Evaluate(Compare)` collapsed into one shared `EvaluateOperator(operatorData,
left, right)`. Risk: did overload dispatch break? Did the helper's parameter
shapes (`data.@this<Operator>`, `data.@this?`) miss a nullability case?

Verified:
- Call sites preserved — `compare.cs:16` `Evaluator.Evaluate(this)`,
  `elseif.cs:21` `await Evaluator.Evaluate(this)`, `if.cs:22` `await
  Evaluator.Evaluate(this)`. Each `this` is the typed action record, so
  C# overload resolution picks the right one-line forwarder.
- Exception filter unchanged (`ArgumentException | OverflowException |
  InvalidCastException`).
- Behavior end-to-end covered by the full PLang condition corpus
  (Operators/, If/Basic, If/ElseBranch, If/Nested, Compound/Or, etc.) and
  by `IfHandlerTests` / `CompareHandlerTests` / `DefaultEvaluatorTests`.

No false green.

### 2. `test/run.cs` — IsEntryGoalStep TrimStart drop (commit 0943e5fda)

Both `.TrimStart('/')` calls removed; comparison now relies on producer
canonicalization (commit `7ed35b550`). Risk: if either side ever returns a
non-canonical path, every entry-goal step misclassifies — Timings count goes
from 3 to 0, or sub-goal steps leak in making it 5.

Verified by `Run_Timings_OnlyEntryGoalTopLevelSteps_NestedRollUp` at
`RunActionTests.cs:518` — entry goal has 3 steps (step 1 calls a 2-step
helper); asserts `run.Timings.Count == 3` AND indices == {0,1,2}. Either
direction of breakage would fail this test:
- Over-match (sub-goal leaks in) → count = 5
- Under-match (wrong path comparison) → count = 0

Plus the deletion-test reasoning: if I deleted the entire
`bool IsEntryGoalStep(...)` predicate body and returned `true`, the test
above would fail (count = 5). If I returned `false`, count = 0. The test
catches a complete-disable mutation. Good.

### 3. `tester/File.cs` — slim to discovery-only state (commit 1b1b226bb)

`File.Goal` is now `required Goal Goal { get; init; }` (non-null). Six flat
properties dropped. discover.cs rewritten with 6 Stale exit branches.

Verified Goal is non-null on every path through `DiscoverOne`:
- L84-91 (goal read fail) → `new Goal { Path = goalFile }` ✓
- L94-96 (typed/parse/fallback chain) → coalesces to non-null ✓
- L102-106 (no PrPath derivable) → uses `sourceGoal` ✓
- L111-116 (no .pr) → uses `sourceGoal` ✓
- L122-129 (pr read fail) → uses `sourceGoal` ✓
- L131-139 (pr parse fail) → uses `sourceGoal` ✓
- L143-149 (hash mismatch) → uses `sourceGoal` ✓
- L160-166 (happy path) → uses `prGoal` ✓

No null leak. `required` keyword would have surfaced a missing init at
compile time anyway, but I verified the runtime construction sites too.

Straggler check: `grep -rn 'EntryGoalName|file\.Path|file\.PrPath|file\.Directory|file\.GoalHash|file\.BuilderVersion' PLang/ PLang.Tests/` returns zero hits. Slim
fully landed.

**One minor non-blocking finding:** of the 6 Stale branches in discover.cs,
only 2 have tests (`no .pr`, `rebuild needed`). The other 4 (`goal read
error`, `no PrPath derivable`, `pr read fail`, `pr parse fail`) are
reachable but untested. The slim didn't introduce the gap; the original
inline code had the same branches. Two tests would carry real value
(corrupt JSON .pr, goal-read failure); the third is edge-case theoretical.
Filed in `test-report.json` as severity=minor. Not gating PASS.

### 4. `step.@this` Guidance/Level/Confidence drop (commit 463339c90)

Three properties removed plus their MergeFrom backfill and the
`enrichResponse` keep-true block in `builder/code/Default.cs`.

Straggler check: `grep -rn 'Guidance|\.Level|\.Confidence' PLang/ PLang.Tests/`
(filtered to property access shape) returns nothing in production C# or test
code. Old .pr files (verified the Icelandic fixture at
`Tests/TestModule/EdgeCase/.build/testdiscoverhandlesicelandicgoalnames.test.pr`)
still serialize `"guidance"`, `"level"`, `"confidence"` keys — STJ's default
"ignore unknown members on deserialize" eats them silently, and the
serializer-side properties are gone so new builds won't emit them. Zero
behavior impact.

## v5 finding closure

Coder commit `dfd7429a7`:

```diff
- Path.Combine(RepoRoot, "os", "system", "builder", "templates", "v2", "stepActionDetails.template");
+ Path.Combine(RepoRoot, "os", "system", "builder", "llm", "templates", "stepActionDetails.template");
```

Plus the line-8 doc comment. Exactly what I asked for. All 5 previously
failing tests pass.

## Pattern note (lesson, not finding)

The v5 → v6 cycle illustrates the cross-bot coordination gap: builder-bot's
template restructure (commit 0f8886ab0) updated every `.goal` source
referencing the moved templates but missed a hardcoded C# string path in a
test file. Filed in my v5 summary as a move-checklist proposal: when any bot
moves a filesystem resource, grep the project for both `"…/oldpath"` and
`"oldpath"` string literals in C#/PLang sources before claiming green.
Worth surfacing to character-proposals if it recurs.

## Verdict + next

```
VERDICT: PASS
Next: run.ps1 security stepvartypes-incremental "Review the code on branch fix-stepvartypes-incremental" -b fix-stepvartypes-incremental
```
