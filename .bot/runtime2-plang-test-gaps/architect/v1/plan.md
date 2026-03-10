# PLang Test Gap Analysis — Handoff to Tester/Coder

## Overview

The runtime2 PLang test suite has 29 test suites (was 23, 6 new added). Module-level coverage has improved significantly. The remaining gaps are in **engine-level behavior**: error flows, events, caching, actors, and setup.

This document maps what's tested vs what's not, organized by concern. The tester writes `.test.goal` files, the coder fixes anything that breaks.

## Expected Deliverables

**~25 new `.test.goal` files** in `Tests/Runtime2/`, one concern per file:

### Error Handling (6 suites)

| # | Test Suite | Location | Tests |
|---|-----------|----------|-------|
| 1 | ErrorCall | `Tests/Runtime2/ErrorCall/` | `on error call GoalName` — standalone, no retry |
| 2 | ErrorProps | `Tests/Runtime2/ErrorProps/` | `%!error.Message%`, `%!error.Key%`, `%!error.StatusCode%` access in handler |
| 3 | ErrorOrdering | `Tests/Runtime2/ErrorOrdering/` | RetryFirst vs GoalFirst — does retry or goal fire first? |
| 4 | ErrorInHandler | `Tests/Runtime2/ErrorInHandler/` | Error thrown inside an error handler — does it propagate? |
| 5 | ErrorNested | `Tests/Runtime2/ErrorNested/` | Inner goal has its own error handler — both layers work |
| 6 | ErrorTypes | `Tests/Runtime2/ErrorTypes/` | Different error sources (throw, file not found, http error) — `%!error%` shape for each |

### Events (8 suites)

| # | Test Suite | Location | Tests |
|---|-----------|----------|-------|
| 7 | EventBeforeStep | `Tests/Runtime2/EventBeforeStep/` | `before step` fires before each step executes |
| 8 | EventAfterStep | `Tests/Runtime2/EventAfterStep/` | `after step` fires after each step executes |
| 9 | EventAfterAction | `Tests/Runtime2/EventAfterAction/` | `after action output.write` captures action parameters |
| 10 | EventRemove | `Tests/Runtime2/EventRemove/` | Register event, fire once, remove, fire again — count stays at 1 |
| 11 | EventMultiple | `Tests/Runtime2/EventMultiple/` | Two events on same hook — both fire |
| 12 | EventPriority | `Tests/Runtime2/EventPriority/` | Higher priority event fires first — assert execution order |
| 13 | EventWildcard | `Tests/Runtime2/EventWildcard/` | `before action file.*` matches file.read, file.write, etc. |
| 14 | EventVarChange | `Tests/Runtime2/EventVarChange/` | `OnVariableChange` fires when variable is set |

### Caching (3 suites)

| # | Test Suite | Location | Tests |
|---|-----------|----------|-------|
| 15 | CacheSliding | `Tests/Runtime2/CacheSliding/` | Sliding expiration extends window on access |
| 16 | CacheKey | `Tests/Runtime2/CacheKey/` | Custom cache key — same key hits, different key misses |
| 17 | CacheDynamicKey | `Tests/Runtime2/CacheDynamicKey/` | Cache key contains `%variable%` — resolves dynamically |

### Goal Calls (4 suites)

| # | Test Suite | Location | Tests |
|---|-----------|----------|-------|
| 18 | GoalCallDynamic | `Tests/Runtime2/GoalCallDynamic/` | `call %goalName%` — goal name from variable |
| 19 | GoalCallMissing | `Tests/Runtime2/GoalCallMissing/` | Call non-existent goal — error path, `%!error.Key%` |
| 20 | GoalCallRelative | `Tests/Runtime2/GoalCallRelative/` | Call goal from subdirectory — relative path resolution |
| 21 | GoalCallReturn | `Tests/Runtime2/GoalCallReturn/` | Return value via `write to %var%` (not global variable) |

### Actors (3 suites)

| # | Test Suite | Location | Tests |
|---|-----------|----------|-------|
| 22 | ActorSwitch | `Tests/Runtime2/ActorSwitch/` | `actor="system"` / `actor="service"` — action runs under specified actor |
| 23 | ActorDatasource | `Tests/Runtime2/ActorDatasource/` | Per-actor datasource isolation — system data not visible to user |
| 24 | ActorContext | `Tests/Runtime2/ActorContext/` | Actor-specific context variables accessible |

### Setup & Library (2 suites)

| # | Test Suite | Location | Tests |
|---|-----------|----------|-------|
| 25 | SetupGoal | `Tests/Runtime2/SetupGoal/` | Run-once semantics — may need special test pattern |
| 26 | LibraryLoad | `Tests/Runtime2/LibraryLoad/` | `library.load` basic usage |

### Total: 26 test suites

