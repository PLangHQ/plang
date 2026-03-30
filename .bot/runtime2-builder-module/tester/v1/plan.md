# Tester v1 Plan — Builder Module

## Scope
Reviewing coder v1 + codeanalyzer v4 (PASS) output for the `PLang.Runtime2.modules.builder` module.

## Steps
1. Run full C# test suite, collect pass/fail counts
2. Run coverage (Cobertura) on builder module files
3. Read all 10 test files + production code for false-green hunting
4. Check PLang test existence and quality
5. Write test-report.json and verdict

## Key Files
- Production: `PLang/Runtime2/modules/builder/providers/DefaultBuilderProvider.cs` (352 lines)
- 8 action handlers (thin delegators)
- Entity: `Goal.MergeFrom()`, `Goal.Parse()`, `Step.Merge()`
- Tests: 10 test files in `PLang.Tests/Runtime2/Modules/builder/`
- PLang tests: 6 files in `Tests/Runtime2/Builder/`
