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

## Compound conditions — `condition.compare` + single `condition.if` with `and`/`or`

Multiple top-level `condition.if` instances in the same step do NOT compound — the runtime treats them as an **if/elseif/else chain** (first match wins, others skipped). To express `if A and B, call X` you must:

1. Evaluate each sub-expression with `condition.compare`
2. Capture each `%!data%` into a named variable via `variable.set`
3. A single `condition.if` with `Operator="and"` (or `"or"`) referencing those variables
4. The body action

`if %a% > 1 and %b% < 10, call DoThing`:

```json
[
  {"module":"condition","action":"compare","parameters":[
    {"name":"Left","value":"%a%","type":"object"},
    {"name":"Operator","value":">","type":"operator"},
    {"name":"Right","value":1,"type":"object"}]},
  {"module":"variable","action":"set","parameters":[
    {"name":"Name","value":"%andL%","type":"string"},
    {"name":"Value","value":"%!data%","type":"object"}]},
  {"module":"condition","action":"compare","parameters":[
    {"name":"Left","value":"%b%","type":"object"},
    {"name":"Operator","value":"<","type":"operator"},
    {"name":"Right","value":10,"type":"object"}]},
  {"module":"variable","action":"set","parameters":[
    {"name":"Name","value":"%andR%","type":"string"},
    {"name":"Value","value":"%!data%","type":"object"}]},
  {"module":"condition","action":"if","parameters":[
    {"name":"Left","value":"%andL%","type":"object"},
    {"name":"Operator","value":"and","type":"operator"},
    {"name":"Right","value":"%andR%","type":"object"}]},
  {"module":"goal","action":"call","parameters":[
    {"name":"GoalName","value":{"name":"DoThing"},"type":"goal.call"}]}
]
```

The `and` / `or` operators are truthy checks on Left and Right — both must be pre-computed booleans, not raw inline expressions. Use `or` analogously. Three or more operands chain: stage each into `%v1%`, `%v2%`, ..., then nest two-operand `and`/`or` `condition.if`s with intermediate variables.
