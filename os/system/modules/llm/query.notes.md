`llm.query system=…, user=…` → single `Messages` parameter of type `list<llmmessage>`:

```json
[{"Role":"system","Content":"…"},{"Role":"user","Content":"…"}]
```

`schema=…` → `Schema` parameter. If JSON-shaped, set `"type": {"name": "json"}` and emit as a structured object (not a string containing JSON).

`continue the conversation` (in any wording) → `Conversation={continue: true}`: the earlier queries' messages go first. Left out, the query starts afresh.
