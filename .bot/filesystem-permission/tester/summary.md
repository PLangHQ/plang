# tester — filesystem-permission

## Version
v4 (reviews coder v4 + v5 — the versions under review)

## What this is
The `filesystem-permission` branch adds PLang's consent-gated filesystem
access: a `Permission` record, a `Path.Authorize(verb)` gate that prompts the
actor on out-of-root access, per-actor in-memory + sqlite grant storage, a v2
Path-in/Data-out FS surface, and a snapshot/resume engine for stateless
suspend.

tester v3 returned **NEEDS-FIXES** (3 major false-greens, 6 minor). coder v4
closed all 9 findings; coder v5 then fixed the deferred Scenario4 bug by
dropping `PermissionRecord.AppId`. This v4 pass re-reviews both.

## What was done
- Clean rebuild (stale-binary rule). C# **2853/2853 pass, 0 skip**; PLang
  **203/203 pass** (4 intentional fail-fixtures excluded — not regressions).
- **Mutation tests** on the three v3 major findings — re-applied the exact
  mutation v3 survived and confirmed a test now dies:
  - F1: `RootComparison` → `OrdinalIgnoreCase` → kills
    `IsInRoot_UpperCasedRoot_TreatedAsOutOfRoot_OnUnix`.
  - F2: `isMove` branch → `File.Copy` → kills both `Move_*` tests.
  - F4/v5: disable persisted `Find` → kills `Scenario4`.
- Read the v5 production change (`PermissionRecord.AppId` removal) for
  security regressions.
- Verdict: **PASS**. Output: `v4/result.md`, `v4/plan.md`, `v4/verdict.json`,
  shared `test-report.json`.

## Outcome
All 9 v3 findings closed and mutation-verified. The three major false-greens
each now kill a test under the mutation v3 survived — the suite is honest
about the security gate.

v5's `AppId` drop is sound: `AppId` was a per-instance GUID that never
survived a restart, so it defeated the "a" = always-persist contract rather
than adding isolation. Grant identity is now `(Actor + Path + Verb)`; actor
isolation holds via name filter + signature verify, root isolation via the
per-root `SettingsStore`. Scenario4 is the real cross-App regression gate.

## Minor notes (non-blocking)
- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring
  over-claims: it survives the F1 mutation, so it does not gate
  `RootComparison`. It pins the re-prefix path of line 189's plain
  `StartsWith`. Valid test, wrong comment.
- **N2** — `PLangFileSystem.cs:227`'s `RootComparison` is gated only
  transitively (shared property); the line is effectively defensive.
- **N3** — no different-root isolation test; v5 makes the root directory the
  sole persistence boundary and only same-root sharing is pinned.

## Next
Branch is test-clean. N1–N3 are optional polish for a future coder pass.
Note: coder v4/v5 production code (the v5 `AppId` removal) has not had a
codeanalyzer pass — worth one before branch close.
