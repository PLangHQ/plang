**Omit `Message` unless the step text names a custom error message.** `Expected` is taken from the step text literal — do NOT duplicate it into `Message`, and do NOT write `Message=%!data%`. `Expected` carries the real literal, never `%!data%`.

Correct (`assert %message% equals 'hello plang'`) — no `Message`:
```json
{"module":"assert","name":"equals","parameter":[
  {"name":"Expected","value":"hello plang","type":{"name":"text"}},
  {"name":"Actual","value":"%message%","type":{"name":"item"}}
]}
```

## Picking the action

There is no `assert.isEmpty` / `assert.isNotEmpty`. Emptiness is an equality check against the empty value:

| step says | action |
|---|---|
| `is empty` | `equals` with `Expected` empty |
| `is not empty` / `is present` / `has a value` | `notEquals` with `Expected` empty |
| `is true` / `is false` | `isTrue` / `isFalse` — ONLY for an actual boolean |
| `is null` / `is not null` | `isNull` / `isNotNull` |
| `equals X` / `does not equal X` | `equals` / `notEquals` |
| `contains X` / `does not contain X` | `contains` / `notContains` |
| `is greater than X` / `is less than X` | `greaterThan` / `lessThan` |

`isTrue` is not a catch-all for "the check passes" — it asserts that the value IS the boolean true. `assert %name% is not empty` is `notEquals`, not `isTrue`.
