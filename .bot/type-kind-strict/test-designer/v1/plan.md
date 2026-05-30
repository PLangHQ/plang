# test-designer v1 — type-kind-strict — plan

## Frame

Architect carved 5 stages: type-value-model → text + name canonicalisation → kind derivation + canonicalisation → variable.set + strict → LLM representation. Hand-off docs: [test-strategy.md](../../architect/plan/test-strategy.md) + [test-coverage.md](../../architect/plan/test-coverage.md). Three integration cuts pin the contract; per-topic C# units + per-surface `.goal` tests sit beneath.

This is **net-new behaviour** (Strict is brand new; `text` type is brand new; family-`Kind` removal + Data.Kind fold + ClrType internal are mechanical-but-semantic) — not a pure refactor. So the strategy is **ceiling-heavy**: every new surface gets a failing-test pin, every failure-matrix row gets a negative test, every integration cut a dedicated test. The existing PLang `--test` suite and C# regression suites cover the floor (they shouldn't change behaviour — `string` still resolves, numerics still convert, wire still round-trips).

## Independent additions (beyond the architect's matrix)

Per the test-design principle "think independently — don't just translate the architect's plan", I'm adding these rows that the architect didn't list. Each has a reason:

1. **`type.@this.Null` still has `Name="null"`, `Kind=null`, `Strict=false` after the rename.** Stage 1 renames Value→Name; the static Null sentinel is the easiest place to break. Surface-only fix risk.
2. **`type("Text")` — case insensitivity on Name.** PLang's type names are case-insensitive elsewhere (primitive Aliases is OrdinalIgnoreCase); Stage 1 factory must preserve. Easy regression.
3. **`type("text", "MD")` — case on Kind.** Whether kind is lowercased by the factory or only at canonicalisation matters for the contract. Pin lowercase via the canonicaliser (Stage 3), not at the factory, so unknown kinds preserve user casing — *or* pin lowercase here. Stage 3 design says "extension, lowercased" so test against lowercase.
4. **`type("text", null, strict:true)` for an unverifiable family — should not throw at construction.** Strict on a family without `IKindValidatable` degrades to "kind-name-accepted" per the design; the factory accepting it silently is the contract, the validator path returns ok.
5. **Empty-string Name → reject.** `type("", "md")` — does the factory accept an empty name? The current `@this(string value)` constructor takes any string. The new factory should reject empty/whitespace Name to avoid the LLM emitting `{"name":""}` quietly.
6. **`text.Build` is null-safe.** Stage 2 says `Build(object?)`; `Build(null)` should return null without throwing. The `image.Build` reference does this; the text mirror should too.
7. **`text.Build("../report.md")` — extension extracted, no path traversal weirdness.** Path-shaped strings flow in (since `text` and `path` overlap at `.txt`/`.md` files). The hook is dumb extension extraction — `Path.GetExtension`-equivalent — so test the dumb case.
8. **`Data.Kind` setter from caller writes through to `Type.Kind`.** If the fold removes the stored field, an external setter that writes `data.Kind = "md"` must not silently no-op or throw — either route through Type.Kind (mutating the entity) or throw a clear "do not set Kind directly" InvalidOperation. Pin the chosen contract.
9. **The wire reads a legacy `.pr` (pre-fold) with separate `type`+`kind` — round-trips identically.** The fold is internal; serialized `.pr.json` files in `.build/` written before this branch must still deserialize. (Forward-compat: trivial. Backward-compat: the contract.)
10. **`variable.set` with a bare `as text` (no kind) leaves `Type.Kind` at the Build-derived value (not null).** The architect lists "kind stamped" — pin that *bare* `as text` followed by `"readme.md"` produces `{text, md}` (the Build hook fires even without an explicit kind annotation), and `as text` with `"plain"` produces `{text, null}`.
11. **`set %x% = "a" as TEXT` (uppercase name) builds.** Case-insensitivity contract at the parser-into-type-factory boundary.
12. **`set %x% as image strict` (no kind) — what happens?** Architect lists strict with mismatched kind, strict with matching kind, strict %var%. Doesn't list strict-without-a-kind. The factory should accept `strict=true, kind=null`; the validator skips byte-check when there's no required kind (nothing to check against). Pin that it's not an error.
13. **`number.Build` doesn't trip on `IKindValidatable` calls** — Stage 4's strict path calls `IKindValidatable.ValidateKind` on the resolved CLR type. `number` doesn't implement it; the strict path must skip cleanly (not throw "not implemented"). Negative-presence test.
14. **`App.Format.FamilyOf` accepts the same inputs `KindOf` did** (Stage 3 rename). PLang type values ("string", "image/jpeg") in, family out. A test that pins the rename is the architect's; a test that pins the *input contract is unchanged* is mine.
15. **The dispatcher rename `App.Type.Kinds` → `App.Type.KindHooks` does not change the dispatcher's `Of(clrType, value)` signature.** The renamed property still returns `kind.@this`. Pin via reflection probe (`App.Type.KindHooks.Of(...)` returns a string?) — survives the rename.
16. **A `[Description]`-free integration cut for Cut 3 (LLM trace).** The architect describes Cut 3 as "force a fresh compile, read the trace, assert vocabulary block carries `number — kinds:` and the `Primitive types:` line is gone". I'll add a sibling: assert the trace shows `type` entry described as `type(name, kind?, strict?)` — directly proves the constructor teaching from Stage 5.

