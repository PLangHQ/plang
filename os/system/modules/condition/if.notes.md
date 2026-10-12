Left — the value tested · say: the value right after `if` · builder: as the step writes it
Operator — the comparison, a choice<operator> · say: `>`, `is`, `is not`, `contains`, `starts with`, `is empty`, `is in […]`, `is a <type>`, … · builder: a quoted text after one `=` (`Operator="=="`, never `Operator=="=="`); a negation is its own operator; left out when the step names only Left (then the condition is Left's own truth). The table below maps the words.
Right — what Left is compared to · say: the value after the operator · builder: left out for isempty / isnotempty, and when Operator is

| the step says | Operator | Right |
|---|---|---|
| `if %x%` alone (is it true, does it exist, is it set) | left out | left out |
| `is 5`, `equals %b%`, `is "x"`, `is true`, `is null` | `Operator="=="` | the value |
| `is not 5`, `does not equal %b%`, `is not null` | `Operator="!="` | the value |
| `is less than`, `is more than`, `is at least`, `is at most` | `Operator="<"`, `">"`, `">="`, `"<="` | the value |
| `contains` / `does not contain` | `Operator="contains"` / `"notcontains"` | the value |
| `starts with` / `does not start with` | `Operator="startswith"` / `"notstartswith"` | the value |
| `ends with` / `does not end with` | `Operator="endswith"` / `"notendswith"` | the value |
| `is in [..]` / `is not in [..]` | `Operator="in"` / `"notin"` | the list |
| `is empty` / `is not empty` | `Operator="isempty"` / `"isnotempty"` | left out |
| `is a number` / `is not a list` / `is file` / `is not dict` | `Operator="is"` / `"isnot"` | the type name: number, list, file, dict, path, … |

**`is` / `is not` followed by a TYPE'S NAME — any plang type (`file`, `dict`, `path`, `text`, `number`, `list`, `datetime`, …), with or without `a`/`an` — is the TYPE CHECK `is`/`isnot`, Right the type's name.** `is file` → `Operator="is", Right="file"`; `is not a dict` → `Operator="isnot", Right="dict"`. Never `==`/`!=` (those compare two VALUES — `is "file"` as a quoted string is an equality, but a bare type word is a type check) and never `Operator="not"`.

`if %name% is "Ingi", write out "hi"` is `condition.if(Left=%name%, Operator="==", Right="Ingi") { output.write(Data="hi") }`.

`if "= %oldHash%" is "= %hash%", call Keep` is `condition.if(Left="= %oldHash%", Operator="==", Right="= %hash%") { goal.call(Name="Keep") }` — each quoted text is a whole operand; the `= ` inside the quotes is part of the text, NOT the operator.

- A quoted text is the whole operand, every symbol inside it included (`"= %oldHash%"` is the text `= %oldHash%`). The operator is the word or symbol BETWEEN the two operands (`is`, `equals`, `>`, `contains`, …), never a character inside a quote.
- `is` and `isnot` take only a type name; a value after "is" is `==`.
- There is no istrue, isfalse, isnull or isnotnull.
- `!%b%` / `if not %b%` (a bare bool negated) is `Operator="==", Right=false` — there is no `not` operator.
- `if '<file>' exists` / `when <file> is there`: a file-existence test asks file.exists first and tests its result — `file.exists(Path='<file>'); condition.if(Left=%!data%) { … }`. A quoted file name on its own is text (always true); never `condition.if(Left='file.json')`.
- `if A and B`: one condition.if holds only one comparison. `and` is NESTED ifs — the body under both: `condition.if(A) { condition.if(B) { … } }`. `or` is the SAME body under an if and an elseif: `condition.if(A) { … }; condition.elseif(B) { … }`. (No `and`/`or` operator on a single if.)
- `if <file> exists, call A, else call B`: file.exists first, the if reads its answer, each call in its branch — `file.exists(Path=<file>); condition.if(Left=%!data%) { goal.call(Name="A") }; condition.else() { goal.call(Name="B") }`. file.exists is the FIRST action; each call goes INSIDE its branch's `{ }`, never after the if or the else.
