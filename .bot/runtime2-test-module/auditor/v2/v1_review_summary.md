# Review Summary — Auditor v1 Feedback Addressed by Coder

v1 failed with 4 findings, all clustered around security fix `9dc148f5` being
scoped to the JSON path only. Coder's commit `152d2a8e` addresses all four.

## v1 findings and coder response

### F1 — major, review-gap (Report tests assert input echo, not file content)
- **v1 issue:** `TestReportWritesJunitXml` and `TestReportIncludesCoverageTables`
  asserted `%report.format%` (echoes input param) — deleting `case "junit":`
  leaves both tests green. The only format-routing coverage was effectively
  untested.
- **Coder fix:** Replaced with content assertions:
  - `TestReportWritesJunitXml.test.goal:10` → `assert %report.content% contains '<testsuites'`
  - `TestReportIncludesCoverageTables.test.goal:10` → `assert %report.content% contains 'branchCoverage'`
- **.pr files rebuilt** with correct `assert.contains(Value, Container)` mapping.

### F2 — major, contract (JUnit path bypasses Sensitive masking)
- **v1 issue:** `BuildJSON` (report.cs:281) used `DiagnosticOutput`, but
  `BuildJUnit` (report.cs:307) emits `run.Error?.Message` — built by
  `AssertionError.FormatValue` which fell back to `value.ToString()`, printing
  every field of a record (including `[Sensitive]` ones) into `Error.Message`.
- **Coder fix:** `AssertionError.FormatValue` now delegates to new
  `App.Utils.Json.FormatForDiagnostic`, which JSON-serialises non-primitives
  through `DiagnosticOutput` (which applies the mask modifier). Consolidated
  three copies of `FormatValue` (AssertionError, DefaultAssertProvider,
  report.cs) behind the one owner method — OBP "behavior belongs to the owner".
- **Matches the exact suggestion in v1 finding #2.**

### F3 — minor, review-gap (end-to-end mask test missing)
- **v1 issue:** Existing unit test only proves the JSON modifier works in
  isolation. A regression that drops `DiagnosticOutput` from report.cs:281
  wouldn't be caught.
- **Coder fix:** Two new .test.goal files + fixture:
  - `Tests/TestModule/Report/_fixtures_sensitive/sensitivefail.fixture.goal` —
    asserts `%MyIdentity%` (carrying Sensitive PrivateKey) equals a wrong value.
  - `TestReportMasksSensitiveVariables.test.goal` — JSON path: asserts
    `%report.content% contains 'privateKey'` AND `contains '******'` AND
    `contains 'publicKey'`.
  - `TestReportMasksSensitiveVariablesJunit.test.goal` — JUnit path: same
    assertions on XML content.
- Plus committed snapshot `Tests/TestModule/Report/snapshots/junit_sensitive_masked.xml`
  as a visual reference.

### F4 — nit, contract (non-string Sensitive stripped)
- **v1 issue:** `SensitivePropertyFilter.Mask` silently dropped non-string
  `[Sensitive]` properties (`RemoveAt`), violating `DiagnosticOutput`'s own
  contract: "distinguishing absent / null / redacted matters — the key must
  still appear."
- **Coder fix:** `CreateJsonPropertyInfo(typeof(string), prop.Name)` + `Get = _ => "******"` replaces the property in place.
  Key stays visible, value renders as literal mask regardless of source type.
- Unit test `Sensitive_NonStringProperty_RendersMaskedValueNotStripped` proves
  `byte[]` → `"******"`, key name visible, base64 of raw bytes absent.
