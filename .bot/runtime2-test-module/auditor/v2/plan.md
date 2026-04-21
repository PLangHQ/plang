# Auditor v2 — Plan

## Context

v1 failed with 4 findings (2 major, 1 minor, 1 nit) clustered around security fix
9dc148f5 being scoped to the JSON path only, leaving the JUnit path unmasked.
Coder pushed commit `152d2a8e` addressing all four. This v2 verifies the fix.

## What I already checked (gathering)

1. Read v1 findings from `.bot/runtime2-test-module/auditor-report.json`.
2. Read full diff `b7819961..HEAD` (17 files, +862 -93).
3. Read coder's changes in context:
   - `PLang/App/Utils/Json.cs` — new `FormatForDiagnostic` (F2 common path).
   - `PLang/App/Errors/AssertionError.cs` — `FormatValue` → `FormatForDiagnostic`.
   - `PLang/App/modules/assert/providers/DefaultAssertProvider.cs` — same.
   - `PLang/App/modules/test/report.cs` — same.
   - `PLang/App/Channels/Serializers/SensitivePropertyFilter.cs` — F4 fix.
   - Two new .test.goal files + fixture + JUnit snapshot (F3).
   - Two .test.goal edits routing asserts to `%report.content%` (F1).
4. Ran `dotnet run --project PLang.Tests -- --treenode-filter "/*/*/SensitivePropertyFilterTests/*"` — **9/9 pass**.
5. Ran `plang --test` in `Tests/TestModule` — **20/20 outer pass** including both
   new masking tests and both F1-fixed tests.
6. Inspected committed snapshot `junit_sensitive_masked.xml` — `privateKey = "******"`,
   `publicKey = "MySHZU5Y…"` visible. JUnit path leak closed.

## Discharge assessment

| v1 # | severity | status | evidence |
|---|---|---|---|
| 1 | major | DISCHARGED | `TestReportWritesJunitXml.test.goal:10` asserts `%report.content% contains '<testsuites'`; `TestReportIncludesCoverageTables.test.goal:10` asserts `'branchCoverage'`. Both pass. Deleting `case "junit":` in BuildFormat would now break the JUnit test; dropping branchCoverage block in BuildJson would break the other. |
| 2 | major | DISCHARGED | `AssertionError.FormatValue` now routes through `Json.FormatForDiagnostic` → `DiagnosticOutput`. Snapshot confirms `<failure>` node contains `"privateKey": "******"` and the raw key is absent. End-to-end .test.goal (Junit variant) passes green. |
| 3 | minor | DISCHARGED | `TestReportMasksSensitiveVariables.test.goal` + `...Junit.test.goal` both run `sensitivefail.fixture.goal` with `%MyIdentity%` as Actual and assert `%report.content% contains '******'`. Dropping the DiagnosticOutput modifier from report.cs:281 would now break these tests, not just the unit test. |
| 4 | nit | DISCHARGED | `SensitivePropertyFilter.Mask` now synthesises a string-typed `JsonPropertyInfo` instead of `RemoveAt` for non-string properties. `Sensitive_NonStringProperty_RendersMaskedValueNotStripped` proves `byte[]` → `"******"` with key visible. |

## Cross-cutting observations (no findings)

- **Good OBP move:** consolidating three copies of `FormatValue` into
  `App.Utils.Json.FormatForDiagnostic` is correct — "behavior belongs to the
  owner" (Json owns diagnostic formatting). `AssertionError`, `DefaultAssertProvider`
  and `report.cs` now all delegate to one implementation; a future fix lands
  once, not thrice.

- **Subtle behavior change (not a finding):** `DefaultAssertProvider` previously
  used `value.ToString()` for non-string actual values in failure messages. Now
  it JSON-serializes via `DiagnosticOutput`. Output format is stricter (e.g. a
  `List<int>` used to render as `System.Collections.Generic.List\`1[System.Int32]`,
  now renders as `[1,2,3]`). The whole test suite still passes, so no existing
  assertion snapshots depend on the old shape. Worth noting but not a defect.

- **`CreateJsonPropertyInfo` replacement is correct per System.Text.Json docs.**
  The synthesised property with `Get = _ => "******"` and `PropertyType = string`
  serialises as a string field with the literal mask — confirmed by the new
  unit test which also asserts base64 of the byte array (`3q2+7w==`) is *not*
  present. The mask stays visible even when the source type is `byte[]`.

- **Build non-determinism risk cleared.** I read all three edited .pr files
  (TestReportIncludesCoverageTables, TestReportWritesJunitXml, the two new
  masks). Module/action mapping is correct everywhere. The builder did drop a
  `defaults: asdefault=false` and a `Message: null` param from two unchanged
  steps — those are no-op cleanups from rebuild, not semantic drift.

## What I did NOT re-check

- Step propagation + AfterAction widening (v1 verdict: clean; no code in those
  areas changed in `152d2a8e`). Skipped.
- Other security v1 low-severity findings that were already discharged at v1
  time (only finding #3 was reopened on the JUnit axis).

## Verdict

**pass.** All four v1 findings are discharged by both code fix AND test
coverage. The "code fix verified ≠ finding resolved" rule is satisfied for
every finding — each has either a new .cs unit test or a new .test.goal (or
both) that would catch a regression.

Next bot: **docs**.

## Output artifacts to produce

1. `.bot/runtime2-test-module/auditor/v2/v1_review_summary.md` — summary of v1
   findings and how each was addressed.
2. `.bot/runtime2-test-module/auditor/v2/result.md` — detailed discharge notes.
3. `.bot/runtime2-test-module/auditor/v2/verdict.json` — pass.
4. `.bot/runtime2-test-module/auditor/v2/summary.md` — this version's summary.
5. `.bot/runtime2-test-module/auditor-report.json` — v2 report.
6. `.bot/runtime2-test-module/auditor/summary.md` — updated cross-session index.
7. `.bot/runtime2-test-module/report.json` — session entry appended.
8. `.bot/runtime2-test-module/auditor/v2/changes.patch` — `git diff runtime2..HEAD -- ':(exclude).bot'`.
9. Commit and push everything.
