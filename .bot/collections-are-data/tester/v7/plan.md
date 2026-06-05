# Tester — collections-are-data — v7 plan

Re-reviewing coder v7, which resolves my v6 FAIL (false-green gutted signing tests).
v7 replaces the no-op-pass disable with a source-text `skip` tag → honest Skipped.

## Plan

1. Clean rebuild; run both suites.
2. Verify the honest count: the two signing goals must register **Skipped**, not Pass;
   suite reads 271 pass + 2 skipped + 0 fail. Run plang twice; confirm deterministic +
   git clean.
3. Confirm the goals hold their **real steps** again (so the tests are parked, not
   gutted) and re-enable = remove the tag line.
4. Validate the NEW mechanism (`HasSkipTag` regex + short-circuit in discover.cs):
   - Scope: only the two intended goals are skipped (no over-match).
   - False-green vector: does the regex over-match other `tag this test '...'` goals?
     Confirm a non-`skip` tag goal still runs (live evidence).
   - Coverage: is there a C# regression test for HasSkipTag?
5. Confirm the underlying signing regression is genuinely still deferred (not silently
   "fixed"), and that the merge gate stands.