**Definition of done:**
- Each test suite has a `*.test.goal` file with `Start` goal
- Supporting goals in separate `.goal` files where needed
- All tests build cleanly: `plang p build`
- All tests pass: `plang p !test`
- `.pr` files verified after build (LLM can mismap steps)
- If a test reveals a runtime bug, the coder fixes it and notes what changed

**If the LLM builder can't generate correct `.pr` for error handling tests** (known limitation with `onError` step properties), hand-craft the `.pr` file. Document which `.pr` files were hand-crafted so we know to rebuild them when the builder improves.

**Work in priority order** — error handling first, events second. If something is blocked (e.g., actors need infrastructure), skip it and note the blocker.

## Key PLang Testing Facts

- Variables are **global** — once set, they live through the entire context, including across goal calls and event handlers
- `%!xxx%` context variables are accessible to PLang developers: `%!engine%`, `%!goal%`, `%!step%`, `%!context%`, `%!callStack%`, `%!fileSystem%`, `%!channels%`, `%!memoryStack%`
- Events are the test capture mechanism — bind `afterAction` or `afterStep` to capture what happened, then assert on it
- The builder is PLang code (`system/Build.goal`) — build-time features fire naturally during `plang p build`
- Process: write `.test.goal` → `plang p build` → verify `.pr` → `plang p !test`

---

## 1. Error Handling (BIG GAP)

### Covered
- `on error ignore` — ErrorHandling test proves execution continues
- `on error retry N times, ignore` — Retry test proves retry exhaustion then continue
- `on error retry N times, then call GoalName` — Retry test proves error goal fires after retries

### Not Covered

**NOTE:** The tester report flagged that `on error call GoalName` is blocked by the LLM builder not reliably generating the `onError` step property. Consider pre-building `.pr` files for error handling tests to bypass builder limitations.

**`on error call GoalName` (standalone, no retry):**
```plang
Start
- throw "test error", on error call CatchError
- assert %caughtMessage% equals "test error"

CatchError
- set %caughtMessage% = %!error.Message%
```

**`%!error%` property access in error handlers:**
```plang
Start
- throw "bad thing", on error call InspectError
- assert %errorKey% is not null
- assert %errorMessage% equals "bad thing"

InspectError
- set %errorKey% = %!error.Key%
- set %errorMessage% = %!error.Message%
```

**RetryFirst vs GoalFirst ordering:**
```plang
Start
/ RetryFirst: retries exhaust, THEN call goal
- set %order% = ""
- call FailOnce, on error retry 1 times, then call LogError
- assert %order% equals "retry,goal", "should retry first then call goal"

/ GoalFirst: call goal first, THEN retry
- set %order2% = ""
- call FailOnce2, on error call LogError2, then retry 1 times
- assert %order2% equals "goal,retry", "should call goal first then retry"
```

**Error in error handler — does it propagate?**

**Nested error handling — error inside a goal that has its own error handler**

**Error variables available:** `%!error%`, `%!error.Message%`, `%!error.Key%`, `%!error.StatusCode%`

---

## 2. Events (HALF UNTESTED)

### Covered
- `beforeGoal` / `afterGoal` — Events test (shallow, no assertion on behavior)
- `beforeAction` + `skipAction` — EventOverride test

### Not Covered

**`beforeStep` / `afterStep`:**
```plang
Start
- before step call TrackStep
- set %value% = "hello"
- assert %stepCount% greater than 0

TrackStep
- set %stepCount% = %stepCount% + 1
```

**`afterAction`:**
```plang
Start
- after action output.write call CaptureWrite
- write out "hello world"
- assert %capturedContent% equals "hello world"

CaptureWrite
- set %capturedContent% = %!action.parameters.content%
```

**`event.remove` — unregistering an event:**
```plang
Start
- before action output.write call Counter
- write out "first"
- assert %writeCount% equals 1
- remove event %beforeWriteEvent%
- write out "second"
- assert %writeCount% equals 1, "should stay 1 after event removed"
```

**Multiple events on same hook — both fire:**

**Event priority ordering — higher priority fires first:**

**Wildcard patterns — `before action file.*` matching all file actions:**

**`OnVariableChange` event:**

**`OnCacheHit` / `OnCacheMiss` events:**

---

## ~~3. Context Variables (BARELY TESTED)~~ RESOLVED

Now covered by **ContextVars2** test suite:
- `%!goal.Name%` — asserted equals "Start"
- `%!step%` — asserted not null
- `%!context%` — asserted not null
- `%!fileSystem%` — asserted not null
- `%!callStack%` — asserted not null

Plus original ContextVars (`%!engine.Name%`) and CallStack (`%!callStack.Depth%`).

**Remaining small gaps:** `%!memoryStack%`, `%!channels%`, `%!context.Id%` — minor, low priority.

---

## 4. Caching (MINIMAL)

### Covered
- Basic cache hit — Cache test proves second call returns cached value

### Not Covered

