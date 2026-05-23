When a step says `call X, param=value` (whether inside `foreach` or standalone), the named parameters are passed to the called goal as ITS parameters — they belong **inside `GoalName.value.parameters`**, NOT as top-level parameters on the `goal.call` action.

The top-level `goal.call` action has only a few real slots:
- `GoalName` (required) — `{"name": "X", "parameters": [...]}` where `parameters` carries the args passed to X.
- `Actor` (optional, almost always null) — for actor delegation, not for passing arguments. **Do NOT put `%goal%` or any iteration variable here.**

For `foreach %list%, call X, section=%item%` the action set is `loop.foreach` + `goal.call`. The `section=%item%` goes inside `goal.call`'s `GoalName.parameters`:

```json
{"name":"GoalName","value":{"name":"X","parameters":[{"name":"section","value":"%item%","type":"string"}]},"type":"goal.call"}
```

### Payload `name` is a goal identifier — never a CLR type or schema token

The `{"name": "..."}` payload is the goal name **verbatim from the step text**. Common wrong shapes:

- `"App.Goals.Goal.GoalCall"` — CLR class FullName. Wrong.
- `"goal.call"` — the PLang **type descriptor** for this very parameter. The VALUE goes inside `{"name": "..."}` and must be the user's goal identifier from the step text. Never copy the type token into the value slot.
- Any dotted identifier that's a type rather than a goal.
