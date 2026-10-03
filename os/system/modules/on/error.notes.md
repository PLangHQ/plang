A clause: it comes right after the action it is a clause of, as the next action of the step. It answers when that action fails — after the attempt, so an `on.timeout` deadline is past and an `on.cache` stores only real work.

Recovery — the actions to run when the action fails: what the step says AFTER "on error" (and after any filter like `404`/`key X`). It is the clause's own content, NEVER the action the clause attaches to: `read %path%, on error 404, write out "missing"` → Recovery is `[output.write(Data="missing")]` (the `read` stays the step's action), never `[file.read(...)]`.
Status — handle only errors with this status, written as its code (`on error 404` → `Status=404`).
Key — handle only errors with this key (`on error key "NotFound"`).
Message — handle only errors whose message contains this text.
RetryCount — how many times to run the action again, ONLY when the step writes a number ("retry 3 times" → 3); each retry is a fresh attempt. A retry with no number ("retry", "then retry") leaves RetryCount out — its default — never an inferred 1.
RetryOver — the time the retries are spread over, a duration ("over 30 seconds" → "30s").
Order — set `GoalFirst` ONLY when the step runs the recovery BEFORE retrying — "call X first, then retry". Left out (the default, retry first) when the step retries then calls — "retry N times, then call X". The word order in the step decides: what the step says first runs first.
Ignore — true when the step says to carry on past the error ("on error ignore", "on error 'NotFound' ignore").

- The recovery runs only on error: it is never also an action of the step.
- Status, Key and Message filter which errors are handled; they never hold what the recovery does. Left out when the step names no filter.
- Several on.error clauses are asked in the order written; the first that matches takes the error, and the others never see it.
