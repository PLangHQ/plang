# Test baseline — `clr-navigators` before implementation

**Captured:** 2026-07-07, HEAD `e6faad8f6` (before Stage 1).
**Runner:** `./dev.sh test` (C# suites, analyzers off). Generator green.

These are **red on HEAD `e6faad8f6` before any Stage-1 change** — pre-existing to my work. I have
**not** investigated *why* they fail; the cause is not asserted here and is out of scope unless a
stage is meant to fix one. This file is only a reference line: after each stage, a failure **not** on
this list is a regression I introduced; a failure that **leaves** this list is progress. Do not treat
any of these as "mine" unless my change is why.

## Counts

| Suite | Failed | Total |
|---|---|---|
| Modules | 35 | 910 |
| Types | 22 | 720 |
| Wire | 18 | 490 |
| Data | 35 | 893 |
| Generator | **0 (green)** | 198 |
| Runtime | 32 | 750 |
| **Total** | **142** | **3961** |

## ⚠️ Blast-radius — baseline-red tests my work is EXPECTED to move

The reader pivot (json → `clr`, no longer → `dict`) and the kind-navigation change **intentionally
alter** what these tests assert. Some are red now for the context-never-null reason; some assert the
*old* eager-dict behavior and will need rewriting to the clr/kind model. Track these deliberately —
a green flip here is the goal, and if one stays red I must confirm it's the *new* representation it's
testing, not a regression:

- `Data/Materialize_JsonObjectRoot_NarrowsToDict`, `Materialize_JsonArrayRoot_NarrowsToListValueType` — assert eager dict/list narrowing; the pivot changes this to `clr(json)`. **Expect to rewrite.**
- `Data/Navigation_ObjectShape_NavigatesByKey`, `Navigation_KnownType_MaterialisesViaReader_AndNavigates`, `Scalar_VariableInterpolation_BareVarIsRaw_DottedNavigates` — navigation over json; new path is `Type["item"].Kind["json"]`.
- `Data/Cut1_NavigatedConfigJson_StillRoundTripsSemantically`, `Cut1_UntouchedConfigJson_SerializesByteIdentical`, `Cut4_FieldRead_MaterialisesBody`, `Cut5_RoundTrip_PreservesExactKind_AcrossTower` — json body materialization + round-trip.
- `Data/*MalformedJson*`, `SetPath_OnMalformedJson_*`, `Read_OfMalformedJson_ProducesError_NotThrow` — error-at-first-touch on bad json; the guard + reader pivot touch this.
- `Types/Body_ApplicationJson_StampsBinaryJson_LazyThenNavigates`, `Body_TextHtml_*`, `Body_BrokenJson_*`, `Body_ImagePng_*`, `Body_MissingContentType_*`, `Body_TextCsv_*`, `Metadata_StatusIsProperty_NotBody` — http body → kind stamping/navigation.
- `Types/SetAsTextMd_NavigationResolvesKindFromVariableExpression`, `UnwrapJsonElement_Null_ProducesDataNullSingleton`, `Data_PropertyAccess_UsesDeclaredTypeForMaterialization` — kind resolution + materialization.
- `Types/LlmQuery_Build_WithSchema_ReturnsOkWithJson`, `LlmQuery_Build_WithFormatNoSchema_*`, `LlmQuery_Build_NeitherSchemaNorFormat_*` — the `context.Ok(raw, kind)` producer door (Stage 5).
- `Modules/Query_CacheHit_PropertiesPreserved`, `Roundtrip_LlmDict_ToStep_*`, `RealTraceJson_ToStep_*`, `Raw_STJ_Serialize_Deserialize_ShouldPreserveActions` — llm result + wire round-trip (the exact bug's shape).
- `Types/BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins`, `Modules/ValidateActions_*`, `Set_NullValue_StoresAndRetrieves` — `variable.set` (Stage 1 apex-fix touches the Type clause here).
- `Wire/Variables_SurviveWireRoundTrip_WithValueAndType`, `Serialize_Object_ReturnsJson`, `Properties_RoundTrip_*` — the wire round-trip Stage 1/4 fix.

Everything else in the list below is **out of blast radius** — if any of those flips (either way),
investigate, because my change shouldn't be touching it.

## Full failing list (by suite)

Modules (35): GenerateLlmFilePath_ProducedViaPathDerivationVerbs, TraceWrite_GoesThroughPathVerbs_NotFileWriteAllText, GetApp_CorruptJson_KeepsGeneratedId, Raw_STJ_Serialize_Deserialize_ShouldPreserveActions, RealTraceJson_ToStep_ShouldPreserveActions, Roundtrip_LlmDict_ToStep_ShouldPreserveActions, SaveGoal_SerializesToPrPath, SaveGoal_WithSubGoals_SingleFile, SaveGoal_CamelCase_StoreOnly, ValidateActions_NormalizesIntStringToInt, ValidateActions_SkipsVariableReferences, ValidateActions_NormalizesBoolStringToJsonBool, ValidateActions_GoalCallPath_Resolved, Set_NullValue_StoresAndRetrieves, MyIdentity_UpdatedAfterSetDefault, GoalCall_ExistingProperties_UnchangedAfterNewFields, Query_CacheHit_PropertiesPreserved, Describe_ModifierActions_AppearInSummary, ErrorPropagation_VariablesGet_SettingsMissing_ReturnsAskError, ActorDataSource_IsCreatedLazily, Settings_CorruptDatabase_ReturnsSettingsError, Settings_DotNotation_MissingKey_ReturnsAskError, Variables_Clone_SettingsData_MissingKey_ReturnsAskError, Integration_FileExists_FlowsThroughVariables_ToOutput, Integration_FileNotExists_FlowsThroughVariables_ToOutput, Foreach_BodyGoalCallFails_PropagatesError, Foreach_BodySucceeds_CompletesAllIterations, Foreach_NumberCollection_RunsBodyOnceWithNumber, Foreach_StringCollection_BodyReceivesWholeString, Foreach_BodyInnerGoalFailsInsideConditionIf_PropagatesError, Foreach_EmptyCollection_ReturnsZeroCount, Foreach_Dictionary_KeyIsStringNotIndex, Foreach_OrchestatesGoalCall, Foreach_SetsItemVariable, ValidateBuild_VariableReference_ReturnsNull

Types (22): UnwrapJsonElement_Null_ProducesDataNullSingleton, CryptoVerify_DefaultsAlgorithmFromHashValue, SetAsTextMd_NavigationResolvesKindFromVariableExpression, Wire_Write_OmitsTypeForNullSentinel, Data_PropertyAccess_UsesDeclaredTypeForMaterialization, Data_Materialization_CachesResultOnFirstAccess, ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes, Body_ApplicationJson_StampsBinaryJson_LazyThenNavigates, Body_TextHtml_StampsBinaryHtml, Body_BrokenJson_UntouchedIsRawBytes_NoEagerFail, Body_ImagePng_ScalarIsBytes_ValueMaterializesImage, Body_MissingContentType_StampsBinary, Body_TextCsv_StampsBinary, Metadata_StatusIsProperty_NotBody, LlmQuery_Build_WithSchema_ReturnsOkWithJson, LlmQuery_Build_WithFormatNoSchema_ReturnsOkWithFormatValue, LlmQuery_Build_NeitherSchemaNorFormat_ReturnsBareOk, SchemeHandler_Exposes_PublicSingleStringConstructor, Formats_ExtensionToPlangName_ReadsThroughRegistry, Redirect_Signature_IsFreshForDestination_NotOriginalUrl, BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins

Wire (18): Cut4_TamperingPropertyValue_FailsOuterSignatureVerify, OuterSignature_AfterPropertiesValueTamper_FailsVerify, Properties_RoundTrip_ListOfPrimitives, Properties_RoundTrip_NestedDictOfPrimitives, Serialize_Object_ReturnsJson, Deserialize_ShallowNesting_StillWorks, Sensitive_IdentityData_PrivateKeyExcluded, EndToEnd_SuspendedState_SurvivesDisk_AndResumesToSuccess, EmptyApp_WireIsValidJson_AndRestoresClean, BuildAndTestingBits_SurviveWireRoundTrip, MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal, TypedSnapshotString_NavigateEditResume_PersistsEdit, NavigateAndEditCapturedVariable_ThenResumeToSuccess, Variables_SurviveWireRoundTrip_WithValueAndType, SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess, ThrowTimeSnapshot_EditSurvivesResume, PlangPath_AsSnapshotConvert_EditSurvivesResume

Data (35): Materialize_JsonObjectRoot_NarrowsToDict, Materialize_JsonArrayRoot_NarrowsToListValueType, Add_ThrowsWhenPathIsEmptyString, GetByPrPathAsync_ReturnsNull_ForCachedSetupGoal, FullPipeline_LoadAndExecute_VariablesOutputDefaults, ResolveValue_FullMissingVariable_FailsVariableNotFound, FilePaths_FromRoot_RelativeAbsoluteSubfolderDotSlash, ReadFile_ReturnMapsResultToVariable, FilePaths_FromSubfolder_AbsoluteRootWorks, AsT_PlainDataTarget_VarReference_ReturnsLiveVariableData, Decompress_InvalidInner_ReturnsError, Navigation_ObjectShape_NavigatesByKey, Navigation_KnownType_MaterialisesViaReader_AndNavigates, Cut1_UntouchedConfigJson_SerializesByteIdentical, Scalar_VariableInterpolation_BareVarIsRaw_DottedNavigates, Cut1_NavigatedConfigJson_StillRoundTripsSemantically, Cut2_ImagePng_MaterializesOnly_WhenWidthRead, Cut4_StatusRead_DoesNotMaterialiseBody, Cut5_RoundTrip_PreservesExactKind_AcrossTower, Materialise_Failure_SurfacedAs_DataError_NotThrown_ToCourier, Cut4_FieldRead_MaterialisesBody, SetPath_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound, Navigation_ReadsValueWhichMaterialises, MalformedJson_ErrorNamesTheSource, SetPath_NestedOnMalformedJson_SurfacesMaterializeFailed_NotNotFound, MalformedJson_ErrorsAtFirstTouch_NotAtRead, Navigation_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound, HttpStatusRead_DoesNotMaterialiseBody, HttpResponse_BodyIsLazyValue_StatusHeadersDurationAreProperties, HttpGet_OpensHttpChannel_StopsContentTypeDeserialize, TableXlsx_HasNoReaderYet_ThrowsUntilOneIsAdded, Snapshot_FromWire_StillExists, Read_OfMalformedJson_ProducesError_NotThrow, Read_WrappedAsTaskFailure_NeverEscapesToCourier, Set_StripsPercentFromName

Runtime (32): AtSchemaBlocked_AsDictKey_WireMarkerOnly, BangType_ReturnsHeadlineType, ViaChannel_AssemblesActions_AndKeepsParamType, BinaryEquality_SameByteSequence_Equal, DatetimeIsoTextCoerces_BothDirections, Pile2_SqliteSettings_BindsSerializedBlob_NoToRaw, PathIsUnder_ReplacesRelativeStartsWith, Variables_HandleReportsConceptName_NotNamespaceTail, EqualsHandler_OnSuccess_VariablesNotPopulated, EqualsHandler_OnFailure_PopulatesVariablesFromSnapshot, AllAssertHandlers_OnFailure_ConsistentlyPopulateVariables, MultiBranch_SecondBranchMatches_BranchIndexIs1, Discover_ExcludeFilter_MatchingTests_MarkedSkipped, Discover_UserTags_ExtractedFromTestTagActionInPr, Discover_AutoTags_TraverseSubGoals_UnionsAcrossCallChain, MultiActionOrchestrate_InnerElseIfMatches_FilterSkipsPhantomSites_SubStepsRun, Run_Timings_OnlyEntryGoalTopLevelSteps_NestedRollUp, Run_FixtureThrowsUnexpectedException_CapturedAsFail_LoopContinues, NewInstance_IsEnabled_FalseByDefault, Run_OutputCapture_OutputChannelOnly_ErrorChannelExcluded, PrimitiveBindFailure_NamesParameter_AndNeverLeaksIConvertible, Run_FixtureWithConditionIf_ProductionSubscriber_RecordsBranchLabelAndChain, Run_OnlyReadyTests_Executed_StaleAndSkippedPreserved, Run_TimeoutExceeded_TestMarkedTimeout, Run_AssertionFailureInTest_CapturedInResult_NoPropagatedException, Run_TestingIsEnabled_SetToTrueInChildApp, Roundtrip_StepsAndActions_Preserved, Run_FreshAppPerTest_IsolationBoundaryIsFileLevel, TryConvertTo_DictToStep_Works, TryConvertTo_StringToListOfInt_ConvertsThenWrapsAsList, Run_ParallelExecution_RespectsSemaphoreLimit

## Re-baseline command

```bash
./dev.sh test 2>&1 | grep -E "^====.*(FAILED|green)"
```
