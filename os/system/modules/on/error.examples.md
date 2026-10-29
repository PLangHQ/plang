`on.error` is the action behind an `on error …` clause (in any wording: "on error", "if it fails", "catch error", "on failure"). It comes right after the action it is a clause of. The clause's own content — a call, a set — is an action in its `Recovery`: never another action of the step, never beside it. Several `on error` clauses in one step are several `on.error` clauses, asked in the order written; the first whose filters match takes the error.

Step text: `read file.txt, on error call HandleMissing`
Formal: `file.read(Path="file.txt"); on.error(Recovery=[goal.call(Name="HandleMissing")])` — no filter, no retry, nothing to ignore. The read is the step's action; the call is in `Recovery`.

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Formal: `signing.verify(Data=%data%, Contracts=["C1"]); on.error(Recovery=[goal.call(Name="HandleContractError")])`

Step text: `write 'hi' to dbg, on error set %failed% = true`
Formal: `output.write(Data="hi", channel="dbg"); on.error(Recovery=[variable.set(Name=%failed%, Value=true)])` — the recovery is a `set`; it goes inside `Recovery`, never a top-level variable.set after the clause (that would run every time, not only on error).

Step text: `if %a% == %b% then, on error call Caught` (with `call Handle` indented below it)
Formal: `condition.if(Left=%a%, Operator="==", Right=%b%); on.error(Recovery=[goal.call(Name="Caught")])` — the comparison is the step's action (condition.if), its body the indented step; comparing values that can't be compared fails, and the on.error after the if catches it. The on.error is never dropped because the action it guards is an if.

Step text: `foreach %items%, call DoItem item=%item%, on error set %caught% = true`
Formal: `loop.foreach(Collection=%items%); on.error(Recovery=[variable.set(Name=%caught%, Value=true)]); goal.call(Name="DoItem", Parameter=[{item: %item%}])` — the loop is the step's action (loop.foreach), the on.error comes after it, and the `set` nests in `Recovery`; an empty on.error with the set left beside it does nothing and is refused.

Step text: `save %doc%, on error key Conflict, write out "already exists"`
Formal: `file.save(Path=%doc%, Value=%!data%); on.error(Key="Conflict", Recovery=[output.write(Data="already exists")])` — a named error key filters which errors this handles.

Step text: `read %path%, on error 404, write out "missing"`
Formal: `file.read(Path=%path%); on.error(Status=404, Recovery=[output.write(Data="missing")])` — a bare number is a status, never a key.

Step text: `http get %url%, on error retry 3 times`
Formal: `http.request(Url=%url%); on.error(RetryCount=3)` — retry with no recovery.

Step text: `http get %url%, on error retry 3 times over 30 seconds`
Formal: `http.request(Url=%url%); on.error(RetryCount=3, RetryOver="30s")` — the retries are spread evenly over that time.

Step text: `write out "hi", on error ignore`
Formal: `output.write(Data="hi"); on.error(Ignore=true)`

Step text: `delete file 'old.txt', on error 'NotFound' ignore`
Formal: `file.delete(Path="old.txt"); on.error(Key="NotFound", Ignore=true)` — a file that isn't there is NotFound; the step carries on past it.

Step text: `call Save, on error retry 2 times, then call Rollback`
Formal: `goal.call(Name="Save"); on.error(RetryCount=2, Recovery=[goal.call(Name="Rollback")])` — retry first, then the recovery: the default order, so `Order` is left out.

Step text: `call Save, on error call Rollback first, then retry 2 times`
Formal: `goal.call(Name="Save"); on.error(RetryCount=2, Order="GoalFirst", Recovery=[goal.call(Name="Rollback")])` — `GoalFirst` is fix, then retry: the recovery runs, then the step retries and gets the retry's result. Without a `RetryCount` the recovery's result stands.

Step text: `render %template%, on error 404 call Fallback then retry, write to %text%`
Formal: `ui.render(Template=%template%); on.error(Status=404, RetryCount=1, Order="GoalFirst", Recovery=[goal.call(Name="Fallback")]); variable.set(Name=%text%, Value=%!data%)` — the recovery fixes what the step reads (`Fallback` sets `%template%`), and the retry reads it anew.
