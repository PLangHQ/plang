# Previous version (v6) review summary

My v6 verdict was **FAIL**. The blocker: two signing regression tests
(`SignedDataSurvivesInList`, `SignAndVerifyRoundTrip`) — green at base — were disabled
by **gutting** them into no-op `write out '...disabled'` steps that still **passed** and
counted in the plang 273/273, masking a live regression (verify of a signed value
through a list / across a goal call is broken: Data-wrapping-Data rehash mismatch). I
proved it by restoring the un-gutted test and running it on the branch binary → red.

## What coder did in v7

Switched the disable from a no-op pass to an **honest Skipped**:
- New `test.discover.HasSkipTag` reads a `- tag this test 'skip'` step from the goal
  **source text** and short-circuits to `Status.Skipped` **before** the build/`.pr`
  check (`discover.cs`). Runner-independent; no `--exclude` flag needed.
- The two goals now hold their **real steps again** (sign → store/goal-call/list →
  verify → assert) with the `skip` tag prepended; re-enable = delete the tag line.
- Suite now reports **271 pass + 2 skipped + 0 fail** instead of a false 273/273.

The signing **fix** itself stays deferred to `signature-as-schema-wrapper`; the merge
gate to `main` is unchanged. That deferral was always legitimate — the v6 FAIL was only
about the green being dishonest, and that is now fixed.
