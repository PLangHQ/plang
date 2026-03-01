# Condition Module

If/else branching. Evaluate a condition and call different goals based on the result.

## Actions

### if

Execute a goal conditionally.

```plang
/ Simple boolean check
- if %isActive% is true then call HandleActive

/ With else
- if %isActive% is true then call HandleActive, else call HandleInactive

/ Comparison
- if %age% > 18 then call IsAdult, else call IsMinor

/ String comparison
- if %name% is "john" then call GreetJohn

/ Combined conditions
- if %name% is "john" and %age% < 30 then call YoungJohn

/ Boolean variable (cast)
- if %Valid% (bool) then call IsValid, else call NotValid
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Condition | bool | yes | The condition to evaluate |
| GoalIfTrue | goal | no | Goal to call if condition is true |
| GoalIfFalse | goal | no | Goal to call if condition is false |

**Returns:** The condition value (true or false).

## Important

PLang conditions compile to a boolean at build time. The LLM interprets your natural language condition and produces a boolean expression. Complex conditions like `and`/`or` are supported.

There is no standalone `else` step — the else clause is part of the `if` step:

```plang
/ Correct
- if %x% > 10 then call Big, else call Small

/ Wrong — there is no standalone else step
- if %x% > 10 then call Big
- else call Small    ← this won't work
```

## Examples

### Check File Before Reading

```plang
Start
- check if 'config.json' exists, write to %exists%
- if %exists% is true then call LoadConfig, else call UseDefaults

LoadConfig
- read 'config.json' into %config%

UseDefaults
- set %config% = {debug: false, port: 8080}
```

### Multiple Conditions

```plang
Start
- set %score% = 85
- if %score% > 90 then call GradeA
- if %score% > 80 and %score% <= 90 then call GradeB
- if %score% <= 80 then call GradeC

GradeA
- write out 'Grade: A'

GradeB
- write out 'Grade: B'

GradeC
- write out 'Grade: C'
```
