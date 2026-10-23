# item-doors — tester review (tip 86adaf86b)

**Verdict: PASS.**

- **No new red.** The full run on a fresh build matches the expected state exactly:
  - C#: all green except Wire's six snapshot tests, which are the snapshot branch's and in the baseline.
  - plang: 301 pass, BareLiteralJudgedByForm red, 3 stale under `plan/snapshot`.
- **The three timing tests held** for 10 runs each under a forced sweep, with the load average between 11 and 30.
- **Coverage.** For each of the branch's ten behaviours, I removed it in a throwaway worktree, and a test went red every time (announced; reverted; the worktree is deleted).
- **One gap: start.Set's "all or none".**
  - When the good flags are written as they pass, nothing goes red.
  - I added `StartTests.Set_ARefusedFlag_WritesNoneOfTheOthers`. Under that mutation it is red with "expected 15, found 3"; on clean source it is green.

```
item-doors review (decisions 225, 237, 249, 257, 262, 267, 270, 294, 218a/221, 228)
Done
- full run on a fresh build: no red name outside .bot/kind-is-type/coder/baseline-failures.txt
- ErrorInPlayTests, SleepTests, SettingCacheTests each run 10× under `./dev.sh test --force`: 30/30 green
- each behaviour mutated in a worktree: a test goes red for every one, after one test is added
- added PLang.Tests/Data/App/Core/StartTests.cs Set_ARefusedFlag_WritesNoneOfTheOthers
HASH: (this commit)
CHECK: C# 6 red = the Wire snapshot six; plang 301 pass / 1 fail (BareLiteralJudgedByForm) / 3 stale (plan/snapshot)
OPEN: none
```

## 1. Full run

`./dev.sh build`, then `./dev.sh test` (all six suites ran), then `cd test && plang --test`.

