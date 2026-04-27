# Tester v1 Plan

## Intent
Run all tests (C# and PLang) on `runtime2-builder-onerror-fix` branch to validate the builder onError fix, RetryOverSeconds->RetryOverMs rename, and new PLang test suites.

## Steps
1. Build the C# solution (`dotnet build`)
2. Run all C# tests (`dotnet run --project PLang.Tests`)
3. Find all PLang test goals in `Tests/App/` and build+run them
4. Analyze test results and write findings
5. Write test-report.json, verdict.json, summary.md
6. Commit and push

## Scope
- Matching coder v2 — reviewing rebuild of .pr files and restructured retry tests
- No code changes expected — pure test execution and analysis
