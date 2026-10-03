# Durations

A `duration` is a length of time — `"2s"`, `"500ms"`, `"1m30s"`, `"PT1H"`. Each member gives the whole span measured in one unit, as a number:

- `%elapsed.seconds%` — the span in seconds (`1m30s` is `90`).
- `%elapsed.minutes%` — in minutes (`1.5`); `%elapsed.hours%` — in hours; `%elapsed.days%` — in days; `%elapsed.milliseconds%` — in milliseconds.

These are totals, not parts: a `1m30s` span is `90` seconds and `1.5` minutes, not `30` seconds and `1` minute.
