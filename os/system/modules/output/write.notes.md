`output.write` has a `channel` parameter for routing to a non-default channel. The channel clause names an EXISTING registered channel — it is routing, not registration.

**Only emit `channel` when the step text expresses intent to send output to a named channel** (any phrasing: `channel=X`, `to X channel`, `on X channel`, `via X`, etc.). When the step text has no channel clause, OMIT `channel` from both `parameters` and from `formal`. Do NOT fill it with `%!data%` or any placeholder — `%!data%` is the prior action's result for chaining into intended slots, never a fallback for unnamed optional slots.

Correct (`write out %message%`, no channel named):
```json
{"module":"output","action":"write","parameters":[{"name":"Data","value":"%message%","type":"item"}]}
```

Correct (`write out "hi" to logger channel`):
```json
{"module":"output","action":"write","parameters":[
  {"name":"Data","value":"hi","type":"text"},
  {"name":"channel","value":"logger","type":"text"}
]}
```

**DO NOT** emit a peer `channel.set` action when the user names a routing channel — `channel.set` is for *registering* a channel handler, never for routing on `output.write`.

## Interpolation is automatic; mark user-facing prose for translation

Interpolation needs no special type. A literal with `%var%` holes interpolates at runtime automatically (the authored template is stamped when the `.pr` loads) — there is no "template" type. A plain or routing literal uses `type:"text"`: `{"name":"Data","value":"row %i%","type":"text"}`. A bare `%var%` reference uses `type:"item"` (the apex/untyped type; runtime resolves the actual type).

User-facing **prose** (a string a human reads) is *translatable* — mark it on the text type so it can be routed through the translator at the output edge:

`write out "Hello %name%"` → `{"name":"Data","value":"Hello %name%","type":{"name":"text","translate":true}}`

Translation is orthogonal to interpolation: the authored template is translated first, then the holes are filled. Do NOT mark routing tokens, debug strings, or identifiers translatable.
