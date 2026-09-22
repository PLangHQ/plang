`condition.compare` answers a yes/no question and hands the answer on. The question can be about anything — a number, a text, a list, a file — and the subject's own module is NOT involved just because the question is about its contents. Whatever follows is an ordinary action and belongs to its own module.

Step text: `compare %a% > %b%, write to %isGreater%`
Mapping: `condition.compare Left([object] %a%), Operator([operator] >), Right([object] %b%) | variable.set Name([string] %isGreater%), Value([object] %!data%)`

Step text: `check if %myList% contains 20, write to %has20%`
Mapping: `condition.compare Left([object] %myList%), Operator([operator] contains), Right([object] 20) | variable.set Name([variable] %has20%), Value([bool] %!data%)` — a question about a list is still a comparison; `list` is not used.

Step text: `check if %name% starts with "plang", write to %isPlang%`
Mapping: `condition.compare Left([object] %name%), Operator([operator] startswith), Right([text] plang) | variable.set …`

Step text: `check if %content% is empty, write out "nothing here"`
Mapping: `condition.compare Left([object] %content%), Operator([operator] isempty) | output.write Data([text] nothing here)` — what follows a comparison is any action at all, not only a `variable.set`.
