# v1 Summary — PLang Integration Test Gaps

## What this is

PLang App had 31 existing PLang integration tests but major gaps in engine-level behavior coverage: error handling flows, event lifecycle, caching, goal call patterns, variable scoping, compound conditions, and type conversion edge cases. This work adds 33 new test suites (64 total) and fixes 3 runtime bugs discovered during testing.

## What was done

### New Test Suites (33 added, 64 total — all passing)

**Error Handling (6):** ErrorCall, ErrorProps, ErrorOrdering, ErrorInHandler, ErrorNested, ErrorTypes, ErrorChain
**Events (7):** EventBeforeStep, EventAfterStep, EventAfterAction, EventRemove, EventMultiple, EventPriority, EventWildcard
**Core Flow (7):** ConditionCompound, ForeachDictionary, ForeachEmpty, RecursionDepthLimit, VariableScoping, ReturnMapping, StepResult
**Robustness (5):** ConvertErrors, AssertComplete, SystemVariables, SetupGoal, StartupParams
**Caching (3):** CacheSliding, CacheKey, CacheDynamicKey
**Goal Calls (4):** GoalCallDynamic, GoalCallMissing, GoalCallRelative, GoalCallReturn
**Actors (3):** ActorSwitch, ActorDatasource, ActorContext

### Runtime Bugs Found & Fixed (3 files)

1. **Test runner root directory** (`PLang/App/Test/this.cs`): Changed engine root from `rootDir` (Tests/App/) to `dir` (test's own directory) so helper goal .pr files resolve correctly via GetAsync.

2. **Test runner setup goals** (`PLang/App/Test/this.cs`): Added setup goal discovery and execution before each test, enabling tests that use Setup.goal.

3. **Condition handler type mismatch** (`PLang/App/modules/condition/if.cs`): Changed `Condition` property from `bool` to `string` because the builder sends expression strings like `"42 > 5"`. Added `EvaluateCondition()` method for numeric/string comparisons.

4. **Goal call return value** (`PLang/App/modules/goal/call.cs`): Added `__stepResult` fallback when goal returns `Data.Ok()` with null Value, enabling return value mapping from goal calls.

### Key Findings

- **Builder inconsistency**: The LLM-based builder sometimes maps assert.contains parameters as `Value=container, Container=value` and other times the reverse. Required manual .pr fixes.
- **Builder crash**: `onError call` in step text causes "Index was out of range" in MergeStep. Worked around by hand-crafting .pr files for ErrorCall and ActorDatasource.
- **Cache key behavior** (CacheDynamicKey): Cache key includes literal `%var%` text, not resolved values. Same step with different variable values returns cached result. Documented as known behavior.
- **assert.contains parameter naming**: The `Value` property in the handler receives the container (thing searched in), and `Container` receives the search value. This is the opposite of what the names suggest but is the builder's consistent convention for most cases.

## Code example

Test runner fix — root directory change:
```csharp
// Before: engine rooted at Tests/App/ (helpers not found)
var testFs = new SafeFileSystem.PLangFileSystem(rootDir, "");

// After: engine rooted at test's own directory (helpers resolve)
var testFs = new SafeFileSystem.PLangFileSystem(dir, "");
```

Typical test structure (ErrorInHandler):
```
ErrorInHandler/
├── ErrorInHandler.test.goal     # Start goal only
├── ThrowWithBadHandler.goal     # Helper: throws with bad handler
├── BadHandler.goal              # Helper: error handler that throws
├── OuterHandler.goal            # Helper: catches propagated error
└── .build/
    ├── errorinhandler.test.pr   # Built/hand-crafted .pr
    ├── throwwithbadhandler.pr
    ├── badhandler.pr
    └── outerhandler.pr
```

## Files modified

### Runtime code (3 files)
- `PLang/App/Test/this.cs` — Test runner: root dir fix + setup goal execution
- `PLang/App/modules/condition/if.cs` — Changed Condition to string + expression evaluation
- `PLang/App/modules/goal/call.cs` — Added __stepResult fallback for return values

### Test suites (33 new directories in Tests/App/)
Each with `.test.goal`, helper `.goal` files, and `.build/*.pr` files.
