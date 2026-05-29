# Tester v2 — builder-ergonomics (re-test of coder v2)

Coder v2 (`b86539c99`) addressed my v1 FAIL findings:
- **F1** (false-green `UnknownVerb.test.goal`): deleted `Tests/ConfidenceCheck/` entirely.
- **F2** (flaky static-cache test): replaced `CacheSize` count assertion with per-key
  reference-identity; removed `ClearCacheForTests`/`CacheSize` test-only API.
- **F3** (untested channel guard): added `Tests/Channels/GoalChannelRecursion/` —
  goal-channel writes its own name, asserts `errorKey == 'ChannelNotFound'`.

## Verify plan

1. Clean rebuild (stale-binary protocol).
2. Run both suites — expect 0 regressions.
3. F1: confirm deletion; assess whether deletion (vs another repro shape) is right.
4. F2: read the new test, confirm it pins caching and the global API is gone.
5. F3: read the new `.pr` — does each step's action match its text (builder false-green)?
   Then **independently re-run the mutation** (delete `_executing.Value = true`,
   rebuild, confirm the test fails with the right key) — don't trust the coder's claim.

## Status: COMPLETE — verdict PASS. See result.md.
