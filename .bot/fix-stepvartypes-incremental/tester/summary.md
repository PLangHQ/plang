# tester — fix-stepvartypes-incremental

**Version:** v5
**Verdict:** FAIL

## What this is

Fresh-eyes pass after a busy interval since my v4 PASS. Six commits landed:
codeanalyzer v3 (FAIL with HIGH OBP-5 + 4 LOW); coder's three commits to address
those plus a separate `tester/File.cs` slim (drop the 6 properties duplicated
from Goal) and a separate `step.@this` cleanup (drop unused Guidance/Level/
Confidence); and finally a **builder-bot** commit `0f8886ab0` that restructured
template files under `os/system/builder/llm/templates/`.

The coder commits each claimed `3036/3036 C# + 208/208 PLang` and were correct
at their commit. The builder-bot commit landed *after* and made no test claim.
HEAD is red: 5 C# tests fail.

## What was done

1. Clean rebuild PlangConsole → 0 errors (450 warnings, all generator nullable
   noise — unchanged baseline).
2. `cd Tests && plang --test` → **208/208 pass, 0 fail.**
3. `dotnet run --project PLang.Tests` → **3031/3036, 5 failed.**
4. Cross-checked the 5 failures: all in
   `PLang.Tests/Builder/CompilePromptTests/StepActionDetailsRenderTests.cs`,
   all the same `DirectoryNotFoundException` for the moved template at
   `os/system/builder/templates/v2/stepActionDetails.template`. Path hardcoded
   at line 28.
5. Verified no other source references the dead v2 paths (only that one test
   file and its own line-8 doc comment).
6. Read the slim `tester/File.cs` + new `discover.cs` — every code path
   populates `Goal` non-null (source-read fail → minimal `new Goal { Path }`;
   .pr-missing/corrupt/hash-mismatch → `sourceGoal` from parsed .goal; happy
   path → `prGoal`). Required-property discipline holds.
7. Read the simplified `IsEntryGoalStep` in test/run.cs — both ends now use
   canonical form per branch commit `7ed35b550`. Correct after TrimStart drop.

## The failing 5 (one root cause)

```
PLang.Tests/Builder/CompilePromptTests/StepActionDetailsRenderTests.cs:28
  Path.Combine(RepoRoot, "os", "system", "builder", "templates", "v2",
               "stepActionDetails.template");
                                  ^^^^^
File moved by 0f8886ab0 to:
  os/system/builder/llm/templates/stepActionDetails.template
```

Five test methods all hit `RenderAsync` → `File.ReadAllTextAsync(TemplatePath)` →
`DirectoryNotFoundException`:

- `Render_PerActionBlockOnlyForPlannerSet`
- `Render_ActionInPlannerSet_GetsAllThreeBlocks`
- `Render_ActionInPlannerSet_EmptyBlocksOmitted`
- `Render_ModifierActionInPlannerSet_GetsItsNotesRendered`
- `Render_NotesNotLeakedForActionsOutsidePlannerSet`

## Fix (two lines)

`PLang.Tests/Builder/CompilePromptTests/StepActionDetailsRenderTests.cs`

- Line 28 — replace `"templates", "v2", "stepActionDetails.template"` with
  `"llm", "templates", "stepActionDetails.template"`.
- Line 8 — update the doc comment to point at the new location.

No behavior change in the production renderer; the template content moved
unchanged.

## Coder substantive work (no findings)

Read the diffs end-to-end:

- `condition/code/Default.cs` — `EvaluateOperator` extract collapses three
  identical 9-line bodies into one shared method. Behavior preserved
  (operator data, left, right routed the same way; same exception filter;
  same `EvaluationError` call). Clean.
- `test/run.cs:159-166` — both TrimStart calls and the stale comment gone.
  Producer canonicalization (`7ed35b550`) makes both ends leading-slash.
- `test/report.cs:46` — dead `var app = Context.App;` removed.
- `tester/File.cs` — slim record: required `Goal`, `Status`, `StatusReason`,
  `Tags`. Six redundant flat properties removed. Discovery rewritten to
  always produce a non-null Goal — verified every branch.
- `step/this.cs` — `Guidance`/`Level`/`Confidence` removed plus their
  MergeFrom backfill. `builder/code/Default.cs:553-558` enrichResponse
  block removed. .pr files with stale null fields still deserialize (STJ
  ignores unknown). Clean.
- Icelandic fixture `.test.goal` updated to read `Goal.Name` instead of
  `EntryGoalName` and `.pr` rebuilt — verified the .pr step text and
  module/action line up.

## Code example — the failure shape

```csharp
// PLang.Tests/Builder/CompilePromptTests/StepActionDetailsRenderTests.cs:26-28
private static readonly string RepoRoot = LocateRepoRoot();
private static readonly string TemplatePath =
    Path.Combine(RepoRoot, "os", "system", "builder", "templates", "v2", "stepActionDetails.template");
//                                                       ^^^^^^^^^^^^^^^^^^^^^^
// builder commit 0f8886ab0 moved this to llm/templates/. Path is dead.
```

## Pattern / lesson

When two bots edit overlapping surfaces (here: coder fixes runtime code and
builder restructures templates), a test that hardcodes a filesystem path on
one side is invisible to the bot working on the other. The builder bot
correctly updated every `.goal` source referencing the templates, but the
C# tests' string-formed paths weren't in its grep scope. A `grep -r "templates/v2"`
across `PLang.Tests/` before claiming green would have caught this — and is
the move-checklist gap to flag.

## Verdict + next

```
VERDICT: FAIL
Issues: 5 C# tests fail in StepActionDetailsRenderTests — stale path
        os/system/builder/templates/v2/stepActionDetails.template at line 28
        after builder commit 0f8886ab0 moved templates under llm/templates/.

Next: run.ps1 coder stepvartypes-incremental "Fix tester v5: update
      StepActionDetailsRenderTests.cs:28 + line-8 doc comment to point at
      os/system/builder/llm/templates/stepActionDetails.template" -b
      fix-stepvartypes-incremental
```
