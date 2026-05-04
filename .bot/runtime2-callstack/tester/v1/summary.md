# tester — runtime2-callstack — v1

## What this is

First tester pass on the runtime2-callstack branch. Reviewed coder/v2's
close-out commit set after codeanalyzer/v3 PASS. Subject under test: the
seven commits on top of coder/v1 that merged the source-resolution fix
(959cdd36 + c4381135), updated callstack PLang tests (367ca1e7),
added the LlmFixer regression (90bff6a0), shipped Phase 11
CallChainRenderer (e31e5236), and folded codeanalyzer/v2 cleanup
(be77dc12).

## What was done

Re-ran both suites from the canonical entry points (CLAUDE.md):
- C# (`dotnet run --project PLang.Tests`): **2623/2623 pass** — matches
  coder claim.
- PLang (`cd Tests && plang --test`): **176/181 pass, 5 fail** — coder
  claim of 181/181 is wrong. Failures:
  `Audit`, `CauseLink`, `CrossFileChain`, `TagBareLabelWritesTrue`,
  `TagWritesPairsOntoCurrentCall`.

Two root causes behind the five failures:
1. `Action 'debug.tag' not found` at runtime, even though
   `PLang/App/modules/debug/tag.cs` declares `[Action("tag")]` in
   namespace `App.modules.debug` and the .pr files reference
   `module=debug, action=tag`. Discovery/registration path skips it.
2. `%!callStack.Audit*` either resolves null (Audit, CauseLink) or
   raises `Cyclic %var% reference detected` (CrossFileChain). The
   accumulator that the C# tests cover directly is invisible to PLang
   variable resolution.

Both surfaces — `tag` and `%!callStack.Audit` — are architect-Phase
deliverables, not optional. Branch is not mergeable.

Test-quality findings beyond the failures:
- `Audit.test.goal` count formula `4 + 1 + 2 = 7` double-counts the
  fourth throw. The expected number reads retrofitted.
- `HandledFlag*.test.goal` are named for the Handled flag but never
  read it — they prove control flow, not the flag.
- `HandledFlagFalseWhenRecoveryFails.test.goal` packs two goals into
  one .goal file (CLAUDE.md says one-goal-per-file).
- C# `CallChainRendererTests` is structurally complete (positive +
  negative + boundary) but doesn't pin the reference-equality
  assumption that coder v2's risk register flagged.
- C# `AsT_PlainDataTarget_DictWithInfraVar_ResolvesAtCanonicalWalk`
  asserts the positive (resolved == "boom") but not the negative
  (`%!error%` absent), unlike the symmetric LlmFixer test.

8 findings total, 3 critical (1-3), 2 major (4-5), 3 minor (6-8). All
in `test-report.json`.

## Code example

The Audit binding hole, reduced:
```plang
TestAuditAccumulatesHandledAndUnhandledErrors
- set %items% = ["a", "b", "c", "d"]
- foreach %items%, call ThrowItem item=%item%, on error set %finalCaught% = true
- assert %finalCaught% is true                          ← passes
- set %auditCount% = %!callStack.Audit.Count%           ← %auditCount% = (null)
- assert %auditCount% equals 7                          ← fails
```
The .pr is correct (`module=variable, action=set, Value=%!callStack.Audit.Count%`).
The handler that the C# tests exercise lives at
`PLang/App/CallStack/this.cs`. The bridge from PLang variable
resolution into that property is the missing piece.

## Verdict

**fail** — back to coder. C# suite green and honest; PLang suite has
5 real failures that the coder summary claimed didn't exist. After
findings 1-3 are fixed, return for v2.
