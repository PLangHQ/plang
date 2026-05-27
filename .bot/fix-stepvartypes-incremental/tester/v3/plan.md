# tester v3 — fix-stepvartypes-incremental

## Scope

Verify coder commits (7ed35b550, 7fa6d16ad, ff9dee864, 606689e62) actually closed the v2 PLang failures. Apply two new rules:

1. **Validate builder first** — build at least one test with cache=false to confirm the builder works before trusting `plang --test`.
2. **Strict red is red** — any test failure (C# or PLang) = FAIL verdict regardless of who introduced it.

## Plan

1. Clean rebuild PlangConsole.
2. Build PLang.Tests, run C# suite.
3. Write a 4-primitive builder smoke test at `Tests/BuilderSanity/` (set, foreach, if, call) with assertions. Build it with cache=false. Run via `plang --test`. If it fails, builder is broken and `plang --test` results are unreliable.
4. Run full `plang --test`. Apply strict-red rule to the final count.
5. Verdict.

## Artifact added to repo

`Tests/BuilderSanity/BuilderSanity.test.goal` + `AddItem.goal`, `MarkBig.goal`, `Finalize.goal`. Builder smoke test — covers ~90% of the builder's hard surface. Reusable across future tester runs.
