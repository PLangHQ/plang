`on.error` is the action behind an `on error …` clause (in any wording: "on error", "if it fails", "catch error", "on failure"). It comes right after the action it is a clause of. The clause's own content — a call, a set — is an action in its `Recovery`: never another action of the step, never beside it. Several `on error` clauses in one step are several `on.error` clauses, asked in the order written; the first whose filters match takes the error.

Step text: `read file.txt, on error call HandleMissing`
Properties: `{"Recovery": [goal.call(Name="HandleMissing")]}` — no filter, no retry, nothing to ignore. The read is the step's action; the call is in `Recovery`.

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Properties: `{"Recovery": [goal.call(Name="HandleContractError")]}`

Step text: `save %doc%, on error key Conflict, write out "already exists"`
Properties: `{"Key": "Conflict", "Recovery": [output.write(Data="already exists")]}` — a named error key filters which errors this handles.

Step text: `read %path%, on error 404, write out "missing"`
Properties: `{"StatusCode": 404, "Recovery": [output.write(Data="missing")]}` — a bare number is a status code, never a key.

Step text: `http get %url%, on error retry 3 times`
Properties: `{"RetryCount": 3}` — retry with no recovery.

Step text: `http get %url%, on error retry 3 times over 30 seconds`
Properties: `{"RetryCount": 3, "RetryOver": "30s"}` — the retries are spread evenly over that time.

Step text: `write out "hi", on error ignore`
Properties: `{"Ignore": true}`

Step text: `delete file 'old.txt', on error 'NotFound' ignore`
Properties: `{"Key": "NotFound", "Ignore": true}` — a file that isn't there is NotFound; the step carries on past it.

Step text: `call Save, on error retry 2 times, then call Rollback`
Properties: `{"RetryCount": 2, "Recovery": [goal.call(Name="Rollback")]}` — retry first, then the recovery: the default order, so `Order` is left out.

Step text: `call Save, on error call Rollback first, then retry 2 times`
Properties: `{"RetryCount": 2, "Order": "GoalFirst", "Recovery": [goal.call(Name="Rollback")]}` — `GoalFirst` is fix, then retry: the recovery runs, then the step retries and gets the retry's result. Without a `RetryCount` the recovery's result stands.

Step text: `render %template%, on error 404 call Fallback then retry, write to %text%`
Properties: `{"StatusCode": 404, "RetryCount": 1, "Order": "GoalFirst", "Recovery": [goal.call(Name="Fallback")]}` — the recovery fixes what the step reads (`Fallback` sets `%template%`), and the retry reads it anew.
