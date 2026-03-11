# Tester v2 Plan — Re-run after Coder v2 Fixes

## Scope
Validate coder v2 fixes for auditor FAIL findings. Verify all 4 auditor findings + security #4 are properly fixed and tested.

## Steps
1. Run full C# test suite (1595 expected) — verify zero regressions
2. Read all 3 changed production files (if.cs, compare.cs, DefaultEvaluator.cs)
3. Read all test files covering changes (IfHandlerTests, CompareHandlerTests, DefaultEvaluatorTests, ConditionHandlerTests)
4. Apply false-green hunting: deletion test on try/catch blocks, review-driven code check, weak assertion scan
5. Verify each auditor finding has a corresponding test
6. Write test-report.json and verdict
