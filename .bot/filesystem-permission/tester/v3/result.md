# Tester v3 — result

**Branch:** filesystem-permission · **Reviewing:** coder v3 · **Verdict: NEEDS-FIXES**

## Test run (clean rebuild)

| Suite | Total | Pass | Fail | Skip |
|---|---|---|---|---|
| C# (`dotnet run --project PLang.Tests`) | 2846 | 2846 | 0 | 0 |
| PLang (`plang --test` from `Tests/`) | ~219 | 213 | 6 | 0 |

The 6 PLang failures are **intentional fail-fixtures** — `Tests/TestModule/Report/_fixtures_fail/failsvar.fixture.goal` (asserts `42 == 99`) and `_fixtures_sensitive/sensitivefail.fixture.goal` (asserts identity `== 'will-not-match'`). They exist to be failed-by-design so the Report module's tests can verify failure reporting. `plang --test` discovers them directly because of the leading-underscore fixture convention. `sensitivefail` was checked individually and fails with the correct masked assertion (`privateKey: "******"`) — working as designed. **Not coder regressions.**

No `baseline-tests.md` exists in any coder version dir (finding 9). Regression vs pre-existing was reconstructed by clean rebuild + fixture-provenance check: **no regressions.**

## The headline: v3's fix is invisible to the suite

v3 changed exactly one line of behavior — `PLangFileSystem.cs:227`:

```diff
- if (!path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
+ if (!path.StartsWith(RootDirectory, App.FileSystem.Path.RootComparison))
```

`RootComparison` is `Ordinal` on Linux, `OrdinalIgnoreCase` on Windows. The point: on a case-sensitive Linux filesystem, `/SRV/myapp` must **not** be treated as in-root just because the root is `/srv/myapp`. That is a permission-gate bypass if it regresses.

**Deletion test:** revert line 227 to `OrdinalIgnoreCase`. Result: **0 tests fail.**

Why every test survives the revert:
- `ValidatePathTests.InRootAbsolute_LeftAlone` — exact-case in-root path. Matches under `Ordinal` *and* `OrdinalIgnoreCase`.
- `ValidatePathTests.OutOfRootPath_DoesNotThrow` — `//tmp/elsewhere.txt`. Matches under *neither*.
- No test ever supplies a path whose root prefix differs from the real root **only in case** — the one input that separates the two comparison modes.

Coverage makes this worse, not better: `coverage.cobertura.xml` shows line 227 with `hits=1` and `Path.Authorize.cs` at **98.4% line / 100% branch**. The gate is fully line-covered while the rule it enforces is behaviorally unverified. Textbook coverage dazzle — see finding 1 for the fix (a Linux-gated `ValidatePath` test with an upper-cased root segment, plus the `PathAuthorizeTests` mirror). The same blind spot covers `Path.Authorize.IsInRoot` (`RootComparison` at `Path.Authorize.cs:95-96`).

This is review-driven code — codeanalyzer v2 #2 *requested* this exact change — which is the single highest-risk category for a missing test. It has none.

## Move can't be told from Copy

`Path.Operations.cs:228-235` `PerformTransfer` branches on `isMove`: line 233 `File.Move` (removes source) vs line 234 `File.Copy` (keeps source). The Move tests in `MoveCopyBundledConsentTests` (`Move_OneMissingGrant`, `Move_BothPathsMissing`) assert `result.Success`, `AskCount`, and question text — they **never touch the filesystem afterward**. `Copy_MirrorsMove` asserts both files exist (right for copy).

