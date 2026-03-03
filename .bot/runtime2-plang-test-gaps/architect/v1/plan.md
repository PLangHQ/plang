# PLang Test Gap Analysis — Handoff to Tester/Coder

## Overview

The runtime2 PLang test suite has 23 test suites covering module actions. Module-level coverage is reasonable. The big gaps are in **engine-level behavior**: error flows, events, caching, context variables, goal calls, actors, and setup.

This document maps what's tested vs what's not, organized by concern. The tester writes `.test.goal` files, the coder fixes anything that breaks.

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

## 3. Context Variables (BARELY TESTED)

### Covered
- `%!engine.Name%` — ContextVars test
- `%!callStack.Depth%` — CallStack test

### Not Covered

Each of these should be tested for basic access and navigation:

```plang
Start
/ Goal context
- assert %!goal% is not null
- assert %!goal.Name% equals "Start"

/ Step context
- assert %!step% is not null
- assert %!step.Text% is not null

/ Engine navigation
- assert %!engine% is not null
- assert %!fileSystem% is not null

/ Context itself
- assert %!context% is not null
- assert %!context.Id% is not null

/ Memory stack
- assert %!memoryStack% is not null
```

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

## 5. Goal Calls (NO DEDICATED TEST)

### Covered
- Basic `call GoalName` — used everywhere indirectly
- Variables survive goal call — Events test proves this

### Not Covered

**Goal with parameters:**
```plang
Start
- call Greet name="world", write to %greeting%
- assert %greeting% equals "hello world"

Greet
- set %result% = "hello %name%"
```

**Return values from goal calls — `write to %var%`**

**Dynamic goal name — `call %goalName%`:**
```plang
Start
- set %goalName% = "DynamicTarget"
- call %goalName%
- assert %dynamicResult% equals "it worked"

DynamicTarget
- set %dynamicResult% = "it worked"
```

**Calling non-existent goal — error path**

**Relative goal resolution — calling goals from subdirectories**

**Recursive calls / max depth (callstack overflow guard at 1000)**

---

## 6. Variables (CORE MODULE, 4/5 ACTIONS UNTESTED)

### Covered
- `variable.set` — used everywhere

### Not Covered

**`variable.exists`:**
```plang
Start
- set %myVar% = "hello"
- check if variable %myVar% exists, write to %exists%
- assert %exists% is true
- check if variable %nonexistent% exists, write to %notExists%
- assert %notExists% is false
```

**`variable.get` (explicit action):**

**`variable.remove`:**
```plang
Start
- set %temp% = "value"
- remove variable %temp%
- check if variable %temp% exists, write to %gone%
- assert %gone% is false
```

**`variable.clear`:**

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

### convert — missing 3 actions:
- `todatetime` — zero coverage
- `todouble` — zero coverage
- `tolong` — zero coverage

### event — missing 4 actions:
- `afterAction`, `afterStep`, `beforeStep` — zero PLang coverage
- `event.remove` — zero coverage

### list — missing 3 actions:
- `flatten`, `range`, `set` (mutate by index)

### variable — missing 4 actions:
- `clear`, `exists`, `get`, `remove`

### library — entire module:
- `library.load` — zero coverage

### math — 1 action:
- `random` — test that result is within expected range

---

## 10. Not Implemented (Design Exists, No Code)

These exist in the model but aren't wired:

- **Step timeout** — `Step.Timeout` exists as a property but `ExecuteActionsAsync` never checks it
- **Step WaitForExecution** — async/fire-and-forget flag exists, not implemented
- **Builder validation** — designed (see `runtime2-builder-validation` branch), not built

---

## Priority Order for Tester

1. **Error handling** — most complex, most user-facing, biggest gap
2. **Events** — half the surface untested, critical for the validation system design
3. **Context variables** — quick to write, validates developer access to `%!xxx%`
4. **Goal calls** — parameters, return values, dynamic names
5. **Variable module** — 4 missing actions, quick wins
6. **Caching** — sliding, keys, events
7. **Convert/list/math gaps** — small, quick
8. **Actors** — may need infrastructure work
9. **Setup goals** — may need special test pattern

## Test Writing Reminders

- Goal name MUST be `Start`
- Supporting goals go in separate `.goal` files
- Always verify the `.pr` file after building — the LLM can mismap steps
- Use event binding to capture side effects (output, errors) for assertion
- Variables are global — set in an error handler or event handler, assert in `Start`
- Use mock spy for action interception when events aren't enough
