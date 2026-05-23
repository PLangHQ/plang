For `if %x% is not empty` (and equivalents like "is not blank") use the dedicated `isnotempty` operator. Don't compose `isempty` + `Right=false` — `Right` is ignored for unary operators and the negation gets silently dropped.

For other negated forms like "is not equal", use `!=` directly. Negate is only needed when there's no dedicated inverse operator.

**Omit `Right` and `Negate` from `parameters` and from `formal` when the operator is unary** (`isnotempty`, `isempty`, `isnull`, `isnotnull`, `istrue`, `isfalse`). Do NOT emit `Right=%!data%` or `Negate=false` as placeholders — formal mirrors parameters exactly, and unary operators take neither.

Correct (`if %content% is not empty, call DoWork`):
```json
{"module":"condition","action":"if","parameters":[
  {"name":"Left","value":"%content%","type":"object"},
  {"name":"Operator","value":"isnotempty","type":"string"}
]}
```
formal: `condition.if(Left=%content%, Operator="isnotempty")` — no `Right`, no `Negate`.

`condition.if` is a **peer**, not a modifier. The action AFTER an `if X, ...` clause is a separate peer entry in the top-level `actions` array — the condition decides whether subsequent actions run, but it doesn't wrap them as modifiers. Putting `goal.call` (or any non-modifier) into `condition.if`'s `modifiers` array trips "goal.call is not a modifier" at runtime.

Compound conditions split into multiple `condition.if` instances. `if %a% > 1 and %b% < 10, call DoThing` → TWO `condition.if` actions plus the `goal.call`. The planner's set lists `condition.if` once; the compiler expands. Flag the expansion in `warnings`:

```json
{"warnings": [{"key": "expanded-condition", "message": "condition.if expanded to two actions for compound condition"}]}
```
