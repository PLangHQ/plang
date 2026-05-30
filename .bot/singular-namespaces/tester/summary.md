# Tester summary — singular-namespaces

**Latest version:** v2 (reviewing coder v2) — **VERDICT: FAIL** (1 MAJOR false-green, surgical fix)

## What this is

A 4-stage refactor: namespace singularization, non-null invariants (Stage 2), accessor
reshape, and the type-entity move (Stage 4, Entry-fold). My v1 returned FAIL with 7 findings.
Coder v2 addresses all 7 and additionally completes `Data.Context` non-null + a `type.@this.Null`
sentinel + a `type.Promote()` fail-loud throw.

## What was done (v2 review)

Clean rebuild (stale-binary trap avoided), then:
- **C# 3694/3694 across 4 consecutive runs** — F6 flake (`[NotInParallel]` fix) confirmed gone.
- **PLang 253/253** — all branch tests `[Pass]`; the `builder.validate` deserialize line is the
  same benign mock-fixture diagnostic, not a failure.
- Read every changed test + `.pr`. 6 of 7 fixes are **honest**, not papered over (F2 golden is a
  real SHA byte-diff, F3 distinguishes registry vs static via `path`, F4 reads `%name!Type%`,
  F5 asserts the typed `ChannelNotFound` key on a literal-channel index-miss).
- **One MAJOR false-green found** via deletion test: the headline F1 `Promote()` throw is
  uncovered — removing it keeps 3694/3694 green — and the test named `...ThrowsHard_NoSilentFallback`
  asserts `ClrType IsNull()` (no throw, different property). Plus 3 minor notes.

Output: `v2/result.md`, `v2/coverage.json`, `test-report.json` (branch root), `v2/verdict.json`.

## The finding that flips the verdict (deletion test)

```
// type/this.cs:168 — temporarily replaced the throw with `return this;`
if (Context == null) return this;   // -> C# suite STILL 3694/3694
```
The fail-loud contract the coder added this version has zero coverage; reverted immediately,
`git status` clean. The test that looks like it covers it tests `ClrType` (which never calls
`Promote()`) and asserts a null return, not a throw.

**Fix for coder:** add `Assert.That(() => _ = unstampedDomainType.Fields).Throws<InvalidOperationException>()`
and rename the misnamed test to `ClrType_OnUnstampedDomainType_ReturnsNull`.
