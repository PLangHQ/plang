# Standing test-failure clusters — compare-redesign

**Living doc.** Built 2026-06-15 from a full six-suite run on `compare-redesign`
(after v10 "clr dissolved from the wire"). Purpose: survive context windows so we
fix by root-cause cluster, not test-by-test. Update counts/status as clusters close.

## Totals (this run)
310 failing C# tests across the six PLang.Tests suites:
Types 13 · Wire 29 · Runtime 57 · Data 99 · Modules 106 · Generator 7.

These predate the v10 arc (the v10 work was clr-on-the-wire and moved counts *down*;
this is the leftover standing red set Ingi wants cleared).

## ⚠ Parsing gotcha (cost me a bisect)
Building the failing-set with `grep '^failed '` MISSES failures whose line is
prefixed by interleaved stdout noise (e.g. `Not found: .build/handleplang.prfailed Get_...`).
`Get_SignedPlangResponse_SetsServiceIdentity` looked like a guid-regression but was
pre-existing (the `^failed` anchor dropped it from the baseline set). Use
`grep -a 'failed <Name>'` (no `^`) when confirming whether a specific test is a true
regression vs already-red.

## clr-removal progress (see clr-removal-epic.md)
- **BCL-leaf bucket DONE (2026-06-15):** `datetime` owns plain `System.DateTime`;
  new `guid : item` leaf type. ~340 clr-wraps (Guid 304 + DateTime 32) now lift to
  real items. Validated; zero regressions. Did not flip a currently-red test green
  on its own — it's epic progress (fewer clr carriers), not a standalone test win.

## Reproduce
```bash
./dev.sh build
for p in Types Wire Runtime Data Modules Generator; do
  TEST_TIMEOUT=70s PLang.Tests/$p/bin/Debug/net10.0/PLang.Tests.$p --timeout 70s > /tmp/suite_$p.log 2>&1 || true
done
# Data & Runtime SEGFAULT at teardown AFTER printing — read counts/names from the log,
# not the exit code. grep '^failed ' /tmp/suite_<P>.log for names.
```

