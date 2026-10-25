# Baseline: packages at eda88a882 (runtime2 55302f8a5's tree)

## C# (`./dev.sh test --force`)
| suite | total | failed |
|---|---|---|
| Modules | 1343 | 0 |
| Types | 964 | 0 |
| Wire | 601 | 6 |
| Data | 877 | 0 |
| Generator | 191 | 0 |
| Runtime | 1357 | 0 |

Wire's six (pre-existing snapshot reds):
- SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess
- PlangPath_AsSnapshotConvert_EditSurvivesResume
- TypedSnapshotString_NavigateEditResume_PersistsEdit
- NavigateAndEditCapturedVariable_ThenResumeToSuccess
- MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal
- ThrowTimeSnapshot_EditSurvivesResume

## plang (`cd test && plang --test`, PlangConsole rebuilt)
303 total, 298 pass, 1 fail (`/app/type/item/BareLiteralJudgedByForm.test.goal`), 0 timeout, 3 stale, 1 skipped.

No build errors.
