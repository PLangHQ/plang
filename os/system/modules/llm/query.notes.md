`llm.query system=…, user=…` → single `Messages` parameter of type `list<message>`:

```json
[{"Role":"system","Content":"…"},{"Role":"user","Content":"…"}]
```

`schema=…` → `Schema` parameter. If JSON-shaped, set `"type": {"name": "json"}` and emit as a structured object (not a string containing JSON).

Conversation — the earlier llm.query response this call continues · say: `continue the conversation` (in any wording) · ask: which of the step's variables holds the earlier response the step continues? · builder: `Conversation=%x%`, where %x% is the response the step names, else the variable the nearest earlier llm.query in the goal wrote to — it continues that response directly (never `{continue: true}` or a bare `true`); left out, the query starts afresh
