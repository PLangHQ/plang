**Omit `Message` unless the step text names a custom error message.** `Expected` is taken from the step text literal — do NOT duplicate it into `Message`, and do NOT write `Message=%!data%`. `Expected` carries the real literal, never `%!data%`.

Correct (`assert %message% equals 'hello plang'`) — no `Message`:
```json
{"module":"assert","name":"equals","property":[
  {"name":"Expected","value":"hello plang","type":{"name":"text"}},
  {"name":"Actual","value":"%message%","type":{"name":"item"}}
]}
```

Emptiness is an equality check against the empty value: `is empty` → `equals` with `Expected` empty. There is no `assert.isEmpty`.
