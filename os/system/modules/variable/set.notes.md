When the **intent** is to assign a value to a named variable with an "only-if-unset" flag (`set %x% = … as default`, "default to", and equivalents in other languages), use plain `variable.set` plus `{"name":"AsDefault","value":true,"type":"bool"}`.

The action for variable assignment is ALWAYS `variable.set` — **never** `code.setDefault`. `code.setDefault` selects a default *provider* (signing/crypto/identity/key role) and has nothing to do with assigning a value to a `%variable%`.

`set <X> = <Y>` is always ONE `variable.set`. The `Type` parameter, when present, follows the structured `type` shape documented in the user message's "Type reference" block — emit only when the step text gives an explicit `as <name>[/<kind>] [strict]` or `(<kind>)` annotation, otherwise omit and let the runtime infer.

## `<action>, write to %x%` — chain shape

When a step ends with `, write to %x%`, emit the producer + a trailing peer `variable.set` (NEVER a modifier; the runtime rejects `variable.set is not a modifier`):

```
producer.action(...) | variable.set(Name=%x%, Value=%!data%[, Type?={...}])
```

`Type` is optional — include only when the write-target has an explicit `(<kind>)` tag or `as <name>` clause; otherwise omit.

The trailing `Value`'s `type` MUST match the producer's `→ returns T`:

| Producer | `→ returns` | Trailing `Value` `type` |
|---|---|---|
| `list.count` | `number` | `"number"` |
| `list.contains` / `list.any` / `variable.exists` | `bool` | `"bool"` |
| `list.join` / `ui.render` | `text` | `"text"` |
| `list.split` | `list` | `"list"` |
| `file.exists` | `path` | `"path"` |
| `crypto.hash` | `bytes` | `"bytes"` |
| `llm.query` | `object` | `"object"` *(genuinely polymorphic)* |

Fall back to `"object"` ONLY when the producer's catalog entry has no `→ returns` line. Do NOT emit a separate `Type` parameter on the trailing `variable.set` — the producer's `Data<T>` carries the type.
