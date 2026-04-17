# Timeout Module

Cap how long an action is allowed to run.

`timeout` is an **action modifier**: it wraps a single action with a hard deadline. If the action exceeds the deadline it is cancelled and the modifier returns a 408 Timeout error.

## Actions

### after (written as `timeout after ...`)

```plang
/ Fail the action after 500 ms
- call !SlowWork
    timeout after 500 ms

/ Combine with on error — convert a timeout into a fallback goal call
- http get 'https://api.example.com/slow'
    timeout after 2 seconds
    on error call UseCachedResult
```

On timeout, `timeout.after` returns `Data.FromError` with:

- Message: `"Timed out after Nms"`
- Key: `"Timeout"`
- StatusCode: `408`

The error flows into any outer `on error` modifier just like any other failure.

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Ms | int | yes | — | Deadline in milliseconds |

## Semantics

- **Cooperative cancellation.** The deadline is enforced via a `CancellationToken` pushed onto the current context. Handlers that honour the token (HTTP, file I/O, `timer.sleep`, etc.) abort promptly. CPU-bound handlers that never check the token cannot be force-killed.
- **Nested timeouts compose.** An inner `timeout after 100 ms` inside an outer `timeout after 1 second` is capped by whichever fires first.
- **Parent cancellation propagates.** If a caller cancels the enclosing operation, `timeout.after` re-throws the cancellation instead of swallowing it as a timeout.

## Example

```plang
TestTimeoutOnSlowAction
- set %timedOut% = true
- timer.sleep ms=2000
    timeout after 100 ms
    on error ignore
- assert %timedOut% equals true
```

The sleep would run for 2 seconds but the 100 ms timeout fires first; `on error ignore` absorbs the Timeout error so the assertion still runs. See `tests/modifiers/TimeoutOnSlowAction.test.goal`.
