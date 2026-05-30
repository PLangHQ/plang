**Omit `Message` from `parameters` and from `formal` unless the step text names a custom error message.** `Expected` is taken from the step text literal — do NOT duplicate it into `Message`, and do NOT write `Message=%!data%`.

`Expected` value MUST match between `formal` and `parameters`. The shape `formal='assert.equals(Expected=%!data%, …)'` while `parameters` carries the real literal in `Expected` is forbidden — formal mirrors parameters exactly.

Correct (`assert %message% equals 'hello plang'`):
```json
{"module":"assert","action":"equals","parameters":[
  {"name":"Expected","value":"hello plang","type":{"name":"text"}},
  {"name":"Actual","value":"%message%","type":{"name":"object"}}
]}
```
formal: `assert.equals(Expected="hello plang", Actual=%message%)` — no `Message`.

Correct (`assert %message% equals 'hello plang', message: "greeting wrong"`):
```json
{"module":"assert","action":"equals","parameters":[
  {"name":"Expected","value":"hello plang","type":{"name":"text"}},
  {"name":"Actual","value":"%message%","type":{"name":"object"}},
  {"name":"Message","value":"greeting wrong","type":{"name":"text"}}
]}
```
