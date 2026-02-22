# v7 Review Summary

Tester v7 found 2 major + 2 minor new findings, plus 3 carry-forwards. Code analyzer v2 then did a higher-level review finding 4 cross-concern gaps. Coder v6 addressed the analyzer's 4 gaps plus 2 of the tester's v7 findings:

| Finding | Source | Status |
|---------|--------|--------|
| #1 Cycle detection untested | Tester v7 (major) | **Still open** — zero tests for _resolvingVars |
| #2 GetChild depth through MemoryStack | Tester v7 (major) | Fixed — `Get_DeeplyNestedPath_ReturnsErrorData` test added |
| #3 fromJson deep nesting | Tester v7 (minor) | Fixed — distinct `JsonDepthExceeded` catch + test |
| #4 Clr() boundary | Tester v7 (minor) | Still open |
| Decimal precision | Analyzer v2 | Fixed — `UnwrapJsonNumber` with `TryGetDecimal` |
| Clone context | Analyzer v2 | Fixed — `clone.Context = Context` |
| fromJson depth error key | Analyzer v2 | Fixed — separate `InvalidOperationException` catch |
| GetChild MemoryStack integration | Analyzer v2 | Fixed — same as tester #2 |
