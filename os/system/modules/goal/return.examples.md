Step text: `return %result%`
Properties: `{"Data": "%result%"}`

Step text: `if %goal.IsCached%, return %goal.Cache%`
Properties: `{"Data": "%goal.Cache%"}` — the return is the if's body: it goes inside the if's `{ }`, `condition.if(Left=%goal.IsCached%) { goal.return(Data=%goal.Cache%) }`, never beside the if.

Step text: `if %items% is empty, return`
Properties: `{}` — a return that names no value; the body of the if: `condition.if(Left=%items%, Operator="isempty") { goal.return() }`.
