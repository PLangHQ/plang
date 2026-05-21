# Tester v4 — result

**Branch:** filesystem-permission · **Reviewing:** coder v4 + v5 · **Verdict: PASS**

## Test run (clean rebuild)

| Suite | Total | Pass | Fail | Skip |
|---|---|---|---|---|
| C# (`dotnet run --project PLang.Tests`) | 2853 | 2853 | 0 | 0 |
| PLang (`plang --test` from `Tests/`) | 203 | 203 | 0 | 0 |

PLang count excludes the 4 intentional fail-fixtures (`_fixtures_fail`,
`_fixtures_sensitive`) — confirmed against coder's new `baseline-tests.md`,
not regressions. No `[Skip]` remains in the C# suite (Scenario4 un-skipped
in v5).

One stdout line — `Failed to deserialize List\`1 ...` — appears next to a
`mock handle` test; it is a negative-path test feeding bad JSON, not a
failure (suite is 203/203). Pre-existing, unrelated to permission work.

## The three v3 major findings — mutation-verified

A finding is only closed if the mutation v3 *survived* now kills a test.

| Finding | Mutation | v3 result | v4 result |
|---|---|---|---|
| F1 — case-comparison gate untested | `RootComparison` → `OrdinalIgnoreCase` | 0 fail | **1 fail** — `IsInRoot_UpperCasedRoot_TreatedAsOutOfRoot_OnUnix` |
| F2 — Move indistinguishable from Copy | `isMove` branch → `File.Copy` | 0 fail | **2 fail** — `Move_OneMissingGrant`, `Move_BothPathsMissing` |
| F4/v5 — Scenario4 verified nothing | disable persisted `Find` | (empty body) | **fail** — `Scenario4_RestartStillNoPrompt_PersistedGrantSurvivesNewApp` |

All three now have a test that dies when the behavior breaks. The suite is
honest about the security gate.

## F1 — closed, with one comment caveat

`IsInRoot_UpperCasedRoot_TreatedAsOutOfRoot_OnUnix` is the real gate. It
builds a `Path` whose `Absolute` is the upper-cased root prefix, registers an
"n" channel, and asserts `PermissionDenied`. Under the `OrdinalIgnoreCase`
mutation `IsInRoot()` auto-grants and the assertion fails — confirmed. This
test genuinely pins `RootComparison` as used by `Path.Authorize.IsUnder`.

Caveat (minor — N1 below): the companion `ValidatePathTests`
`UpperCasedRootPrefix_TreatedAsNewPath_AndRePrefixed_OnUnix` does **not**
depend on `RootComparison`. It survives the F1 mutation untouched. Its
docstring claims it "catches a regression where line 191 / line 227 flipped
to case-insensitive" — it does not. What it actually pins is the re-prefix
path driven by `PLangFileSystem.cs:189`'s plain `StartsWith(RootDirectory)`
(culture comparison, no `RootComparison` argument). Still a valid pin of the
re-prefix behavior; the docstring just over-claims its link to the v3 fix.

## v5 code review — dropping `PermissionRecord.AppId`

v5 is real production code, not test code, so I read it for security
regressions. `PermissionRecord` went 5 args → 4; `Covers` and the
`Actor.Permission` Find/Revoke paths no longer compare `AppId`.

**Sound.** Grant identity is now `(Actor + Path + Verb)` with the per-actor
sqlite store providing scope. Isolation still holds:

- **Actor** — `Find` filters persisted rows by `_actor.Name` and `TryCover`
  re-checks it; signature verify on top. `PerActorIsolation_...` covers it.
- **Root** — the persisted `SettingsStore` lives under the root directory.
  Two apps on the same root *should* share grants (that is the "a" =
  "always" contract); two apps on different roots get different sqlite
  files. The root directory is the boundary.

`AppId` was a per-instance GUID (fresh per `new App()`), so it never
survived a restart — it actively *defeated* the persist contract rather than
adding isolation. Removing it is correct. Scenario4 is the regression gate:
app1 grants via "a", app2 (`new App()` on the same root, zero-answer
stateless channel) reads with no prompt — asserts `Success` **and**
`Type != "ask"`. Disabling persisted `Find` kills it (verified).

## v3 minor findings — spot-checked, all closed

- **F3** — 6 placeholder PLang goals deleted; `GrantAStoresPersisted` and
  `Authorize/StatefulAuthorizeGrantsAndContinues` (the two real ones) kept
  and still green.
- **F5** — `TwoHomes_..._AndRoutingHonoured` now inspects `SettingsStore`
  and asserts `/disk` present + `/mem` absent (real routing proof);
  `IdempotentAdd_..._NoDuplicateRow` now does Revoke→assert-null (a
  surviving duplicate would still cover); `IdempotentAdd_Persisted...`
  counts sqlite rows == 1.
- **F6** — `LegacyV1FsSurface_RoundTripsFile_AlongsideV2` now does a real v1
  write/read round-trip plus a v2 cross-read; name matches the body.
- **F7** — `IsInRoot_PathUnderOsDirectory_AutoGrants_NoChannelAsk` registers
  no channel; only the OsDirectory carve-out lets it return Ok without a
  prompt.
- **F8** — Move/Copy "n" answer (PermissionDenied + no FS mutation) and
  stateless-channel (`Data<Ask>` bubble + no FS mutation) now covered.
- **F9** — `coder/baseline-tests.md` present and accurate.

## Minor notes — non-blocking

- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring
  over-claims (see F1 above). Fix the comment to say it pins the re-prefix
  behavior of line 189's plain `StartsWith`, not a `RootComparison`
  regression.
- **N2** — `PLangFileSystem.cs:227`'s `RootComparison` is gated only
  transitively (the shared property's value is pinned by the `IsInRoot`
  test). Branch analysis: `path` always reaches line 227 either
  exact-root-prefixed or clearly out-of-root, so the comparison there is
  effectively defensive — no input exercises it as a decision point.
  Acceptable under the single-home design; flagged for awareness.
- **N3** — No different-root isolation test. v5 makes the root directory the
  *sole* boundary for persisted grants. Scenario4 pins same-root sharing;
  nothing pins that two apps on *different* roots do not share a persisted
  grant. Structurally guaranteed by the per-root `SettingsStore`, but a
  one-test pin would harden the now-single boundary.
- **Process** — coder v4 and v5 did not append sessions to `report.json`
  (last entry is tester v3).

## Verdict

**PASS.** All 9 v3 findings are genuinely closed — the three major
false-greens are each killed by the mutation v3 survived. The v5
`AppId`-drop is a correct, well-scoped change and Scenario4 is a real
cross-App persistence gate. N1–N3 are cosmetic / defensive-hardening notes,
not false greens and not blockers.
