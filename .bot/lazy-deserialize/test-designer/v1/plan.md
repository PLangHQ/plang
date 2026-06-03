# test-designer v1 — lazy-deserialize — plan

> **Update (after architect 829785fbe — "add `table` type, revert json→object"):**
> The shape-based typing revision changes the format-mapping contract — json/xml/yaml stay `{object, kind}` (today's behavior), and csv/xlsx land the new `{table, kind}`. Tests updated in-place rather than re-numbered:
> - `FormatRemapTests.cs`: `…ApplicationJson_ReturnsTextJson_NotObjectJson` → `…ReturnsObjectJson`; `…DotCsv_ReturnsTextCsv` → `…ReturnsTableCsv`; added `.xlsx → {table, xlsx}` and a csv extension/MIME convergence row.
> - `ChannelReadBoundaryTests.cs`: `…StampsTextJson_NotObjectJson` → `…StampsObjectJson_NoParseAtStamp` (the lazy invariant: stamping does *not* parse) + a new `…StampsTableCsv_NoParseAtStamp` row.
> - `PerTypeReadEntriesTests.cs`: `Reader_Of_TextJson_…` → `Reader_Of_ObjectJson_…` + a new `Reader_Of_TableCsv_…`.
> - `TypeOwnedReadParityTests.cs`: `TextJsonRead_…` → `ObjectJsonRead_…`.
> - `Cut2_TouchMaterializes.cs`: reworded — *untouched is the raw string* even when `type=object` (stamping doesn't parse). Added a csv/table row.
> - `NavigationAccessTests.cs`: added two rows — `Navigation_ObjectShape_NavigatesByKey` + `Navigation_TableShape_NavigatesByRowColumn` (shape decides the navigation model).
> - New file: `OneBoundaryTests/TableTypeTests.cs` (6 rows) — table type exists, `(table, csv)` reader discovered, xlsx-stamps-but-no-reader-yet rides as raw bytes, grid-shape advertised, stamping-doesn't-parse, navigation by row/column.
> - Goal layer: `ReadConfigJson_UntouchedIsJsonString.test.goal` flipped to `object`; `AsJson_ResolvesTypeUnknown.test.goal` now uses `as object/json`; new `ReadCsv_LandsAsTable.test.goal`.
>
> Independent #16 stays but gets a csv twin. The `table` row count grows the suite to 35 C# files / ~177 tests + 10 goal tests. None of the independent additions are invalidated by the revision.

> **Update v1.2 (per `architect/test-designer-sync.md`) — Converter resolution:**
> Edits in `ConverterDeletionsTests.cs`:
> - `TypeJson_TypeIsGone` → `TypeJson_StillExists_ReadsTypeDescriptor`. `type.json` stays — it reads the type descriptor `{name,kind,strict}` (the wire `type` slot), not a value; not a value-materializer, not in the value-reader registry.
> - `PathConverterRegistrationSites_NoLongerAddPathJsonConverter` → `…NowWireSingleJsonConverter` (the 6 sites wire the single json `Converter` instead).
> - Added: `SingleJsonConverter_Exists_AtChannelSerializerJson` — the existence pin for `app/channel/serializer/json/converter.cs`, class `Converter` (built per-actor with context).
> - Added: `SingleJsonConverter_RoutesMidGraphFieldToTypeRead_ViaRegistry` — the behaviour pin (consults registry/`OwnerOf`, routes to owning type's `Read`).
> - Added: `NestedPathField_ThreeLevelsDown_DeserialisesViaConverter` — the load-bearing regression the coder caught (a `path` three levels down via `As<T>`).
>
> Already-correct rows left as-is: `PathJsonConverter_TypeIsGone`, the "gone or folded" rows for ErrorWire/HashDataConverter/TimeSpanIso8601 (they collapse into the single `Converter` + each type's `Read`).

## Frame

Architect carved 5 stages and 5 integration cuts: reader registry + consolidation → numbers (Way 3) → lazy Data → one I/O boundary → access-driven resolution. Hand-off docs: [test-strategy.md](../../architect/plan/test-strategy.md) + [test-coverage.md](../../architect/plan/test-coverage.md) + [architect-verdict.md](../../architect/architect-verdict.md).

This branch is **structurally a refactor + capability extension**, not net-new behaviour. Stage 1 is "no behaviour change" by design — the existing suite is the floor. Stages 2–5 add the new behaviours (full number tower, lazy `_raw`, verbatim passthrough, channel as the one boundary, type-unknown error path). So the contract is **floor-then-ceiling**: the existing tests must stay green, and every new surface gets a fresh pin.

The strongest payoffs (the two the architect calls out) drive the integration cuts:
- **Verbatim passthrough** — untouched raw-backed Data serializes raw byte-identical (Cut 1).
- **Signature verifies against raw, no materialisation** (Cut 3).
The number tower (Cut 5) and the http body-lazy/metadata-eager split (Cut 4) round out the cuts.

## Independent additions (beyond the architect's matrix)

Per the test-design principle "think independently — don't just translate the architect's plan", these rows are mine. Each carries a reason.

1. **`reader.@this.Of` is the same registry shape as `renderer.@this.Of`** — same `_generated`/`_runtime` split, same `"*"` wildcard, same precedence. A reflection-level structural-equivalence test (mirror-of-renderer probe). If reader diverges in subtle ways (e.g. uses `kind` slot differently), the test sees it.
2. **Discovery's static-Read scan honours the same `serializer/<format>.cs` pivot as the renderer's static-Write scan.** The architect says "mirror"; the implementation can mirror in spirit and diverge in mechanism. Pin the mechanism: a fresh static `Read` in a `serializer/Default.cs` file is auto-discovered without touching central code.
3. **`reader.Of("text", "json")` returns a non-null delegate after consolidation.** The architect's matrix has per-type round-trips; I add the registry-level "the entry exists for the (text, json) key" probe — the failure-mode if `text` declares no `Read` for `json` is `Of` returns null and the dispatch silently degrades.
4. **`Data._raw` is private, not `[Out]`** — verbatim passthrough lives by `_raw`; if it gets serialised as a property by accident, the wire shape grows a new key that nothing else knows about. Reflection probe: `_raw` is `private`, not annotated with anything that makes the renderer pick it up. (The wire shape is canonical — see CLAUDE.md "Data is not enveloped".)
5. **`Data._raw` survives `.Value` access AND survives a serialize roundtrip on an untouched value.** The architect lists "survives materialisation"; I extend it to "survives the courier path that would otherwise re-encode it" — a Wire.Write of untouched raw-backed Data does *not* clear `_raw` (otherwise a second read would re-materialise).
6. **`Data.Value` on an authored value (`_value` set, `_raw` null) never calls the reader.** The architect lists this; I add the *negative* — a reflection probe that counts reader dispatches and asserts zero invocations for the authored-value path. Catches the bug where the lazy path unconditionally runs and the authored value gets re-typed.
7. **A mutation on a raw-backed Data invalidates `_raw`** — covered by the architect; I add the *follow-on*: a subsequent Wire.Write emits the renderer's output, not the (now stale) raw. Pins the "post-mutation, raw is no longer authoritative" rule end-to-end.
8. **`number.Read` parses negative zero (`-0.0`) and signed `decimal` correctly across `float`/`double`/`decimal` kinds** — the union model historically lost sign. Pinned as a parametric row.
9. **`number.Read` of a value too big for the named kind errors at materialise-time, not at read-time** (lazy means the error moves). Negative test of the access-time error surface. Goes hand-in-hand with Stage 3's "malformed json errors at first touch" row.
10. **`number` arithmetic that *narrowing* would lose precision on holds the wider kind** — `decimal(10) + decimal(0.1) → decimal` (not narrowed to int). Pins the "narrow only when value fits" half of promote-then-narrow.
11. **`number` arithmetic against the same operand twice is deterministic** — `(a + b) + c == a + (b + c)` for integers across kinds. Independent integer-tower associativity probe; cheap but pins that promote-then-narrow doesn't introduce silent ordering bugs.
12. **`http.response.@this` deletion is checked by absolute name** — `System.Reflection.Assembly.GetType("app.http.response.@this")` returns null. The architect lists "result is plain Data"; the absent-type probe is the strict version. A surface-level rename that left the type in place would slip past behaviour tests.
13. **`http.get`'s typed return on the goal surface is now `Data`, not `app.http.response.@this`** — the action handler's `Run` signature lost the `http.response` reference. Reflection on `app.module.http.code.Default.@this` method signature. Catches the case where the type "deletes" but the dispatch return-type still points at it via metadata.
14. **`channel.read` of a stream whose `Mime` is unset (defaulted to `text/plain`) produces a `{text, null}` Data** — the architect says "stamps type/kind from Mime"; pin the default case explicitly so a careless implementation doesn't stamp `{object, null}` or throw.
15. **`channel.read` of an `application/octet-stream` Mime produces a `{bytes, null}` Data and `_raw` is `byte[]`, not `string`.** Decision 3 ("raw is bytes only where the source is genuinely bytes") needs a specific octet-stream probe — otherwise the bytes-or-text branch is silently text-only.
16. **`Format.TypeFromExtension(".json")` matches `Format.TypeFromMime("application/json")`** — same `{text, json}` result. The architect lists each separately; the *equivalence* check guarantees both paths converge after the structured-text remap.
17. **The malformed-json error message names the source variable** — Stage 3's "error names the source" row. I pin "the error's Key/Message contain the variable name or path" so it's actionable, not a generic STJ message.
18. **Type-unknown structured access error message includes the literal `add ` `as <type>`** — Stage 5's clear error. The exact phrasing is the LLM teaching surface; pin the string so it's the contract, not a styling free-for-all.
19. **`%response!status%` reading does not trigger `_raw` materialisation** — strengthens Cut 4. A probe-based assertion that the body's `_value` stays null after a property read. (Cut 4 already says this verbally; I add the per-action C#-side pin.)
20. **`LiftDataIfShaped` removal is checked by name AND by behaviour** — reflection probe that the private static method is gone, plus a behaviour probe that a payload with `name`+`value` keys at the value slot stays as a dict (no shape-guess lift). Two-prong because the method could be renamed without the heuristic actually being removed.

These 20 additions are independent rows; the rest of the suite translates the architect's matrix into concrete test names.

## Batches

C# placement: `PLang.Tests/App/LazyDeserialize/<Topic>Tests/`. Goal placement: `Tests/LazyDeserialize/`.

### Batch 1 — `ReaderRegistryTests/` (Stage 1, C#)

`ReaderRegistryShapeTests.cs`
- `Reader_TypeExists_AtAppTypeReaderNamespace`
- `Reader_HasOf_TakingTypeAndKind`
- `Reader_HasRegister_TakingTypeKindAndDelegate`
- `Reader_HasWildcardConstant_MatchingRendererAnyFormat` (independent #1)
- `Reader_DelegateSignature_RawKindContext_ReturnsObject`
- `Reader_PrecedenceProbe_RuntimeExactBeatsGeneratedExact` (independent #1)
- `Reader_PrecedenceProbe_ExactBeatsWildcard` (independent #1)
- `Reader_Of_ReturnsNull_WhenNoEntry` (negative)
- `Reader_DiscoversStaticReadInSerializerDefault` (independent #2)

`PerTypeReadEntriesTests.cs`
- `Reader_Of_TextJson_ReturnsDelegate` (independent #3)
- `Reader_Of_PathDefault_ReturnsDelegate` (path's wildcard)
- `Reader_Of_NumberInt_ReturnsDelegate`
- `Reader_Of_NumberBigInteger_ReturnsDelegate` (Stage 2 surface but the entry should exist by Stage 1's end)
- `Reader_Of_ImagePng_ReturnsDelegate`
- `Reader_Of_DurationIso8601_ReturnsDelegate` (TimeSpanIso8601 folded in)
- `Reader_Of_HashDefault_ReturnsDelegate` (crypto.hash FromWire folded in)

`ConverterDeletionsTests.cs` — what should be gone by Stage 1's end
- `PathJsonConverter_TypeIsGone` — `Assembly.GetType("app.type.path.JsonConverter")` returns null
- `TypeJson_TypeIsGone` — `app.type.json` class gone
- `ErrorWire_TypeIsGone_OrFoldedIntoRead` — `app.error.ErrorWire` either deleted or no longer registered as a `JsonConverter<IError>` standalone
- `HashDataConverter_TypeIsGone_OrFolded` — `app.module.signing.Signature+HashDataConverter` likewise
- `TimeSpanIso8601_TypeIsGone_OrFolded` — `app.channel.serializer.TimeSpanIso8601` likewise
- `PathConverterRegistrationSites_NoLongerAddPathJsonConverter` — reflection scan of the 6 sites' Converters lists; none mentions the deleted type

`DistributedOwnerOfTests.cs`
- `OwnerOf_CentralSwitch_NoLongerExists` (`app.type.convert.@this.OwnerOf` either deleted or routes through declarations — pinned the *routes-through* contract by behaviour: adding a new family-owned CLR type doesn't require editing `convert/this.cs`)
- `Number_DeclaresIntLongDecimalDoubleFloat` (and after Stage 2, the full tower — Stage 2 row pins the extension)
- `Text_DeclaresString`
- `Path_DeclaresPathSubclasses`
- `Image_DeclaresByteArrayForPngGifJpeg`
- `OwnerOf_RoutingComposes_FromFamilyDeclarations` (a probe: ask the registry which family owns `typeof(uint)` and assert `number`, not a hand-written branch)

`TypeOwnedReadParityTests.cs` — Stage 1's "no behaviour change" pin
- `PathRead_MatchesPriorJsonConverterRead` (parametric: a set of canonical path strings)
- `NumberRead_MatchesPriorConvertOutput` (parametric: int, long, decimal, double, float)
- `HashRead_MatchesPriorFromWireOutput`
- `ErrorRead_MatchesPriorErrorWireOutput`
- `TimeSpanRead_MatchesPriorTimeSpanIso8601Output`
- `TextJsonRead_MatchesPriorPlangJsonReaderOutput`

`ReadFailureTests.cs` — negative path
- `Read_OfMalformedJson_ProducesError_NotThrow`
- `Read_OfTypeUnknownToReader_ReturnsNullDelegate` (registry behaviour) → caller surfaces a `TypeUnknown` error
- `Read_WrappedAsTaskFailure_NeverEscapesToCourier` (the courier rule)

`ResidualTryConvertTests.cs` — generic plumbing stays
- `TryConvert_NullableUnwrap_StillWorks`
- `TryConvert_AssignableFastPath_StillWorks`
- `TryConvert_ListElementWalk_StillWorks`

`SnapshotCarveOutTests.cs` — the snapshot signatures
- `Snapshot_FromWire_StillExists` (reflection)
- `App_SnapshotToWire_StillExists`
- `App_SnapshotFromWire_StillExists`
- `App_ResumeFromWire_StillExists`

### Batch 2 — `NumberTowerTests/` (Stage 2, C#)

`NumberStorageTests.cs` (the union-replacement)
- `Number_OldUnionFieldsGone_iAnd_dAnd_f` — reflection probe `_i`, `_d`, `_f` no longer present
- `Number_NumberKindEnum_Removed_OrRedefined` — old enum gone or no longer the canonical kind label
- `Number_ExactClrValue_StoredVerbatim_Int` (`int(5)` round-trips as `int`, not `long`)
- `Number_ExactClrValue_StoredVerbatim_UInt`
- `Number_ExactClrValue_StoredVerbatim_ULong`
- `Number_ExactClrValue_StoredVerbatim_Int128`
- `Number_ExactClrValue_StoredVerbatim_UInt128`
- `Number_ExactClrValue_StoredVerbatim_Half`
- `Number_ExactClrValue_StoredVerbatim_Float` (no float→double collapse — Stage 2 marquee row)
- `Number_ExactClrValue_StoredVerbatim_Decimal`
- `Number_ExactClrValue_StoredVerbatim_BigInteger`
- `Number_ExactClrValue_StoredVerbatim_Sbyte_Byte_Short_Ushort` (parametric)

`NumberKindDerivationTests.cs`
- `Kind_DerivesFromValueClrType_Int → "int"`
- `Kind_DerivesFromValueClrType_UInt → "uint"`
- `Kind_DerivesFromValueClrType_Float → "float"` (independent: not "double")
- `Kind_DerivesFromValueClrType_BigInteger → "biginteger"`
- `Kind_ForAllTowerEntries_RoundTripsThroughKindsList`
- `Kinds_AdvertisesFullTower` (the catalog: `sbyte byte short ushort int uint long ulong int128 uint128 half float double decimal biginteger`)
- `KindToClr_CoversFullTower` (each kind name maps back to its CLR type)
- `BuildHook_StampsFromValueGetType_NoFloatCollapse` (the data/this.cs:242 site is gone)

`NumberReadTests.cs` — Stage 2's reader-side parsing
- `Read_NumberInt_FromString_PreservesInt`
- `Read_NumberUInt_FromBigDecimalString` (`"3000000000"` as `uint` → uint(3000000000))
- `Read_NumberBigInteger_From22DigitString_LossLess` (independent: the architect lists this; pin lossless)
- `Read_NumberFloat_NegativeZero_PreservesSignAndKind` (independent #8)
- `Read_NumberDecimal_PrecisionPreserved_28Digits`
- `Read_NumberHalf_FromString_PreservesHalf`
- `Read_TooBigForNamedKind_ErrorsAtMaterialise_NotAtRead` (independent #9; lazy means error moves)
- `Read_NonNumericString_ProducesTypedError`

`NumberArithmeticTests.cs`
- `IntPlusInt_StaysInt`
- `UIntPlusUInt_PromotesAndNarrowsToLong_NoWrap` (the `3000000000u + 2000000000u` marquee row)
- `IntPlusFloat_PromotesToDouble`
- `IntPlusDecimal_PromotesToDecimal`
- `DoublePlusDecimal_RaisesExplicitCastError` (the "correct not easy" row; negative)
- `DivisionProducingFraction_LandsOnDecimalOrDouble_PerOperandKinds`
- `BigIntegerLossless_AcrossSumOfManyInts`
- `Narrowing_OnlyWhenValueFits` (independent #10: `decimal(0.1) + decimal(10)` stays decimal, not int)
- `IntegerAssociativity_AcrossKinds` (independent #11: `(a+b)+c == a+(b+c)`)

`NumberDeclaresClrTypesTests.cs` (the distributed OwnerOf row, scoped to number)
- `Number_DeclaresFullTowerCrlTypes` (the catalog, asserted via the family declaration surface)
- `Number_AddingNewCrlType_RequiresOnlyNumberEdit` (architectural probe: a synthetic test that walks the OwnerOf composition and asserts no central switch)

### Batch 3 — `LazyDataTests/` (Stage 3, C#)

`LazyDataShapeTests.cs`
- `Data_HasRawField_String_Or_ByteArray` — reflection probe of the new backing slot (name flex: `_raw`/`_bytes`/whatever the coder picks, but the *type* is `object?` admitting `string`+`byte[]`)
- `Data_RawField_IsPrivate_NotPublicNotOut` (independent #4 — verbatim passthrough demands raw isn't a wire property)
- `Data_RawField_NotPickedUpByRendererNormalize` (companion to #4; Wire.Write of an authored value omits a `raw` key)
- `Data_PreservesExistingValueFactory_AndDynamicData` (two-laziness preservation)

`LazyMaterialisationTests.cs`
- `Value_MaterialisesViaReader_WhenValueNull_AndRawSet`
- `Value_ReturnsValueDirectly_WhenValueSet_AndRawNull` (authored values short-circuit)
- `Value_AuthoredPath_NeverInvokesReader` (independent #6: probe-counted)
- `Value_RawSurvivesMaterialisation` (read `.Value`, then assert `_raw` still set)
- `RawBackedSerialize_AfterValueWasRead_StillEmitsRawVerbatim` (independent #5 — the survival-through-courier row)
- `ConvertValue_IsRemoved` (reflection probe: `app.data.@this.ConvertValue` no longer present, fold complete)
- `Navigation_ReadsValueWhichMaterialises` (the on-navigate materialisation path replaces ConvertValue)
- `VarReference_InAuthoredValue_StillResolvesFreshPerRead` (unchanged contract — Stage 3 must not break it)

`MutationInvalidatesRawTests.cs`
- `SetValueDirect_InvalidatesRaw` (after `data.SetValueDirect(newValue)`, `_raw` is null)
- `NavigationSet_InvalidatesRaw`
- `AfterMutation_SerializeUsesRenderer_NotRaw` (independent #7: end-to-end)

`RawTypeShapeTests.cs` — Decision 3
- `Raw_ForTextSource_StoredAsString_NotUtf8Encoded`
- `Raw_ForBinarySource_StoredAsByteArray`
- `Raw_NoUtf8EncodeTax_OnTextRoundTrip` (a perf-shape probe: the byte[] of the source equals UTF-8 encoding of `_raw` — i.e. we did not encode it ourselves)

`WireReadLazyTests.cs`
- `WireRead_CapturesValueSlotRaw_DefersMaterialisation`
- `WireRead_DoesNotEagerlyDeserialiseValueSlot` (probe: a malformed value slot does not throw at Wire.Read time)
- `WireRead_StampsTypeKindFromTypeSlot`
- `LiftDataIfShaped_MethodIsGone` (independent #20 — reflection probe by exact name)
- `LiftDataIfShaped_BehaviourIsGone` (independent #20 — payload with `name`+`value` keys at value slot stays as dict, no lift)
- `NestedSignedData_RebuiltByContainingTypeReader_NotByKeyGuess` (the case `LiftDataIfShaped` covered)

`MaterialiseErrorPathTests.cs`
- `MalformedJson_ErrorsAtFirstTouch_NotAtRead` (Stage 3 negative)
- `MalformedJson_ErrorNamesTheSource` (independent #17 — error.Key or Message contains the variable identifier)
- `Materialise_Failure_SurfacedAs_DataError_NotThrown_ToCourier` (the courier-rule pin)

### Batch 4 — `OneBoundaryTests/` (Stage 4, C#)

`ChannelReadBoundaryTests.cs`
- `ChannelRead_StampsTypeKind_FromMime`
- `ChannelRead_ProducesLazyData_RawSetValueNull`
- `ChannelRead_DefaultTextPlainMime_StampsTextNullKind` (independent #14)
- `ChannelRead_OctetStreamMime_StampsBytesAndRawIsByteArray` (independent #15)
- `ChannelRead_ApplicationJsonBody_StampsTextJson_NotObjectJson` (the structured-text remap; Decision)
- `ChannelRead_ApplicationPlangBody_DelegatesToPlangSerializer_LazyContainer`
- `StreamChannel_NoLongerReturnsBareText` (the `app/channel/stream/this.cs:69` smell)

`ChannelKindLayoutTests.cs`
- `ChannelKinds_AllLiveUnder_channel_type` — reflection scan for the kinds (stream, session, message, event, goal, noop, file, http) under `app.channel.type.*`
- `FileChannel_Exists_AtAppChannelTypeFile`
- `HttpChannel_Exists_AtAppChannelTypeHttp`

`FileChannelTests.cs`
- `FileChannel_ReadsBytesViaPathReadBytes_AuthGateEnforced` (no `System.IO` in the channel; PLNG002 clean)
- `FileChannel_Mime_DerivedFromExtension`
- `FileRead_OpensFileChannel_NoReadTimeConvertInFilePathReadText` (the `FilePath.ReadText` site stops converting at read)

`HttpChannelTests.cs`
- `HttpChannel_IsBidirectional`
- `HttpGet_OpensHttpChannel_StopsContentTypeDeserialize`
- `HttpResponse_TypeDeleted_ByAbsoluteName` (independent #12 — `Assembly.GetType("app.http.response.@this")` is null)
- `HttpGet_Run_ReturnTypeIsData_NotHttpResponse` (independent #13)
- `HttpResponse_BodyIsLazyValue_StatusHeadersDurationAreProperties`
- `HttpStatusRead_DoesNotMaterialiseBody` (independent #19)

`FormatRemapTests.cs`
- `TypeFromMime_ApplicationJson_ReturnsTextJson_NotObjectJson`
- `TypeFromMime_ApplicationXml_ReturnsTextXml` (forward-looking; if xml MIME exists in the registry)
- `TypeFromExtension_DotJson_ReturnsTextJson`
- `TypeFromExtension_DotCsv_ReturnsTextCsv` (when csv is in the registry)
- `TypeFromExtension_DotPng_ReturnsImagePng` (image stays image)
- `TypeFromMime_ApplicationOctetStream_StampsBytesNotObject`
- `TypeFromExtension_AgreesWith_TypeFromMime_ForDotJson` (independent #16 — convergence pin)

### Batch 5 — `AccessResolutionTests/` (Stage 5, C#)

`ScalarAccessTests.cs`
- `Scalar_BytesValue_DecodesUtf8_WhenValidUtf8`
- `Scalar_BytesValue_StaysBytes_WhenInvalidUtf8` (no silent corruption)
- `Scalar_TextValue_ReturnsString_NoStructuredParse`

`NavigationAccessTests.cs`
- `Navigation_KnownType_MaterialisesViaReader_AndNavigates`
- `Navigation_TypeUnknown_ProducesAddAsTypeError` (the contract error)
- `Navigation_TypeUnknownErrorMessage_ContainsLiteralAsType` (independent #18: the exact phrasing)
- `Navigation_OnAuthoredDictValue_DoesNotTriggerReader`

`AsCastTests.cs`
- `AsJson_OnTypeUnknownValue_ReadsTowardJson`
- `AsType_OnAlreadyTypedValue_NoOp_OrRetypes` (pinned to the contract chosen by the coder; comment names the choice)

`PropertyAccessTests.cs`
- `PropertyRead_ReadsFromProperties_NotValue`
- `PropertyRead_NeverMaterialisesValue` (the body-never-touched contract; companion to #19)

`NoContentSniffingTests.cs`
- `Reader_DoesNotSniffJsonByLookingForLeadingBrace` (negative: a `{`-prefixed string with no `as json` is not auto-typed)
- `Reader_DoesNotSniffXmlByLookingForAngleBracket`
- `Reader_DoesNotSniffCsvByLookingForCommas`
- `Reader_DoesNotSniffYamlByLookingForColon`

### Batch 6 — `IntegrationCutsTests/` (the 5 cuts)

`Cut1_VerbatimPassthrough.cs`
- `Cut1_UntouchedConfigJson_SerializesByteIdentical`
- `Cut1_UntouchedWirePayload_SerializesByteIdentical`
- `Cut1_NavigatedConfigJson_StillRoundTripsSemantically`
- `Cut1_ReaderProbeCount_StaysZero_OnUntouchedPath`

`Cut2_TouchMaterializes.cs`
- `Cut2_ConfigJson_UntouchedIsTextString_NavigatedReturnsField`
- `Cut2_BigIntegerString_ReadsLossless_OnArithmetic`
- `Cut2_ImagePng_MaterializesOnly_WhenWidthRead`

`Cut3_SignThenWireThenVerify.cs`
- `Cut3_SignedData_VerifiesAgainstRaw_WithoutMaterialising`
- `Cut3_NestedSignedData_InnerSignatureReachesVerify` (the post-LiftDataIfShaped row)
- `Cut3_TamperedRaw_FailsVerification` (negative)

`Cut4_HttpBodyLazyMetadataEager.cs`
- `Cut4_StatusRead_DoesNotMaterialiseBody`
- `Cut4_FieldRead_MaterialisesBody`
- `Cut4_HttpResponseTypeIsGone` (the reflection check, end-to-end)

`Cut5_NumberTowerRoundTrip.cs`
- `Cut5_RoundTrip_PreservesExactKind_AcrossTower` (parametric)
- `Cut5_PromoteThenNarrow_NoSilentWrap`
- `Cut5_DoubleDecimal_RaisesExplicitCastError` (negative)

### Batch 7 — `Tests/LazyDeserialize/*.test.goal` (PLang surface)

The goal-layer rows from `test-coverage.md`:

- `ReadConfigJson_UntouchedIsJsonString.test.goal` — read `config.json`, assert `%cfg%` equals the file's raw json text (no parse).
- `ReadConfigJson_NavigateMaterialisesField.test.goal` — read `config.json`, then `%cfg.port%` returns `8080`.
- `HttpStatusRead_DoesNotMaterialiseBody.test.goal` — `get http`, assert `%response!status%` is `200` and `%response.field%` (afterwards) returns the field. (The lazy contract is hard to prove from PLang without a probe; this is a smoke test, with the strict probe in C# Batch 4.)
- `BigIntegerSumOverflowsUInt_LandsCorrect.test.goal` — `set %a% = 3000000000`, `set %b% = 2000000000`, `set %sum% = %a% + %b%`, assert `%sum%` equals `5000000000`. The marquee no-wrap row.
- `DoublePlusDecimal_Errors.test.goal` — `set %a% = 1.5` (double), `set %b% = 0.1m` (decimal — *if* PLang surface admits the literal; else use `as decimal`), `set %sum% = %a% + %b%`, assert an error is raised. Negative.
- `NavigationOnTypeUnknown_AsksForAsType.test.goal` — `set %x% = "{\"port\":8080}"`, then `%x.port%` — assert error contains "add `as <type>`".
- `AsJson_ResolvesTypeUnknown.test.goal` — `set %x% = "{\"port\":8080}" as json`, then `%x.port%` returns `8080`.
- `SignAndVerifyRoundTrip.test.goal` — sign a Data, transport it through a goal-call boundary, verify; assert success.
- `TamperedSignedData_FailsVerify.test.goal` — sign, mutate `_raw` (or its goal-visible analogue), verify; assert error. Negative.

(Specific fixtures — a `config.json` fixture and a small `.png` fixture — sit alongside the `.test.goal` files. The coder generates these; ~50 bytes each.)

### Batch 8 — Integration cut runners

The Cut*.cs files in Batch 6 *are* the integration runners; they exercise the engine end-to-end. No separate "Cut*.cs harness" file — keeping batches 6 and 8 merged in the actual layout. Listed separately above only for the architect's strategy alignment.

## Notes for the coder

- **One stage at a time.** Strict order — Stage 1 must leave both suites green before Stage 2 begins; Stages 2/3/4/5 unblock their own tests as the surfaces appear. The architect's "Stage 1 — no behavior change" rule means Batch 1's `TypeOwnedReadParityTests.cs` is the floor: existing-output equivalence must hold before consolidation lands. If it doesn't, the consolidation got something subtly wrong and the parity rows catch it.
- **Test bodies are `Assert.Fail("not implemented")` / `- throw "not implemented"`.** Names + comments are the spec. Where I baked a particular choice — `_raw` is private (independent #4), the type-unknown error wording (independent #18), narrowing-only-when-value-fits (independent #10) — the comment names the choice so the coder can flip the test with one line if a different shape is right.
- **No tests for renames where coverage already exists.** Wire's general "round-trips a Data" suite already exists; I don't repeat it. The new rows are `Wire.Read` *defers* the value slot — a behavioural-not-shape change.
- **`config.json` and `.png` fixtures.** Two fixtures suffice for the goal layer; the C# layer constructs synthetic raw bytes inline.
- **Reflection probes are deliberately liberal on names.** "Has a private field that holds string-or-byte[] raw" rather than "has a private field named `_raw`". The coder picks the field name; the contract is the *shape*.

## Open items (test-designer judgment, flag if coder disagrees)

1. **Independent #4 (`_raw` is private)** — pinned as private. If the coder chooses a protected slot to enable a subclass laziness pattern, flip the probe to `private | protected`. The contract is "not on the wire".
2. **Independent #18 (the exact error wording for type-unknown structured access)** — pinned to contain the literal `add ` `as `. If the coder picks different wording, update the probe; the *contract* is that the error names the fix.
3. **Independent #10 (narrowing-only-when-value-fits)** — `decimal + decimal` stays decimal even when the value would fit an int. The alternative is "narrow aggressively" — I picked conservative because Way 3's rule says the result kind is the wider of the operand kinds, widened further only if needed. If the coder picks aggressive narrowing the test row name flips.
4. **Cut 1's reader-probe-count (independent #6 + Cut1 row)** — the architect says "assert the reader was never invoked"; the probe needs a mechanism. Suggest the coder add a debug-only counter on `reader.@this` (incremented in `Of` or in dispatch). Tests can toggle it via the existing debug seam; production cost is zero.
5. **Goal-layer `DoublePlusDecimal_Errors.test.goal`** — needs the PLang surface to admit `decimal` literals (or `as decimal` modifier). If neither exists, the goal test is impractical and the negative row stays C#-only. Flag back if so.

## Verdict (target)

All test files written, all bodies `Assert.Fail`/`throw "not implemented"`, `dotnet build PLang.Tests` is green (0 errors, no new warnings introduced). `plang --test` discovers the `.test.goal` files. PASS = the contract is on disk and ready for the coder.
