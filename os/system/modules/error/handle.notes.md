`error.handle` is the **modifier** that wraps an action with recovery — it goes inside the host action's `modifiers` array, NEVER as a peer in top-level `actions`.

When the step text says `<main>, on error <recovery>` (or `on error call X`), the recovery action goes INSIDE `error.handle.Actions` as a list of action records.

Three failure modes to avoid:

1. **Don't duplicate** — if `variable.set` is the recovery, it appears ONCE inside `Actions`. Never also as a top-level peer. A duplicate peer would run the recovery unconditionally on every step execution, defeating the whole "on error" intent.
2. **Don't stuff recovery content into `Key` / `Message`** — `Key` filters by a named error key (e.g. `"Conflict"`, `"404"`); `Message` filters by error message substring. The recovery clause's content (variable name, "true", goal name) is NEVER what `Key` or `Message` should contain. If the step text uses `on error <verb> ...` with no filter clause (no `key X`, no status code, no message match), DO NOT invent a filter — leave `Key`/`Message`/`StatusCode` absent.
3. **`on error call FailureGoal`** routes to an `error.handle` modifier wrapping the host, with `goal.call(GoalName={name:"FailureGoal"})` inside `Actions`. NEVER fill the host's own callback slots (`OnToolCall`, `OnValidateResponse`, `OnStream`) with the error handler — those are normal action parameters for unrelated purposes, and silent misbehaviour results.

Example — `write 'hi' to logger, on error set %writeFailed% = true`:

```json
{"actions": [
  {"module": "output", "action": "write",
    "parameters": [{"name": "Data", "value": "hi", "type": "object"},
                   {"name": "channel", "value": "logger", "type": "string"}],
    "modifiers": [
      {"module": "error", "action": "handle",
        "parameters": [{"name": "Actions",
          "value": [{"module": "variable", "action": "set",
            "parameters": [{"name": "Name", "value": "%writeFailed%", "type": "string"},
                           {"name": "Value", "value": true, "type": "bool"}]}],
          "type": "list<action>"}]}
    ]}
]}
```
