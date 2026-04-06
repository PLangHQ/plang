# Tester v3 Plan — runtime2-setup-goal

## Scope
Review coder v3 (Setup.goal run-once system + SettingsData sharing fix). Code analyzer v4 approved.

## Steps
1. Run C# tests (1478 expected) — verify all pass
2. Run PLang tests (23 expected) — verify all pass from Tests/App/
3. Run coverage with TUnit --coverage
4. Read all changed source files and test files
5. Analyze test quality: false greens, missing coverage, weak assertions
6. Focus areas:
   - Setup.@this: RunAsync, IsExecuted, Record
   - Steps.RunAsync: run-once logic, record-on-success semantics
   - EngineGoals: setup filtering, AllIncludingSetup
   - Actor: shared SettingsData registration
   - PLangContext: Setup property, Clone
7. Write test-report.json, verdict.json, summary.md
8. Commit and push
