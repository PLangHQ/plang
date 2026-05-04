# tester — runtime2-callstack — v1 plan

## Subject under test

coder/v2 closes the runtime2-callstack branch after codeanalyzer v3 PASS:
- Source-resolution merge (`959cdd36`, `c4381135`)
- Callstack tests fixed (`367ca1e7`)
- LlmFixer regression (`90bff6a0`)
- Phase 11 CallChainRenderer (`e31e5236`)
- Concurrency/cycle cleanup from codeanalyzer v2 (`be77dc12`)

## Plan

1. Run full C# (`dotnet run --project PLang.Tests`) and PLang (`cd Tests && plang --test`) suites. Confirm 2623/181 numbers from coder summary.
2. Run coverage on touched files only: `App/Data/this.cs`, `App/Errors/CallChainRenderer.cs`, `App/Errors/Error.cs`, `App/CallStack/Call/this.cs`, `App/Goals/Goal/this.cs`, `App/Variables/this.cs`, `App/this.cs`.
3. Test-quality hunt — false greens. Specifically:
   - `DataAsTResolutionTests.cs` — does it actually exercise the AsT_Convert path vs the old recursive path? Would a regression to the old behavior fail?
   - `AsTIdentityTests.cs` — `AsT_PlainDataTarget_DictWithInfraVar_ResolvesAtCanonicalWalk`: assert that the resolved value is the dynamic-data result, not just non-null.
   - `CallChainRendererTests.cs` — recursion compression and cause annotation each need a positive AND a negative case. Compression must not collapse a frame with errors. Cause annotation must not fire for inherited cause.
   - `Audit.test.goal` — count change 4→7. Verify the goal asserts the count, and that a wrong implementation would fail.
   - `HandledFlagFalseWhenRecoveryFails.test.goal` — was a placeholder, now real. Verify it tests the documented semantics.
   - PLang `.pr` files for builder false greens — verify step text matches `actions[0].module.action`.
4. Spot-check the deletion test on Phase 11 renderer: would deleting the cause-annotation block fail any test?
5. Write report and verdict.
