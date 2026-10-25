# bugs — baseline (runtime2 3a29633f8 + architect plan commits, before any code)

C# (`./dev.sh test`): Modules 1361 green, Types 968 green, Wire 602 / 6 failed, Data 877 green, Generator 191 green, Runtime 1357 green.
Wire's 6 (pre-existing, the snapshot reds): SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess,
NavigateAndEditCapturedVariable_ThenResumeToSuccess, PlangPath_AsSnapshotConvert_EditSurvivesResume,
TypedSnapshotString_NavigateEditResume_PersistsEdit, MidStackChain_…, ThrowTimeSnapshot_….

plang (`cd test && plang --test '--test.setting.exclude=["llm"]'`): 1 failed (/app/type/item/BareLiteralJudgedByForm.test.goal),
3 could not load (no .pr), the llm tests excluded.
