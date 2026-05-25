## Operator catalog — runtime-supported only

The Operator enum in Type Information lists what the runtime recognizes:
`==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, and, or`.

There is NO `isnotempty`, `istrue`, `isfalse`, `isnull`, `isnotnull`. Emitting them throws `NullReferenceException` at runtime. Map common natural-language predicates as follows:

| Step text phrasing | Operator | Right | Negate |
|---|---|---|---|
| `if %x% is empty` | `isempty` | — (omit) | — (omit) |
| `if %x% is not empty` / `is not blank` | `isempty` | — (omit) | `true` |
| `if %x% is true` | `==` | `true` | — |
| `if %x% is false` | `==` | `false` | — |
| `if %x% is null` | `==` | `null` | — |
| `if %x% is not null` | `!=` | `null` | — |
| `if %a% equals %b%` | `==` | `%b%` | — |
| `if %a% does not equal %b%` | `!=` | `%b%` | — |
| `if %xs% contains 'foo'` | `contains` | `'foo'` | — |
| `if %xs% does not contain 'foo'` | `contains` | `'foo'` | `true` |

`Negate` is the universal way to invert any operator's result. Use it whenever the runtime doesn't have a dedicated inverse (e.g. `isempty` has no `isnotempty` cousin — `isempty` + `Negate=true` is the way).

## Omit `Right` and `Negate` when not needed

`Right` is meaningful for binary operators only (`==, !=, >, <, contains, startswith, endswith, in`). For the unary `isempty`, omit `Right` entirely — do NOT emit `Right=%!data%` or `Right=false` as a placeholder.

`Negate` defaults to false. Omit it entirely when not negating — do NOT emit `Negate=false` as a placeholder.

Correct (`if %content% is not empty, call DoWork`):

```json
{"module":"condition","action":"if","parameters":[
  {"name":"Left","value":"%content%","type":"object"},
  {"name":"Operator","value":"isempty","type":"operator"},
  {"name":"Negate","value":true,"type":"bool"}
]}
```

formal: `condition.if(Left=%content%, Operator="isempty", Negate=true)` — no `Right`.

Correct (`if %!build.summary% is true, call EmitSummary`):

```json
{"module":"condition","action":"if","parameters":[
  {"name":"Left","value":"%!build.summary%","type":"object"},
  {"name":"Operator","value":"==","type":"operator"},
  {"name":"Right","value":true,"type":"bool"}
]}
```

formal: `condition.if(Left=%!build.summary%, Operator="==", Right=true)` — no `Negate`.

## `condition.if` is a peer, not a modifier

The action AFTER an `if X, ...` clause is a separate peer entry in the top-level `actions` array — the condition decides whether subsequent actions run, but it doesn't wrap them as modifiers. Putting `goal.call` (or any non-modifier) into `condition.if`'s `modifiers` array trips "goal.call is not a modifier" at runtime.

## Compound conditions

Compound conditions split into multiple `condition.if` instances. `if %a% > 1 and %b% < 10, call DoThing` → TWO `condition.if` actions plus the `goal.call`. The planner's set lists `condition.if` once; the compiler expands. Flag the expansion in `warnings`:

```json
{"warnings": [{"key": "expanded-condition", "message": "condition.if expanded to two actions for compound condition"}]}
```
