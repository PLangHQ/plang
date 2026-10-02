Left — the first value compared · say: the value before the comparison · builder: as the step writes it
Operator — the comparison, a choice<operator> · say: `>`, `is`, `contains`, `is empty`, … (as in condition.if) · builder: a quoted text after one `=` (`Operator="=="`, never `Operator=="=="`); a negation is its own operator; left out for isempty / isnotempty. The table below maps the words.
Right — the second value · say: the value after the comparison · builder: left out for isempty / isnotempty

| the step says | Operator | Right |
|---|---|---|
| `is 5`, `equals %b%`, `is "x"`, `is true`, `is null` | `Operator="=="` | the value |
| `is not 5`, `does not equal %b%`, `is not null` | `Operator="!="` | the value |
| `is less than`, `is more than`, `is at least`, `is at most` | `Operator="<"`, `">"`, `">="`, `"<="` | the value |
| `contains` / `does not contain` | `Operator="contains"` / `"notcontains"` | the value |
| `starts with` / `does not start with` | `Operator="startswith"` / `"notstartswith"` | the value |
| `ends with` / `does not end with` | `Operator="endswith"` / `"notendswith"` | the value |
| `is in [..]` / `is not in [..]` | `Operator="in"` / `"notin"` | the list |
| `is empty` / `is not empty` | `Operator="isempty"` / `"isnotempty"` | left out |
| `is a number` / `is not a list` | `Operator="is"` / `"isnot"` | the type name: number, list |

- `is` and `isnot` take only a type name; a value after "is" is `==`.
- There is no istrue, isfalse, isnull or isnotnull.
- compare answers a yes/no and hands it on: `compare %a% > %b%, write to %isGreater%`; the `write to %x%` is its own action.
- The subject's own module is not involved just because the question is about its contents (a question about a file is still a compare, not a file action).
