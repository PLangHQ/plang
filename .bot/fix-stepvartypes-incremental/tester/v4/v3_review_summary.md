# v3 review summary

## v3 verdict: FAIL — but the 6 "failures" were my counting error

I counted `[Fail]` lines in `plang --test` stdout and got 6. Reported as FAIL on strict-red rule. Coder pushed a fix (9af7fd8b2) that explains and resolves it:

The 6 `[Fail]` lines were **the nested test.report's own console rendering** of intentional-failure fixtures (failsvar.fixture.goal, sensitivefail.fixture.goal) bleeding into the outer `plang --test` stdout. The `test.report` action, when called inside a test that's testing report-rendering behavior, was emitting its own `[Pass]/[Fail]` status summary alongside the data it returned. The outer suite already consumed the failures correctly as data (Properties.summaryFail etc.); the stdout pollution made them look like top-level failures.

Coder fix: skip `RenderConsole` / `RenderCoverageTables` when `testing.CurrentTest != null` (i.e., nested in another test).

## What changed

- `PLang/app/modules/test/report.cs:30-45` — gated console summary on `CurrentTest == null`.

## v4 result

- C# 3036/3036 pass
- PLang 208/208 pass (was: 212 + 6 phantom = 218 reported; 208 is the true top-level count)
- `[Fail]` line count: 0. `FAIL: ` line count: 0.
- BuilderSanity smoke still passes.

## Lesson saved

`/memory/feedback_grep_fail_lines_unreliable.md` — going forward, prefer `FAIL: ` count or exit code over `[Fail]` grep, since stdout can carry nested test.report rendering.
