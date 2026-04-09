# Auditor v1 Plan — Builder Module

## Scope
Cross-cutting integrity review of the `runtime2-builder-module` branch. All previous reviewers passed:
- Codeanalyzer v4: PASS (2 rounds of fresh-eyes analysis)
- Tester v2: PASS (all 7 findings resolved, 1 production bug fixed)
- Security v1: PASS (0 critical/high, 2 medium, 3 low)

## What I Already Read
- All 11 builder module source files (provider + 8 actions + types + BuilderTypeInfo)
- Goal/this.cs (Parse, MergeFrom, ToText, FormatForLlm)
- Step/this.cs (Merge, Clone)
- Engine/Modules/this.cs (Describe, GetDefaults)
- Engine/Utility/Json.cs (shared options)
- Engine/Providers/this.cs (registry, ResolveType)
- All 10 test files
- Security-report.json, test-report.json, all bot summaries/verdicts

## Focus Areas
1. **Cross-file contracts** — Does a change in one file break assumptions in another?
2. **Review gaps** — Did codeanalyzer/tester/security miss anything?
3. **Architectural fit** — Does it follow OBP, lazy-load, provider pattern correctly?
4. **Test adequacy** — Are the assertions strong enough to catch regressions?
5. **Foundation ripple** — Changes to Goal/Step entities affect all downstream code

## Known Findings So Far
1. `Describe()` [Provider] filter has no dedicated test — codeanalyzer v4 recommended it, tester missed it
2. `FormatForLlm` creates `new JsonSerializerOptions()` per call — minor perf concern
3. Need to verify `ResolveGoalCallPaths` file.Exists usage is correct
4. Need to check if `Goal.Parse` line comment detection has a false positive for `//` comments
