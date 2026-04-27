# Tester v6 Summary — runtime2-builder-plan

## What this is

Test quality analysis for the massive runtime2-builder-plan branch (693 files, 260K+ insertions). Covers Data<T> composition, Data.Compare, Return removal, condition orchestration, foreach inline, builder eval suite, TypeMapping unwrapping, and builder validation.

## Test Results

- **C# tests**: 2069 total, 2065 pass, 4 fail (2-4 non-deterministic)
- **PLang tests**: N/A — branch uses eval suite via builder, not runtime test runner
- **Coverage**: 139/145 changed production files have coverage data

## Findings (21 total: 6 critical, 9 major, 6 minor)

### Critical (must fix)

1. **validateResponse.cs — 0% coverage** — 106 lines of LLM response validation (JSON parsing, step count, index checks). The gatekeeper for builder correctness is completely untested.
2. **promoteGroups.cs — 0% coverage** — New builder action with untested group promotion logic.
3. **list.any — 0% coverage** — 55-line action with operator evaluation and two property extraction paths (dict + JsonElement).
4. **list.group — 0% coverage** — 50-line grouping action with key extraction logic.
5. **Foreach dict iteration is a FALSE GREEN** — `Foreach_IteratesDictionary` only asserts `result.Success`. On current code, `%val%` gets a `KeyValuePair` struct (not value) and `%key%` gets the loop index (not dict key). Test passes green despite completely wrong variable contents. Fix from `fix-plang-tests` branch needs integration.
6. **Handled break + condition orchestration barely tested** — Step.RunActions() `Handled` break has zero direct tests. Only 4 orchestration tests exist, all with 2-action steps. No elseif (3+ conditions), no multi-action branches, no all-false, no error-in-branch, no assertion that `Handled=true` is set.

### Major (should fix)

7. **Data.ToBoolean / As<T> / ShallowClone — 0%** — Core Data API methods (truthiness, type conversion, cloning) have zero direct tests. ToBoolean bugs would break all condition evaluation.
8. **IBuildValidatable.ValidateBuild — 0%** — Two implementations (variable.set and llm.query) untested. Reflection-based invocation in Validate() action untested.
9. **Return removal backward compat — untested** — Old .pr files with `return` property silently lose return mapping. No test verifies deserialization or documents migration.
10. **List tests lack value assertions** — Most list tests only check Success, not actual values. Missing: empty list edge cases, out-of-range Remove, Sort descending, Contains on dict.
11. **timer module — 0%** — New start/end timer actions untested.
12. **cache.store — 0%** — Modified for __data__ collection (Return removal) but untested.
13. **LLM retry test broken** — `Query_OnValidateResponse_MaxRetries_ReturnsError` hits file-not-found instead of testing actual retry logic.
14. **Actor settings leak** — `UserActor_DuringBuilding_DoesNotPersistAcrossEngineInstances` shows settings persisting when they shouldn't.
15. **ReservedKeywords.cs — 0%** — Modified but untested.

### Minor

16-21. Data.Compare weak edges (boolean mismatch, type-kind mismatch, nested diff), JsonStringNavigator (14%), MemoryStepCache (41%), UI render (0%), empty collection edge cases, LLM tool call flaky.

## What was done

1. Ran full C# test suite (2069 tests, 4 failures across 2 runs)
2. Collected Cobertura coverage for all 145 changed production files
3. Analyzed every 0% file for test necessity
4. Deployed 7 parallel analysis agents across: Data.Compare, condition orchestration, foreach/loop, list modules, Data/Variables core, builder/TypeMapping, engine/goal/step core, remaining modules
5. Identified broken test setups, false greens, and coverage gaps

## Key False-Green: Foreach Dict

The most dangerous finding. `Foreach_IteratesDictionary` is green but the variables contain completely wrong types:
- `%val%` = `KeyValuePair<string, object?>` struct (should be just the value)
- `%key%` = numeric loop index 0 (should be the dict key string)

The test only asserts `result.Success` without checking variable contents. This means every PLang program using foreach on a dictionary is silently broken. The fix exists on `fix-plang-tests` branch but hasn't been merged.

## Verdict: FAIL

Send back to **coder** for fixes. Priority order:
1. Merge foreach fix from `fix-plang-tests` + add value assertion tests
2. Add validateResponse tests (builder gatekeeper)
3. Add condition orchestration tests (elseif, multi-action, all-false)
4. Add list.any/group tests
5. Add Data.ToBoolean/As<T> tests
6. Fix the 3 broken/flaky tests

## Files

- `coverage.json` — Full coverage data for all changed files
- `verdict.json` — Pass/fail status
- `plan.md` — Analysis plan
