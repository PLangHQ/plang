# Tester v2 Plan — DataSource + Settings Bridge

## Scope
Review test quality for coder v2 of the DataSource + Settings Bridge feature.

## Steps
1. Read coder v2 summary and codeanalyzer v2 report to understand what was built and reviewed
2. Read all source files (14 changed files) and test files
3. Run C# test suite (dotnet run --project PLang.Tests)
4. Run PLang test suite (plang p !test from Tests/Runtime2/)
5. Analyze test quality using false-green hunting techniques
6. Check PLang test existence for new settings module
7. Verify .pr file correctness for any Settings PLang tests
8. Run dotnet-coverage if available
9. Write test-report.json, coverage.json, summary.md, verdict.json
10. Update session report and commit

## Key Questions
- Do C# tests verify intent (side effects) or just implementation (return values)?
- Are PLang tests correctly mapping to settings module actions?
- Does the test runner correctly detect assertion failures?
- Are edge cases covered (empty table, concurrent access, null keys)?
