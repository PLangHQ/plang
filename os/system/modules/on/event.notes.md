`on.event` is the action behind a step that LEADS with "before …", "after …" or "on <event> …" and names what to call when it happens ("before each step call X", "after write on 'audit' channel call Y"). It is a binding: the call runs when the event fires, not now. A step that leads with `call` is `goal.call`, even when a name in it says "event", "before" or "after". An "on error …" clause after an action is `on.error`, not this.

`Item` is what the event belongs to, written as the path that reaches it — read it off what the step names:
- the step names a channel ("on "input" channel", "on the audit channel") → that channel: `%!channels.input%`, `%!channels.audit%` — never a goal, step or action type
- the step names an action as module.action ("action file.read", "action variable.set", "action output.write") → that action: `%!app.module.file.read%`, `%!app.module.variable.set%`, `%!app.module.output.write%` — not `%!app.type.action%`, which is every action
- "each goal" / "each step" / "each action" → `%!app.type.goal%`, `%!app.type.step%`, `%!app.type.action%`
- one goal by name → `%!app.goal["/Name"]%`
- every action of a module → `%!app.module.http%`

`When` is `before` or `after`. `Event` is the item's verb:
- a channel's event is always written: `write`, `read` or `ask` ("before write on … channel" → `Event: "write"`)
- a goal, step or action's event is `start` — left out, it is the default. "after action output.write" is the action's start, not a write.

The called goal reads `%!event%` (the event), `%!event!item%` (what it fired for) and `%!event!result%` (the result so far, after it). A call bound before an event can cancel it with `on.cancel`.
