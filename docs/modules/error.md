# Error Module

Throw errors and handle them with `on error`.

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

## Error Handling

Use `on error` at the end of a step to handle errors:

```plang
/ Ignore the error and continue
- throw error 'something went wrong', on error ignore

/ Call a goal on error
- read 'missing-file.txt' into %data%, on error call HandleError

/ Errors in called goals bubble up
- call !RiskyOperation, on error call HandleFailure
```

## Error Propagation

When a step fails and there's no `on error` handler, the error bubbles up through the call stack:

1. Step fails → check step's `on error`
2. No handler → goal fails → check caller's `on error`
3. No handler → program stops with error output

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
