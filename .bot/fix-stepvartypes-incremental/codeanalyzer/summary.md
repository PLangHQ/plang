# codeanalyzer — fix-stepvartypes-incremental

**Version:** v3 (branch-wide OBP shape-smell scan)

## What this is

v1/v2 ran a narrow lens on 8 files and PASSed. Ingi confirmed a new 5th OBP shape smell (producer raw, consumers transform) and asked for a branch-wide pass through that lens. v3 widens scope to all 12 production C# files on the branch and finds findings v1/v2 missed.

## What was done

Five-pass review, branch-wide, with **explicit Pass 1b against the proposed 5-item OBP smell list** (the 5th item is in `.bot/<branch>/claude-md-proposals.md`, not yet merged into `CLAUDE.md` — but the user just confirmed it's the rule).

**Verdict: NEEDS WORK** — one HIGH (OBP-5), one MEDIUM (proposed OBP-6), four LOW.

After the initial v3 draft Ingi flagged a 6th smell category I'd missed in v1/v2/v3: `tester/File.cs` holds a `Goal? Goal` reference *and* flat mirrors of 6 Goal-reachable properties (`Path`, `PrPath`, `EntryGoalName`, `GoalHash`, `BuilderVersion`, `Directory`). Costs more memory than the 8-byte reference and the two views can silently drift. Filed as proposed OBP smell #6 in `.bot/<branch>/claude-md-proposals.md`. Importantly: collapsing `File.cs` closes the OBP-5 finding for free, because `test/run.cs:165,168` would then read `file.Goal?.Path` (a typed `path.@this`) instead of `file.Path` (a `string`) — and `path.@this`-typed equality doesn't need a TrimStart.

### HIGH finding (the one v1/v2 missed)

`PLang/app/modules/test/run.cs:165, 168` — `test.Path.TrimStart('/')` and `step.Goal?.Path?.ToString().TrimStart('/')`. The branch's canonicalization commit `7ed35b550` made `path.@this.Relative` always return leading-`/` form. Both ends are canonical now; the trims cancel each other out. The trim is **cargo defensive code** that happens to work today and becomes a silent-divergence trap if anyone later deletes one trim but not the other (Pass 4.5 asymmetry tell).

Root fix: drop both `.TrimStart('/')` calls. The stale "step.Goal.Path arrives with a leading slash" comment goes with them. If anything breaks, the bug is in `discover.cs` not enforcing canonical form on `File.Path` — fix there.

### LOW findings

1. `PLang/app/modules/test/report.cs:49` — `var app = Context.App;` unused (carryover from v2).
2. `PLang/app/modules/test/report.cs:307` — `new[] { '/', '\\' }` allocated per row inside `GroupBy` (carryover from v2).
3. `PLang/app/tester/Run.cs:38` and `tester/File.cs:38` — OBP smell #1 (public `HashSet<string>` mutated from outside owner). One caller each, low cost; fix when next touched.
4. `PLang/app/modules/condition/code/Default.cs:16, 31, 46` — identical three-line guard pasted into three sibling `Evaluate(...)` methods. Extract `private static data.@this<bool>? GuardOperator(...)`.

### Why v1/v2 missed the HIGH

I ran Pass 1b's 4-item checklist mechanically and didn't lift up to "is this property's shape forcing every consumer to transform it." The 5th smell I proposed *because* I noticed `test/run.cs:165` exists; then in v1 I gave run.cs CLEAN because the 5th smell wasn't in the checklist yet. Classic miss — same trip I'm now trying to prevent by dereferencing Pass 1b to CLAUDE.md (separate character proposal already filed in `.bot/<branch>/character-proposals.md`).

## Code example — the headline finding

```csharp
// PLang/app/modules/test/run.cs:165-168
var entryGoalPath = test.Path.TrimStart('/');
bool IsEntryGoalStep(global::app.goals.goal.steps.step.@this? step)
    => step != null
    && string.Equals(step.Goal?.Path?.ToString().TrimStart('/'), entryGoalPath, StringComparison.Ordinal);
```

Producer contract (`PLang/app/types/path/this.cs:111`):
> Canonical PLang root-relative form: leading "/" anchors at the app root, "/" as separator regardless of OS

So `test.Path` is `"/Modules/foo.test.goal"`, `step.Goal?.Path?.ToString()` is `"/Modules/foo.test.goal"`. Both. With slash. The TrimStart calls are doing nothing — until someone deletes one.

## Files

- `.bot/fix-stepvartypes-incremental/codeanalyzer/v3/plan.md`
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v3/report.md`
- `.bot/fix-stepvartypes-incremental/codeanalyzer/v3/verdict.json`
