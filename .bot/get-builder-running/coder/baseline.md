# Baseline — what fails on arrival from `goal-graph-singular`, and why

Taken at **`d1f46926b`** (goal-graph-singular head; last code commit `85730c198`), **2026-09-25**, after a
clean rebuild (all `bin/` + `obj/` of PlangConsole, PLang, PLang.Tests, PLang.Generators removed first).
Machine-readable list next to this file: `baseline-failures.tsv` (`suite<TAB>test<TAB>group`).

Diff any later run against this **by test name**, never by count — the flakes below move counts on
identical code.

## Totals — C# (six suites)

| Suite | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| Modules | 977 | 939 | 38 | 0 |
| Types | 729 | 705 | 23 | 1 |
| Wire | 471 | 444 | 19 | 8 |
| Data | 868 | 816 | 45 | 7 |
| Generator | 195 | 173 | 18 | 4 |
| Runtime | 673 | 643 | 25 | 5 |
| **All** | **3913** | **3720** | **168** | **25** |

**Provenance, honestly:** 166 of the 168 were already red in my earliest sweep on this branch
(2026-09-23), and the other two are renamed cases of an old red (see Data). Nothing new went red during
the branch's last batch (list conversion, debug watch, per-Data event lists). Whether each red is
older than the branch — i.e. red on `runtime2` too — was measured **only for Wire**
(`.bot/goal-graph-singular/coder/wire-reds-classified.md`): three Wire reds pass on `runtime2` and are
branch regressions (#23). For the other suites, "red since 09-23" is all I can prove.

The cause column comes from the failure text; where I traced it, it says so. "Owner" is the open item
in `.bot/goal-graph-singular/coder/open-items.md`, or **none** — those have no item yet.

## Totals — `plang --test`

```
Test summary: 324 total, 0 pass, 0 fail, 0 timeout, 324 stale, 0 skipped
TestRunFailed (400): 322 tests could not load: old .pr format ("steps" is now "step") — rebuild it;
  1 test could not load: old .pr format ("parameter" is now "property") — rebuild it;
  1 test could not load: no .pr.
```

Exit code **1**. Nothing runs. By reason:

| Count | Reason | Which | Owner |
|---|---|---|---|
| 322 | old `.pr` format, `steps` key | every other test | #21 / #12 — needs a running builder |
| 1 | old `.pr` format, `parameter` key (has `step`, still `parameter`) | `BuilderSanity/BuilderSanity.test.goal` | #21 / #12 |
| 1 | no `.pr` at all | `Errors/ThrowAttachesData.test.goal` | #21 / #12 — never built |

This is the intended loud failure (#21a): an unloadable test fails the run instead of passing silently.

## Flakes — pass or fail on identical code

| Test | Suite | This run |
|---|---|---|
| `ChannelEntity_Write_RoundTripsDataThroughTheElement` | Runtime | red |
| `RunAsync_FailedStepNotRecorded` | Runtime | red (its `failed` line is glued to stdout `hellofailed …` in the log — a name grep misses it) |
| `AssertionError_Message_MasksSensitiveViaDiagnosticOutput` | Wire | red — also #23 (masking regression); flips between runs |
| `Run_ParallelExecution_RespectsSemaphoreLimit` | Runtime | green |
| the `After_*` timeout tests | — | green |
| `PathKind_FileScheme_ViaCreate_IsFile`, `PathKind_HttpsScheme_ViaCreate_IsHttps`, `Path_BareIsFile`, `Path_HttpsScheme_IsHttps` | — | green — #9, not reproduced since 09-23 |

## Every failing test

### Wire (19)

#### Wire — snapshot restore, parked (#18)
The wire read works; each converts a plain JSON string into a snapshot through a door the design lacks.
- `SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess`
- `MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal`
- `PlangPath_AsSnapshotConvert_EditSurvivesResume`
- `ThrowTimeSnapshot_EditSurvivesResume`
- `TypedSnapshotString_NavigateEditResume_PersistsEdit`
- `NavigateAndEditCapturedVariable_ThenResumeToSuccess`

#### Wire — security regressions, parked (#23)
Pass on `runtime2`, fail here. A tampered value verifies (fails open); a secret reaches an assertion message.
- `Cut4_TamperingPropertyValue_FailsOuterSignatureVerify`
- `OuterSignature_AfterPropertiesValueTamper_FailsVerify`
- `AssertionError_Message_MasksSensitiveViaDiagnosticOutput`

#### Wire — Statics' own untyped bag (none)
`Statics` stores `Dictionary<string, Dictionary<string, object?>>`; restore puts back "".
- `Statics_RoundTrip_PreservesNameValuePairs`

#### Wire — old serializer expectations (none)
Tests expect the retired JSON options (camelCase, null-skipping, CLR dict round-trip); the plang wire does not do that. Stale expectations or real gaps — not decided.
- `Serialize_Object_ReturnsJson`
- `Serialize_Object_IgnoresNullProperties`
- `Serialize_Enum_UsesCamelCase`
- `Properties_RoundTrip_ListOfPrimitives`
- `Properties_RoundTrip_NestedDictOfPrimitives`
- `Roundtrip_PreservesData`
- `Roundtrip_StreamBased_PreservesData`

#### Wire — other (none)
- `OnAsk_OnMessageChannel_FiresPreSerialise` — NullReference in the channel's ask event.
- `TemplateParam_ResolvesOnLoad_NoFlagStaysLiteral` — `%name%` resolves on load without the template flag.

### Modules (38)

#### Modules — condition fixtures in the pre-Child shape (none)
Traced in `IfHandlerTests.Run_IfElse_TrueRunsThen`: the fixture lays out then/else as flat sibling actions; a condition's body is now `action.Child`, so the branch writes nothing. The others fail the same way (branch output empty / "ran" missing); not each traced.
- `If_OrchestratedBranchAction_ReturnsError_PropagatesThroughStep`
- `If_OrchestratedSuccess_MarksResultHandled`
- `Run_ConditionTrue_OrchestrateThenBranch`
- `Run_ConditionFalse_SkipsThenBranch`
- `Run_IfElse_TrueRunsThen`
- `Run_IfElse_FalseRunsElse`
- `Run_InnerGoalCondition_OrchestatesIndependently`
- `IfTrue_Orchestrate_RunsThenBranch`
- `IfFalse_Orchestrate_RunsElseBranch`
- `Foreach_BodyInnerGoalFailsInsideConditionIf_PropagatesError`

#### Modules — ui include renders a size summary (none)
An included partial renders as `(N bytes)` instead of its text — looks like the partial is read as binary; not traced.
- `FluidInclude_InRootTemplate_RendersSilently`
- `Render_IncludeResolvesFromGoalDirectory`
- `Render_Include_NestedPathResolvesRelativeToPartial`
- `Render_Include_RendersPartialInline`
- `Render_Include_InheritsVariables`

#### Modules — settings / identity (none)
- `Variables_Clone_SettingsData_MissingKey_ReturnsAskError` — missing setting succeeds with null instead of the ask error.
- `Settings_DotNotation_MissingKey_ReturnsAskError` — same.
- `ErrorPropagation_VariablesGet_SettingsMissing_ReturnsAskError` — same.
- `Set_NullValue_StoresAndRetrieves` — a null property is written with no type; the deserializer rejects it.
- `MyIdentity_UpdatedAfterSetDefault` — returns "default", not the updated identity.
- `ActorDataSource_IsCreatedLazily` — the data source exists before first use.
- `Settings_CorruptDatabase_ReturnsSettingsError` — the fixture writes to `.db/system.sqlite` in a folder it never created.

#### Modules — goal read/written through STJ (none)
The tests use `System.Text.Json` on a goal; the goal's `step` list is not STJ-shaped any more. Tests to move to the goal's own reader/writer (as `GoalPathTypingTests` was).
- `ReflectionRead_ReproducesTheGoalGraph_LikeStj`
- `SaveGoal_CamelCase_StoreOnly`
- `SaveGoal_WithSubGoals_SingleFile`
- `SaveGoal_SerializesToPrPath`

#### Modules — test DLL fixtures built against removed types (none)
`TypeLoadException: app.module.signing.code.ISigning` — the fixture DLL references an interface that is gone.
- `LoadAction_ValidDll_RegistersProvider`
- `LoadAction_NoCtorDll_ReturnsProviderConstructorError`

#### Modules — debug trace (none)
NullReference inside the debug trace writer's path derivation.
- `GenerateLlmFilePath_ProducedViaPathDerivationVerbs`
- `TraceWrite_GoesThroughPathVerbs_NotFileWriteAllText`

#### Modules — other (none)
- `Discover_WithDotDotTraversal_DeniedByAuthGate` — NullReference where an auth denial is expected. **Security-shaped**: look first.
- `Hash_SHA256_ProducesCorrectHash` — hash of "test" is wrong; likely something other than the raw text is hashed (not traced).
- `Hash_Keccak256_ProducesCorrectHash` — same.
- `ModuleActions_RenderTheirNames_ThroughFluid` — renders `read`, expected `file.read`.
- `GoalCall_StillIncluded` — assertion false.
- `Query_CacheHit_PropertiesPreserved` — a cached property comes back JSON-quoted (`"preserved"` with quotes).
- `Set_NameTypedAsText_DeclinesAtDispatch` — error key is `CreateItemDeclined`, test expects `CreateVariableDeclined`; stale expectation.
- `Validate_TypeMismatch_ReturnsError` — no error returned.

### Types (23)

#### Types — test DLL fixtures built against removed types (none)
`TypeLoadException: app.type.catalog.ITypeRenderer`.
- `LoadDll_SealedNameAsRendererTypeName_FailsWith_TypeLoadCollision`
- `LoadDll_Money_RegistersTypeAndRenderer_ProducesExpectedWireString`
- `LoadDll_CustomInt_OverridesBuiltInName_RuntimeRendererWins` — the built-in number wins over the DLL's int.

#### Types — kinds catalog (none)
`number` is missing from the advertised kinds.
- `Schema_Kinds_CoversAdvertisedAndExtensionFamilies`
- `Schema_Kinds_AdvertisesNumberPrecisions`
- `Entity_Kinds_PopulatedForNumber`
- `SetAsTextMd_NavigationResolvesKindFromVariableExpression` — kind comes back "null".

#### Types — path-backed image (none)
- `SetAsImage_MintsPathBackedHandle_NoReadAtSet`
- `Serialize_PathBackedImage_EmitsRealBytes_NotEmpty` — wire carries the signed Data, not the image bytes.
- `Serialize_WalksNestedImage_InsideDictionary` — same.
- `Serialize_StrictMismatch_FailsCleanly_BeforeStreamWrite` — succeeds instead of failing.

#### Types — other (none)
- `Deserialize_FullDict_Works` — STJ cannot construct `app.type` (ctor parameter binding).
- `Deserialize_JustName_Works` — same.
- `Data_PropertyAccess_UsesDeclaredTypeForMaterialization` — no type registered as `json`.
- `Data_Materialization_CachesResultOnFirstAccess` — same.
- `LlmQuery_Build_WithSchema_ReturnsOkWithJson` — fixture gives no `message`; `ValueRequired`.
- `LlmQuery_Build_WithFormatNoSchema_ReturnsOkWithFormatValue` — same.
- `LlmQuery_Build_NeitherSchemaNorFormat_ReturnsBareOk` — same.
- `BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins` — no terminal variable type set.
- `BuilderValidate_BuildReturnsOkWithTypeName_SetsTerminalVariableSetType` — same.
- `Wire_Write_OmitsTypeForNullSentinel` — null is written with its type.
- `Resolve_Context_NotStored_OnInstance` — context is stored on the instance.
- `Redirect_Signature_IsFreshForDestination_NotOriginalUrl` — KeyNotFound.

### Data (45)

#### Data — test `.pr` files in the old format (#21 / #12)
`PrFormatOutdated: old .pr format ("steps" is now "step")`. Green again once the builder regenerates them.
- `FullPipeline_LoadAndExecute_VariablesOutputDefaults`
- `FilePaths_FromRoot_RelativeAbsoluteSubfolderDotSlash`
- `FilePaths_FromSubfolder_AbsoluteRootWorks`
- `ReadFile_ReturnMapsResultToVariable`

#### Data — fixture modules not registered (none)
`KeyNotFoundException: No module named '<x>'` (`old`, `bogus`, `legacy`, `throwing`, `nonexistent`) — the lookup throws before the behaviour the test checks (an action-not-found error). Whether the lookup should return an error instead of throwing, or the fixtures are stale, is not traced.
- `MergeStep_EmptyActions_ClearsStepActions`
- `ValidateActions_OneInvalid_ReturnsActionNotFound`
- `ValidateActions_MixedValidAndInvalid_ReturnsActionNotFound`
- `StepRunAsync_HandlerWithoutICodeGenerated_ReturnsError`
- `StepRunAsync_ExceptionInHandler_ReturnsError`
- `StepRunAsync_ActionNotFound_ReturnsError`

#### Data — plural-to-singular rename, stale expectations (none)
Expect `Steps` / `Actions`; the path is now `Step` / `Action`.
- `Parse_yields_expected_token_stream(goal·Step[planStep·index],`
- `Parse_yields_expected_token_stream(goal·Step[step·Index]·Action,`
- `Split_peels_head_from_tail`

#### Data — reader counts / sniffing (none)
Each expects the reader invoked 0 times and sees 1.
- `Reader_DoesNotSniffXmlByLookingForAngleBracket`
- `Reader_DoesNotSniffCsvByLookingForCommas`
- `Reader_DoesNotSniffJsonByLookingForLeadingBrace`
- `Reader_DoesNotSniffYamlByLookingForColon`
- `Value_AuthoredPath_NeverInvokesReader`
- `VarReference_InAuthoredValue_StillResolvesFreshPerRead`

#### Data — number precision on read (none)
Reads land in `decimal`/`long`/`int`, not the exact kind.
- `Read_NumberBigInteger_From22DigitString_LossLess`
- `Read_NumberUInt_FromBigDecimalString_ProducesUInt`
- `Read_NumberHalf_FromString_PreservesHalf`
- `Read_NumberFloat_NegativeZero_PreservesSignAndKind`
- `Cut5_RoundTrip_PreservesExactKind_AcrossTower`
- `Reader_Of_NumberInt_ReturnsDelegate` — no reader delegate.
- `Reader_Of_NumberBigInteger_ReturnsDelegate` — same.
- `NumberRead_MatchesPriorConvertOutput` — NullReference.

#### Data — json materialisation (none)
`Expected to be of type this but Value is of type this` — narrows to a different `@this` than the test expects.
- `Materialize_JsonObjectRoot_NarrowsToDict`
- `Materialize_JsonArrayRoot_NarrowsToListValueType`
- `Navigation_ReadsValueWhichMaterialises`

#### Data — format registry (none)
- `TimeSpanIso8601_LivesInFormatLayer_NotOnType` — not registered.
- `ErrorWire_RegisteredOnlyWhereItApplies_Snapshot` — not registered.
- `Snapshot_FromWire_StillExists` — not found.
- `TableXlsx_HasNoReaderYet_ThrowsUntilOneIsAdded` — no throw.

#### Data — other (none)
- `GetValue_WithMismatchedType_ReturnsDefault` — throws `FormatException` instead of default.
- `GetValue_Generic_WrongType_ReturnsDefault` — same.
- `Add_ThrowsWhenPathIsEmptyString` — throws `ArgumentException` (test expects a different type).
- `ResolveValue_FullMissingVariable_FailsVariableNotFound` — succeeds with null.
- `AsT_SameType_ReturnsSourceInstance` — a new instance comes back.
- `AsT_PlainDataTarget_VarReference_ReturnsLiveVariableData` — same.
- `IsEmpty_EmptyString_ReturnsTrue` — false.
- `Type_LazyDerivation_WithContext` — expects `System.String`, gets the plang text type; stale CLR expectation.
- `Cut1_UntouchedConfigJson_SerializesByteIdentical` — the wire carries the signed Data, not the byte-identical source.
- `ToDictionary_ReturnsAllVariables` — "expected 30 but found 30": a plang number compared to a CLR int.
- `Set_StripsPercentFromName` — false.

### Generator (18)

#### Generator — `matrix.*` fixture modules not registered (none)
`KeyNotFoundException: No module named 'matrix.<x>'` — the test-only `matrix.*` modules are not found by the registry; not traced.
- `AppRun_OnSuccess_FinallySnapshotsAndPops`
- `AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate`
- `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate`
- `AppRun_CalledTwiceByRetryModifier_TwoFramesAndSnapshots`
- `AppRun_PushesAndPopsCallstackFrame_AroundHandler`
- `AppRun_SavesAndRestoresContextGoal`
- `AppRun_SavesAndRestoresContextStep`
- `AppRun_HandlerThrows_TranslatesToServiceError_AndPopsFrame`
- `IContextHandler_ContextSameInstance_AsExecuteAsyncArg`
- `StringPlain_ReadTwice_ReturnsCachedBackingField`
- `ReResolveAcrossCalls_SharedParameterData_RawValueUnchanged`

#### Generator — deep `%var%` resolution inside nested list/dict (none)
Variables nested inside a list or dict parameter stay literal (`%x%`).
- `DataWrappedList_NestedVarInDict_DeepResolvesAndTypes`
- `DataWrappedDict_NestedVar_DeepResolves`
- `DeepResolutionDict_PrimitiveVar_Substituted`
- `DeepResolutionDict_NestedList_FullyWalked`
- `DeepResolutionList_NestedDict_SubstitutesInside`
- `DeepResolutionList_NestedListsAndDicts_FullyWalked`

#### Generator — analyzer (none)
- `DoesNotFire_OnSystemIoFile_InsidePathTypesNamespace` — PLNG002 fires inside `app.type.path` where it is exempt.

### Runtime (25)

#### Runtime — flakes
- `ChannelEntity_Write_RoundTripsDataThroughTheElement`
- `RunAsync_FailedStepNotRecorded`

#### Runtime — condition fixtures in the pre-Child shape (none)
Same shape as the Modules condition group (branch index / label never recorded); not each traced.
- `Simple_IfTrue_BranchIndexIs0`
- `Simple_IfFalse_BranchIndexIs1`
- `MultiBranch_FirstBranchMatches_BranchIndexIs0`
- `MultiBranch_SecondBranchMatches_BranchIndexIs1`
- `Run_FixtureWithConditionIf_ProductionSubscriber_RecordsBranchLabelAndChain`

#### Runtime — source-reading tests pointed at moved files (none)
They read a C# source file by path; the file moved.
- `Pile2_SqliteSettings_BindsSerializedBlob_NoToRaw` — `PLang/app/module/settings/Sqlite.cs` gone.
- `PathIsUnder_ReplacesRelativeStartsWith` — `PLang/app/module/builder/code/Default.cs` gone.
- `Sort_TwoPhase_KeysMaterialiseAsync_OrderSync_NoGetResult` — method `SortByValue` not in the file read.

#### Runtime — item apex reports "*" (none)
The concept name comes back `*`.
- `Trace_HandleReportsConceptName_NotNamespaceTail`
- `CallStack_ReportsItemApex_KindCallstack`
- `Variables_HandleReportsConceptName_NotNamespaceTail`
- `App_ReportsItemApex_KindApp`

#### Runtime — shared item instances have a settable `Template` (none)
Every primitive item (`text`, `bool`, `number`, …) exposes a settable `Template` property.
- `NoInstanceProperty_HasASetter`
- `EveryInstanceField_IncludingInherited_IsReadonly`

#### Runtime — debug module (none)
- `Debug_LevelAction_AttachesWidenedHandlers_NoThrowOnFire` — output header format changed (`=== DEBUG [BEFORE]`, test expects `ACTION [BEFORE]`).
- `NewInstance_IsEnabled_FalseByDefault` — a new debug instance is enabled.

#### Runtime — other (none)
- `AtSchemaBlocked_AsDictKey_WireMarkerOnly` — NullReference instead of `ArgumentException`.
- `FileWriteOut_UnNarrowed_EmitsRawContentBytes` — wire carries the signed Data, not the raw content.
- `Directory_WriteOut_EmitsFlatListingOfLocations_NotContents` — same.
- `ErrorAsStringSlot_OriginalErrorStaysPrimary_ConversionFailureGoesOnChain` — the conversion failure is primary instead of the original error.
- `BeforeAction_SignatureUnchanged_NoPayloadWidening` — a payload field is set that should be null.
- `BuilderCatalog_ForFixedTypeSet_RendersByteIdentical_BeforeAndAfterEntryFold` — 16 entries rendered, expects more than 20.
- `VariableSet_BangSyntax_WritesProperty` — "expected 100 but found 100": plang number vs CLR int.

## Reproduce

From the repo root:

```bash
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole < /dev/null
./dev.sh full < /dev/null                      # six suites; logs in /tmp/devsh_<Suite>.log
for s in Modules Types Wire Data Generator Runtime; do
  grep -E '^\s+(total|failed|succeeded|skipped):' /tmp/devsh_$s.log
  grep -oE 'failed [A-Za-z][^ ]*' /tmp/devsh_$s.log | cut -c8- | sort -u   # names (also catches "hellofailed …")
done
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test < /dev/null; echo "exit=$?"
```

Compare names against `baseline-failures.tsv`, not counts.
