# tester v6, second pass — snapshot: FAIL (one flaky plang test)

Tip `18a3b5d51` (coder `d0a068281`, `33ad97de6`; builder `18a3b5d51`). Test-only diff since the first pass (`2df750c73`): `git diff 2df750c73..18a3b5d51 -- PLang PLang.Tests test` touches no production file.

## Counts

- C#: 5641 total, **0 red**, 45 skipped (Modules 1477, Types 1037, Wire 641, Data 871, Generator 191, Runtime 1424). Same totals as the first pass; the placeholder and the empty-chain test became one real test, and `ErrorCallbackTests` gained one.
- plang: 312 total (was 308: four new plan tests), 311 pass, 1 skipped (`DurationPartsAndCompare`), 0 stale, on three of five full runs. **Two full runs went red on `ResumeTakesInTheBinding`.**
- No red C# name. One red plang name that passed in the first full run, so it is new: finding A.

## Finding A — major, flaky: `ResumeTakesInTheBinding.test.goal`

| run | result |
|---|---|
| alone (`plang --test path=/plan/snapshot/module/snapshot/resume/ResumeTakesInTheBinding`), 15 clean runs | 15 pass |
| the plan folder (`path=/plan/snapshot`), 8 clean runs | **2 pass, 6 fail** |
| the plan folder, `parallel=1` | fail |
| with one other plan test (any of the five), 3 runs of two pairs | pass, pass, fail; pass, fail, pass |
| the full suite | pass, fail, pass |

- Error: `%logged% is not set — nothing to answer for it.` The bound call `Log` (`before each goal, call Log`, bound after the save, taken in by the resumed sub app) did not run before `Resumed` started.
- Not a particular neighbour: any second test makes it flaky, and `parallel=1` does not cure it. Alone it is stable.
- **A failed run poisons the next ones.** The test deletes `workspace/resumed.marker` only at its end; a failed run leaves it, and every later run (even alone) then takes the `Resumed` branch straight after `call Plan`, before any binding, and fails the same way until the file is removed by hand. I cleared it between runs above.
- The architect's note says a production change may keep a binding once on resume; I have not diagnosed whether that is this. The test is a red name in the plan's own folder as it stands.
- Suggestion: find why the binding is not in the resumed sub app when other tests ran in the process (the sub app takes it in "at birth" from its maker); then make the test clean its marker first, not last (delete at the top of `Start`, before `call Plan`).

## The first pass's findings

| # | finding | now |
|---|---|---|
| 1 | placeholder `ResumeChain_MultiActionStep…`; rest-after-call mutant survives | fixed. `SnapshotResume_InsideACall_FinishesIt_ThenTheRestOfTheCallingStep_Once` (a counter that adds; the call must count once, the action after it run once). Mutant B (`if (!again) return result;`) is now **red in that one C# test** (no plang test catches it). |
| 2 | call re-run mutant survives; `FreshAppGoesOn` idempotent | fixed. `%count%` is set by the test before `call Record`, so a double run counts to 2. Mutant A (`at + 1` → `at`): red in the new C# test, `FreshAppGoesOn` and `EditedStopPoint`. |
| 3 | five behaviours C#-only | four now have a plang test: `EventComesBack`, `EditedStopPoint`, `EditLeavesTheProgram`, `ResumeTakesInTheBinding`. The changed-goal warning stays C# by ruling. |
| 4 | `start.goal`/`start.md` stale | fixed. All six tests are indexed; the C#-only ones are named in comments. |
| 5 | warning and callback asserted loosely | fixed. The warning tests assert `'RecompiledGoal' step 0, action 0` and `'Order' step 1, action 0`; `Callback_StandsAtTheErrorsCall_AfterItsFramesEnded` reads the callback's goal frame. (`Callback_ErrorWithContext_AnswersASnapshot` still asserts only non-null; the new test carries the content.) |
| 6 | empty-chain test misnamed | fixed (replaced). |
| 7 | `snapshot.sign` / baseline file | ruled by the architect (287); the baseline file is still missing. |

## The new plang tests

- `.pr` read: every step's text matches its `module.action` (`on.event`, `goal.call`, `signing.sign`, `snapshot.resume`, `assert.equals`, `variable.set` + `on.error`).
- `EventComesBack`: mutation, `[global::app.Snapshot]` removed from `call.Event` (`PLang/app/call/this.cs:75`, reverted): red in `EventComesBack`, `CallStack_RoundTrip_TheEventRunningInAFrame_ComesBackWithIt`, `ARunSavedInsideABoundCall_ReadsItsEventAgain_AsTheResumedAppsOwn` and the surface golden `TheCoresSurface_IsThePinnedOne`.
- `EditedStopPoint`: red under mutant A; it asserts `before c ran`, so a resume at the saved step (not the edited one) or one that re-ran step 2 would show.
- `EditLeavesTheProgram`: not mutation-checked. The step-place refusal (`ReadOnly`) comes from the type system's property (`type/property/this.cs:136`), which I did not mutate.
- `ResumeTakesInTheBinding`: finding A.

## Alone and in the folder

Alone: all six plan tests and the two callback tests pass (clean workspace). In the folder: see finding A; the other five pass every time.

## Coverage

`coverage-2.json`. 166 files, **1908 added executable lines, 1740 reached, 168 not** (first pass 1737; 3 more now reached). Unreached groups unchanged: see `result.md`.

## Mutation log (announced; all reverted, `git status` clean)

`PLang/app/call/action/this.cs:74`: `at + 1` → `at` (red); → `return result` (red, one C# test); `PLang/app/call/this.cs:75`: `[Snapshot]` removed from `Event` (red).
