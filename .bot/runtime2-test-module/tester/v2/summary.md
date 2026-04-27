# v2 Summary — Test Module Plan Revised After Test-Designer Review

## What this is

Revised PLang test module plan incorporating test-designer's review feedback. No code changes — this is plan refinement before implementation.

## What was done

Responded to all 11 review points:

**Accepted into v1 scope (4 items):**
- `test.skip` action — tests can declare themselves skipped with a reason
- Per-test timeout — 30s default, CancellationTokenSource per test, App disposal on timeout
- JUnit XML output — alongside JSON, for CI integration
- AfterAction event for coverage — subscribe to lifecycle events instead of patching dispatcher

**Corrected v1 plan (3 items):**
- Isolation unit is `App.@this`, not Actor/Engine — explicit about what "fresh engine" means
- Variable dump happens inside assert handlers at failure time, not in the runner after step exit
- Goal naming: file is the test, entry goal is whatever it's named (matches all 142 existing tests)

**Deferred (2 items):**
- Tag/filter system → v2
- `.pr` snapshot testing → builder feature, not test runner

**Already correct, made explicit (1 item):**
- C# main loop — `test.run` iterates in C#, never uses PLang foreach

## Key design decision

The variable-dump-in-assert-handler correction is the most impactful. The assert handlers (`equals.cs`, `isTrue.cs` etc.) snapshot `context.Variables` on failure and attach it to `AssertionError`. This avoids changing `IAssertProvider` interface — the snapshot happens in the handler layer (which has `IContext`), not the provider layer.

## Status

Plan v2 ready for user approval before implementation begins.
