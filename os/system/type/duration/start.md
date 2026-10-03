# duration
A length of time: a number and its unit (200ms, 30s, 5m, 1h, 1d), or ISO 8601 (PT5M).

# Durations

A `duration` is a length of time — `"2s"`, `"500ms"`, `"1m30s"`, `"PT1H"`. Each member gives the whole span measured in one unit, as a number:

- `%elapsed.seconds%` — the span in seconds (`1m30s` is `90`).
- `%elapsed.minutes%` — in minutes (`1.5`); `%elapsed.hours%` — in hours; `%elapsed.days%` — in days; `%elapsed.milliseconds%` — in milliseconds.

These are totals, not parts: a `1m30s` span is `90` seconds and `1.5` minutes, not `30` seconds and `1` minute.

## days
The whole span measured in days.

`%elapsed.days%`

**Returns:** a number.

## hours
The whole span measured in hours.

`%elapsed.hours%`

**Returns:** a number.

## minutes
The whole span measured in minutes.

`%elapsed.minutes%`

**Returns:** a number.

## seconds
The whole span measured in seconds (1m30s is 90).

`%elapsed.seconds%`

**Returns:** a number.

## milliseconds
The whole span measured in milliseconds.

`%elapsed.milliseconds%`

**Returns:** a number.