These 16 additions are ~35% of the final count; the rest translates the architect's matrix. Together they tighten case-insensitivity, null/empty handling, the wire backward-compat seam, and the strict-without-kind degradation.

## Batches

Eight batches, named by topic. C# placement mirrors `PLang.Tests/App/TypeKindStrict/<Topic>Tests/`. PLang `.test.goal` lives at `Tests/TypeKindStrict/`.

### Batch 1 — `TypeValueModelTests/` (Stage 1, C#)

`TypeFactoryTests.cs` — the normalising factory + slash tolerance.
- `Factory_NameKindStrict_CarriesAllThree`
- `Factory_String_CanonicalisesNameToText`
- `Factory_SingleStringWithSlash_SplitsToNameAndKind` (`"text/markdown"` → `{name:text, kind:markdown}`)
- `Factory_SingleStringNoSlash_KindIsNull`
- `Factory_MultiSlash_SplitsOnFirst` (`"a/b/c"` → `{name:a, kind:b/c}`)
- `Factory_StrictDefaultsFalse`
- `Factory_CaseInsensitiveName` (`type("Text")` → Name="text")
- `Factory_EmptyName_Rejected` (independent #5)
- `Factory_NullSentinel_NameKindStrictPreserved` (independent #1)
- `Factory_StrictTrueOnTextFamily_NoThrowAtConstruction` (independent #4)

`TypeEntityShapeTests.cs` — public surface after the rename.
- `Entity_HasName_NotValue` (the rename)
- `Entity_HasKindAndStrict_AsTopLevelMembers`
- `Entity_ClrType_NotOnPublicSurface` (reflection probe: no public `ClrType` property)
- `Entity_FamilyKindAccessor_Removed` (the old `type.Kind` derived via Format.KindOf is gone)
- `Entity_Kinds_PopulatedForNumber` (advertised vocabulary preserved)
- `Entity_Compressible_DerivesFromName` (no longer derives from family-Kind)
- `Promote_StillThrows_WhenContextUnstamped` (the existing producer-bug guard — adding fields must not trip it)

`DataKindFoldTests.cs` — the Data.Kind fold (independent #8).
- `Data_HasNoStoredKindField` (reflection: no private `_kind` or backing storage besides Type)
- `Data_KindGetter_ReadsTypeKind`
- `Data_KindSetter_Contract` — either writes through to `Type.Kind` *or* throws `InvalidOperationException`; one or the other, pinned. (Test asserts a chosen behaviour; coder picks.)

`WireKindShapeTests.cs` — wire still two flat keys.
- `Wire_Write_EmitsFlatTypeAndKindKeys`
- `Wire_Write_OmitsKindKey_WhenNull`
- `Wire_Write_NoTypeColonKindCompositeString`
- `Wire_RoundTrip_PreservesNameKindStrict`
- `Wire_Read_LegacyPrWithSeparateTypeAndKindFields_Deserializes` (independent #9)
- `Wire_Read_NoTypeNullStringEmitted`

`IKindValidatableMarkerTests.cs` — the new marker.
- `Marker_Defined_InAppDataNamespace` (sibling to IBooleanResolvable)
- `Marker_Signature_BoolAndActualKindTuple`
- `Image_ImplementsIKindValidatable`
- `Text_DoesNotImplementIKindValidatable` (negative; Stage 2)
- `Number_DoesNotImplementIKindValidatable` (negative; independent #13)

`ClrTypeRerouteTests.cs` — the three call-sites still resolve.
- `FileRead_StillResolves_ClrTypeViaRegistry`
- `VariableSet_StillResolves_ClrTypeViaRegistry`
- `SettingsSqlite_StillResolves_ClrTypeViaRegistry`

`DispatcherRenameTests.cs` — `KindHooks` rename (independent #15).
- `AppType_HasKindHooks_NotKinds`
- `KindHooks_Of_StillReturnsStringOrNull`

### Batch 2 — `TextTypeTests/` (Stage 2, C#)

`TextBuildHookTests.cs`
- `Build_ReadmeDotMd_ReturnsMd`
- `Build_NotesNoExtension_ReturnsNull`
- `Build_VarReference_ReturnsNull`
- `Build_PageWithQueryString_ReturnsHtmlLowercase` (`"page.HTML?v=1"` → `"html"`)
- `Build_Null_ReturnsNull` (independent #6)
- `Build_RelativePathString_ReturnsExtension` (`"../report.md"` → `"md"`; independent #7)

`TextTypeShapeTests.cs`
- `Text_HasNoStaticKinds` (kind is open)
- `Text_ShapeIsString`
- `Text_Description_TeachesKindFromExtension` (the description text is the LLM teaching surface)

### Batch 3 — `PrimitiveTableTests/` (Stage 2, C#)

`PrimitiveTableTests.cs`
- `Canonical_StringMapsToText`
- `Aliases_StringStillResolves` (back-compat)
- `Aliases_TextStillResolves`
- `BuilderNames_IncludesText`
- `BuilderNames_ExcludesString`
- `BuilderNames_ExcludesIntLongDecimalDouble`
- `Canonical_IntLongDecimalDouble_MapToNumber`
- `Canonical_FloatMapsToNumber`

### Batch 4 — `KindDerivationTests/` (Stage 3, C#)

`NumberBuildHookTests.cs` (locks the existing behaviour against regression as Stage 3 wires text into the same dispatcher path)
- `Build_IntegerLiteral_ReturnsInt`
- `Build_DecimalLiteral_ReturnsDecimal`
- `Build_ScientificLiteral_ReturnsDouble`
- `Build_VarReference_ReturnsNull`

`KindCanonicalisationTests.cs`
- `Canonicalise_Markdown_ToMd`
- `Canonicalise_Jpeg_ToJpg`
- `Canonicalise_UnknownFrobnicate_PassesThrough`
- `Canonicalise_SharedSubtypePicksPrimary` (the .jpg/.jpeg case → "jpg")
- `Canonicalise_NullInput_ReturnsNull`
- `Canonicalise_AliasTableDerived_NotHandWritten` (probe: a new MIME registry entry produces a new alias automatically — coder ensures table is derived, this test pins it)

`ImageValidateKindTests.cs` (negative-path testing of strict's byte-sniff)
- `ValidateKind_GifBytesMatchingGif_OkTrue`
- `ValidateKind_PngBytesMatchingGif_OkFalse_ActualIsPng` (the canonical mismatch row)
- `ValidateKind_GarbageBytes_OkFalse`
- `ValidateKind_EmptyBytes_OkFalse`

`FamilyOfRenameTests.cs` (independent #14)
- `FamilyOf_ImageJpegMime_ReturnsImage`
- `FamilyOf_PlainStringTypeName_ReturnsNull`
- `FamilyOf_UnknownMime_ReturnsNull`
- `KindOf_DoesNotExistAfterRename` (reflection: confirms KindOf is gone or routed to FamilyOf)

`NumericInferenceTests.cs` (the build/runtime agreement; Stage 2 says inference paths agree)
- `MintTyped_FromInt_ProducesNumberIntName_NotInt`
- `MintTyped_FromDouble_ProducesNumberDoubleKind`
- `MintTyped_FromDecimal_ProducesNumberDecimalKind`
- `DataTypeGetter_StringValue_ReturnsText_NotString` (Stage 2 canonicalisation is global)
- `BuildStamp_AgreesWithRuntimeMint` (a literal `5` stamped at build matches what MintTyped produces at runtime)

### Batch 5 — `SetAndStrictTests/` (Stage 4, C#)

`VariableSetTypeParamTests.cs`
- `SetType_IsTypeEntity_NotString` (reflection probe of the partial property)
- `SetType_IsNullable` (optional `as` clause)

`StrictValidateBuildTests.cs`
- `ValidateBuild_StrictImageGifWithGifLiteral_ReturnsNull` (clean)
- `ValidateBuild_StrictImageGifWithPngLiteral_ReturnsError`
- `ValidateBuild_StrictImageGifWithVarRef_ReturnsNull_DefersToRuntime`
- `ValidateBuild_StrictTextMdWithLiteral_ReturnsNull` (no IKindValidatable on text → name-known only, no byte check)
- `ValidateBuild_NotStrict_DoesNotValidate_EvenOnMismatch`
- `ValidateBuild_StrictWithNoKind_ReturnsNull` (independent #12)

`StrictRunTests.cs`
- `Run_StrictImageGifWithRuntimeVarResolvingToPng_ThrowsTypedError`
- `Run_StrictImageGifWithRuntimeVarResolvingToGif_Mints`
- `Run_NotStrict_StampsKindFromBuildHook_NoValidation`

`SetMintCarriesKindTests.cs` (the dropped-kind regression guard, Cut 1's spine)
- `Run_BareSetWithLiteralReadmeMd_MintTypeIsTextMd`
- `Run_SetAsTextWithReadmeMd_MintTypeIsTextMd`
- `Run_SetAsImageGifWithGifBytes_MintTypeIsImageGif`

### Batch 6 — `LlmRepresentationTests/` (Stage 5, C#)

`TypeSchemasRendererTests.cs`
- `Render_AdvertisedKinds_NumberRendersWithPipeList` (`number — kinds: int | long | decimal | double`)
- `Render_ExtensionDerivedKinds_TextRendersWithExtensionTeaching` (`text — kind = extension (md|txt|...)`)
- `Render_ExtensionDerivedKinds_ImageRendersWithExtensionTeaching`
- `Render_RecordType_StillRendersFieldsAsBeforeRefactor` (back-compat; existing TypeSchemas behaviour)
- `Render_EnumType_StillRendersValuesAsBeforeRefactor` (back-compat)
- `Render_TypeEntry_AsConstructor` (`type(name, kind?, strict?)`)

`BuilderNamesTests.cs` (overlaps Batch 3 but framed at the LLM surface)
- `BuilderNames_IsCatalogGenerated_NotHandWritten` (assert it's derived from a registry call, not a literal list)
- `BuilderNames_IncludesText`
- `BuilderNames_ExcludesNumericPrimitives`

### Batch 7 — `Tests/TypeKindStrict/*.test.goal` (PLang surface)

The architect's matrix rows that say `goal`:

- `SetIntLiteralIsNumberInt.test.goal` — `- set %x% = 5` → assert `%x.Type.Name% == "number"` and `%x.Type.Kind% == "int"`
- `SetAsTextWithMdExtension.test.goal` — `- set %x% = "readme.md" as text` → `{text, md}`
- `SetAsTextSlashMarkdownNormalises.test.goal` — `- set %x% = "a" as text/markdown` → kind is `md`
- `SetAsImageGifStrictMatching.test.goal` — `- set %img% = "real.gif" as image/gif strict` builds and runs clean. (Requires a real .gif fixture in the test folder.)
- `SetAsImageGifStrictMismatch.test.goal` — `- set %img% = "photo.png" as image/gif strict` → **build error**. The PLang test asserts the build fails with a typed BuildValidation error. (Requires a real .png.)
- `SetAsImageGifStrictRuntimeVarMismatch.test.goal` — `- set %img% = %upload% as image/gif strict` builds; assert runtime throws typed error when %upload% resolves to PNG bytes.
- `SetAsTextSlashMarkdownStrictUnverifiable.test.goal` — `- set %x% = "a" as text/markdown strict` builds clean (no byte probe for text).
- `SetAsTextDefaultNoValidation.test.goal` — `- set %x% = "a" as text` (no `strict`) builds clean, kind stamped via Build (null for `"a"`).
- `SetAsTextUppercase.test.goal` — `- set %x% = "a" as TEXT` builds (case-insensitive; independent #11).
- `SetAsImageStrictNoKind.test.goal` — `- set %x% = "irrelevant" as image strict` (no kind) builds clean (no byte probe to run; independent #12).

### Batch 8 — Integration cuts

C# `IntegrationCutsTests/` directory, three files mirroring the three cuts.

- `Cut1_TypedSetRoundTripsKind.cs` — builds a goal containing `set %doc% = "readme.md" as text`, runs it through the real engine (no mock filesystem at the goal-discovery seam), asserts the minted variable carries `{name:"text", kind:"md"}`, and `%doc.Type.Name%`/`%doc.Type.Kind%` resolve to those values. The regression guard for the dropped-kind bug.
- `Cut2_StrictMismatchFailsAtBuild.cs` — three sub-tests:
  - literal PNG with `as image/gif strict` → build error
  - `%var%` with `as image/gif strict` → builds clean, runtime throws typed error
  - literal GIF with `as image/gif strict` → builds and runs clean
- `Cut3_LlmSeesOneUnifiedVocabulary.cs` — fresh compile of a goal touching `text`/`number`/`image`, read the trace under `.build/traces/<id>/`, assert: cached system prompt contains `number — kinds: int | long | decimal | double`, contains `text — kind = extension`, contains `type(name, kind?, strict?)` description (independent #16), and the per-step user message does **not** contain the literal `Primitive types:` line. (Trace-based; may need to forcibly clear the cache via `cache:false`.)

## Notes for the coder

- **One stage at a time.** Strict order — stage 1 must leave both suites green before stage 2 begins. Stage 1's tests pin the rename + fold + marker; Stages 2/3/4 unblock their own tests as the surfaces appear.
- **Test bodies are `Assert.Fail("Not implemented")` / `- throw "not implemented"`.** Names + comments are the spec. Where I've baked a particular contract choice (Data.Kind setter in DataKindFoldTests, kind canonicalisation lowercase), the comment says "pin to {choice}; revisit with test-designer if a different shape is right".
- **No tests for renames where coverage already exists.** The architect's "regression floor" rule applies — `App.Format.FamilyOf` / `App.Type.KindHooks` renames are caught by the existing build + suites. I add only the *presence* probes.
- **PLang fixture binaries.** The `.png`/`.gif` PLang tests need real files. Convention: 1×1 transparent PNG + 1×1 transparent GIF committed alongside the `.test.goal`. The coder generates these (smaller than 100 bytes each).
- **Cut 3 is trace-sensitive.** If the architect's plan changes the trace directory layout or filename scheme during implementation, the test reads from whatever the new shape is — adjust the path but keep the assertions.

## Open items (test-designer judgment, ready to flag if coder disagrees)

1. **`Data.Kind` setter contract** — I'm pinning "writes through to Type.Kind" as the contract because it's the least surprising for callers used to the current mutable field. If coder picks "throws on direct set", flip the test, but the *one* behaviour must be pinned to prevent silent no-op.
2. **`Factory_EmptyName_Rejected`** — if there's a strong reason to accept empty name (the LLM might emit it during a partial completion), demote to a warning at the build-validate layer instead of a factory throw. Flag if so.
3. **`SetAsImageStrictNoKind`** — pinned to "build clean, no validation". The alternative is "build error: strict requires a kind". I picked the soft behaviour because (a) the design says strict on unverifiable families degrades silently, (b) "strict with no kind" is the same degradation. If coder picks the hard behaviour, the goal test flips and an extra C# row pins it.

## Verdict (target)

All tests written, all bodies `Assert.Fail`/`throw "not implemented"`, suite still compiles, suite still green elsewhere. PASS = the contract is on disk and ready for the coder.
