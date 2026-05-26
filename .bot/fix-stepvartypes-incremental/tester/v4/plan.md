# tester v4 — fix-stepvartypes-incremental

## Scope

Verify coder commit 9af7fd8b2 (`test.report: suppress console summary when nested inside another test`). My v3 reported 6 red tests; coder argues those were nested test.report's own console output bleeding into stdout, not real top-level failures.

## Plan

1. Clean rebuild PlangConsole.
2. Build C# suite, run it.
3. Re-run BuilderSanity smoke (already built; doesn't need rebuild — just confirm it still passes).
4. Re-run `plang --test`, count both `[Fail]` and `FAIL: ` lines. If they diverge significantly, the v3 grep was matching nested noise.
5. Save the lesson about `[Fail]` line counting being unreliable.

## Lesson learned

`grep -c '[Fail]'` on `plang --test` output overcounts because nested `test.report` renders the intentional-failure fixtures it consumes — those `[Fail]` lines leak into stdout but are not top-level test failures. Real failures show as `FAIL: <path>` headers.
