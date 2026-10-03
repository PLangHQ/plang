# Timer Module

Pause execution and measure elapsed time.

These are regular actions (not modifiers). They appear in step bodies just like `file.read` or `variable.set`.

## Actions

### sleep

Pause the current step for a fixed duration.

```plang
- sleep 2 seconds
- wait for 500 ms
```

The duration is a `duration` value — `"2s"`, `"500ms"`, `"0.5s"`; a bare number is refused. The delay honours the context cancellation token, so a parent `timeout after` or an external cancellation aborts the wait immediately instead of waiting it out.

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Duration | duration | yes | — | How long to pause — `"2s"`, `"500ms"`, `"0.5s"`; a bare number is refused |

### start

Record the start time of a named (or default) stopwatch.

```plang
/ Start the default timer
- timer.start

/ Start a named timer
- timer.start name='parse'
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Name | string | no | `"default"` | Timer identifier |
| Scope | string | no | `"goal"` | Reserved for future scoping — currently informational |

### end

Stop a started timer and return the elapsed `TimeSpan`.

```plang
- timer.start name='parse'
- call !ParseLargeFile
- timer.end name='parse'
    write to %parseDuration%

/ No name = end the most recently started timer
- timer.end
    write to %elapsed%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Name | string | no | last started | Timer identifier |

**Errors:**

- `ValidationError` — "No timer has been started" if nothing was started and no name given.
- `ValidationError` — "Timer '{name}' was not started" if the named timer is unknown.

## Example

```plang
Profile
- timer.start name='work'
- call !ExpensiveOperation
- timer.end name='work'
    write to %elapsed%
- write out 'Done in %elapsed%'
```
