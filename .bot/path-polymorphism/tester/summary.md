# Tester — path-polymorphism

## Version

v8 (matches coder v8 — the tests-only response to tester v7 NEEDS-FIXES,
plus one bonus production fix in commit 02eb2e1ca that dropped the
`identitydata` alias). Third tester pass; v5 was the first, v7 the second.

## What this is

`path-polymorphism` makes PLang's `path` scheme-polymorphic. Coder v6 fixed
slash-qualified `goal.call` resolution + inverted `File.Exists` in the
builder bootstrap + added an `Actions` filter param to `builder.actions`. A
25-commit typed-returns sweep then flipped 69 handlers `Task<Data>` →
`Task<Data<T>>` and typed nine provider interfaces. Coder v7 was docstring-
only. Tester v7 ran mutation tests over those v6/sweep surfaces and found
five honesty gaps (1 parked `.test.goal2`, 4 missing unit tests). Coder v8
addressed all five tests-only, and while writing the N4 ReturnTypeName
fixture discovered + fixed an `identitydata` alias shadowing
`[PlangType("identity")]`.

This pass validates v8's test additions actually guard what they claim.

## What was done

Clean rebuild (stale-binary trap), re-ran both suites:
**C# 2906/2906 (+17 from v7), plang 204/204, 0 stale (+1 from v7), build
clean.** Deltas match coder's claimed counts exactly.

Mutation-tested each v7 finding against the new test:

1. **F4-CARRY** — `.test.goal2` → `.test.goal`. File now in pass list.
2. **N1** — `GoalCallResolutionTests` (4 tests). Mutation 1 (bare-name
   subPath) → 2/4 red. Mutation 2 (skip leaf-match) → 3/4 red. Both
   failure patterns exactly match which tests should and shouldn't depend
   on each branch.
3. **N2** — `GetActionsTests` (+4). Mutation (force `if (false …)`) →
   3/4 new tests red; "empty list = no-filter" correctly stays green.
4. **N3** — `BuilderRunAsyncTests` (2). Mutation (drop the `!`) → 2/2 red.
5. **N4** — `ModulesDescribeReturnTypeTests` (7). Mutation (bare-Data →
   "WRONG") → 1/7 red, the one test exercising the bare branch.

Bonus production fix audited: live source no longer contains the
`identitydata` string (only frozen LLM-trace JSONs do). Pinned by N4.

## Verdict

**PASS.** All five v7 findings honestly closed. No new findings.

One non-blocking observation (v8-O1, informational): `RegisterDomainTypes()`
is now an empty body — nothing guards against a future re-introduction of a
`Register("xxx", typeof(X))` that re-shadows a `[PlangType]` attribute. A
generic "attribute name beats runtime override" test would close it. Not a
v8 regression and no concrete bug today.

## For v9

Nothing required from coder. The branch is in a clean, mutation-verified
state on top of v8.
