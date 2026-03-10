# PLang Test Gap Analysis — v1 Summary

## What this is

A comprehensive analysis of PLang `.test.goal` coverage gaps in runtime2. Initially 23 test suites; now 29 after 6 new suites were added. Module-level coverage has improved significantly. The remaining holes are engine-level behavior: error flows, events, caching, actors, and setup.

## What was done

Mapped all runtime2 modules (16 modules, ~60 actions) and engine subsystems against existing test coverage. Identified gaps at both the module action level and the engine behavior level. Updated after 6 new test suites resolved several gaps.

## Key findings (updated)

### Resolved
- **Context variables**: Now covered by ContextVars2 (`%!goal.Name%`, `%!step%`, `%!context%`, `%!fileSystem%`, `%!callStack%`)
- **Goal calls**: Now partially covered by GoalCall (parameters, variable flow back)
- **Variable module**: Now covered by VariableOps (`exists`, `remove`, `clear`)
- **Convert module**: Now covered by Convert2 (`todouble`, `tolong`, `todatetime`)
- **List module**: Now covered by ListOps2 (`range`, `set`, `flatten`)
- **Math module**: Now covered by Math2 (`random` with range assertion)

### Remaining gaps
- **Error handling**: Only `on error ignore` and `retry+ignore/call` tested. Missing: standalone `on error call`, `%!error%` property access, RetryFirst vs GoalFirst ordering, error-in-error-handler, nested error handling. Builder limitation flagged — may need hand-crafted `.pr` files.
- **Events**: 4 of 16 event types tested. Missing: step-level hooks, afterAction, event.remove, priority ordering, wildcards, OnVariableChange, OnCacheHit/Miss.
- **Caching**: Only basic hit tested. Missing: sliding, custom keys, cache events.
- **Goal calls (remaining)**: Dynamic names, error paths, relative resolution, recursive calls, return values.
- **Actors**: Zero PLang coverage.
- **Setup goals**: Zero PLang coverage (C# tests are thorough).
- **library.load**: Entire module untested.
- **Unimplemented features**: Step timeout and WaitForExecution exist in model but not wired.

## Status

Analysis complete. Updated after tester's first pass (6 new suites, 29 total). Handoff document at `v1/plan.md`.
