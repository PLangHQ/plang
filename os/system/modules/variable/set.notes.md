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

Use the producer's declared return type (catalog shows `→ returns T`) as the `Value` parameter's `type` annotation: `{"name":"Value","value":"%!data%","type":"<T>"}`. Do **NOT** emit a separate `Type` parameter on the trailing `variable.set` — the `Data<T>` wrapper from the producer carries the type; a `Type` param forces a coercion that fights the typed return. (`Type` on `variable.set` is reserved for explicit user-supplied intent like `set %x% = "hi", type=string`.)

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
