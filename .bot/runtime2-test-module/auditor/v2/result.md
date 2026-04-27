# Auditor v2 — Discharge Verification

**Verdict: pass.** All 4 v1 findings discharged by both code fix AND test coverage.

## Verification method

For each finding I applied the "code fix verified ≠ finding resolved" rule:
a fix without a test that would catch the regression is not discharged.

## F1 — major, review-gap — DISCHARGED

Fix location: `Tests/TestModule/Report/TestReportWritesJunitXml.test.goal:10`,
`Tests/TestModule/Report/TestReportIncludesCoverageTables.test.goal:10`.

.pr inspection confirms the LLM mapped these to the correct
`assert.contains(Value, Container)` action with literal values `<testsuites`
and `branchCoverage`. Regression test: if someone deleted `case "junit":` in
`BuildFormat` (or dropped the branchCoverage block from `BuildJson`), these
asserts would fail — they are now tied to the actual formatter output, not the
input parameter echo.

Runtime proof: `plang --test` shows both goals pass green.

## F2 — major, contract — DISCHARGED

Fix locations:
- `PLang/App/Errors/AssertionError.cs:42` — `FormatValue → Json.FormatForDiagnostic`.
- `PLang/App/Utils/Json.cs:63-74` — new `FormatForDiagnostic` routes non-primitives
  through `DiagnosticOutput` (`catch { return type.Name; }` fallback never
  re-enters `value.ToString()`).
- `PLang/App/modules/assert/providers/DefaultAssertProvider.cs:174` and
  `PLang/App/modules/test/report.cs:329` — same delegation.

Runtime proof: `Tests/TestModule/Report/snapshots/junit_sensitive_masked.xml`
shows the `<failure>` node:

```xml
<failure>Expected: &quot;will-not-match&quot;, Actual: {
  &quot;name&quot;: &quot;default&quot;,
  &quot;publicKey&quot;: &quot;MySHZU5Y6c37jHoJZxdvvwja2BvODRScybSMvmenZg0=&quot;,
  &quot;privateKey&quot;: &quot;******&quot;,
  ...
}</failure>
```

The previously-leaked `PrivateKey` is masked, and the JSON-shaped serialization
inside the XML CDATA proves `FormatValue` is going through `DiagnosticOutput`
and not `value.ToString()`.

Regression test: `AssertionError_Message_MasksSensitiveViaDiagnosticOutput` in
`SensitivePropertyFilterTests.cs:169-183` constructs an `AssertionError` with
a `LeakySecretRecord(Name, [Sensitive] Secret)` and asserts `Message`
contains `"******"` and does not contain the raw secret. Reverting
`AssertionError.cs:42` to `value.ToString()` breaks this test.

## F3 — minor, review-gap — DISCHARGED

Fix location: `Tests/TestModule/Report/TestReportMasksSensitiveVariables.test.goal`
+ `...Junit.test.goal` + `_fixtures_sensitive/sensitivefail.fixture.goal`.

Fixture drives `%MyIdentity%` into `AssertionError.Actual` + the captured
`Variables` snapshot. Outer tests then assert on `%report.content%` from both
the JSON and JUnit formatters. Regression: dropping `DiagnosticOutput` from
`report.cs:281` (swapping to `CamelCaseIndented`) would break these .test.goal
tests — not just the C# unit test on the modifier in isolation.

Runtime proof: both goals pass green.

## F4 — nit, contract — DISCHARGED

Fix location: `PLang/App/Channels/Serializers/SensitivePropertyFilter.cs:45-65`.
Instead of `RemoveAt`, now creates a same-named string-typed replacement
`JsonPropertyInfo` and swaps it in at the same index.

Regression test: `Sensitive_NonStringProperty_RendersMaskedValueNotStripped`
serialises `NonStringSecretCarrier { Key = byte[] }` via `DiagnosticOutput`
and asserts `"key"` key is present, `"******"` value is present, base64 of
raw bytes (`3q2+7w==` and url-safe `3q2-7w`) is absent. Reverting the
`typeInfo.Properties.RemoveAt(i)` behaviour would break the test.

## Cross-cutting observations (not findings)

### Good — OBP consolidation
Coder consolidated three copies of `FormatValue` (AssertionError,
DefaultAssertProvider, report.cs) behind `App.Utils.Json.FormatForDiagnostic`.
This is correct by "behavior belongs to the owner" — `Json` owns diagnostic
formatting. A future fix to diagnostic formatting now lands once.

### Subtle behavior change — not a defect
`DefaultAssertProvider.FormatValue` previously returned `value.ToString()` for
non-string values. Now it JSON-serialises. A `List<int>` previously rendered as
`System.Collections.Generic.List\`1[System.Int32]`; now as `[1,2,3]`. The
full test suite (20/20 PLang + 9/9 filter unit tests) passes, so no existing
snapshot depends on the old shape. Net improvement — readable output and
Sensitive masking by construction.

### Minor — committed JUnit snapshot is rewritten on every test run (nit, not filed)

`TestReportMasksSensitiveVariablesJunit.test.goal:15` does
`file.save path='snapshots/junit_sensitive_masked.xml'`, so running this test
always rewrites the tracked file. Contents change each run: `%MyIdentity%`
lazily creates a default identity (new PublicKey per environment), `duration`
and `created` timestamp shift. Any CI run or local test run produces a dirty
working tree against the committed snapshot. Not a security regression — the
masking itself is stable and correct — but it's test hygiene. Options:
(a) gitignore `snapshots/` and treat the committed copy as a one-time visual
reference, (b) write to `.test-output/` instead, (c) use a deterministic
identity fixture. Noting for docs / future cleanup. Not blocking pass.

### JSON envelope Sensitive coverage is now monotonically correct
`BuildJson` envelope includes `variables = (run.Error as AssertionError)?.Variables`
(`report.cs:260`) and is serialised via `DiagnosticOutput` at line 281. So raw
object graphs in `Variables` (which may contain Sensitive properties) are
masked at serialisation time. The `error = run.Error?.Message` scalar (line 257)
is a pre-formatted string that also went through `FormatForDiagnostic` at
`AssertionError` construction time. Both paths masked. Plus `BuildJUnit` at
line 307 uses the same pre-formatted `Message`, so the mask propagates there
too — which is exactly what F2 was about.

## Prior-reviewer assessment

- **codeanalyzer:** No re-assessment needed — this commit didn't touch any
  files in their prior pass.
- **tester:** v6 approved at ea7aeb85 and missed F1 (input-echo assertion) +
  F3 (no end-to-end mask test). This was a weak-test pattern: name-claim
  mismatch between test name ("WritesJunitXml") and what the asserts actually
  verify (the format parameter). Tester rated F11 minor; I re-rated major in
  v1 because it compounded F2. Coder's fix retroactively strengthens the
  tests in a way that should have been present at tester v6 sign-off.
- **security:** Originally opened finding #3 naming both results.json and
  junit.xml; the fix commit 9dc148f5 closed only the JSON path. Security
  partial-credit was correct at v1 time; now fully closed by F2.

## What I did NOT re-check

- Step propagation + AfterAction widening (v1 clean, no files in that area
  touched by 152d2a8e).
- Other security low-severity findings already closed at v1 time.

## Handoff

Route to **docs** bot.
