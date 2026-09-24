`error.handle` is the action behind an `on error …` clause (in any wording: "on error", "if it fails", "catch error", "on failure"). It wraps the action before it. The clause's own content — a call, a set — is its own action and is not a property of `error.handle`.

Step text: `read file.txt, on error call HandleMissing`
Properties: `{}` — no filter, no retry, nothing to ignore. The read and the recovery call are their own actions.

Step text: `verify %data% with contracts ['C1'], on error call HandleContractError`
Properties: `{}`

Step text: `save %doc%, on error key Conflict, write out "already exists"`
Properties: `{"Key": "Conflict"}` — a named error key filters which errors this handles.

Step text: `read %path%, on error 404, write out "missing"`
Properties: `{"StatusCode": 404}` — a bare number is a status code, never a key.

Step text: `http get %url%, on error retry 3 times`
Properties: `{"RetryCount": 3}` — retry with no recovery body.

Step text: `write out "hi", on error ignore`
Properties: `{"IgnoreError": true}`

Step text: `call Save, on error call Rollback first, then retry 2 times`
Properties: `{"RetryCount": 2, "Order": "GoalFirst"}` — `GoalFirst` is fix, then retry: the recovery runs, then the step retries and gets the retry's result. Without a `RetryCount` the recovery's result stands.

Step text: `render %template%, on error 404 call Fallback then retry, write to %text%`
Properties: `{"StatusCode": 404, "RetryCount": 1, "Order": "GoalFirst"}` — the recovery fixes what the step reads (`Fallback` sets `%template%`), and the retry reads it anew.
