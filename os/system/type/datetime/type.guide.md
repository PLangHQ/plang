# Dates and times

A `datetime` is a moment — a date, a time of day, and an offset from UTC. Its parts read with a dot.

## Parts

- `%when.year%`, `%when.month%`, `%when.day%` — the date, as numbers.
- `%when.hour%`, `%when.minute%`, `%when.second%`, `%when.millisecond%` — the time, as numbers.
- `%when.weekday%` — the day name (Monday, …), a text.
- `%when.ticks%` — 100-nanosecond ticks since year 1, a number.

## Whole parts

- `%when.date%` — just the date (a `date`); `%when.time%` — just the time of day (a `time`).
- `%when.offset%` — the offset from UTC (a `duration`).
