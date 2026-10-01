`llm.query system=…, user=…` → single `Messages` parameter of type `list<message>`:

```json
[{"Role":"system","Content":"…"},{"Role":"user","Content":"…"}]
```

`schema=…` → `Schema` parameter. If JSON-shaped, set `"type": {"name": "json"}` and emit as a structured object (not a string containing JSON).

`continue the conversation` (in any wording) → `Conversation={continue: %x%}`, where %x% is the earlier llm.query's response the conversation continues: the one the step names, else the variable the nearest earlier llm.query in the goal wrote to. Its messages go first. Never `{continue: true}`. When no earlier llm.query wrote a response, ask which conversation. Left out, the query starts afresh.
