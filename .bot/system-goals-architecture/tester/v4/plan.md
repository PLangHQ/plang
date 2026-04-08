# Plan v4 — Test Quality Analysis for system-goals-architecture

## Scope
Coder v4 is the latest version on this branch. The branch is a massive restructuring: 241 new C# files (170 production, 66 test), renaming from Runtime2 to App namespace, adding builder module, navigator system, and many new action handlers.

## Steps

1. **Run C# test suite** — record pass/fail counts
2. **Run Cobertura coverage** — parse for new production files, identify gaps
3. **Identify 0% coverage files** — categorize by criticality
4. **Analyze low-coverage files** — identify what's NOT covered
5. **Investigate test failures** — root-cause the 6 streaming test failures
6. **False-green hunting** — deep-read key test files for weak assertions
7. **Write test-report.json and verdict**

## Status: COMPLETE — all steps done, findings compiled below.
