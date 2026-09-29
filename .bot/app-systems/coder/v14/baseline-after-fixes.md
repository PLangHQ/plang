# Baseline after the fix pass (decision 302) — 36 failing, each with its owner

The 116 inherited failures (`baseline-audit.md`) are down to **36**. `../baseline-failures.txt` is now these 36
names — the gate (`namediff.sh`) diffs failing names against it. No regressions along the way: every commit was
name-diffed against the list before it (the only extra names were load-timing flakes, one made deterministic).

## Still red, and why (nothing here is unexplained)

### Waits on decision 304 — the birth shape (with Ingi; patch kept, `set.cs:197–202` fix proposed) — 18
A raw string/byte[] is born an unread `source`, so code that looks at the value misses text/binary:
- Hash_SHA256_ProducesCorrectHash, Hash_Keccak256_ProducesCorrectHash
- IsEmpty_EmptyString_ReturnsTrue
- BuilderValidate_BuildReturnsOkWithTypeName_SetsTerminalVariableSetType, BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins
- Statics_RoundTrip_PreservesNameValuePairs
- Reader_DoesNotSniffCsvByLookingForCommas, …Json…LeadingBrace, …Xml…AngleBracket, …Yaml…Colon
- Value_AuthoredPath_NeverInvokesReader, VarReference_InAuthoredValue_StillResolvesFreshPerRead
- DataWrappedDict_NestedVar_DeepResolves, DataWrappedList_NestedVarInDict_DeepResolvesAndTypes,
  DeepResolutionDict_NestedList_FullyWalked, DeepResolutionDict_PrimitiveVar_Substituted,
  DeepResolutionList_NestedDict_SubstitutesInside, DeepResolutionList_NestedListsAndDicts_FullyWalked
  (TemplateStamp can't see a `source`; born text, its text arm stamps it)

### With Ingi — rulings — 6
- FileWriteOut_UnNarrowed_EmitsRawContentBytes — what `write out %file%` prints in Out (content vs location).
- Serialize_StrictMismatch_FailsCleanly_BeforeStreamWrite — the failure is right; 253 bytes were already written
  (the half-written stream: render-then-emit vs a buffering channel).
- CallStack_ReportsItemApex_KindCallstack, Variables_HandleReportsConceptName_NotNamespaceTail,
  Trace_HandleReportsConceptName_NotNamespaceTail — whether %!callStack%/%!variables%/%!trace% exist beside
  %!app.…% (0d).
- EveryInstanceField_IncludingInherited_IsReadonly — the item base mutates `_history` and `_on` on a shared
  instance; whether a value's history and events belong to the instance is Ingi's (decision 322).
  (`Template` is born, not stamped, since 90ba528bf — NoInstanceProperty_HasASetter passes.)

### Red by ruling — 1
- Query_CacheHit_PropertiesPreserved — the llm cache is Ingi's specimen.

### Waiting on a redesign or a question — 11
- Snapshot read-back (waits on the ISnapshot redesign): MidStackChain_SurvivesDisk_ResumesDeep_AndUnwindsToEntryGoal,
  NavigateAndEditCapturedVariable_ThenResumeToSuccess, PlangPath_AsSnapshotConvert_EditSurvivesResume,
  SerializedString_ConvertsToSnapshotViaTypeSystem_AndResumesToSuccess, ThrowTimeSnapshot_EditSurvivesResume,
  TypedSnapshotString_NavigateEditResume_PersistsEdit
- json narrowing (Ingi's json question): Materialize_JsonArrayRoot_NarrowsToListValueType,
  Materialize_JsonObjectRoot_NarrowsToDict, Navigation_ReadsValueWhichMaterialises
- the fixture item's decode (json decodes to an unread {item, json} the courier can't build a record from — the
  same json question): Roundtrip_PreservesData, Roundtrip_StreamBased_PreservesData

Resolved from the ? group (decisions 323–326): Snapshot_FromWire (deleted), Serialize_Object_IgnoresNullProperties,
Serialize_Enum (value as declared), SetAsTextMd (`!type`), Wire_Write_OmitsTypeForNull, MyIdentity (real provider),
AsT_PlainDataTarget (Follow; AsCanonical deleted), Cut1 (json content relays verbatim).

## Load-flaky under the full parallel sweep (not in the list; pass alone)
- ReadUrl_Fetches_OverHttp — once, 23 s, read null (local test server). To be made explicit if it recurs.
- Run_ParallelExecution_RespectsSemaphoreLimit — was flaky; made deterministic (f54e2cee8).
- After_EachRetry_IsAFreshAttempt_WithAFreshDeadline — once.
