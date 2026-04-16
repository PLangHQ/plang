# Tester v6 Plan — runtime2-builder-plan

## What I'm testing

The `runtime2-builder-plan` branch — a massive branch (693 files changed, 260K+ insertions) covering:
1. **Data<T> composition** across all modules
2. **Data.Compare** for structural JSON diff
3. **Return removal** — actions use %__data__% instead of Return property
4. **Condition orchestration** — multi-action if/elseif/else within a step
5. **Foreach inline** — uses Data.EnumerateItems(), supports dict
6. **Builder eval suite** — 38 .goal files
7. **TypeMapping Data<T> unwrapping** (v6 coder work)
8. **Builder validation** (validateResponse, promoteGroups)

## Approach

1. Run full C# test suite — record pass/fail
2. Collect Cobertura coverage — focus on changed files
3. Analyze test quality: false-green hunting, deletion test, weak assertions
4. Identify missing coverage for new production files
5. Write test-report.json and verdict

## Status: COMPLETE

- C# tests: 2069 total, 2065-2067 pass, 2-4 fail (non-deterministic)
- Coverage collected and parsed
- Quality analysis done across all major areas
- Report ready to write
