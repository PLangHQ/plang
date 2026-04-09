# Tester v2 Summary

## What this is

PLang .goal tests for App action modules that previously had zero or partial coverage. These tests validate the full pipeline: LLM builder -> .pr generation -> GoalMapper -> runtime execution.

## What was done

Created 4 new test suites and extended 3 existing ones:

### New (all pass except Condition)
- **File** (`Tests/App/File/File.test.goal`) — save, read, exists, copy, move, list, delete
- **Output** (`Tests/App/Output/Output.test.goal`) — write smoke test
- **Assert** (`Tests/App/Assert/Assert.test.goal`) — notEquals, isFalse, isNull, greaterThan, lessThan
- **Condition** (`Tests/App/Condition/`) — if/else with goal calls + 3 helper goals. **FAILS** due to builder issue.

### Extended (all pass, but ListOps has false green)
- **Math** — power, sqrt, floor, ceiling, modulo (all correct)
- **Mock** — exercise intercepted action, verify callCount, reset (all correct)
- **ListOps** — get, remove, indexOf, sort, reverse, unique (**false green** — actions misaligned)

### Test results
- C# tests: 1423/1423 pass (no regressions)
- PLang tests: 20/22 pass (Condition fails, ErrorHandling pre-existing)

## Builder issues found

Two builder system prompt problems prevent full test coverage:

1. **condition.if hardcodes condition value** — The LLM evaluates `%x% equals 10` at build time and writes `"value": true` instead of keeping it as a runtime expression. Also, optional goal.call params get included with empty value instead of being omitted, causing "Goal '' not found".

2. **Off-by-one step index when appending** — When new steps are added to an existing goal, the LLM returns action mappings shifted by one index. On re-build, the review approves the misaligned data because it doesn't verify step text matches action semantics.

## Code example

File.test.goal — representative of the pattern for all new tests:
```plang
Start
/ save and read
- save "hello world" to file 'test_output.txt'
- read file 'test_output.txt', write to %content%
- assert %content% equals "hello world"
/ exists
- check if file 'test_output.txt' exists, write to %info%
- assert %info% is not null
```

Generated .pr correctly maps to `file.save`, `file.read`, `assert.equals`, `file.exists`, `assert.isNotNull`.

## What's next

- Fix builder system prompt for condition.if expression handling
- Fix builder system prompt for off-by-one step index on append
- Once builder is fixed, rebuild Condition and ListOps tests
- ErrorHandling test failure needs separate investigation
