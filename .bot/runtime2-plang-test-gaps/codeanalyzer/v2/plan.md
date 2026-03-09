# Code Analyzer v2 — Plan

**Branch:** runtime2-plang-test-gaps
**Scope:** Full 5-pass review of all C# code changes since runtime2 base, including coder v2 fixes.

## Context

v1 analyzed the initial 6 files and found them clean. Since then:
- Coder v2 enforced Goal.Path requirement, fixed Names property, fixed Get() variations
- Tester v2/v3 validated fixes, found and verified fixes for Names setup filter and empty Path bypass

This v2 review covers the **final state** of all changed files — a fresh analysis of the complete branch diff.

## Files to Analyze

### Runtime source (5 files):
1. `PLang/Executor.cs` — setup integration in Run2
2. `PLang/Runtime2/Engine/Goals/Goal/Methods.cs` — return value propagation
3. `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` — lastResult tracking
4. `PLang/Runtime2/Engine/Goals/Goal/this.cs` — PrPath computed property, Path enforcement
5. `PLang/Runtime2/Engine/Goals/Setup/this.cs` — convention-based discovery, RunAsync
6. `PLang/Runtime2/Engine/Goals/this.cs` — PrPath keying, Names filter, Get() fallback chain
7. `PLang/Runtime2/Engine/Test/this.cs` — per-test root, setup before test, Data-based results

### C# test files (7 files):
- GoalsTests.cs, SetupTests.cs, EngineTests.cs, StartGoalTests.cs, StepErrorHandlingTests.cs, StepRetryTests.cs, ConditionHandlerTests.cs, ForeachTests.cs

## Passes
1. OBP Compliance (5 rules)
2. Simplification
3. Readability
4. Behavioral Reasoning (data flow, type surface, clone family)
5. Deletion Test
