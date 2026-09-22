`condition.compare` answers a yes/no question and hands the answer on. The question can be about anything — a number, a text, a list, a file — and the subject's own module is NOT involved just because the question is about its contents. Whatever follows is an ordinary action of its own.

Step text: `compare %a% > %b%, write to %isGreater%`
Properties: `{"Left": "%a%", "Operator": ">", "Right": "%b%"}`

Step text: `check if %myList% contains 20, write to %has20%`
Properties: `{"Left": "%myList%", "Operator": "contains", "Right": 20}` — a question about a list is still a comparison.

Step text: `check if %name% starts with "plang", write to %isPlang%`
Properties: `{"Left": "%name%", "Operator": "startswith", "Right": "plang"}`

Step text: `check if %content% is empty, write out "nothing here"`
Properties: `{"Left": "%content%", "Operator": "isempty"}` — `isempty` takes no `Right`. What follows a comparison is any action at all, not only a `variable.set`.
