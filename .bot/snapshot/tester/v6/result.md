# tester v6 — snapshot: FAIL (two confirmed false greens in the resume core)

Tip `2df750c73`. Fork point and runtime2 tip: `b283811f6`.

## Counts

| | snapshot `2df750c73` | runtime2 `b283811f6` |
|---|---|---|
| C# Modules | 1477 (0 red, 12 skipped) | 1434 (0 red, 12 skipped) |
| C# Types | 1037 (0 red, 4 skipped) | 1034 (0 red, 4 skipped) |
| C# Wire | 641 (0 red, 4 skipped) | 612 (**6 red**, 4 skipped) |
| C# Data | 871 (0 red, 7 skipped) | 880 (0 red, 7 skipped) |
| C# Generator | 191 (0 red, 4 skipped) | 191 (0 red, 4 skipped) |
| C# Runtime | 1424 (0 red, 14 skipped) | 1410 (0 red, 14 skipped) |
| **C# total** | **5641, 0 red, 45 skipped** | 5561, 6 red, 45 skipped |
| plang | **308: 307 pass, 0 fail, 0 stale, 1 skipped** | 309: 305 pass, 0 fail, **3 stale**, 1 skipped |

- runtime2's 6 reds are the `SnapshotWireTests` the plan names (`SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess`, `TypedSnapshotString_NavigateEditResume_PersistsEdit`, `PlangPath_AsSnapshotConvert_EditSurvivesResume`, `NavigateAndEditCapturedVariable_ThenResumeToSuccess`, `MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`, `ThrowTimeSnapshot_EditSurvivesResume`). All six are green on snapshot. **No new red name.**
- runtime2's 3 stale plang tests are the unbuilt ask tests under `test/plan/snapshot/module/callback/`; the branch moved them out, as planned.
- The one plang skip on snapshot is `/app/type/duration/DurationPartsAndCompare` (not this branch's).
- **Process violation:** `.bot/snapshot/coder/baseline-failures.txt` does not exist. I compared by name against runtime2 run by me instead.
- The full plang suite ran twice (the second under the coverage collector) and the plan folder twice: same result each time; `git status` clean after.

## Each plan test alone

`plang --test path=/<folder>` from `test/` (a test is rooted at `test/`; running `plang --test` from inside the test's own folder fails with `Goal '/plan/snapshot/.../Record' not found` — a harness artifact, not an order dependence).

| test | alone | in the plan folder (twice) |
|---|---|---|
| `plan/snapshot/module/snapshot/save/SavesTheRun` | pass | pass |
| `plan/snapshot/module/snapshot/resume/FreshAppGoesOn` | pass | pass |
| `module/callback/inprocessresume` | pass | |
| `module/callback/runcallbackverb` | pass | |

No order dependence. Both `.pr` files read: every step's text matches its `module.action` (`snapshot.save`, `file.save`, `file.read`, `assert.equals`, `goal.call`, `snapshot.resume`).

## Coverage of the plan's behaviours

Only two `.test.goal` files exist under `test/plan/snapshot/`. The plan's other proofs are C#.

| behaviour | plang test | C# test that pins it | verdict |
|---|---|---|---|
| save and resume a run | `SavesTheRun` (save, write, read back, navigate — no resume); `FreshAppGoesOn` (resume in a sub app of the asker) | `SignedSnapshotTests.AFreshApp_GoesOn_FromTheFirstAppsSignedPlang`, `APlangReadThroughAFile_GoesOn`, `OnePlang_GoesOnTwice_PastTheWindow`; `SnapshotWireTests.EndToEnd_SuspendedState_SurvivesDisk_AndResumesToSuccess`, `MidStackChain_…`; `CallSnapshotTests`; `ResumeInAnAppTests` (9) | pinned, **with a hole**: finding 1, 2. The plang "fresh app" is a sub app in the same process (its `start.md` says so; the fresh-app proof is C#). |
| the error's callback | `module/callback/runcallbackverb`, `inprocessresume` (not in the plan folder, they pin it in plang) | `ResumeInAnAppTests.AFailedRun_AnswersItsError_ItsCallbackStillResumes`; `ErrorCallbackTests` (weak, finding 5) | pinned (the first two go red under mutant 2) |
| a run's bindings saved, inherited by sub apps | **none** | `SavedBindingsTests` (11: `ASubAppsSave_HoldsItsBindingsOnly`, `ABindingBeforeEachGoal_IsSaved_AndFiresAgainInAFreshApp`, `…WhoseGoalTheFreshAppHasNotLoaded_LoadsIt_AndFires`, on the app, on a module's action, C#-made not written, by-variable and channel refused) | C#-only; plang-visible (`on.event` steps) |
| `%!event%` across a save | **none** | `CallStackEventTests` (2: `CallStack_RoundTrip_TheEventRunningInAFrame_ComesBackWithIt`, `ARunSavedInsideABoundCall_ReadsItsEventAgain_AsTheResumedAppsOwn`) | C#-only; plang-visible |
| edit the stop point, sign, resume | **none** | `SignedSnapshotTests.AnEditedPlace_SignedAgain_IsWhereTheRunGoesOn_AndTheLiveGoalIsUntouched`, `AnEditedSnapshot_SignedAgainByAStep_Verifies`, `AnEditedSnapshot_NotSignedAgain_IsRefused`, `AHandEdit_IsRefused`; `SnapshotWireTests.PlangPath_AsSnapshotConvert_EditSurvivesResume` | C#-only; plang-visible (`set %snap….index%`, `sign`, `resume`). The test reads sound: ignoring either edited number changes `a` or `b`. |
| a changed goal resuming with a warning | **none** | `CallSnapshotTests.Call_Resume_AnotherActionAtThePlace_GoesOnThere_AndSaysSo`, `Call_Resume_AStepInsertedWhereTheRunStopped_RunsIt_ThenTheStepItStoppedIn`, `Call_Restore_AGoalChangedSinceTheSave_StandsAtItsPlaceInIt`, `…AnActionPastItsStepsEnd_IsRefused_NamingTheCount` | C#-only; the warning is asserted loosely (finding 5). Needs a goal rebuilt between save and resume: hard in one plang test. |
| an edit never touches the live program | **none** | the same `AnEditedPlace…LiveGoalIsUntouched` (live steps' indexes, `DeepEquals` of the re-written snapshot, no `property` added), `AStepsPlace_IsNoProgramsToWrite…`, `Call_Restore_DoesNotMutateLiveGoal` | C#-only. Not mutation-checked by me (the coder reports it was). |

The plan's own `start.goal` stages (finding 4):

| stage | `start.goal` names | exists? |
|---|---|---|
| 1 | `EachPartAtItsPlace`, `EachPartStartsAsSaved`, `AlwaysFreshNotSaved` | no |
| 2 | `StartsInTheSubGoal`, `FramesKeepTheirVariables`, `RebuiltGoalRefuses` | no |
| 3 | `ReadFromAFile` | no |
| 4 | `SensitiveNotSaved` | no (C#: `SnapshotWireTests.ASensitiveVariable_IsNotInTheSnapshotsText`, `ASensitiveMember_…`) |
| 5 | `FreshAppGoesOn` | a folder with a `.test.goal`, not a goal file |

The new action `snapshot.save` has plang tests (both). Not in plang at all: the saved code providers (`code.load`, a DLL coming back in a fresh app — `ProvidersSnapshotTests`, C#), the sensitive-value rule, signing refusals (unsigned, other app, archived identity, hand edit).

## Findings

1. **major, false green: the stop point's "go on after a returned call" is unpinned.** `ResumeChain_MultiActionStep_ContinuesAtActionIndexPlusOne` (`PLang.Tests/Wire/App/CallbackTests/SnapshotResumeTests.cs:116`) is `Assert.That(true).IsTrue()` with a comment pointing at "2a.8's cross-goal .test.goal fixture" (`StatelessCrossGoalResumes`) — that fixture is one of the three stale/unbuilt tests this branch moved out. File touched by the branch six times (boy-scout scope). Mutation test, `PLang/app/call/action/this.cs:74` `if (!again) return await Step!.Code.Start(context, at + 1, result);` → `if (!again) return result;` (the rest of a step's chain after a returned call is dropped): **0 red in all six C# suites and plang** (5641 + 308). `call/list/this.Snapshot.cs:81–83` states this as the rule: "the action after each call".
2. **major, false green: a returned call that runs again is invisible.** Same line, `at + 1` → `at` (the call action runs again when its callee returns): **0 red** anywhere. `FreshAppGoesOn` cannot tell: Record resets `%count%` and `%where%` and recomputes, so a double run of Record/Sub ends on the same `"Alice in Reykjavik 1 yes"`. The test proves the state comes back, not that nothing runs twice.
   - Control: line 82 `at` → `at + 1` (the stopped action not run again) goes red: 26 C# tests, plus `FreshAppGoesOn` and `runcallbackverb`.
3. **major, missing-plang-test: five of the seven main behaviours have no plang test** — bindings inherited by sub apps, `%!event%` across a save, edit-the-stop-point-sign-resume, the changed-goal warning, the live program untouched. Each is C#-pinned and sound; each is also how a developer meets it in plang. First two and the edit/sign/resume are feasible as `.test.goal`; the warning needs a goal rebuilt between save and resume.
4. **major, plan index: `test/plan/snapshot/start.goal` and `start.md` are the 2026-10-26 six-stage plan.** 8 of the 9 test calls in `start.goal` name goals that do not exist; `SavesTheRun` is not indexed at all, and `FreshAppGoesOn` is named by a folder path, not a goal file; `start.md` lacks stages 7–11 (frames laid out as written, added properties, signed view, the run's other state, re-sign). The index cannot be run or trusted.
5. **minor, weak assertions.**
   - `Call_Resume_AnotherActionAtThePlace_GoesOnThere_AndSaysSo` asserts the warning contains `variable.set` and `test.test`; the message is `'<goal>' step <n>, action <at> is … now, not … as it was saved: going on there` (`call/action/this.cs:80`) and architect 284 wants the goal and step named — neither is asserted.
   - `ErrorCallbackTests.Callback_ErrorWithContext_AnswersASnapshot` asserts `callback.Snapshot` is not null. The behaviour is pinned elsewhere (the two tests above); this one would pass on any snapshot.
6. **minor, misnamed test:** `SnapshotResume_NestedChain_UnwindsToParentAfterSubGoalCompletes` (`SnapshotResumeTests.cs:103`) resumes an empty snapshot and asserts `NoFrameToResume`; nothing nested is run.
7. **info:** Ingi's decision 72 (a developer re-signs with `- sign snapshot 'run.plang'`, a `snapshot.sign` action) is not in the tree: `PLang/app/module/snapshot/` holds `resume.cs` and `save.cs` only. Re-signing is the generic `signing.sign` (the C# tests use it). Stage 11 appears unbuilt; confirm that is the plan.
8. **info, process:** no `baseline-failures.txt` (above).

### Suggested fixes (tests only)

- For 1 and 2: a C# test in `CallSnapshotTests` with `Program(...)`'s multi-action lines (`"a ; b"`): a step `goal.call X ; variable.set(Name=%tail%, …)` saved inside X, then resumed; assert the call's callee ran once (a counter that adds, not sets) and the action after the call ran once. It goes red under both mutants. Delete the `Assert.That(true)` placeholder.
- For `FreshAppGoesOn`: make the effects non-idempotent — Sub's first step appends a line to `workspace/ran.txt`, Record's tail appends another; assert the file holds exactly one of each. Mutants 1 and 2 then go red in plang.
- For 3: plang tests for `%!event%` across a save and for edit → `sign` → `resume` at the changed step.
- For 4: rewrite `start.goal` as the index of what exists, and `start.md` to the stages that landed.

## Coverage (C# `--coverage` on TUnit's own platform; plang under `dotnet-coverage`)

`coverage.json` in this folder. Changed lines of `git diff origin/runtime2...origin/snapshot -- PLang/` (`.cs`): 166 files with coverage entries, **1908 added executable lines, 1737 reached, 171 not** (91.0%). No global target.

- Reached only by plang: 0 lines. Reached only by C#: 765 lines. The `.goal` tests add no coverage of this diff that C# does not already give.
- Unreached groups, by file, and what each looks like:
  - **missing test:** `module/screen/type/screen/display/this.cs` (13 of 13: teardown, `ScreenEventFailed`), the four Snapshot serializers' refusals (`SnapshotFormatOutdated`, `GoalKeyIncomplete`, `GoalNotFound`, `StepKeyIncomplete`, `ActionKeyIncomplete`, `ActionNotFound`, `ModuleKeyIncomplete`), `event/on/this.cs:158–172` (`SnapshotRowUnread`), `event/on/list/this.cs:79–84` (writing the saved `on` rows), `event/binding/action/this.cs:42–46` (`BindingNoAction`), `goal/step/list/this.cs:68` (Cancelled), `goal/step/action/list/this.cs:54–56`, `test/list/this.cs:142,158` (Stale paths), `module/build/code/Default.cs:228,338`, `type/item/path/http/this.cs` (non-2xx map, 404 Stat, network error), `snapshot/this.Variables.cs:28–29` (`SnapshotHoldsTheApp`).
  - **looks like code nothing uses:** the `Create(raw, type, data)` factory arms in `call/{action,binding,goal,isolated,step,composed}/this.cs`, `Clone() => this` overrides (`actor`, `call/list`, `error`, `goal`/`step` Item), `IsLeaf => false` overrides, `event/on/hit.cs:6`, `event/on/miss.cs:6`, `goal/list/this.cs:88` (always-false freshness).
  - Closed since the trial at `368048d00`: `module/snapshot/resume.cs` `SnapshotSignatureInvalid` and `save.cs` `SignerGaveNoSignature` are now reached.

## Mutation log (announced; all reverted, `git status` clean)

`PLang/app/call/action/this.cs`: line 74 `at + 1` → `at` (survives everywhere); line 74 → `return result` (survives everywhere); line 82 `at` → `at + 1` (26 C# reds, `FreshAppGoesOn`, `runcallbackverb`).
