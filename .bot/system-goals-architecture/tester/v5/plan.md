# Plan v5 — Fresh-Eyes Test Quality Review

## Context
Coder fixed the 4 critical 0% coverage gaps from v4. All 2017 tests pass. This version does a fresh-eyes review — looking at test files and production code NOT covered in v4's analysis.

## Steps
1. Re-run tests and coverage to verify coder's fixes
2. Deep-read new test files (ErrorCheckTests, AppRunTests, GoalCallTests, GoalReturnTests) for false greens
3. Fresh-eyes review of previously unreviewed test files:
   - OperatorTests, CompareHandlerTests, DefaultEvaluatorTests
   - FileHandlerTests, SignActionTests, VerifyActionTests
   - ForeachTests, CallFrameTests, CallStackIntegrationTests
4. Production code review for untested paths:
   - foreach.cs, DefaultFileProvider.cs, if.cs
   - DefaultHttpProvider.cs streaming, GoalSteps.cs, contains.cs
5. Deep verification of ErrorCheckTests vs error/check.cs production code paths
6. Write updated test-report.json and verdict

## Status: COMPLETE
