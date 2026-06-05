# Tester — collections-are-data — v6 plan

Validating coder v6 (resolves codeanalyzer v3 F1/F3/F4; F2 deferred). codeanalyzer v4
= PASS, and it explicitly handed tester two checks on F2: (1) confirm the disable is
exactly the two signing goals; (2) confirm the C# verify-against-raw probe still covers
the primitive.

## Plan

1. Clean rebuild; run both suites (C# authoritative).
2. F2 is the focus — disabled tests are a classic false-green:
   - Find the disabled signing tests; read how they were disabled.
   - Compare against base: were they passing regression tests before this branch?
   - PROVE whether the behavior is actually broken now: restore the real test on the
     current binary and run it. Red = masked regression.
   - Confirm scope (exactly two goals) and whether the C# probe really covers the
     broken developer surface or a lower-level path.
3. Independently re-verify the F1 aliasing fix (mutation on `CopyStructure`).
4. Re-confirm git stays clean across plang runs (warm-cache trap).

## Blocker / decision

The signing regression itself is deferred by design to the sibling branch
`signature-as-schema-wrapper` (correct — coder can't fix it here). The on-branch
question is whether masking it as a *passing* no-op (counted in 273/273) is acceptable,
or whether it must be an honest *skip*.
