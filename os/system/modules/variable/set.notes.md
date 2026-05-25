When the **intent** is to assign a value to a named variable with an "only-if-unset" flag (PLang `set %x% = … as default`, "default to", and equivalents in other languages), use plain `variable.set` plus `{"name":"AsDefault","value":true,"type":"bool"}`.

The action for variable assignment is ALWAYS `variable.set` — **never** `code.setDefault`. `code.setDefault` selects a default *provider* (signing/crypto/identity/key role) and has nothing to do with assigning a value to a `%variable%`. Match by the intent (assigning to a variable), not by surface words like "default" in the step text.

## Set shapes — one `variable.set` per step, no helper actions

PLang actions handle whole clauses. `set <X> = <Y>` is always ONE `variable.set`:

- `set %x% = 'literal'` → `parameters: [{Name:"%x%",type:"variable"},{Value:"literal",type:"string"}]`.
- `set %x% = '%greeting% %name%'` (template with `%var%` interpolations) → `Value="%greeting% %name%"` with `type:"tstring"`.
- `set %x% = 5` → scalar — no math action needed.
- `set %x% = %y%` (Value is a single `%var%` reference, not a literal) → `Type="object"`. The runtime resolves `%y%` at dispatch and infers the actual type. **NEVER pick `Type="string"` for a bare `%var%` reference** — if `%y%` resolves to a non-string at runtime the runtime fails with `PrimitiveConversionFailed`. Only override when the step text explicitly expresses an intent like "as text" / "as string" / "som streng" / "sem texta" → then `Type="string"`. Same for explicit `int`/`bool`/`json`/etc. intent. Match by INTENT, not surface words.

## `<action>, write to %x%` — chain shape

When a step ends with `, write to %x%` (or `write to %x%`, or any "capture the result" phrasing), emit a producer action + a trailing peer `variable.set`:

```
producer.action(...) | variable.set(Name=%x%, Value=%!data%)
```

The trailing `variable.set` is a **peer**, NEVER a modifier on the producer (the runtime rejects `variable.set is not a modifier`).

### The trailing Value's `type` MUST match the producer's `→ returns T`

This is the biggest source of typing drift in compile snapshots. Before you emit the trailing `variable.set`, **look up the producer's `→ returns T` line in the Action Detail section above** and use that exact `T` as the `type` on the `Value` parameter. Do not default to `"object"` when the catalog has told you a more specific type.

Worked examples — match the producer's returns line, copy the `T`:

| Producer step | Producer's `→ returns` | Trailing `variable.set Value` type |
|---|---|---|
| `list.count %xs%, write to %n%` | `int` | `"int"` |
| `list.contains %xs% has 'foo', write to %has%` | `bool` | `"bool"` |
| `list.any %xs% where Status equals 'Fail', write to %hasFail%` | `bool` | `"bool"` |
| `list.join %xs% with ", ", write to %csv%` | `string` | `"string"` |
| `list.split "a,b" by ",", write to %parts%` | `list` | `"list"` |
| `variable.exists %x%, write to %ok%` | `bool` | `"bool"` |
| `file.exists 'foo.txt', write to %present%` | `path` | `"path"` |
| `ui.render template, write to %html%` | `string` | `"string"` |
| `crypto.hash %data%, write to %h%` | `byte[]` | `"byte[]"` |
| `llm.query …, write to %r%` | `object` | `"object"` *(genuinely polymorphic)* |
| `file.read 'data.txt', write to %c%` | *(no `→ returns` line)* | `"object"` *(fall back)* |

Rule of thumb: `"object"` is the fallback ONLY when the producer's catalog entry has no `→ returns` line. If the line is present, the LLM is dropping it.

Do **NOT** emit a separate `Type` parameter on the trailing `variable.set` — the `Data<T>` wrapper from the producer carries the type at runtime; a `Type` param forces a coercion that fights the typed return. (`Type` on `variable.set` is reserved for explicit user-supplied intent like `set %x% = "hi", type=string`.)

Correct (`llm.query system=…, user=…, write to %result%` — `llm.query` returns `object`):

```json
{"actions": [
  {"module":"llm","action":"query","parameters":[...]},
  {"module":"variable","action":"set","parameters":[
    {"name":"Name","value":"%result%","type":"variable"},
    {"name":"Value","value":"%!data%","type":"object"}
  ]}
]}
```

Correct (`list.count %tests%, write to %count%` — `list.count` returns `int`):

```json
{"actions": [
  {"module":"list","action":"count","parameters":[{"name":"ListName","value":"%tests%","type":"variable"}]},
  {"module":"variable","action":"set","parameters":[
    {"name":"Name","value":"%count%","type":"variable"},
    {"name":"Value","value":"%!data%","type":"int"}
  ]}
]}
```
