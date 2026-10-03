# number
A number, whole or decimal, of any size. Its kind is the precision it is held in (int, long, decimal, double, …).

# Numbers

A `number` is any numeric value — a count, a price, a measurement. Arithmetic uses the operators `+`, `-`, `*`, `/` across steps; the members below reshape a single number.

## Methods

Numbers' methods read with a dot and parentheses, and chain:

- `%delta.abs()%` — drop the sign.
- `%average.floor()%`, `%average.ceiling()%` — round down or up to a whole number.
- `%price.round(2)%` — round to a number of decimal places.
- `%area.sqrt()%` — the square root.
- `%price.min(100)%`, `%score.max(0)%` — the smaller or larger of two numbers.

## abs
The value with its sign removed.

`%delta.abs()%`

**Returns:** the absolute value, a number.

## floor
The value rounded down to a whole number.

`%average.floor()%`

**Returns:** a number.

## ceiling
The value rounded up to a whole number.

`%average.ceiling()%`

**Returns:** a number.

## sqrt
The square root of the value.

`%area.sqrt()%`

**Returns:** a number.

## round
The value rounded to a number of decimal places.

`%price.round(2)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| decimals | the first argument, a number | number | yes | — | how many decimal places to keep |

**Returns:** the rounded number.

## min
The smaller of this value and another.

`%price.min(100)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| b | the first argument | number | yes | — | the other number to compare against |

**Returns:** the smaller of the two, a number.

## max
The larger of this value and another.

`%score.max(0)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| b | the first argument | number | yes | — | the other number to compare against |

**Returns:** the larger of the two, a number.
