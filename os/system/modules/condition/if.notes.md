Left — the value tested, as the step writes it.
Operator — the comparison, one of choice<operator>. A negation is its own operator, never written anywhere else.
Right — what Left is compared to. Left out for isempty / isnotempty.

| the step says | Operator | Right |
|---|---|---|
| `is 5`, `equals %b%`, `is "x"`, `is true`, `is null` | == | the value |
| `is not 5`, `does not equal %b%`, `is not null` | != | the value |
| `is less than`, `is more than`, `is at least`, `is at most` | <, >, >=, <= | the value |
| `contains` / `does not contain` | contains / notcontains | the value |
| `starts with` / `does not start with` | startswith / notstartswith | the value |
| `ends with` / `does not end with` | endswith / notendswith | the value |
| `is in [..]` / `is not in [..]` | in / notin | the list |
| `is empty` / `is not empty` | isempty / isnotempty | left out |
| `is a number` / `is not a list` | is / isnot | the type name: number, list |

- `is` and `isnot` take only a type name; a value after "is" is `==`.
- There is no istrue, isfalse, isnull or isnotnull.
- `if A and B`: one condition.if cannot hold both. Compare each side with condition.compare, keep each result with variable.set, then one condition.if over the two with Operator `and` (or `or`).
