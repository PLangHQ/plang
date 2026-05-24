`output.write` has a `channel` parameter for routing to a non-default channel. The channel clause names an EXISTING registered channel — it is routing, not registration.

**Only emit `channel` when the step text expresses intent to send output to a named channel** (any phrasing: `channel=X`, `to X channel`, `on X channel`, `via X`, etc.). When the step text has no channel clause, OMIT `channel` from both `parameters` and from `formal`. Do NOT fill it with `%!data%` or any placeholder — `%!data%` is the prior action's result for chaining into intended slots, never a fallback for unnamed optional slots.

Correct (`write out %message%`, no channel named):
```json
{"module":"output","action":"write","parameters":[{"name":"Data","value":"%message%","type":"object"}]}
```

Correct (`write out "hi" to logger channel`):
```json
{"module":"output","action":"write","parameters":[
  {"name":"Data","value":"hi","type":"object"},
  {"name":"channel","value":"logger","type":"string"}
]}
```

**DO NOT** emit a peer `channel.set` action when the user names a routing channel — `channel.set` is for *registering* a channel handler, never for routing on `output.write`.
