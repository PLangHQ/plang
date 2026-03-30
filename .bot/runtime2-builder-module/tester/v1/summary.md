# Tester v1 Summary — Builder Module

## What this is
Test quality analysis of the builder module (coder v1). Validates that tests verify intent, not just implementation. Hunts false-greens in a 2018-test suite.

## What was done

### Test run
- **C#**: 2018 total, 2014 passed, 0 failed, 4 skipped (pre-existing sub-engine skips)
- **PLang**: 6 test goals, 3 functional, 3 deferred stubs (acceptable — require constructed Step/Action objects)
- **Coverage**: DefaultBuilderProvider 96.9%, action handlers all 100%

### Findings: 3 major, 4 minor

**Major false-greens (all same pattern: check existence, not correctness):**

1. **GoalCall PrPath not verified** (`ValidateActionsTests:71`) — Test creates a .pr file, runs validate with a GoalCall, checks `result.Success` but never verifies `goalCall.PrPath` was actually set. The entire ResolveGoalCallPaths method could be a no-op and this test passes.

2. **SaveApp content not verified** (`AppTests:82`) — Test name says "UpdatesTimestamp" but only checks `File.Exists`. Never reads the file back. Serialization could be broken.

3. **SaveGoals content not verified** (`SaveGoalsTests:41`) — Checks `File.Exists` but never verifies content. The CamelCase test partially covers this (checks naming policy) but the primary save test doesn't verify data roundtrips.

**Minor findings:**
4. GoalsSave error guards (empty list, null PrPath) — untested, explains 10% gap
5. App corrupt JSON error path — untested, explains 20.7% gap
6. CorruptPrFile warning propagation — not asserted in GetGoals test
7. MergeFrom duplicate step text — HashSet consumed guard not exercised

### Coverage gaps
| Method | Coverage | Gap |
|--------|----------|-----|
| GoalsSave | 90.0% | Empty goals + null PrPath guards |
| App | 79.3% | Corrupt JSON catch block |
| Goals | 84.3% | File read error path |
| ResolveGoalCallPaths | 89.7% | Non-existent file path |

## Verdict: NEEDS FIXES
Send back to coder to strengthen the 3 major false-green assertions. Minor findings are nice-to-have.

## Code example — the pattern to fix

Before (false-green):
```csharp
var result = await _engine.RunAction(action, _engine.Context);
await Assert.That(result.Success).IsTrue();
// That's it — never checks what was actually saved/resolved
```

After (honest test):
```csharp
var result = await _engine.RunAction(action, _engine.Context);
await Assert.That(result.Success).IsTrue();
// Verify the actual outcome
var goalCall = (GoalCall)actions[0].Parameters[0].Value!;
await Assert.That(goalCall.PrPath).IsEqualTo("/.build/dosomething.pr");
```
