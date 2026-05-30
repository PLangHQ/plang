# Tester summary — singular-namespaces

**Latest version:** v3 (reviewing coder v3) — **VERDICT: PASS**

## What this is

A 4-stage refactor (namespace singularization, non-null invariants, accessor reshape,
type-entity move). Review chain: tester v1 FAIL (7) → coder v2 → tester v2 FAIL (1 MAJOR +
3 minor) → coder v3 → **tester v3 PASS**.

## What was done (v3 review)

Coder v3 changed only tests + `.bot` + rebuilt `.pr` (zero production source), addressing my v2
findings. I clean-rebuilt and verified each fix is *honest*, not just green:

- **F1-RESIDUAL (was MAJOR) — closed, mutation-confirmed.** The `type.Promote()` fail-loud throw
  now has real coverage; the misnamed test was renamed to match its body.
  - Mutation A: remove the Promote throw → **exactly** `TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard`
    fails. Honest.
  - Mutation B: delete `_foldLoaded = true` → **exactly** `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext`
    fails. Honest.
- **N1 — closed.** Source-grep pin now `Assert.That(File.Exists).IsTrue()`, not a vacuous `continue`.
- **N2 — closed.** `Capture.goal` echoes `%!data%`; `.pr` rebuilt; pins channel value-flow.
- **N4 — closed.** `baseline-tests.md` present.

C# **3696/3696**, PLang **253/253**. (First PLang run had 8 `/Modules/Http/*` fails — external
httpbin.org transients, v3 touched no production source — cleared on re-run.) `type/this.cs`
coverage 62.3→78.3% line, 33.3→57.1% branch, throw branch now verified.

## The verification that earns the PASS (mutation, not trust)

```
// type/this.cs Promote(): replace throw with `return this;` -> 3695/3696
//   ONLY TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard fails
// 2-arg ctor: delete `_foldLoaded = true;`        -> 3694/3696
//   TypeFoldRead_OnPrimitiveEntity_DoesNotThrow... fails
```
Both reverted, `git status` clean — no source committed. A test that fails when its guarded
behavior is deleted is honest green. v3 meets that bar on every finding.
