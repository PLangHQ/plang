# Test Plan — Action Modifiers (Phases 1 & 2)

## Overview

Tests for the action modifiers feature: promoting `onError`/`cache`/`timeout` from step-level properties to per-action modifier actions with `IModifier.Wrap()` fold pattern.

## Test Areas & Batch Breakdown

### Batch 1: Core Infrastructure (~8 tests)
C# tests for the foundational types and modifier fold mechanics.

- `ModifierAttribute` — Order property works
- `IModifier` contract — `Wrap()` correctly chains delegates
- `Action.Modifiers` property — defaults to empty list, serializes/deserializes
- `Action.RunAsync` with 0 modifiers — existing behavior unchanged (regression)
- `Action.RunAsync` with 1 modifier — wraps the action
- `Action.RunAsync` with 2 modifiers — correct nesting order (right-to-left fold)
- `Action.RunAsync` with 3 modifiers — full chain (timeout > cache > error)
- Non-IModifier handler in modifiers list — returns clean error

### Batch 2: timeout.after Modifier (~6 tests)
C# tests for the timeout modifier handler.

- `timeout.after` — action completes before timeout → passes through result
- `timeout.after` — action exceeds timeout → returns 408 Timeout error
- `timeout.after` — cancellation token propagated to action
- `timeout.after` — parent cancellation (not timeout) → propagates OperationCanceledException
- `timeout.after` — 0ms timeout → immediate timeout
- `timeout.after` — nested with other modifiers → timeout wraps outer

### Batch 3: cache.wrap Modifier (~7 tests)
C# tests for the cache modifier handler.

- `cache.wrap` — cache miss → runs action, stores result
- `cache.wrap` — cache hit → returns cached, skips action
- `cache.wrap` — action failure → does not cache
- `cache.wrap` — custom key used when provided
- `cache.wrap` — default key derived from goal path + step index
- `cache.wrap` — sliding expiration passed to cache
- `cache.wrap` — cached result restored as `__data__` variable

### Batch 4: error.handle Modifier (~10 tests)
C# tests for the error modifier handler.

- `error.handle` — action succeeds → passes through, no handling
- `error.handle` — IgnoreError → swallows error, returns Ok
- `error.handle` — filter by StatusCode → matches, handles
- `error.handle` — filter by StatusCode → no match, propagates
- `error.handle` — filter by Key → case-insensitive match
- `error.handle` — filter by Message → substring match
- `error.handle` — no filter → matches all errors
- `error.handle` — RetryFirst order → retries before calling goal
- `error.handle` — GoalFirst order → calls goal before retry
- `error.handle` — retry succeeds on 2nd attempt → returns success

### Batch 5: Module Registry & Clone (~5 tests)
C# tests for modifier awareness in the module registry and clone support.

- `Modules.IsModifier()` — returns true for modifier-attributed handler
- `Modules.IsModifier()` — returns false for regular handler
- `Modules.GetModifierOrder()` — returns correct Order value
- `Step.Clone()` — clones action modifiers
- `Modules.Describe()` — modifier actions appear in action summary

### Batch 6: Builder GroupModifiers (~6 tests)
C# tests for the deterministic modifier grouping in the save pipeline.

- `GroupModifiers` — no modifiers in flat list → unchanged
- `GroupModifiers` — modifier after executable → attached to preceding action
- `GroupModifiers` — multiple modifiers on one action → all attached, sorted by Order
- `GroupModifiers` — modifier between two executables → attaches to the one before it
- `GroupModifiers` — leading modifier (no preceding action) → edge case handling
- `GroupModifiers` — mixed: exec, mod, exec, mod, mod → correct grouping

### Batch 7: PLang .goal Tests (~6 tests)
Integration tests written in PLang syntax, with real step text for the coder to build.

- `CacheOnFileRead.test.goal` — file.read with cache for 60 seconds, verify second read hits cache
- `OnErrorRetry.test.goal` — goal call with on error retry 3 times, verify retry count
- `TimeoutOnSlowAction.test.goal` — goal call with timeout, needs slow action mechanism (stub for coder)
- `MultipleModifiersCompose.test.goal` — cache + error modifiers on one action, verify composition
- `PerActionErrorScope.test.goal` — error modifier on first action doesn't affect second
- `OnErrorCallGoal.test.goal` — error modifier calls error goal, verifies %!error% properties

## Totals

- **C# tests:** ~42 (Batches 1-6)
- **PLang .goal tests:** ~6 (Batch 7)
- **Total:** ~48

## File Locations

- C# tests: `PLang.Tests/App/Modules/modifier/` (new directory for modifier infrastructure tests)
  - `ModifierFoldTests.cs` — Batch 1
  - `TimeoutAfterTests.cs` — Batch 2
  - `CacheWrapTests.cs` — Batch 3
  - `ErrorHandleTests.cs` — Batch 4
  - `ModifierRegistryTests.cs` — Batch 5
  - `GroupModifiersTests.cs` — Batch 6
- PLang tests: `tests/modifiers/` (new directory)
  - Individual `.test.goal` files per test
