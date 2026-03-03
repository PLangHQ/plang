# PLang Test Gap Analysis — v1 Summary

## What this is

A comprehensive analysis of PLang `.test.goal` coverage gaps in runtime2. The 23 existing test suites cover module actions reasonably well. The big holes are engine-level behavior: error flows, events, caching, context variables, goal calls, actors, and setup.

## What was done

Mapped all runtime2 modules (16 modules, ~60 actions) and engine subsystems against existing test coverage. Identified gaps at both the module action level and the engine behavior level.

## Key findings

- **Error handling**: Only `on error ignore` and `retry+ignore/call` tested. Missing: standalone `on error call`, `%!error%` property access, RetryFirst vs GoalFirst ordering, error-in-error-handler, nested error handling.
- **Events**: 4 of 16 event types tested. Missing: step-level hooks, afterAction, event.remove, priority ordering, wildcards, OnVariableChange, OnCacheHit/Miss.
- **Context variables**: Only `%!engine.Name%` and `%!callStack.Depth%` tested. Missing: `%!goal%`, `%!step%`, `%!context%`, `%!fileSystem%`, `%!memoryStack%`.
- **Goal calls**: No dedicated test for parameters, return values, dynamic names, relative resolution, or recursion depth.
- **Variable module**: 4 of 5 actions untested (clear, exists, get, remove).
- **Caching**: Only basic hit tested. Missing: sliding, custom keys, expiration, cache events.
- **Actors**: Zero PLang coverage.
- **Setup goals**: Zero PLang coverage (C# tests are thorough).
- **Unimplemented features**: Step timeout and WaitForExecution exist in model but not wired.

## Status

Analysis complete. Handoff document ready for tester/coder at `v1/plan.md`.
