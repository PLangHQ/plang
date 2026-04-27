# Tester — runtime2-green-plang-tests

## v1 (2026-04-21)

Architect Phases 0–2. Built PlangConsole clean; deleted obsolete `Tests/FromJson/` and orphan `Tests/Runtime2/`; executed the Tests/ restructure per `architect/v1/folder_structure.md` (1309 git renames in one commit, folders preserved 1:1 to keep helpers). Discovered `plang build` fail-fasts monolithically; switched to a per-folder build loop (135/141 ok). Pre-baseline 96 pass / 48 fail / 18 stale; post-restructure 109 pass / 48 fail / 4 stale (+13 wins from rebuilds, no regressions). Six build-failures and four fail-clusters handed to architect Phase 4. Verdict: needs-fixes. See [v1/summary.md](v1/summary.md).

## v2 (2026-04-21)

Re-baseline + quality review of coder v1 (Waves 1–4, commits `ce0de138` + `0cbbeb1f`). C# 2273/2274 (same pre-existing LLM flake); PLang **128 pass / 35 fail / 5 stale** — zero regressions, +18 net wins (14 fail→pass + 4 stale→pass). W1 (per-test in-memory System db) and W4b (http.download bytes split) have strong C# test coverage. W3 (Variables unification + `variable.set` return + Action.RunAsync no-mutation) has **three major gaps**: F3-1 return-value not asserted, F3-2 no-mutation contract untested, F3-3 aliasing-without-clone untested — each fails the deletion test (reverting the code passes all C# tests). W4c (five builder prompt rules) is **dormant**: coder reverted the full .pr rebuild after 38-test regression, so the rules land in code but no `.pr` exercises them — tests the rules target still fail. Verdict: needs-fixes → back to coder for W3 gaps; F4c-1 is architect's call. See [v2/summary.md](v2/summary.md) · [v2/result.md](v2/result.md).
