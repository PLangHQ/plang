# Baseline audit — app-systems vs its merge-base (report only, no fixes)

## The three columns
Merge-base with runtime2: `0ea5a4b94` (origin/runtime2 fetched: it is still there). Built in a separate worktree
with dev.sh's flags (Debug, analyzers off) and run with the same runner (the test binary, 900s cap).

- **Base: 4095 tests, 4095 pass, 0 fail.**
- **Branch now: 116 failing names.** 98 exist at base (and pass there); 18 are tests base doesn't have.
- **both: 0. branch-only: 116. base-only: 0.**

**All 116 were already failing when app-systems itself started** (6db8d5732, `v1/baseline-tests.md`: 166
failing then; the branch has since fixed ~50). None is introduced by app-systems' own decisions. They come from
the ~2,300 inherited commits between runtime2 and app-systems' start (the STJ collapse, Phase B tree runtime,
clr-navigators, nav-driven-record-builder, context-never-null, …) that runtime2 doesn't have yet — so for the
merge into runtime2 they are all branch-only.

## Classification (four read-only passes; the flagged rows re-checked by me)
Totals: **STALE ~48, HARNESS ~24, BUG ~17, ? ~26.**

### BUG — the behaviour the test protects should still hold
**Root cause shared by several: a raw `string`/`byte[]` is born as an unread `item.source`** (`type/this.cs:324`),
and code that `Peek()`s for text/binary misses it:
- Hash_SHA256_ProducesCorrectHash, Hash_Keccak256_ProducesCorrectHash — `module/crypto/code/Default.cs:22,29`
  peeks (by design, "a courier read"), so `is binary` misses a source and the wire form is hashed, not the bytes.
  **Verified.**
- IsEmpty_EmptyString_ReturnsTrue — `source` has no `IsEmpty`, inherits false (`item/this.cs:484`).
- BuilderValidate_OnlyOneTerminalVariableSetPerStep_LastInChainWins (+ BuilderValidate_BuildReturnsOkWithTypeName_…, ?)
  — `set.cs:99-103` `Peek() is text` misses the source (the fixture's "foo" is also no real type).
- Statics_RoundTrip_PreservesNameValuePairs (?) — stores `inner.Peek()`, an unread source.

**Others:**
- AsT_SameType_ReturnsSourceInstance — number courier always rebuilds when the binding has a kind
  (`number/this.cs:131-139`), so the same-type pass-through never happens.
- Set_NullValue_StoresAndRetrieves — a null Data is written with no `type` (`data/this.Output.cs:91-99`) and the
  reader refuses an untyped row (`data/reader/this.cs:76`): a stored null can't be read back.
- Redirect_Signature_IsFreshForDestination_NotOriginalUrl — `path/http/this.cs` `JsonSerializer.Serialize(signResult)`
  reflects over the Data since the Wire converter went: X-Signature isn't the signature envelope (likely).
- Directory_WriteOut_EmitsFlatListingOfLocations_NotContents, Serialize_PathBackedImage_EmitsRealBytes_NotEmpty —
  nothing loads a directory listing / path-backed image before Write (the Load() pass is gone, 583592bea).
  (FileWriteOut_UnNarrowed_EmitsRawContentBytes is the same question, filed ?: reference's and data.Output's docs disagree.)
- DeepResolutionDict_NestedList_FullyWalked — a templated dict re-creates a nested list without the template
  (`dict/this.cs:123,375-381`).
- Properties_RoundTrip_NestedDictOfPrimitives — nested dict written as typed entries, read back by a naive Leaf
  (`data/Properties.cs:96-105`).
- Trace_HandleReportsConceptName_NotNamespaceTail — `%!trace%` points at the context trace, which has no
  `[PlangType("trace")]`.
- Query_CacheHit_PropertiesPreserved — llm cache replay returns RawResponse JSON-quoted (`OpenAi.cs:918-949`, line unconfirmed).
- Run_FixtureWithConditionIf_ProductionSubscriber_RecordsBranchLabelAndChain — `Coverage.Watch` records "if" but
  `Add` seeds a lone if as {true,false}: a taken branch never matches (`test/Coverage.cs:55` vs `:124-126`).
- ErrorAsStringSlot_OriginalErrorStaysPrimary_… — text's own courier returns a fresh TypeConversionFailed instead
  of keeping the error value primary as ICreate's default does (`text/this.cs:115-120`).
- Serialize_Object_ReturnsJson — `clr/format/text.cs:14` uses PascalCase STJ; the reflection Output camelCases.
- Set_NameTypedAsText_DeclinesAtDispatch (low) — variable's `Create(item, data)` isn't ICreate's 3-arg courier,
  so its own CreateVariableDeclined is unreachable (the generic CreateItemDeclined fires). Relevant to batch 3 (a).

### Needs a ruling / one check (?)
- **Possible security, checked: not shown.** OuterSignature_AfterPropertiesValueTamper_FailsVerify and
  Cut4_TamperingPropertyValue_… run under `.Testing()`, whose signing mock verifies anything — harness. Whether
  real Ed25519 verify covers Properties (verify rehashes `signature.Value` only, `Ed25519.cs:124-129`) is open:
  a real-crypto run decides it. Recommend settling first.
- A raw string with a declared number kind ignores the kind (`number/this.cs:132`: kind honoured only for an
  item): Cut5_RoundTrip_PreservesExactKind…, Read_NumberUInt/BigInteger/Half/NegativeZero — HARNESS if raw
  strings needn't honour the kind, BUG if they must (base's `number.Convert(string, kind)` did).
