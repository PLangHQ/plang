# Tester v2 — plan (reviewing coder v2)

Coder v2 responds to my v1 FAIL (7 findings). My job: confirm each fix is *honest*, not just green.

## Approach
1. Clean rebuild (stale-binary trap) → run C# + PLang suites, confirm green.
2. Re-run C# ×3 to confirm F6 flake (`[NotInParallel]`) is actually gone — flakes hide.
3. For each fix, ask "would it fail if the impl were subtly wrong?":
   - **F1** — back-ref nullability pins + the new `Promote()` fail-loud throw (commit 3c1521c20). The throw is the headline safety change — is it *tested*?
   - **F2** — golden is now SHA256 byte-diff. Real gate?
   - **F3** — `path` vs `int` distinguishes registry from static fallback. Honest?
   - **F4/F5** — read the `.pr`, confirm step text matches action, assertions verify intent.
   - **F7** — does the channel-accessor test verify reachability AND value-flow?
4. Mutation/deletion tests on anything that smells covered-but-unverified.

## Status
See result.md / test-report.json. Headline: 6 of 7 fixes are honest and well-made;
one gap — the F1 `Promote()` throw is deletion-confirmed UNCOVERED, and the test named
`...ThrowsHard_NoSilentFallback` asserts `ClrType IsNull()` (no throw, different property).
