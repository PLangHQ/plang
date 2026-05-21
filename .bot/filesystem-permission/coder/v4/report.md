# Coder v4 — filesystem-permission

## Version
v4 (post-tester-v3 — closes all 9 test-quality findings)

## What this is

Tester v3 (verdict: NEEDS-FIXES) flagged **9 test-quality gaps**, no code
bugs. The v3 code change (`PLangFileSystem.cs:227` Linux case comparison)
was correct but had no regression test; the broader test suite was green
but dishonest about several other behaviors. All 9 addressed in v4.

| # | Finding | Status |
|---|---|---|
| 1 | v3's `RootComparison` fix had no test (deletion test: revert → 0 fails) | **Fixed** |
| 2 | Move can't be distinguished from Copy (swap `isMove` → 0 fails) | **Fixed** |
| 3 | 6 PLang permission goals were placeholders under overclaiming names | **Fixed** (deleted) |
| 4 | `Stage5...Scenario4` empty body reported pass | **Fixed** (added `[Skip(...)]`) |
| 5 | `IdempotentAdd` and `TwoHomes` asserted weak; routing unverified | **Fixed** |
| 6 | `LegacyFsGoalTests_StayGreen_AgainstV2Surface` was a 2-line tautology | **Fixed** (real v1↔v2 round-trip) |
| 7 | `IsInRoot.OsDirectory` clause untested | **Fixed** |
| 8 | Move/Copy "n" answer + stateless `Data<Ask>` branches untested | **Fixed** |
| 9 | No `baseline-tests.md` in coder version dir | **Fixed** (added at `coder/baseline-tests.md`) |

## Deferred (raised by user, not in tester findings)

**SettingsStore cross-App `Data<PermissionRecord>` deserialiser recursion**
(the bug Scenario4 documents in its `[Skip]` reason). User asked to fix this
*after* the F3–F9 cleanup — i.e., this is the next item for v5. The "a"
answer's "always allow" semantics is broken across App restarts until this
is resolved, which means persisted grants today survive only inside a
single-App process. Scenario4 is already wired and marked `[Skip]` with the
full repro reason; flipping it to `[Test]` is the regression gate when the
fix lands.

## File-by-file

### F1 — v3 RootComparison regression

`PLang.Tests/App/FileSystem/PermissionTests/AuthorizeTests/PathAuthorizeTests.cs`
- Added `IsInRoot_UpperCasedRoot_TreatedAsOutOfRoot_OnUnix`: Linux/macOS-gated
  test. Constructs a Path with the upper-cased root prefix, registers a
  channel answering "n", asserts `PermissionDenied`. Under correct
  (`Ordinal`) the prompt fires and refusal surfaces; under the
  `OrdinalIgnoreCase` bug, `IsInRoot=true` auto-grants and the assertion
  fails. This is the security-observable test that v3 was missing.

`PLang.Tests/App/FileSystem/ValidatePathTests.cs`
- Added `UpperCasedRootPrefix_TreatedAsNewPath_AndRePrefixed_OnUnix`: pins
  the observable ValidatePath behavior — upper-cased root is treated as a
  new plang-rooted path and joined onto the real (lowercase) root. The
  actual security gate is the `IsInRoot` test above; this layer down catches
  regressions where line 191's case sensitivity at `IsPlangRooted` flips.

### F2 + F8 — Move/Copy gaps

`PLang.Tests/App/FileSystem/SurfaceTests/MoveCopyBundledConsentTests.cs`
- F2: `Move_OneMissingGrant_*` and `Move_BothPathsMissing_*` now assert
  source is gone, destination exists, and content matches. The
  `isMove`→`isCopy` swap mutation test the tester ran would now fail at
  `File.Exists(srcFile) IsFalse`.
- F8: Three new tests —
  - `Move_BundledAsk_AnswerN_ReturnsPermissionDenied_NoFsMutation`
  - `Copy_BundledAsk_AnswerN_ReturnsPermissionDenied_NoFsMutation`
  - `Move_StatelessChannel_BubblesDataAskUnchanged_NoFsMutation`
  All three assert no filesystem mutation alongside the gate behavior.

### F3 — placeholder PLang goals deleted

Removed under `Tests/Permission/`:
- `RestartStillNoPrompt/`
- `NoGrantSuspends/`
- `RevokeReprompts/`
- `NarrowedGrantRejectsWiderRequest/`
- `ImmediateRereadSkipsPrompt/`
- `Authorize/StatelessAuthorizeResumesAndContinues/`

Kept (real scenarios with verified `.pr`):
- `Tests/Permission/GrantAStoresPersisted/`
- `Tests/Permission/Authorize/StatefulAuthorizeGrantsAndContinues/`

The corresponding scenarios are covered by C# `Stage5MessagesEndToEndTests`
1/2/3/5/6 (Scenario4 deferred — see "Deferred" above).

### F4 — Stage5 Scenario4 empty body

`PLang.Tests/App/FileSystem/Stage5MessagesEndToEndTests.cs`
- Added `[Skip(...)]` with the full deserialiser-recursion reason that was
  previously a code comment with an `await Task.CompletedTask` body that
  reported pass. Now correctly reports skipped.

### F5 — storage assertions

`PLang.Tests/App/FileSystem/PermissionTests/StorageTests/ActorPermissionStorageTests.cs`
- `TwoHomes_...`: renamed to `..._AndRoutingHonoured`, adds a sqlite-table
  inspection that asserts only the signed grant lands in sqlite and the
  unsigned one does not. Proves routing, not just findability.
- `IdempotentAdd_SamePathTwice_Overwrites_NoDuplicateRow`: now follows with
  a `Revoke` + `Find IsNull` chain that fails if a duplicate row had been
  left behind.
- Added `IdempotentAdd_PersistedSamePathTwice_SingleSqliteRow`: direct
  table-row count for the sqlite (signed) home.

### F6 — LegacyFsGoalTests tautology

`PLang.Tests/App/FileSystem/SurfaceTests/MoveCopyBundledConsentTests.cs`
- Renamed `LegacyFsGoalTests_StayGreen_AgainstV2Surface` →
  `LegacyV1FsSurface_RoundTripsFile_AlongsideV2`. Now actually exercises the
  v1 surface (`fs.File.WriteAllTextAsync` / `fs.File.ReadAllTextAsync`),
  writes a file, reads it back via v1, asserts content, then reads the same
  bytes via v2 (`Path.ReadText`). The name finally matches what runs.

### F7 — IsInRoot OsDirectory clause

`PLang.Tests/App/FileSystem/PermissionTests/AuthorizeTests/PathAuthorizeTests.cs`
- Added `IsInRoot_PathUnderOsDirectory_AutoGrants_NoChannelAsk`: constructs
  a path under `app.FileSystem.OsDirectory`, registers no channel, calls
  Authorize, asserts `Success`. The OsDirectory carve-out (system-built-in
  goals like test, build) is the only way this passes without a channel.

### F9 — baseline-tests.md

`/.bot/filesystem-permission/coder/baseline-tests.md`
- Documents canonical suite commands, the 1 expected `[Skip]` in C#, and the
  4 distinct fail-fixture files in PLang (`failsvar` + `sensitivefail` under
  both `TestModule/Report/` and `Modules/Test/Report/`). Future testers can
  separate regressions from pre-existing state without rebuilding to verify.

## Suite state on this commit

- C# (`dotnet run --project PLang.Tests`): **2852 pass, 1 skip, 0 fail**
- PLang (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`): all
  green except the documented intentional fail-fixtures.

The 1 skip is Scenario4 (the deferred SettingsStore deserialiser bug).
