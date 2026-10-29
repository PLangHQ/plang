Step text: `return %result%`
Formal: `goal.return(Data: item = %result%)`

Step text: `if %goal.IsCached%, return %goal.Cache%`
Formal: `goal.return(Data: item = %goal.Cache%)` — the return is the if's body: it goes inside the if's `{ }`, `condition.if(Left=%goal.IsCached%) { goal.return(Data=%goal.Cache%) }`, never beside the if.

Step text: `if %items% is empty, return`
Formal: `goal.return()` — a return that names no value; the body of the if: `condition.if(Left=%items%, Operator="isempty") { goal.return() }`.
