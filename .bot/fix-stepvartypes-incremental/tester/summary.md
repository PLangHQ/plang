# tester — fix-stepvartypes-incremental

**Version:** v4
**Verdict:** PASS

## What this is

v3 FAILed because I counted 6 `[Fail]` lines in `plang --test` stdout. Coder pointed out (commit 9af7fd8b2) those weren't real top-level failures — they were the nested `test.report`'s own console rendering of intentional-failure fixtures (the ones consumed by TestReportRendersFailureWithVariables and TestReportMasksSensitiveVariables). The fix suppresses `RenderConsole` / `RenderCoverageTables` when `testing.CurrentTest != null`. Re-running confirms 208/208 PLang + 3036/3036 C# — all green.

## What was done

1. Pulled (commit 9af7fd8b2).
2. Clean-rebuilt PlangConsole → 0 errors.
3. Ran `plang --test` from `Tests/`: **passes=208, fails=0, `FAIL:` headers=0, `[Fail]` lines=0**. Includes BuilderSanity smoke test passing.
4. Built and ran C# suite via TUnit binary: **3036/3036 pass, 0 failed**.
5. Saved the counting lesson to memory at `/memory/feedback_grep_fail_lines_unreliable.md`.

## Code example — the coder's fix

```csharp
// PLang/app/modules/test/report.cs
if (testing.CurrentTest == null)
{
    var console = new StringBuilder();
    RenderConsole(console, results, testing);
    RenderCoverageTables(console, testing, Context.App.Modules);
    await Context.App.CurrentActor.Channels.WriteTextAsync(
        global::app.channels.@this.Output, console.ToString());
}
```

The parent test consumes results via the returned `Data.Properties` (content, summaryPass, summaryFail). The nested console emission was redundant AND polluted the outer `plang --test` stdout — a clean structural fix.

## Lessons saved (tester memory)

- `feedback_strict_red_is_red.md` — any test failure = FAIL, no carve-outs.
- `feedback_validate_builder_before_plang_tests.md` — build a 4-primitive smoke (set, foreach, if, call) with cache=false before trusting `plang --test`.
- `feedback_grep_fail_lines_unreliable.md` — don't `grep -c [Fail]`; nested test.report bleeds [Fail] lines for fixtures it consumes.
- `plang_string_concat.md` — string concat is interpolation (`'%var%-suffix'`), not `+`.

## Process gap (still open, not gating)

No `coder/` folder on this branch. Four coder commit pushes, zero `coder/v<N>/plan.md` or `summary.md` or `baseline-tests.md`. Worth flagging to docs/architecture, not for me to gate.

## Next

```
run.ps1 security stepvartypes-incremental "Review the code on branch fix-stepvartypes-incremental" -b fix-stepvartypes-incremental
```