## The cross-cutting suspicion (verify before fixing anything)
Several clusters share one shape: **a value that should render as its native/wire
form instead serializes as an `item`/`Data` property-bag** (`{"Cacheable":true,
"Prior":null,"Template":null,"IsLeaf":false}`) OR stays wrapped as `clr`/`this`
instead of unwrapping to a CLR type. Symptoms: "Value is of type `this`, not
`List`1`", "of type `clr`, not `this`", LLM request bodies full of item internals,
`Value()` returning empty/null.

**CONFIRMED (2026-06-15, traced J + C):** one *theme*, **not** one fix site.
Native item types (`text.this`, `dict.this`, `list.this`, `clr`) now leak into
code that was written to consume plain CLR values/strings. Two confirmed sites:
- **C** — `this.Reconstruct.cs` `Walk()` List<>/Dictionary branches check
  `value is IEnumerable` / property-bag, but `list.this` is **not** `IEnumerable`
  (it's `IListLeaf` with `.Items`). So a native list reconstructs to 0 elements.
- **J** — `llm/code/OpenAi.cs:105` (and :984) do raw
  `JsonSerializer.Serialize(v, v.GetType())` on the value. When `v` is a
  `dict.this`/`text.this`, STJ reflects the item's C# props → the
  `{"Cacheable":...,"IsLeaf":false}` garbage in the request body.

Fixes are **per-consumer** (teach `Walk` about `IListLeaf`/`dict.this`; route the
LLM boundary through the wire serializer or an unwrap-to-plain-CLR helper) — OR a
single shared "item → plain CLR" helper that every such site calls. Clusters
**J, C, O, B, D, E, K, M, Q** are all this theme but land in different files;
expect ~6–9 fix sites, not 2–3.

---

## Clusters (highest leverage first)

### J — LLM `Query_*` (45, Modules) ★ highest leverage
- **Symptom:** every `Query_*` fails. Request body shows item internals
  (`{"Cacheable":true,"Prior":null,"Template":null,"IsLeaf":false}`) where a JSON
  schema string / message content is expected; `result.Value()` returns `null`.
- **Hypothesis:** the LLM module builds its request JSON with a serializer that now
  walks `item`/`Data` properties instead of the value. One mock/serialization fix
  likely clears most of the 45.
- **Confidence:** med-high it's a single root cause. **Start here.**

### B — compress / sign / binary / archive (35, Wire+Data+Modules)
- **Symptoms:** "Value is of type `clr`, not `this`" (Compress); "Cannot convert
  `this` to binary" / "cast `item.clr` to binary" / "Archived Data has no byte[]
  value" (Decompress); **CultureInfo Normalize cycle** in the 8 signing tests
  (`Cut2/Cut3`, `OuterSignature`, `StoreView`, `TamperingValueByte`) —
  `Signing failed during lazy Signature populate: NormalizeException: Cycle
  detected during Normalize at type CultureInfo`.
- **Hypotheses (two sub-roots):**
  - **B1 binary/archive:** Compress wraps inner Data as `clr` carrier; the archived
    value no longer materializes to `byte[]`. Tied to clr-still-exists-as-rung-2.
  - **B2 CultureInfo cycle:** `NormalizeObject` (this.Normalize.cs:230) reflects into
    a `CultureInfo` somewhere in the signed graph and self-cycles. Fix = treat
    CultureInfo as a leaf / exclude it, OR find the producer leaking it. **Clean,
    isolated, 8 tests.** Good warm-up cluster.
- **Confidence:** B2 high & self-contained; B1 needs a trace.

### K — settings / variables `Get`/`Set`/`Remove`/`Rename` (33, Modules)
- **Symptom:** `Get_NonexistentVariable_ReturnsNull` → "Expected null but found ''"
  (empty string instead of null); settings missing-key paths.
- **Hypothesis:** a "no value" now returns an empty value object instead of null —
  same family as the render/Value regression. Probably 1–2 roots.
- **Confidence:** med.

### I — permissions / grants / authorize (23, Runtime)
- **Symptom:** `Authorize_*`, `Scenario2/3/4`, grant round-trips → "Expected to not
  be null but value is null". `OutOfRoot_StreamChannel_*` (10 param cases).
- **Hypothesis:** grant signing/verify depends on the same signing/Normalize path as
  B2 (CultureInfo) — may share root with B. Verify after B2.
- **Confidence:** med; possible overlap with B.

### H — tester framework `Discover_*`/`Run_*`/`RunAsync_*` (17, Runtime)
- **Symptom:** "Expected true but found False" — discovery/skip/tag/stale logic.
- **Hypothesis:** self-contained to the `tester` concept; likely unrelated to the
  render regression. Own investigation.
- **Confidence:** low on root; isolated in scope.

### C — `Data.Reconstruct<T>` / `As<T>` (DONE 2026-06-15) ✅
- **Was:** `As_ListInt` "Expected 3 found 0", `As_RecordWithPositionalCtor`,
  `As_T_OnTypeWithNoParameterlessCtor...`, `As_T_UsesPropertyLookupCache`.
- **Root (confirmed):** `Reconstruct`/`As`/`Walk` was a **second, dead** CLR-lowering
  authority — **0 production call sites** (only ~26 test calls). Production lowers
  via the value's own door `item.Clr<T>()` (92 prod sites). The dead path re-walked
  the value with CLR type-switches and didn't recognise `list.this` (not `IEnumerable`).
- **Fix:** DELETED `PLang/app/data/this.Reconstruct.cs` + `AsTreeWalkerTests.cs` +
  `AsReconstructionHookTests.cs` + the 2 `Cut1/Cut2` Identity-reconstruct tests
  (every operation already covered through the real door: `Clr<T>`, `GetValue<T>`,
  `Type.Convert`, `TryConvertTo_DictToClass`, `SensitivePropertyFilterTests`).
  Result: 4 fewer Data failures, **0 new failures**, other suites unchanged.
- **NOTE — reclassification:** the `AsT_*` tests (`AsT_NonGenericArrayList`,
  `AsT_AlreadyTypedData`, `AsT_DifferentContext`, `AsT_DoesNotMutateOriginalRaw`,
  `AsT_ActionListElements`) are **NOT** this cluster — they exercise the *production*
  door `Value<T>()`/`ShallowClone<T>` and still fail (`%var%`-substitution /
  native-collection passthrough). They are a **real standing cluster** → see new **C2**.

### C2 — stale `Value<T>()` resolution-contract tests (DONE 2026-06-15) ✅
- **Was:** 6 `AsT_*`/`As*` tests written for the OLD `As<T>(context)` deep-walk API,
  mechanically wrapped as `ShallowClone<T>(await Value<T>())`. Each asserted a premise
  that contradicts the current value-door contract — and each had a **passing sibling**
  encoding the real contract:
  - `AsT_AlreadyTypedData_ReturnsSelf` (ShallowClone always forms a view) → sib `AsT_ValueAlreadyT_FastPathWrap`
  - `AsT_DoesNotMutateOriginalRaw` (`Value()` renders a template **live**; no-mutation is on `Peek()`) → sib `AsT_DoesNotMutateOriginalDataValue`
  - `AsT_DifferentContext_PicksUpFreshVariableValues` (context rides the Data, never a param) → sib `AsT_CalledTwice_FreshResolutionEachCall`
  - `AsT_ActionListElements_NotRecursedInto` (first walk substitutes; only stored 2nd-read is verbatim) → sib `AsT_TypedContainerSlot_StoredLeavesNotReResolved` (LlmFixer-280k guard)
  - `AsT_NonGenericArrayList/Hashtable_PassesThrough` (own comment: "production never feeds raw ArrayLists")
- **Fix:** DELETED all 6 (no production change — current contract is correct & covered).
  Data 95→89, 0 new failures.
- **NOTE:** `Set_DotPath_ConvertsListOfObject_ToTypedList` is NOT C2 — it's the
  Set_DotPath family (cluster K/N), still standing.

### L — error `Handle_*` / retry (13, Modules)
- **Symptom:** "Expected true, because Data failed: [UserError] anything … found
  False" — error-handler filter/retry not matching/swallowing.
- **Confidence:** low; own trace.

### P — http `Upload_*`/`Post_*`/`Get_*`/`ReadUrl` (11, Modules+Runtime)
- **Symptom:** request body / response parsing. Likely same value-serialization root
  as J (http builds JSON bodies too).
- **Confidence:** med; check after J.

### O — `Normalize_*` (10, Data)
- **Symptom:** `Normalize_HomogeneousPrimitiveList_StaysListOfPrimitives` →
  "of type `this`, not `List`1`". Normalize output shape changed.
