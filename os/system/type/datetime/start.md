# datetime
A date and a time of day, with its offset from UTC.

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

## year
The year.

`%when.year%`

**Returns:** a number.

## month
The month, 1–12.

`%when.month%`

**Returns:** a number.

## day
The day of the month, 1–31.

`%when.day%`

**Returns:** a number.

## hour
The hour, 0–23.

`%when.hour%`

**Returns:** a number.

## minute
The minute, 0–59.

`%when.minute%`

**Returns:** a number.

## second
The second, 0–59.

`%when.second%`

**Returns:** a number.

## millisecond
The millisecond, 0–999.

`%when.millisecond%`

**Returns:** a number.

## ticks
The number of 100-nanosecond ticks since year 1.

`%when.ticks%`

**Returns:** a number.

## weekday
The day of the week by name (Monday, Tuesday, …).

`%when.weekday%`

**Returns:** the weekday name, a text.

## date
The date part, with no time of day.

`%when.date%`

**Returns:** a date.

## time
The time of day, with no date.

`%when.time%`

**Returns:** a time.

## offset
The offset from UTC.

`%when.offset%`

**Returns:** a duration.
