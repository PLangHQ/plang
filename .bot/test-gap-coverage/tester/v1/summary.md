# Test Gap Coverage — Tester v1 Summary

## What this is
Added 6 new PLang integration test suites covering previously untested module actions. These tests validate the full pipeline: LLM builder → .pr generation → GoalMapper → runtime.

## What was done

### Prerequisite fix
- Changed `"OpenApiKey"` to `"OPENAI_API_KEY"` in `PLang/Services/LlmService/OpenAiService.cs:86`
- Rebuilt PlangConsole, copied system dir to `Tests/App/system`

### New test suites (all passing)
| Test | Actions Covered | Files |
|------|----------------|-------|
| GoalCall | `goal.call` with params + return | `Tests/App/GoalCall/GoalCall.test.goal`, `Greet.goal` |
| VariableOps | `variable.exists`, `variable.remove`, `variable.clear` | `Tests/App/VariableOps/VariableOps.test.goal` |
| ContextVars2 | `%!goal.Name%`, `%!step%`, `%!context%`, `%!fileSystem%`, `%!callStack%` | `Tests/App/ContextVars2/ContextVars2.test.goal` |
| Convert2 | `convert.toDouble`, `convert.toLong`, `convert.toDateTime` | `Tests/App/Convert2/Convert2.test.goal` |
| ListOps2 | `list.range`, `list.set`, `list.flatten` | `Tests/App/ListOps2/ListOps2.test.goal` |
| Math2 | `math.random` with range bounds | `Tests/App/Math2/Math2.test.goal` |

### Dropped: ErrorHandling2
The LLM builder is unreliable at generating the `onError` step property. Out of 4 build attempts, only 1 produced correct `onError`. Since the existing ErrorHandling test already covers `on error ignore`, this test was dropped.

## Test results
- **PLang tests**: 29/29 passed (22 existing + 7 new — GoalCall has 2 goal files)
- **C# tests**: 1500/1500 passed

## Builder issues discovered
1. **`onError` unreliability** — The LLM builder inconsistently generates `onError` step properties. The same goal text produces different results across builds. This is a significant gap for error handling test coverage.
2. **`%var.property%` stripping** — The builder sometimes drops `.property` suffixes from variable references (e.g., `%rangeResult.count%` → `%rangeResult%`). Workaround: use `set %extracted% = %obj.prop%` before asserting.
3. **Stale .pr caching** — After changing a .goal file, rebuilding doesn't always pick up changes even though the hash differs. Workaround: delete the .pr file before rebuilding.
4. **Build directory sensitivity** — Must build from `Tests/App/` root. Building from a subdirectory makes paths relative to that subdirectory, breaking runtime goal resolution.

## Code example
```plang
Start
/ Test: variable exists after set
- set %myVar% = "hello"
- check if variable %myVar% exists, write to %existsResult%
- assert %existsResult.exists% is true
/ Test: variable remove
- remove variable %myVar%
- check if variable %myVar% exists, write to %goneResult%
- assert %goneResult.exists% is false
```

Note: `variable.exists` returns a `types.variable` wrapper, not a raw bool. Must access `.exists` property.
