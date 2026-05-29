# Math Module

Arithmetic operations and number functions. All operations preserve the input numeric type when possible (int stays int, long stays long).

`int`, `long`, `decimal`, and `double` are *kinds* of one PLang type — `number`. Math actions return `number`; the kind is preserved when it can be and promoted when an operation needs more range. Behaviour is configurable (Lenient default; Strict throws on overflow) — see **Number Policy** below.

## Actions

### add

Add two numbers.

```plang
- add 5 and 3, write to %result%    / 8
```

### subtract

Subtract B from A.

```plang
- subtract 3 from 10, write to %result%    / 7
```

### multiply

Multiply two numbers.

```plang
- multiply 4 by 5, write to %result%    / 20
```

### divide

Divide A by B. Returns an error if B is zero.

```plang
- divide 10 by 3, write to %result%    / 3.333...
- divide 7 by 2, write to %result%     / 3.5  (not 3 — see intdiv for that)
```

`divide` always leaves the integer track: `7 / 2 → 3.5`, never `3`. The integer-division footgun is the wrong default for a non-programmer audience. If you want truncating C# integer semantics, use [`intdiv`](#intdiv).

**Error:** Returns `DivideByZero` error if dividing by zero.

### intdiv

Truncating integer division of A by B.

```plang
- integer divide 7 by 2, write to %quotient%    / 3
```

The explicit opt-in for the C# semantics `divide` intentionally avoids. Negative numerators truncate toward zero. Pairs with `modulo`.

**Error:** Returns `DivideByZero` error if B is zero.

### modulo

Get the remainder of A divided by B.

```plang
- get remainder of 10 divided by 3, write to %result%    / 1
```

**Error:** Returns `DivisionByZero` error if B is zero.

### power

Raise a number to a power.

```plang
- raise 2 to power 10, write to %result%    / 1024
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Base | number | yes | The base number |
| Exponent | number | yes | The exponent |

### sqrt

Square root. Returns an error for negative numbers.

```plang
- square root of 144, write to %result%    / 12
```

**Error:** Returns `InvalidInput` error for negative values.

### abs

Absolute value.

```plang
- absolute value of -42, write to %result%    / 42
```

### round

Round a number.

```plang
- round 3.7, write to %result%        / 4
- round 3.14159 to 2 decimals, write to %result%   / 3.14
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Value | number | yes | — | Number to round |
| Decimals | int | no | 0 | Decimal places |

### floor

Round down to the nearest integer.

```plang
- floor 3.9, write to %result%    / 3
```

### ceiling

Round up to the nearest integer.

```plang
- ceiling 3.1, write to %result%    / 4
```

### min

Return the smaller of two numbers.

```plang
- min of 5 and 3, write to %result%    / 3
```

### max

Return the larger of two numbers.

```plang
- max of 5 and 3, write to %result%    / 5
```

### random

Generate a random integer.

```plang
/ Random between 0 and 100 (default)
- generate random number, write to %num%

/ Random in range
- generate random number between 1 and 6, write to %diceRoll%
```

**Parameters:**

| Name | Type | Required | Default | Description |
|------|------|----------|---------|-------------|
| Min | int | no | 0 | Minimum value (inclusive) |
| Max | int | no | 100 | Maximum value (inclusive) |

## Type Preservation

Math operations preserve your input types:

- `int + int` → `int`
- `long + long` → `long`
- `int + long` → `long` (widens to the larger type)
- `double + anything` → `double`
- `decimal + anything` → `decimal` (except double)

This prevents accidental type widening in your calculations.

## Number Policy

Two axes — `Overflow` and `Precision` — control what math does in edge cases. Lenient defaults; one config step away from strict.

### Overflow

- **Promote** *(default)* — `int` overflow widens to `long`; `long` overflow widens to `decimal`. Only `decimal` overflow throws.
- **Throw** — any overflow throws immediately.

### Precision (Decimal × Double mix)

- **Double** *(default)* — promote to `double`; IEEE-754 wins, decimal precision lost past ~15 digits.
- **Decimal** — stay `decimal`; throws if the double operand is NaN / Infinity / out-of-range.

### Setting policy

App-wide via `app.config` (a `number.Config` record). Or per-action via `Overflow=` / `Precision=` parameters on any math action.

```plang
/ Per-action: refuse overflow on this one sum
- add %a% and %b%, Overflow=Throw, write to %sum%
```

### Power exponent cap

`math.power` with an integer exponent is capped at `|exponent| ≤ 64` — past that, the action surfaces a `PowerExponentTooLarge` error rather than spinning the actor's core. Fractional exponents (which route through `Math.Pow` and are constant-time) are not subject to the cap.
