# Review of v4

v4 found 6 test failures, 5 critical coverage gaps, and 15+ false-green findings. Coder responded with commit c75da5c0 which:

1. **Fixed all 6 streaming test failures** — centralized GoalCall parameter injection in `App.RunGoalAsync` so parameters are injected for both file-loaded and memory-loaded goals
2. **Added 31 new tests** covering the 4 critical 0% coverage gaps:
   - `ErrorCheckTests.cs` (17 tests) — error/check.cs now at 100%
   - `AppRunTests.cs` (4 tests) — app/run.cs now at 90%
   - `GoalCallTests.cs` (4 tests) — goal/call.cs now at 100%
   - `GoalReturnTests.cs` (6 tests) — goal/return.cs now at 100%

All 2017 tests pass. Critical gaps addressed. v5 does a fresh-eyes review to catch what v4 missed.