| Suite | Result |
|---|---|
| Modules | green, 1313 |
| Types | green, 951 |
| Wire | 6 red of 591: SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess, PlangPath_AsSnapshotConvert_EditSurvivesResume, NavigateAndEditCapturedVariable_ThenResumeToSuccess, TypedSnapshotString_NavigateEditResume_PersistsEdit, MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal, ThrowTimeSnapshot_EditSurvivesResume. All six are in the baseline. |
| Data | green, 860 |
| Generator | green, 191 |
| Runtime | green, 1126 |
| plang | 306 total: 301 pass, 1 fail (`/app/type/item/BareLiteralJudgedByForm`, in the baseline and red by Ingi's choice), 3 stale (`/plan/snapshot/module/callback/*`, no .pr), 1 skipped |

**No new red name.** Fourteen of the baseline's plang reds are green now. The baseline's C# timing red `After_EachRetry_IsAFreshAttempt_WithAFreshDeadline` is also green.

## 2. Flaky under load

A forced sweep (`./dev.sh test --force`) ran alongside. Each class ran 10 times through its test binary, with the load average climbing from 11 to 30. The forced sweep itself came out the same as in §1.

| Test | Runs | Result |
|---|---|---|
| ErrorInPlayTests (7 tests) | 10 | 10/10 green |
| SleepTests (5 tests) | 10 | 10/10 green |
| SettingCacheTests (5, including ReadsBesideWrites) | 10 | 10/10 green |

**Nothing failed, so there is no fix to propose.** Two notes for if one fails later:

- **SleepTests is a wall-clock test.** `Sleep_LastsItsDuration` asserts `elapsed < 10*ms + 5000`, which is 10 s for the 0.5 s case. The 14 s run seen once would fail it. That is the bound under load, not the code. The honest fix is to keep the lower bound, which catches a unit read wrong, and drop the upper bound or tie it to a measured `Task.Delay` baseline.
- **ErrorInPlayTests has no wall-clock assert.** `ErrorInPlay_DuringRecovery_IsTheTimeoutsOwnVerdict` races a 1 ms timeout against a 3 s sleep, so it can only fail if the thread pool is starved for 3 s.
- **SettingCacheTests.ReadsBesideWrites has no timing at all.** `Set` is synchronous and every write finishes before the final read, so a red there would be a real staleness bug.

## 3. Coverage of what item-doors changed

Each mutation was applied alone in a throwaway worktree. I built the test project and ran the suite or class; the source was reverted after each one.

| Behaviour | Mutation | Red |
|---|---|---|
| sign holds a copy | `Ed25519.cs:34` sign the variable itself, no `Settle` | VerifyActionTests: ASignature_HoldsTheValueItSigned_NotTheVariable, ASignatureKeptInAVariable_Verifies, ASignedValue_InAList_Verifies |
| verify follows | `Ed25519.cs:77` no `Follow` | VerifyActionTests: 6 red, including AMemberWrittenAfterSigning_DataHashMismatch, APropertyAddedAfterSigning_DataHashMismatch, AStoredSignedValue_BoundUnderAnotherName_Verifies |
| tamper fails | `Ed25519.cs:118` hash compare off (earlier, for 262) | plang tamper/ContentChangedAfterSigningFailsVerify, tamper/PropertyAddedAfterSigningFailsVerify; the coder's C# pair |
| a list row keeps its signature (267) | `item/this.cs:636` Enroll places a bare Data instead of the carrier's copy | VerifyActionTests: ASignedValue_InAList_Verifies, AProperty_SurvivesTheAdd, AKeptMember_SurvivesTheAdd |
| `!` reaches the origin (225) | `variable/code/Property.cs:56` history fact off | OriginFactTests.AReadFileNarrowedByAUse_StillAnswersItsPath_AndTheDictItsOwnKey |
| `!` reads the binding's properties | `Property.cs:43` off | 8 red across Runtime and Data, including BangSize_AndDotSize_AreDistinct_NoShadowing, AProperty_IsNoMember, PropertyRead_ReadsFromProperties_NotValue |
| `.` and `!` apart: dot reads Kept (237) | `Property.cs:37` off | KeptMembersTests: AVariablesOwnValue_KeepsAMember_InItsBinding, AnAddedMember_RidesTheWire, AnAddedMember_RidesTheStore |
| `.` and `!` apart: a kept member is not a property | `data/this.Navigation.cs:61` `Kept.Add` changed to `Properties.Set` | KeptMembersTests: AnAddedMember_IsNoProperty plus the three above |
| json compares as json | `kind/json/source.cs:41` `Order` changed to `base.Order` | JsonCompareTests: TwoEqualJsonObjects_AreEqual, JsonAndAnEqualDict_CompareAsTheirContent, ValuesSetAsJson_AreJsonsOwn_ThroughThePr |
| json truthy as json | `json/source.cs:31` `IsTruthy()` only | JsonFileTruthTests.AJsonFile_IsTrueAsWhatItHolds, all six empty cases |
| a null under item is the null type (249) | `type/this.cs:338` always `this` | NullUnderItemTests: ANullUnderItem_IsTheNullType, SetToNull_ThroughThePr_ItsTypeIsNull |
| an unnamed duration or size writes in the asker's standard | `duration/span` and `size/count` pinned to short / iec | AnUnnamedDuration_IsWrittenInTheAskersStandard, AnUnnamedSize_IsWrittenInTheAskersStandard, TheSetting_NamesTheStandardOfASpanWithNoForm, TheSetting_NamesACountsStandard |
| settings keep: reread after a write | `actor/setting/this.cs:325` version ignored | 37 red in Runtime, including all of SettingCacheTests but the first, ThisRun_WinsOverTheRow, Debug_TakesALaterValue |
| settings keep: built once | `:325` keep off | SettingCacheTests.ASecondRead_IsTheSameSetting |
| the setting checks: a path no setting goes by (294) | `start/this.cs:80` unknown path let through | Configure_APathNoSettingAnswers_IsRefused ×2, Values_LeaveOutTheCallersWords |
| the setting checks: a value the option can't take | `start/this.cs:82` refusal skipped | 8 red, including Flag_RefusesAValueItsOptionCantTake, Configure_AnOptionTypo_IsRefused, AFormatNoTypeReads_IsRefusedWhenSet |
| **the setting checks: all or none** | `start/this.cs:83` write each flag as it passes | **nothing, so I added** `StartTests.Set_ARefusedFlag_WritesNoneOfTheOthers`: red under the mutation ("Expected to be 15 but found 3"), green clean |
| debug follows the named goal into its calls | `module/debug/this.cs:164` caller clause off | DebugAttachTests: 5/5 red |
| debug does not follow what the named goal didn't call | `:164` every goal marked | DebugAttachTests.AGoalTheNamedOneDidNotCall_IsNotDebugged |
| `--test <path>` | `Executor.cs:102` always the start folder | ExecutorTests.Configure_TestWithAPositional_TestsThatPath ×3 |
| discovery's Validate | `test/list/this.cs:162` `Invalid` skipped | DiscoverActionTests.Discover_APrWhoseCodeNoLongerHolds_IsStaleWithItsReason |

**The test added**, in `PLang.Tests/Data/App/Core/StartTests.cs`:
```csharp
// the flags are written all or none: a refused one after a good one leaves the good one unwritten too
[Test]
public async Task Set_ARefusedFlag_WritesNoneOfTheOthers()
//   start = Parse(["--math.setting.equal.digits=3", "--nothing.here=1"]); set = await start.Set(store, context)
//   set.Success is false; math's equal.digits is still 15
```

**Not run under mutation:** the plang twins of the C# guards (SignedDataSurvivesInList, the null tests, Cut5). For each behaviour above, a C# test already goes red.
