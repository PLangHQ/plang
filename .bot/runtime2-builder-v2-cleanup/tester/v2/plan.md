# v2 Plan: Test Quality Analysis for runtime2-builder-v2-cleanup

## Scope
Coder v2 (cleanup branch): ~160 production C# files changed. Major themes: [Provider] attribute, Data-typed params, event consolidation, variable simplification, file provider extraction, signing pipeline to provider, library→module rename, step runner condition fix.

## Steps
1. Run full C# test suite — **DONE**: 1839 pass, 0 fail, 4 skipped
2. Run coverage — **DONE**: Cobertura XML collected
3. Analyze coverage gaps on changed files
4. Hunt false greens in key test files
5. Check for missing PLang .goal tests
6. Write test-report.json and verdict

## Key Areas to Investigate
- **module.remove** — new action, 0% coverage, zero tests
- **DataList<T>** — new type, 47.4% coverage
- **PlangSerializer async methods** — 0% coverage (Serialize/DeserializeAsync)
- **TryExtractSignedErrorIdentity** — 0% coverage (48 lines)
- **StreamWithProgressAsync** — 46% line, 16% branch
- **list.set** — 0% coverage, zero tests
- **event.skipAction** — 0% coverage, zero tests
- **DefaultAssertProvider** — 94% line but 60.7% branch
- **GoalMapper** — 0% coverage (but may be v1-only path)
- Step runner condition fix — verify test quality
