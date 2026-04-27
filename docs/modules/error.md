# Error Module

Throw errors with `error.throw`, handle them with the `error.handle` modifier (written as `on error ...`).

## Actions

### throw

Throw an error that stops execution of the current goal.

```plang
- throw error 'Something went wrong'

/ With a status code
- throw error 'Not found', status code 404

/ With a custom error key
- throw error 'Invalid input', key 'ValidationError'
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Message | string | yes | — | Error message |
| StatusCode | int | no | 500 | HTTP-style status code |
| Key | string | no | "UserError" | Error key for matching |

## Error Handling — `on error`

Error handling is an **action modifier**: write the handling clause after the action it guards. Under the hood it maps to the `error.handle` modifier, which wraps the preceding action and reacts when the action fails.

```plang
/ Ignore the error and continue
- throw error 'something went wrong', on error ignore

/ Call a goal on error
- read 'missing-file.txt' into %data%
    on error call HandleError

/ Retry up to 3 times before giving up
- call !RiskyOperation
    on error retry 3 times

/ Retry, and call a goal if retries are exhausted
- call !RiskyOperation
    on error retry 3 times, call HandleFailure
```

Because modifiers attach to a **single action**, two actions in the same step get two independent handlers:

```plang
- call MayFail
    on error call MarkHandled
- call AlwaysFails
    on error ignore
```

### Match filters

By default `on error` catches every error from the action. You can narrow the match by status code, key, or message substring:

```plang
/ Only handle 404 responses
- http get 'https://api.example.com/users/%id%'
    on error status 404 call HandleNotFound

/ Only handle errors with a specific key
- validate %payload%
    on error key 'ValidationError' call FixInput

/ Only handle errors whose message contains 'network'
- call !RemoteCall
    on error message 'network' call RetryLater
```

Multiple filters can combine — all must match. If no filters are supplied, all errors match.

### Passing the error to the handler goal

When an error goal is called, the original error is passed as `%!error%`:

```plang
CaptureError
- set %capturedMessage% = %!error.Message%
- set %capturedKey% = %!error.Key%
- set %capturedStatus% = %!error.StatusCode%
```

### Retry + goal ordering

When both a retry count and an error goal are supplied, `error.handle` decides which runs first from an `Order` parameter:

- **`retry first` (default)** — retry up to `N` times; only if every retry still fails does the error goal run.
- **`goal first`** — call the error goal first; if it succeeds the error is considered handled and retries are **skipped**. Retries only run if the goal itself fails.

Final fallback: `on error ignore` clears whatever error remains after retry and goal both fail.

**Parameters (on the underlying `error.handle` action):**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| StatusCode | int | no | — | Match errors with this status code |
| Key | string | no | — | Match errors with this key (case-insensitive) |
| Message | string | no | — | Match errors whose message contains this substring |
| Goal | goal.call | no | — | Goal to call when a matched error occurs |
| RetryCount | int | no | — | Maximum retry attempts |
| RetryOverMs | int | no | — | Total retry budget — `RetryOverMs / RetryCount` between attempts |
| Order | enum | no | RetryFirst | `RetryFirst` or `GoalFirst` |
| IgnoreError | bool | no | false | Consume the error after retry/goal are exhausted |

## Error Propagation

When an action fails and nothing catches it, the error bubbles up:

1. Action fails → its `on error` modifier (if any) runs
2. Error not consumed → step returns the failure → goal fails
3. Goal's caller has no handler → program stops with error output

## Examples

### Validation with Errors

```plang
Start
- set %age% = -5
- if %age% < 0 then call RejectAge
- write out 'Age is valid: %age%'

RejectAge
- throw error 'Age cannot be negative', key 'ValidationError', status code 400
```

### Graceful Error Handling

```plang
Start
- set %beforeError% = true
- throw error 'something went wrong', on error ignore
- set %afterError% = true
- write out 'Both steps ran: %beforeError% and %afterError%'
```

Output:
```
Both steps ran: true and true
```

### Retry with Error Goal Fallback

```plang
Start
- set %counter% = 0
- call IncrementAndFail
    on error retry 3 times
    on error ignore
/ Original + 3 retries = 4 attempts
- assert %counter% equals 4

IncrementAndFail
- set %counter% = %counter% + 1
- throw error 'intentional failure'
```

See also:
- `tests/modifiers/OnErrorCallGoal.test.goal`
- `tests/modifiers/OnErrorRetry.test.goal`
- `tests/modifiers/PerActionErrorScope.test.goal`
