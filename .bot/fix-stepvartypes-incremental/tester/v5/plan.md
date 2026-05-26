# tester v5 — fresh-eyes pass

## Scope

User said "look at it with fresh eyes". Not limited to last-commit-since-v4. Branch
is `fix-stepvartypes-incremental`. Since my v4 PASS (commit e6889e504) the bots
landed:

1. `6f8775ba7` codeanalyzer v3 — FAIL with HIGH OBP-5 (cargo TrimStart) + 4 LOW
2. `0943e5fda` coder — addressed codeanalyzer v3: drop TrimStart at run.cs:165/168,
   extract `EvaluateOperator` in condition/code/Default.cs, drop dead `var app`
   in report.cs.
3. `a481da75c` codeanalyzer v3 — amended with proposed OBP-6 (File.cs reference +
   flat mirror of Goal-reachable properties).
4. `1b1b226bb` coder — slim `tester/File.cs`: drop Path/PrPath/EntryGoalName/
   Directory/GoalHash/BuilderVersion; make `Goal` required non-nullable; drop
   `[PlangType("testfile")]` and `[LlmBuilder]` decorations; rewrite
   discover.cs to populate Goal from .pr when available else .goal source;
   update PLang.Tests/App/Testing/* and two Icelandic .test.goal fixtures.
5. `463339c90` coder — drop unused `Guidance`/`Level`/`Confidence` from step.@this
   plus their backfills in step/this.cs:MergeFrom and builder/code/Default.cs;
   prune stepForLlm.template / stepGroupContext.template guidance lines.
6. `0f8886ab0` **builder bot** — restructure templates: move
   `os/system/builder/templates/v2/{stepForLlm,stepActionDetails,goalFormatForLlm.v2}.template`
   under `os/system/builder/llm/templates/` (with renames); move
   `goalFormatForLlm.template` up to `templates/`; delete dead
   `stepGroupContext.template`. .goal sources and .pr files updated.

## Plan

1. Clean rebuild PlangConsole/PLang.Tests (per stale-binary trap).
2. `plang --test` from `Tests/`.
3. `dotnet run --project PLang.Tests`.
4. Cross-check: do any source/test files still reference the dropped Step
   properties or the moved template paths?
5. Verify the `tester/File.cs` non-null `Goal` discipline — does discover.cs
   really *always* populate it, including all stale branches?
6. Read .pr files for the Icelandic Discover fixture to confirm builder
   produced the right module/action after the .test.goal edit.
7. If green, PASS. If anything red, FAIL with named failures.