- **Hypothesis:** same family as C/the render work. Possibly just stale test
  expectations vs the new "value renders itself" shape — decide per test whether
  test or production is wrong.
- **Confidence:** med.

### G — snapshot / callback resume (10, Wire+Runtime)
- **Symptom:** `CallbackGoalNotFound: Callback frame's goal not found in live
  registry: ''` at `this.Snapshot.cs:151` (Restore). Goal name lost on resume.
- **Hypothesis:** snapshot serialization drops/empties the callback frame's goal id —
  a FromWire-completeness gap (cf. [[born_native_fromwire_completeness]]).
- **Confidence:** med; isolated to snapshot round-trip.

### M — `Compare_*` / `Diff_*` (8, Data)
- **Symptom:** `Compare_DifferentStrings_MatchFalse` → "Expected False found True" —
  comparison returns match where it shouldn't; `IncomparableException: cannot order
  'number' against 'item'`.
- **Hypothesis:** compare reads the item wrapper, not the value → everything looks
  equal. Same render family.
- **Confidence:** med.

### F — build "bare Ok" carries a Type (7, Types)
- **Symptom:** `*_Build_*ReturnsBareOk`, `IClass_BuildDefaultImpl` → "Expected null"
  (Type should be null on a no-value Ok but isn't).
- **Hypothesis:** the `Task<Data>` bare-return path now stamps a Type. Small, isolated.
- **Confidence:** med-high; quick.

### E — image materialize (4, Types+Data)
- **Symptom:** image value doesn't materialize to `byte[]` (`Body_ImagePng`,
  `Load_WalksNestedImage`, `ReadLiftImagePng`). Tied to B1/the render work.

### D — text render (3, Types)
- **Symptom:** `Text_CaseAndTrim` "Expected ABC found ''" — a `text` value's
  string ops return empty. Same render/Value family as J/C.

### Q — render module `Render_*` (4, Modules)
- **Symptom:** `Render_DataObject_ExposesValueNotWrapper` et al — by name, exactly the
  "value vs wrapper" regression. Strong tell for the cross-cutting root.

---

## Uncategorized (71) — mostly Data/Runtime/Wire singletons
Likely fold into the clusters above once roots are confirmed. Notable ones:
`Value_*` (ConcurrentReads/RawDictUnchanged/ReadRepeatedly), `Name_*` var-name
propagation, `GetChild_*` reflection nav, `MalformedJson_*`, `ToString_NullValue`,
`Ok_*`, `Scalar_BytesValue_*`, `WireRead_StampsTypeKindFromTypeSlot`,
`Deserialize_SimpleString`, `Roundtrip_String`, `Serialize_Null`,
`Peek_OnUnmaterialisedReference`, `BangPlane`/`BangTypeList`, `NullsLast_InSortOrdering`,
`PathParam_RecordValue_ReturnsError`. Full list: `grep '^ZZ' /tmp/classified.tsv`.

## Suggested attack order
1. **Confirm the cross-cutting root** — trace `Query_SimpleMessage` (J) and
   `As_ListInt` (C) to the one place value vs item-wrapper diverges.
2. **B2 CultureInfo cycle** (8, clean) — warm-up, may also unblock I.
3. **J LLM** (45) — biggest single payoff if it's one serializer.
4. Then C, O, K, M, D, E, Q as the render-family root lands.
5. Isolated own-investigations: H (tester), L (error-handle), G (snapshot/callback).

**Discipline:** for each cluster decide per-test whether *test* or *production* is
wrong — several O/F tests may be stale expectations against the intended new shape.
Show Ingi the root + fix direction before editing production.
