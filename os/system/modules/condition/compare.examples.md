`condition.compare` answers a yes/no question and hands the answer on. The question can be about anything — a number, a text, a list, a file — and the subject's own module is NOT involved just because the question is about its contents. Whatever follows is an ordinary action of its own.

Step text: `compare %a% > %b%, write to %isGreater%`
Formal: `condition.compare(Left: item = %a%, Operator: choice<operator> = ">", Right: item = %b%)`

Step text: `check if %myList% contains 20, write to %has20%`
Formal: `condition.compare(Left: item = %myList%, Operator: choice<operator> = "contains", Right: number = 20)` — a question about a list is still a comparison.

Step text: `check if %name% starts with "plang", write to %isPlang%`
Formal: `condition.compare(Left: item = %name%, Operator: choice<operator> = "startswith", Right: text = "plang")`

Step text: `check if %content% is empty, write to %isEmpty%`
Formal: `condition.compare(Left: item = %content%, Operator: choice<operator> = "isempty")` — `isempty` takes no `Right`; the `write to %isEmpty%` keeps the yes/no answer, its own variable.set.
