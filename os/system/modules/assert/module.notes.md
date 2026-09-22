**Omit `Message` unless the step text names a custom error message.** `Expected` is taken from the step text literal — do NOT duplicate it into `Message`, and do NOT write `Message=%!data%`. `Expected` carries the real literal, never `%!data%`.

Correct (`assert %message% equals 'hello plang'`) — no `Message`:
```json
{"module":"assert","action":"equals","parameters":[
  {"name":"Expected","value":"hello plang","type":{"name":"text"}},
  {"name":"Actual","value":"%message%","type":{"name":"object"}}
]}
```

Correct (`assert %message% equals 'hello plang', message: "greeting wrong"`):
```json
{"module":"assert","action":"equals","parameters":[
  {"name":"Expected","value":"hello plang","type":{"name":"text"}},
  {"name":"Actual","value":"%message%","type":{"name":"object"}},
  {"name":"Message","value":"greeting wrong","type":{"name":"text"}}
]}
```
