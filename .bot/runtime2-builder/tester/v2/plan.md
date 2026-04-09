# Plan: Create missing PLang tests for App actions (Tester v2)

## Context

App has 15+ action modules but only partial PLang test coverage. PLang `.goal` tests validate the full pipeline (builder -> .pr generation -> GoalMapper -> runtime). Several modules have no dedicated test, and some existing tests miss actions.

## Tests to Create

### New test folders
1. **Condition** — if/else with goal calls
2. **File** — save, read, exists, copy, move, list, delete
3. **Output** — write (smoke test, can't assert console output)
4. **Assert** — notEquals, isFalse, isNull, greaterThan, lessThan

### Extend existing tests
5. **ListOps** — get, remove, indexOf, sort, reverse, unique
6. **Math** — power, sqrt, ceiling, floor, modulo
7. **Mock** — exercise mock intercept, verify, reset

## Key adjustments from plan review
- sort/reverse modify in-place — test by checking original list after operation
- unique returns NEW list — use `write to` for result
- mock.verify takes MockHandle + ExpectedCount
- mock.reset clears Calls list on handle

## Workflow
1. Create/modify all `.goal` files
2. Build: `cd Tests/App && plang p build --llmservice=openai`
3. Read generated `.pr` files — verify correct module/action/parameter mapping
4. Run: `plang p !test` — check results
5. Run: `dotnet run --project PLang.Tests` — C# tests still pass
