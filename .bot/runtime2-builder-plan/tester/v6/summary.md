# Tester v6 Summary — runtime2-builder-plan

## What this is

Test quality analysis for the massive runtime2-builder-plan branch (693 files, 260K+ insertions). Covers Data<T> composition, Data.Compare, Return removal, condition orchestration, foreach inline, builder eval suite, TypeMapping unwrapping, and builder validation.

## Test Results

- **C# tests**: 2069 total, 2065 pass, 4 fail (2-4 non-deterministic)
- **PLang tests**: N/A — branch uses eval suite via builder, not runtime test runner
- **Coverage**: 139/145 changed production files have coverage data

## Findings (15 total: 4 critical, 6 major, 5 minor)

### Critical (must fix)

1. **validateResponse.cs — 0% coverage** — 106 lines of LLM response validation (JSON parsing, step count, index checks). The gatekeeper for builder correctness is completely untested.
2. **promoteGroups.cs — 0% coverage** — New builder action with untested group promotion logic.
3. **list.any — 0% coverage** — 55-line action with operator evaluation and two property extraction paths (dict + JsonElement).
4. **list.group — 0% coverage** — 50-line grouping action with key extraction logic.

### Major (should fix)

5. **timer module — 0%** — New start/end timer actions untested.
6. **cache.store — 0%** — Modified for __data__ collection (Return removal) but untested.
7. **LLM retry test broken** — `Query_OnValidateResponse_MaxRetries_ReturnsError` hits file-not-found instead of testing actual retry logic. Test setup needs a real .pr file.
8. **LLM tool call test flaky** — `Query_ToolCall_LlmRequestsToolAndHandlesError` intermittently returns empty string.
9. **Actor settings leak** — `UserActor_DuringBuilding_DoesNotPersistAcrossEngineInstances` shows settings persisting when they shouldn't.
10. **ReservedKeywords.cs — 0%** — Modified but untested.
11. **Data.Compare weak edges** — Missing boolean mismatch, type-kind mismatch, nested diff structure, case-insensitive field tests.

### Minor

12-15. JsonStringNavigator (14%), MemoryStepCache (41%), UI render (0%), empty collection edge cases.

## What was done

1. Ran full C# test suite (2069 tests, 4 failures across 2 runs)
2. Collected Cobertura coverage for all 145 changed production files
3. Analyzed every 0% file for test necessity
4. Reviewed Data.Compare tests for false-green risks
5. Identified broken test setups (LLM retry, actor settings)

## Verdict: FAIL

Send back to **coder** for fixes. Priority: validateResponse tests, list.any/group tests, fix the 2 broken LLM/actor tests.

## Files

- `coverage.json` — Full coverage data for all changed files
- `verdict.json` — Pass/fail status
- `plan.md` — Analysis plan