**Sliding cache:**
```plang
Start
- call ReadSliding
- call ReadSliding
/ sliding should extend the window
- assert %slidingResult% is not null
```

**Custom cache key:**
```plang
Start
- call ReadWithKey key="mykey"
- call ReadWithKey key="mykey"
- assert %keyResult% equals first value
- call ReadWithKey key="otherkey"
- assert %otherResult% is different
```

**Cache with `%variable%` key — dynamic key resolution**

**`OnCacheHit` / `OnCacheMiss` events firing**

---

## ~~5. Goal Calls (NO DEDICATED TEST)~~ PARTIALLY RESOLVED

Now covered by **GoalCall** test suite:
- Goal with parameters — `call goal Greet name="world"` ✓
- Variable set in called goal flows back — `%greeting%` asserted ✓

**Still not covered:**
- Dynamic goal name — `call %goalName%`
- Calling non-existent goal — error path
- Relative goal resolution — calling goals from subdirectories
- Recursive calls / max depth
- Return values via `write to %var%` (current test uses global variable, not return)

---

## ~~6. Variables (CORE MODULE, 4/5 ACTIONS UNTESTED)~~ RESOLVED

Now covered by **VariableOps** test suite:
- `variable.exists` — set var, check exists = true ✓
- `variable.remove` — remove var, check exists = false ✓
- `variable.clear` — clear all, check var gone ✓

**Note:** `variable.get` as explicit action still not tested, but it's used implicitly everywhere via `%variable%` resolution.

---

## 7. Setup Goals (ZERO PLang COVERAGE)

C# tests exist and are thorough. No PLang test exercises the setup flow.

**Run-once semantics:**
- A Setup.goal runs on first engine start
- Same steps don't re-run on subsequent starts
- Changed steps (different hash) do re-run

This may be hard to test in a single `.test.goal` since setup runs before the test. Might need a dedicated test pattern or C# integration test.

---

## 8. Actors (ZERO PLang COVERAGE)

**Actor switching — system/service/user:**
```plang
Start
- write out "hello" actor="system"
- write out "hello" actor="service"
```

**Per-actor datasource isolation**

**Actor context variables**

---

## 9. Module Action Gaps

### ~~convert — missing 3 actions~~ RESOLVED
Now covered by **Convert2** test suite: `todouble`, `tolong`, `todatetime` ✓

**Note:** Assertions are `is not null` only — value verification would be stronger but works for now.

### event — missing 4 actions:
- `afterAction`, `afterStep`, `beforeStep` — zero PLang coverage
- `event.remove` — zero coverage

### ~~list — missing 3 actions~~ RESOLVED
Now covered by **ListOps2** test suite: `range`, `set` (mutate by index), `flatten` ✓

**Note:** `flatten` and `range` assertions are `is not null` only — weak. Count/value assertions blocked by builder deep-property limitations.

### ~~variable — missing 4 actions~~ RESOLVED
Now covered by **VariableOps** test suite ✓

### library — entire module:
- `library.load` — zero coverage

### ~~math — 1 action~~ RESOLVED
Now covered by **Math2** test suite: `random` with range assertion ✓

---

## 10. Not Implemented (Design Exists, No Code)

These exist in the model but aren't wired:

- **Step timeout** — `Step.Timeout` exists as a property but `ExecuteActionsAsync` never checks it
- **Step WaitForExecution** — async/fire-and-forget flag exists, not implemented
- **Builder validation** — designed (see `runtime2-builder-validation` branch), not built

---

## Updated Priority Order for Tester

1. **Error handling** — most complex, most user-facing, biggest gap. Builder limitation flagged — may need hand-crafted `.pr` files.
2. **Events** — half the surface untested, critical for the validation system design
3. **Caching** — sliding, keys, events
4. **Goal calls** — dynamic names, error paths, recursion, return values
5. **Actors** — may need infrastructure work
6. **Setup goals** — may need special test pattern
7. **library.load** — entire module untested

## What Was Resolved (v1 → v1 update)

Six new test suites added by tester (29 total, all passing):
- **ContextVars2** — `%!goal.Name%`, `%!step%`, `%!context%`, `%!fileSystem%`, `%!callStack%`
- **Convert2** — `todatetime`, `todouble`, `tolong`
- **GoalCall** — goal with parameters, variable flow back
- **ListOps2** — `range`, `set` (mutate by index), `flatten`
- **Math2** — `random` with range assertion
- **VariableOps** — `exists`, `remove`, `clear`

## Test Writing Reminders

- Goal name MUST be `Start`
- Supporting goals go in separate `.goal` files
- Always verify the `.pr` file after building — the LLM can mismap steps
- Use event binding to capture side effects (output, errors) for assertion
- Variables are global — set in an error handler or event handler, assert in `Start`
- Use mock spy for action interception when events aren't enough
- For error handling tests: the LLM builder may not reliably generate `onError` step properties — consider hand-crafting `.pr` files
