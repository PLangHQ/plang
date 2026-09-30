`on.event` is the action behind a step that LEADS with "before …", "after …" or "on <event> …" and names what to call when it happens ("before each step call X", "after write on 'audit' channel call Y"). It is a binding: the call runs when the event fires, not now. A step that leads with `call` is `goal.call`, even when a name in it says "event", "before" or "after". An "on error …" clause after an action is `on.error`, not this.

`Event` is the event itself, written as the path that reaches it: what it belongs to, then `.on.`, then its name. Read both off the step:
- what it belongs to:
  - a channel the step names ("on "input" channel", "the audit channel") → `%!channels.input%`, `%!channels.audit%`
  - an action the step names as module.action ("action file.read", "action variable.set") → `%!app.module.file.read%`, `%!app.module.variable.set%` — not `%!app.type.action%`, which is every action
  - "each goal" / "each step" / "each action" → `%!app.type.goal%`, `%!app.type.step%`, `%!app.type.action%`
  - one goal by name → `%!app.goal["/Name"]%`; every action of a module → `%!app.module.http%`
- its event: a goal, step or action's is `start` ("before each goal" → `%!app.type.goal.on.start%`; "after action output.write" is that action's start, not a write); a channel's is `write`, `read` or `ask` ("before write on … channel" → `%!channels.audit.on.write%`).

`When` is `before` or `after`.

The called goal reads `%!event%` (the event), `%!event!item%` (what it fired for) and `%!event!result%` (the result so far, after it). A call bound before an event can cancel it with `on.cancel`.

`on input call X` — "input" with no channel, event or "before/after" — is `on.input` (listen to this app's input line by line), not this.
