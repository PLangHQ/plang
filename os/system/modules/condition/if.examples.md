Step text: `if %count% > 0, call ProcessItems`
Properties: `{"Left": "%count%", "Operator": ">", "Right": 0}` — the call is the body: it goes in the if's `child`, `[{"text": "call ProcessItems", "action": [goal.call]}]`, never beside the if.

Step text: `if %content% is not empty, call Process`
Properties: `{"Left": "%content%", "Operator": "isnotempty"}` — the negation is the operator's; `isnotempty` takes no `Right`. The `call Process` is the body, in the if's `child`.

Step text: `if %flag%, call Go`
Properties: `{"Left": "%flag%"}` — a bare `%flag%` is Left's own truth; no Operator, no Right.

Step text: `if %done%, return %result%`
Properties: `{"Left": "%done%"}` — Left's own truth; the return is the body: `condition.if(Left=%done%) { goal.return(Data=%result%) }`, never beside the if.

Step text: `if %x% == 1 or %y% == 2, call Handle`
Properties: two actions — `condition.if(Left=%x%, Operator="==", Right=1) { goal.call(Name="Handle") }; condition.elseif(Left=%y%, Operator="==", Right=2) { goal.call(Name="Handle") }` — `or` repeats the body under an elseif; one condition.if holds only one comparison, never an `or` operator.

Step text: `if %x% > 1 and %y% < 10, call Handle`
Properties: nested — `condition.if(Left=%x%, Operator=">", Right=1) { condition.if(Left=%y%, Operator="<", Right=10) { goal.call(Name="Handle") } }` — `and` is the body under both ifs.

Step text: `if data.txt exists, call Found, else call Absent`
Properties: file.exists first, the if reads its answer, each call in its branch — `file.exists(Path="data.txt"); condition.if(Left=%!data%) { goal.call(Name="Found") }; condition.else() { goal.call(Name="Absent") }` — the bare file name is file.exists's Path.
