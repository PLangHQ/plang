# Timer Module

Pause execution and measure elapsed time.

These are regular actions (not modifiers). They appear in step bodies just like `file.read` or `variable.set`.

## Actions

### sleep

Pause the current step for a fixed duration.

```plang
- timer.sleep ms=1000
- timer sleep 250 ms
```

The delay honours the context cancellation token, so a parent `timeout after` or an external cancellation aborts the wait immediately instead of waiting it out.

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| Ms | `ms=<n>` or `<n> ms` (`sleep 250 ms`) | int | yes | — | Duration in milliseconds |

### start

Record the start time of a named (or default) stopwatch.

```plang
/ Start the default timer
- timer.start

/ Start a named timer
- timer.start name='parse'
```

**Parameters:**

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| Name | `name='<name>'` | string | no | `"default"` | Timer identifier |
| Scope | `scope='<scope>'` | string | no | `"goal"` | Reserved for future scoping — currently informational |

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

| Property | How you say it | Type | Required | Default | Description |
|----------|----------------|------|----------|---------|-------------|
| Name | `name='<name>'` | string | no | last started | Timer identifier |

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
