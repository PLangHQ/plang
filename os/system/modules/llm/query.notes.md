`llm.query system=…, user=…` → single `Messages` parameter of type `list<message>`:

```json
[{"Role":"system","Content":"…"},{"Role":"user","Content":"…"}]
```

`schema=…` → `Schema` parameter. If JSON-shaped, set `"type": {"name": "json"}` and emit as a structured object (not a string containing JSON).

Conversation — the earlier llm.query response this call continues · say: `continue the conversation`, `continue %a%` (in any wording) · ask: does the step say to continue an EARLIER llm answer? if it does, which variable holds that earlier llm response; if it does not, the answer is none — a variable the step merely passes as data to work on (a list to sort, a text to translate) is NOT a conversation to continue · builder: `Conversation=%x%`, where %x% is the earlier llm response the step names, else the variable the nearest earlier llm.query in the goal wrote to (never `{continue: true}` or a bare `true`); left out when the step continues no earlier llm answer
