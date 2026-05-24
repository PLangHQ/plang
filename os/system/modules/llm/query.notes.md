`llm.query system=…, user=…` → single `Messages` parameter of type `list<llmmessage>`:

```json
[{"Role":"system","Content":"…"},{"Role":"user","Content":"…"}]
```

`schema=…` → `Schema` parameter. If JSON-shaped, set `"type": "json"` and emit as a structured object (not a string containing JSON).
