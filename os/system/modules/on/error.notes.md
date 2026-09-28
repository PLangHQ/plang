A clause: it comes right after the action it is a clause of, as the next action of the step. It answers when that action fails — after the attempt, so an `on.timeout` deadline is past and an `on.cache` stores only real work.

Recovery — the actions to run when the action fails: what the step says after "on error" (or "if it fails", in any language).
StatusCode — handle only errors with this status code.
Key — handle only errors with this key (`on error key "NotFound"`).
Message — handle only errors whose message contains this text.
RetryCount — how many times to run the action again ("retry 3 times"); each retry is a fresh attempt.
RetryOver — the time the retries are spread over, a duration ("over 30 seconds" → "PT30S").
Order — GoalFirst when the step runs the recovery before retrying ("call X, then retry"); left out for the default, retrying first ("retry 3 times, then call X").
IgnoreError — true when the step says to carry on past the error.

- The recovery runs only on error: it is never also an action of the step.
- StatusCode, Key and Message filter which errors are handled; they never hold what the recovery does. Left out when the step names no filter.
- Several on.error clauses are asked in the order written; the first that matches takes the error, and the others never see it.
