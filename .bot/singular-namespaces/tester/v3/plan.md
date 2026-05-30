# Tester v3 — plan (reviewing coder v3)

Coder v3 responds to my v2 FAIL (1 MAJOR + 3 minor). My job: confirm each fix is *honest*,
with mutation tests on the new throw-coverage tests (the highest false-green risk this round).

## Checks
1. Clean rebuild (stale-binary trap) → C# + PLang suites.
2. **F1-RESIDUAL** — two new tests added (`TypeFoldRead_OnUnstampedDomainEntity_ThrowsHard`,
   `TypeFoldRead_OnPrimitiveEntity_DoesNotThrow_EvenWithoutContext`) + rename. Mutation-confirm
   BOTH: remove the Promote throw → first must fail; delete `_foldLoaded=true` → second must fail.
3. **N1** — `if (!File.Exists) continue` → `Assert.That(File.Exists).IsTrue()`. No longer vacuous.
4. **N2** — Capture echoes `%!data%`. Read rebuilt `.pr`, confirm value-flow pinned.
5. **N4** — baseline-tests.md present this version.

## Result
All four addressed and verified. Both new throw tests mutation-confirmed honest (each catches
its own deletion, exactly one targeted failure). C# 3696/3696, PLang 253/253 (8 HTTP fails on
first run were httpbin.org transients — v3 changed zero production source — cleared on re-run).
VERDICT: PASS.
