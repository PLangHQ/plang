# Tester v1 Plan — In-Memory SQLite DataSource

## Scope

Review coder v1 output for `runtime2-inmemory-datasource` branch. The coder implemented:
- `SqliteDataSource.InMemory(name)` with sentinel connection
- `Engine.Building` object (mirrors Test/Debug pattern)
- `Actor.CreateDataSource()` navigates to Testing/Building flags
- 7 new C# tests

## Steps

1. Run full C# test suite — verify all 1472 pass
2. Run coverage (if tool works in WSL2)
3. Analyze each new test for false-green patterns:
   - Does it verify intent or just implementation?
   - Would it catch a subtle bug? (flip condition, swap params, return early)
   - Are mocks hiding real behavior?
   - Edge cases tested?
4. Apply deletion test — for each changed line, would any test fail if deleted?
5. Check for PLang tests (architect scoped out — verify this is documented)
6. Write test-report.json, verdict.json, summary.md
