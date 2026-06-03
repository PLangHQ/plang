## Operators — runtime-supported only

Valid `Operator` values: `==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, and, or`. There is NO `isnotempty`/`istrue`/`isfalse`/`isnull`/`isnotnull` — emitting one throws at runtime. Map predicates:

| phrasing | Operator | Right | Negate |
|---|---|---|---|
| `is empty` | isempty | omit | omit |
| `is not empty` / `not blank` | isempty | omit | true |
| `is true` / `is false` | == | true / false | — |
| `is null` / `is not null` | == / != | null | — |
| `equals %b%` / `does not equal %b%` | == / != | %b% | — |
| `contains 'foo'` / `does not contain 'foo'` | contains | 'foo' | — / true |

`Negate=true` inverts ANY operator — the only way to negate ones with no inverse (`isempty`, etc.).

## Omit `Right`/`Negate` when not applicable

`Right` is for binary operators only — omit it for unary `isempty` (never `Right=%!data%` or `Right=false`). `Negate` defaults false — omit when not negating.

- `if %content% is not empty` → `condition.if(Left=%content%, Operator="isempty", Negate=true)` (no Right)
- `if %flag% is true` → `condition.if(Left=%flag%, Operator="==", Right=true)` (no Negate)

## Compound `and`/`or`

Multiple top-level `condition.if` in ONE step do NOT compound — the runtime treats them as an if/elseif/else chain (first match wins). For `if A and B, call X`: evaluate each side with `condition.compare`, capture each `%!data%` into a var, then ONE `condition.if` with `Operator="and"` (or `"or"`) over those vars, then the body. `and`/`or` are truthy checks on the pre-computed booleans, not inline expressions. 3+ operands: stage into `%v1%`, `%v2%`, … and nest.

`if %a% > 1 and %b% < 10, call DoThing`:
`condition.compare(Left=%a%, Operator=">", Right=1), variable.set(Name=%andL%, Value=%!data%), condition.compare(Left=%b%, Operator="<", Right=10), variable.set(Name=%andR%, Value=%!data%), condition.if(Left=%andL%, Operator="and", Right=%andR%), goal.call(GoalName={name:"DoThing"})`
