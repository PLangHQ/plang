# tester — lazy-deserialize — v3 result

**VERDICT: PASS**

## Test runs (clean rebuild — stale-binary trap honored)
- **C#**: 4021 / 4021 pass, 0 fail, 0 skip.
- **PLang**: 273 / 273 pass, 0 fail, 0 skip. Run **twice** from clean git — identical,
  `git status` clean after (no `.pr` rewritten → not a warm-cache artifact).
- The `total: 104/131` and `34/53 branches` lines are action/branch *coverage* metrics,
  not failures. The line-1 `builder.validate: Failed to deserialize` text is goal
  **stdout** from a negative-path test (the "negative-goal rewrites"), not a test failure.

## Builder false-green check (all LazyDeserialize `.pr`)
Read every step's `text` vs `actions[0]` — all match, no index drift:
`sign→signing.sign`, `verify→signing.verify`, `add to list→list.add`,
`+→math.add`, navigation/`as`→`variable.set`, asserts→`assert.equals`.

## Goal-tests defer strict assertions to C# — all anchors verified strong
The LazyDeserialize goal tests are honest *smoke* tests; the strict assertion lives in
C#. I audited each claimed anchor (file + method + quoted assertion) — all exist and
check specific error keys / exact values, not bare success:

| Goal test | C# anchor | Strong assertion |
|---|---|---|
| TamperedSignedData | FailureMatrixTests.SigningVerify_AfterWireByteTamper_ReturnsDataHashMismatch | `Error.Key == "DataHashMismatch"` |
| HttpStatusRead (disabled) | HttpChannelTests.HttpStatusRead_DoesNotMaterialiseBody | `MaterializeCount == 0` |
| NavigationOnTypeUnknown | NavigationAccessTests.Navigation_TypeUnknownErrorMessage_ContainsLiteralAsType | `Contains("add ")` + `Contains("as ")` |
| ReadCsv_LandsAsTable | TableTypeTests (grid shape + cell nav) | headers/ColumnCount/RowCount + cell |
| BigIntegerSumOverflows | Cut5_NumberTowerRoundTrip.Cut5_PromoteThenNarrow_NoSilentWrap | `Kind == PKind.Long` + `== 5000000000L` |
| (double default) | DataTests.UnwrapJsonElement_FractionalNumber_DefaultsToDouble | `IsTypeOf<double>` + `19.99d` |
| (hash excludes name) | CanonicalizationTests | hashInput `DoesNotContain "name":"x"` |
| DoublePlusDecimal | NumberPolicyResolutionTests | `Precision == PPrecision.Error` |
| (no Data<T> wrapper) | SetTypeInferenceTests | `.Type.Name == "text"` (no generic) |

## False-green hunt: the untested half of the headline fix
`ead0caa83` changed **both** `variable.set` and `list.add` to `ShallowClone`. `list.add`
got `SignedDataSurvivesInList.test.goal` (sign → add → verify element — green, ~28ms).
The symmetric **`variable.set` List/Dict arm** got **no** nested-signed-Data regression
(`Set_ListValue_StoresDistinctListInstance` only checks outer-container distinctness with
plain-string elements).

**Probe (C#, immune runner, throwaway — deleted, nothing committed):**
`sign → set %bundle% = [signed] → pull element[0] → assert Signature != null → verify`.
**Result: PASS** — `ShallowClone` shares `_value` by reference and the param-resolution
walk preserves nested `Data` elements, so the signature survives and verifies.

→ The gap is **regression-pinning only** (Finding #1, minor), not a live false-green.

## Findings (all minor — see test-report.json)
1. `variable.set` List/Dict arm lacks a nested-signed-Data regression (behavior probe-confirmed correct).
2. Goal tamper test mutates via string-interp (no-signature path, not hash-mismatch) — deferred-to-C# is honest and strong.
3. No `baseline-tests.md` from coder (process; harmless this round since all-green).

## Note carried forward (not a tester finding)
Codeanalyzer's open F2 — `Materialize`/`Materialise` one-vowel naming + the untracked
collections-are-data deferral — remains open. Code-shape concern, owned by analyzer/architect.
