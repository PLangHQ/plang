When a step says `call X, param=value` (whether inside `foreach` or standalone), the named parameters are passed to the called goal as ITS parameters — they belong **inside `GoalName.value.parameters`**, NOT as top-level parameters on the `goal.call` action.

The top-level `goal.call` action has only a few real slots:
- `GoalName` (required) — `{"name": "X", "parameters": [...]}` where `parameters` carries the args passed to X.
- `Actor` (optional, almost always null) — for actor delegation, not for passing arguments. **Do NOT put `%goal%` or any iteration variable here.**

### `Actor` must come from the step text — never infer

`Actor` is reserved for explicit cross-actor delegation. **Omit it entirely** unless the step text *names* an actor for this call — typically with phrasing like:

- `call X on actor "logger"`
- `call X as %userActor%`
- `call X, actor=%audit%`

If the step text doesn't contain a phrase like that, the `Actor` parameter does not exist on the action. Do not invent a value:

- Don't fill `Actor` because the call lives in `/system/...` ("must be a system call") — the path tells you where the goal lives, not which actor to run it on.
- Don't fill `Actor` because the call is inside a sub-goal, a recovery handler, a foreach, or any other nested structure — nesting doesn't change actor identity.
- Don't fill `Actor` because the catalog *shows* the parameter — optional parameters with no value in the step text MUST be omitted, not stubbed with a guess.

The omission rule for optional parameters applies in full force here: no `"value": null`, no `"value": "system"`, no `"value": "%!actor%"`. Leave the slot out of `parameters`.

For `foreach %list%, call X, section=%item%` the action set is `loop.foreach` + `goal.call`. The `section=%item%` goes inside `goal.call`'s `GoalName.parameters`:

```json
{"name":"GoalName","value":{"name":"X","parameters":[{"name":"section","value":"%item%","type":"string"}]},"type":"goal.call"}
```

### Payload `name` is a goal identifier — never a CLR type or schema token

The `{"name": "..."}` payload is the goal name **verbatim from the step text** — copy the path exactly, whatever its form: `Goal`, `Folder/Goal`, `../X`, `/root/Y`. Keep every character of the path; dropping or rewriting a segment (e.g. `Folder/Goal` → `Folder`) points the call at a goal that doesn't exist and fails at runtime with NotFound.

Common wrong shapes:

- `"goal.call"` — the PLang **type descriptor** for this very parameter. The VALUE goes inside `{"name": "..."}` and must be the user's goal identifier from the step text. Never copy the type token into the value slot.
- Any dotted identifier that's a type rather than a goal.
