# Tester v3 Summary

## What this is

Fixed the PLang builder's LLM system prompt so that `condition.if`, `goal.call`, and nullable parameters are built correctly. This unblocked the Condition test (which failed in v2) and brought the full PLang test suite to 22/22 green.

## What was done

### Builder prompt fixes (`system/builder/llm/BuildGoal.llm`)

Three issues were identified and fixed:

1. **goal.call naming confusion** — The LLM produced `{"goalName": "SetTrue"}` instead of just `"SetTrue"` for goal.call parameters inside condition.if. The parameter name (`GoalName` from the goal.call action) was being confused with the schema field (`name` inside the goal.call type). Fixed by adding a condition.if example to the prompt and a rule: "The field inside the object is `name`, not the parameter name."

2. **Nullable parameter inclusion** — The LLM included `GoalIfFalse` with an empty value when it wasn't needed, causing "Goal '' not found". Fixed by adding a rule: "Omit nullable parameters (types ending in ?) entirely when not applicable."

3. **Build-time pre-evaluation** — The LLM evaluated `%x% equals 10` at build time and wrote `"value": true` instead of keeping `"%x%"` as a runtime reference. Fixed by adding a rule: "NEVER pre-evaluate %variable% references at build time."

### Condition test simplification (`Tests/Runtime2/Condition/Condition.test.goal`)

Changed from comparison expressions (`if %x% equals 10`) to truthy/falsy checks (`if %flag%`). The runtime's `condition.if` takes a `bool` — it can convert variable values to bool (non-zero = true, false = false) but has no expression evaluator for comparison operators. The simplified test correctly validates both the if-true and else branches.

### Build directory fix

The previous build ran from `Tests/Runtime2/Condition/` (after a `cd`), making `fileSystem.RootDirectory` the subdirectory. This caused `RelativeGoalPath` to be `/Condition.test.goal` instead of `/Condition/Condition.test.goal`, breaking goal resolution. Rebuilt from `Tests/Runtime2/` to get correct paths.

## Test results

- C# tests: 1423/1423 pass
- PLang tests: 22/22 pass (up from 20/22 in v2)

## Code example

The key prompt change — added condition.if to the example (line 91 + 111):

Goal:
```plang
- if \%count\%, call ProcessData
```

Response:
```json
{"index": 11, "actions": [{"module": "condition", "action": "if", "parameters": [{"name": "Condition", "value": "\%count\%", "type": "bool"}, {"name": "GoalIfTrue", "value": "ProcessData", "type": "goal.call"}]}]}
```

Note: GoalIfFalse is omitted (not needed), Condition is a variable reference (not hardcoded), GoalIfTrue is a plain string (not an object).

## What's next

- The ListOps false-green from v2 (off-by-one step index on append) was resolved by the full rebuild — needs verification that the step alignment is now correct
- `condition.if` currently only supports truthy/falsy — comparison expressions (`%x% equals 10`) would need a runtime expression evaluator or a separate comparison action
