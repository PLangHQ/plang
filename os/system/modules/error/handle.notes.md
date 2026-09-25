A modifier: it wraps the action it sits on, in that action's `modifier` list.

recovery — the actions to run when the wrapped action fails: what the step says after "on error" (or "if it fails", in any language).
StatusCode — handle only errors with this status code.
Key — handle only errors with this key (`on error key "NotFound"`).
Message — handle only errors whose message contains this text.
RetryCount — how many times to run the wrapped action again ("retry 3 times").
RetryOverMs — the time, in milliseconds, the retries are spread over.
Order — GoalFirst when the step runs the recovery before retrying ("call X, then retry"); left out for the default, retrying first ("retry 3 times, then call X").
IgnoreError — true when the step says to carry on past the error.

- The recovery runs only on error: it is never also an action of the step.
- StatusCode, Key and Message filter which errors are handled; they never hold what the recovery does. Left out when the step names no filter.
