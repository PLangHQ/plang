# Auditor — runtime2-test-module

## v1 (2026-04-21)

Cross-cutting audit of the test module after codeanalyzer v3 / tester v6 / security v1 + fix commit 9dc148f5. Verdict: **fail**. Two major findings, both around the security fix: (F2) `BuildJUnit` path emits `AssertionError.Message` unmasked despite security #3 naming `junit.xml` as an affected artefact; (F1) the two Report format-routing tests assert on an input-echo scalar, so a JUnit regression has no detection. F3 (no e2e test for mask) and F4 (non-string strip fallback) are minor/nit. Root-cause fix in `AssertionError.FormatValue` (route through `Json.DiagnosticOutput` instead of `value.ToString()`) addresses F2 for console+JSON+JUnit in one edit. See [v1/summary.md](v1/summary.md).

## v2 (2026-04-21)

Re-audit after coder commit `152d2a8e` addressing all 4 v1 findings. Verdict: **pass**. F2 fixed as suggested — `AssertionError.FormatValue → Json.FormatForDiagnostic → DiagnosticOutput` — and consolidated with DefaultAssertProvider + report.cs copies for OBP "behavior belongs to the owner". F1 tests now assert on `%report.content%` with format-specific markers. F3 discharged by new `TestReportMasksSensitiveVariables{,Junit}.test.goal` + fixture + committed `junit_sensitive_masked.xml` snapshot. F4 discharged by synthesized `JsonPropertyInfo` replacement. 20/20 PLang tests + 9/9 filter unit tests pass. Hand off to **docs**. See [v2/summary.md](v2/summary.md).
