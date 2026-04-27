# Auditor v1 — runtime2-test-module

Reviewed after codeanalyzer v3 (clean), tester v6 (approved), security v1 (pass, 4 low), and security fix commit 9dc148f5.

## Previous reviewers — assessment

- **codeanalyzer v1→v3**: agree. Root-cause fix at SplitAtConditions is correct; Step-propagation indexer is the single choke point. Spot-checked other `_items[i]` call sites (`FirstConditionIndex`, `IsFirstCondition`, `ComputeBranchChain`) — none need Step propagation, all read identity/action-name only.
- **tester v6**: partial. F11 was rated minor but it masks a real security gap (see finding #1). Rest discharged correctly.
- **security v1**: partial. Fix shipped (9dc148f5) for finding #3 addresses `results.json` but not the JUnit path that the original finding explicitly named.

## Findings

### 1 — MAJOR — `TestReportWritesJunitXml` + `TestReportIncludesCoverageTables` don't discriminate format routing (+ mask the security gap in finding #2)
- **Files**: `Tests/TestModule/Report/TestReportWritesJunitXml.test.goal:9-12`, `Tests/TestModule/Report/TestReportIncludesCoverageTables.test.goal:8-11`
- **Missed by**: tester (rated minor), me rating up.
- **Issue**: Both tests assert `%report.format%` which at `report.cs:77` is set *before* the switch at line 48, so it echoes the input parameter regardless of which branch ran. Delete `case "junit":` entirely — WritesJunitXml still passes (format='junit' is preserved, default branch writes `results.json`, `file.exists` succeeds). Delete the `branchCoverage` block in `BuildJson` — IncludesCoverageTables still passes. The tests carry name-claims their assertions don't verify.
- **Blast radius**: These are the *only* PLang tests for format routing. Combined with finding #2, a regression on the JUnit path has no detection.
- **Fix**: one assert per test against `%report.content%` or `%report.reportPath%`:
  - WritesJunitXml: `assert %report.content% contains '<testsuites'`
  - IncludesCoverageTables: `assert %report.content% contains 'branchCoverage'`

### 2 — MAJOR — Sensitive-property masking fix (9dc148f5) only covers `results.json`; JUnit XML path is untouched
- **File**: `PLang/App/modules/test/report.cs:284-324` (`BuildJUnit`)
- **Missed by**: coder (scope of fix), tester (not re-tested after security fix), security (the original finding #3 explicitly named both `results.json` and `junit.xml`).
- **Issue**: `BuildJUnit` emits `run.Error?.Message` at line 307 via plain `StringBuilder` — no `JsonSerializerOptions`, no mask modifier. `AssertionError.Message` is formatted at `AssertionError.cs:36` using `value.ToString()` for non-strings. If a test fails an assert whose Expected/Actual is an object with a `[Sensitive]` property (the exact case the fix targets — `Identity.PrivateKey`), the ToString output can include the sensitive payload, and the JUnit XML writes it verbatim. The `BuildJson` path masks via `DiagnosticOutput`; the JUnit path doesn't. Same threat model (CI artefact upload), same vector, only one side fixed.
- **Fix**: either (a) always route error messages through a sanitizer that formats Expected/Actual via `DiagnosticOutput`, not `value.ToString()`; or (b) add a pass over `AssertionError.Message` before JUnit emission. The cleaner change is to fix `AssertionError.FormatValue` itself to use `DiagnosticOutput` — both console, JSON, and JUnit paths then benefit.

### 3 — MINOR — No end-to-end test proves Sensitive masking reaches `results.json`
- **Files**: `PLang.Tests/App/Serializers/SensitivePropertyFilterTests.cs`, absent in `Tests/TestModule/Report/`
- **Missed by**: coder (didn't add one), tester (security fix shipped after tester v6 approval).
- **Issue**: Unit test `Sensitive_DiagnosticOutput_MasksValueAsAsterisks` proves the modifier works in isolation. But no test asserts that a real failing `.test.goal` with an Identity-like variable produces a `results.json` containing `******`. If someone reverts `report.cs:281` from `DiagnosticOutput` to `CamelCaseIndented`, all tests still pass.
- **Fix**: add a `Tests/TestModule/Report/TestReportMasksSensitiveVariables.test.goal` fixture + test that fails on an Identity, then asserts `%report.content% contains '******'` and `%report.content% doesNotContain '<the-actual-private-key>'`.

### 4 — NIT — `SensitivePropertyFilter.Mask` silently strips non-string `[Sensitive]` properties
- **File**: `PLang/App/Channels/Serializers/SensitivePropertyFilter.cs:53-58`
- **Issue**: The docstring for `DiagnosticOutput` says "distinguishing absent / null / redacted matters when a human is reading a crash dump — the key must still appear." But for a `[Sensitive] byte[] Key` property, `Mask` falls through to `RemoveAt(i)` — property disappears. Violates the stated intent for non-string sensitive props.
- **Impact**: low. No current `[Sensitive]` non-string properties in the codebase today (I checked — Identity.PrivateKey is string). Latent risk when one is added.
- **Fix**: render a placeholder ValueProvider (e.g., `new { redacted = "<sensitive>" }`) instead of removing.

## Seams checked that were clean

- **AfterAction payload widening** — All emit sites (`Action/this.cs:117`, `Modifiers/this.cs:61`, `Debug/this.cs:181-194`) pass the new `(context, action?, result?)` signature. All subscribers (`on.cs:51`, `run.cs:85`, `Debug/this.cs:183/190`, mock/action.cs) conform. `EventBinding.Handler` is strongly typed — any miss would be a compile error.
- **SplitAtConditions / Step propagation** — Only `this[i]` indexer sets Step. Audited all `_items[i]` call sites in the Steps/Actions classes: none need Step.
- **PushCancellation / PopCancellation** — Both call sites (`timeout/after.cs:28/55`, `run.cs:130/152`) use try/finally for symmetric Pop. No leak under exceptions.
- **`Goal.LoadedFromPrPath` / `GetRuntimeDirectory`** — Single consumer (`Path.cs:60`). No other `Goal.Path`-as-directory consumers that should have migrated.
- **Coverage subscriber in production `run.cs`** — In place, reads `Context.Events` on the child App. Tester v4 F2 concern (production 0% coverage) no longer applies after v2's `ChildAppCreated` hook wiring.

## Summary

Three of the four findings cluster around the security fix for finding #3. The fix is a solid design (Strip vs. Mask split), the unit tests are solid, but it was bolted onto `results.json` only and neither the JUnit path nor an end-to-end test came along. Compounding this, the two tests that *should* have caught a regression on the JUnit path don't actually verify content. Not a showstopper — no critical/high-severity — but enough incomplete plumbing that I don't want to pass without a coder pass.

**Verdict: fail** — route back to coder for findings #1 and #2 (both one-session fixes).
