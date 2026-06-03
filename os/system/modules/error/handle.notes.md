`error.handle` is the **modifier** that wraps an action with recovery — it goes inside the host action's `modifiers` array, NEVER as a top-level peer.

When the step text has a trailing `on error <recovery>` clause (in ANY natural language — "on error", "if it fails", "catch error" and their equivalents in other languages all count), the recovery action goes INSIDE `error.handle.Actions` (a list of action records). Never drop the clause.

Three failure modes to avoid:

1. **Don't duplicate** — the recovery action appears ONCE, inside `Actions`. Never also as a top-level peer (a peer would run it unconditionally every execution, defeating "on error").
2. **Don't stuff recovery content into `Key`/`Message`** — `Key` filters by a named error key (e.g. `"Conflict"`, `"404"`), `Message` by a message substring. The recovery's content (a goal name, variable, `"true"`) is never a filter. If the step names no filter (`key X`, a status code, a message match), leave `Key`/`Message`/`StatusCode` absent — don't invent one.
3. **Don't fill the host's callback slots** (`OnToolCall`, `OnValidateResponse`, `OnStream`) with the error handler — those are unrelated parameters.

`write 'hi' to logger, on error set %writeFailed% = true`:
`output.write(Data="hi", channel="logger") | error.handle(Actions=[variable.set(Name=%writeFailed%, Value=true)])`
