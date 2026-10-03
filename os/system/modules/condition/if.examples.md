Step text: `if %count% > 0, call ProcessItems`
Properties: `{"Left": "%count%", "Operator": ">", "Right": 0}` — the call is the body: it goes in the if's `child`, `[{"text": "call ProcessItems", "action": [goal.call]}]`, never beside the if.

Step text: `if %content% is not empty, call Process`
Properties: `{"Left": "%content%", "Operator": "isnotempty"}` — the negation is the operator's; `isnotempty` takes no `Right`. The `call Process` is the body, in the if's `child`.

Step text: `if %flag%, call Go`
Properties: `{"Left": "%flag%"}` — a bare `%flag%` is Left's own truth; no Operator, no Right.

Step text: `if %done%, return %result%`
Properties: `{"Left": "%done%"}` — Left's own truth; the return is the body: `condition.if(Left=%done%) { goal.return(Data=%result%) }`, never beside the if.