- ResolveValue_FullMissingVariable_FailsVariableNotFound — `set %r% = %nonexistent%` succeeds with null (5861af2b2 said error).
- Snapshot read-back family (≈7: NavigateAndEditCapturedVariable…, ThrowTimeSnapshot…, TypedSnapshotString…,
  MidStackChain…, PlangPath_AsSnapshotConvert…, SerializedString_Converts…, Snapshot_FromWire_StillExists) —
  restore deferred to the ISnapshot redesign (295f592b6); some tests call `Value<snapshot>()` on text where
  `SnapshotFromWire` is the door.
- Materialize_JsonArrayRoot/ObjectRoot, Navigation_ReadsValueWhichMaterialises — json stays `clr(json)` (ruling 6)
  vs decision 113 "narrows when examined".
- Cut1_UntouchedConfigJson_SerializesByteIdentical, Serialize_StrictMismatch_FailsCleanly…, Wire_Write_OmitsTypeForNullSentinel,
  Roundtrip_PreservesData / Serialize_Object_IgnoresNullProperties, SetAsTextMd_NavigationResolvesKind…
  (is `.Type` navigable), DataWrappedDict/DeepResolutionList nested template stamps, Reader_DoesNotSniff*/Value_AuthoredPath
  (probe counts a source in history as "parsed"), MyIdentity_UpdatedAfterSetDefault.

### STALE (~48) — asserts behaviour a change deliberately replaced; grouped by cause
- `if` evaluates only; a branch body is the condition's Child (72d72f438, decisions 158/201): IfTrue/IfFalse_Orchestrate,
  Run_Condition*, Run_IfElse_*, Run_InnerGoalCondition…, If_Orchestrated*, Foreach_BodyInnerGoalFails…  (≈11)
- branchIndex no longer stamped; coverage derives it (72d72f438): Simple_If*_BranchIndexIs*, MultiBranch_*  (4)
- STJ collapse / converters deleted (295f592b6, 0fee3709e, c6444c702): ErrorWire…, TimeSpanIso8601…, Reader_Of_Number*,
  NumberRead_MatchesPriorConvertOutput, Deserialize_JustName/FullDict, ReflectionRead…, SaveGoal_* (STJ read of .pr)
- decisions: 21 (goal.Description → Comment), 26 (root names), 77/stage 4 (field names), 100/275 (type words),
  106 (app type), 113/115(1) (MaterializeFailed), 242 (ActionEntry gone), 267 (store born in memory), clr kind = format.
- set's build-time type-match check deleted on purpose (21df818cf): Validate_TypeMismatch_ReturnsError.
- values are plang items now (compare `30` to a number item): ToDictionary_ReturnsAllVariables, VariableSet_BangSyntax_…
- Pile2_SqliteSettings… (the file it scans moved), llm.query's System/User slots → Message.

### HARNESS (~24) — the test's setup, not the product
- Fixture .pr files still use the plural `"steps"` key (2707f2dd6): FilePaths_FromRoot/FromSubfolder, FullPipeline_…,
  ReadFile_ReturnMapsResultToVariable — rebuild the fixtures.
- Test-only shims/probes: DataReadExtensions (no catch → GetValue_*_ReturnsDefault), MaterializeProbeExtensions
  (a source in history counts as parsed), TemplateStamp (no `source` arm), number read helper passing a raw string.
- Two-app setup (EngineTests MakeStep uses the class app, handler registered on a local engine): StepRunAsync_*.
- Moved paths in source-scanning tests: Plng002 path constant, PathIsUnder_ReplacesRelativeStartsWith.
- `.Testing()` signing mock verifies anything: Cut4_TamperingPropertyValue…, OuterSignature_AfterPropertiesValueTamper…
- Context-less construction now throws (AtSchemaBlocked_AsDictKey), in-memory store under `.Testing()`
  (Settings_CorruptDatabase…, ActorDataSource_IsCreatedLazily), fixture item classes with no Create/PlangType,
  Resolve_Context_NotStored (every kind class is named `@this`), LlmQuery_Build_* missing Message.