**Deletion test:** swap `isMove` so Move calls `File.Copy`. The Move tests still pass (they don't look at disk); the Copy test still passes (its `src exists` assertion is true for a broken move-as-copy too). The defining behavior of Move — source gone — is unverified. Finding 2.

## PLang permission goals: green names, hollow bodies

8 PLang `.test.goal` files under `Tests/Permission/`. I read every one and verified the `.pr` for the real ones.

| Goal | Body | Real test? |
|---|---|---|
| `GrantAStoresPersisted` | `file.save` to `//tmp/...` (out-of-root) + ProbeAnswerer "a" | **Yes** — .pr verified: `channel.set`, `file.save`, `file.read`+`variable.set`, `assert.equals` |
| `Authorize/StatefulAuthorizeGrantsAndContinues` | out-of-root `//tmp/...` + VerbBasedAnswerer | **Yes** |
| `RestartStillNoPrompt` | in-root `fixture.txt` round-trip | No — placeholder |
| `NoGrantSuspends` | in-root `fixture.txt` round-trip | No — placeholder |
| `RevokeReprompts` | in-root `fixture.txt` round-trip | No — comment says "placeholder so the runner reports green" |
| `NarrowedGrantRejectsWiderRequest` | in-root `fixture.txt` round-trip | No — comment says "Happy-path placeholder" |
| `ImmediateRereadSkipsPrompt` | in-root double read | No — name claims "skips prompt", nothing verifies it |
| `Authorize/StatelessAuthorizeResumesAndContinues` | in-root `fixture.txt` round-trip | No — comment says "In-root happy path here" |

In-root paths auto-grant (`Path.Authorize.cs:35`). The 6 placeholder goals never reach a prompt, suspend, revoke, or narrowing path — they would pass even if `Path.Authorize` were deleted entirely. `plang --test` prints `[Pass] Permission/RestartStillNoPrompt/Start.test.goal` — green, under a permission-scenario name. The coder's in-goal comments are honest, but the **goal name** is what a dashboard or future auditor reads. Finding 3: rename to describe the actual behavior, or delete (C# `Stage5MessagesEndToEndTests` covers scenarios 1/2/3/5/6 properly).

## Smaller gaps

- **Finding 4** — `Stage5...Scenario4` has an empty body (`await Task.CompletedTask`) and reports *passed*. Should be `[Skip(...)]`.
- **Finding 5** — `ActorPermissionStorageTests.IdempotentAdd_..._NoDuplicateRow` asserts only `Find != null`; never proves no duplicate. `TwoHomes_...FindReturnsCorrectOne` never verifies in-memory-vs-sqlite routing.
- **Finding 6** — `LegacyFsGoalTests_StayGreen_AgainstV2Surface` body is a 2-line tautology unrelated to its name.
- **Finding 7** — `IsInRoot()`'s `OsDirectory` clause (system goal files) is never tested.
- **Finding 8** — Move/Copy bundled consent is only tested with answer "a"; the "n" (PermissionDenied) and stateless-channel (Data<Ask>) branches are untested.
- **Finding 9** — process: no `baseline-tests.md` in any coder version.

## What is genuinely solid

Credit where due — the suite is not weak everywhere:
- `PathAuthorizeTests` — covers "a"/"y"/"n"/garbage answers, stateless bubble, `PermissionDenied` carrying the constructed permission, and `Error.Key`/`StatusCode` round-trip (403). Good error-detail assertions.
- `ActorPermissionStorageTests` — strong negative cases: per-actor isolation, read-only grant rejects delete, non-matching glob, **signature-tamper rejection** (real signed-then-mutated payload).
- `FileSystemPermissionFlowTests` — 10×3 parametrized; the in-root / stateful / stateless trio is the right matrix.
- `Stage5` scenarios 2/3/5 — verify `RawSignature`, `AskCount` deltas — verify intent, not just `Ok()`.

The problem is concentrated: the v3 change and Move semantics are the false greens, and the PLang layer over-claims.

## Verdict

**NEEDS-FIXES.** Every finding is a test-quality gap — no code bug, the v3 fix itself is correct. But findings 1–3 leave the suite dishonest about a security gate, and they are cheap: roughly one ValidatePath test, one Move assertion block, and a rename/delete pass on six goal files.
