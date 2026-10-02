# Condition Module
Comparisons: ask whether something holds — equal, greater, contains, starts with, empty — and either branch on the answer (if/elseif/else) or keep it. Any yes/no question belongs here, whatever it is about: whether a list holds an item, whether text starts with something, whether a number is bigger. The subject's own module is not involved just because the question is about its contents. What follows the comparison is an ordinary action — call a goal, return from the goal (`if %done%, return`), write out, keep the answer in a variable — and is that action's own module.

## Branching: if, elseif, else

`if` runs what follows it when its condition is true. To choose among several cases, chain `elseif` (or `or if`) and `else` (or `otherwise`) after it — the first branch whose condition holds runs, and `else` runs when none do:

```plang
- if %total% > 20, write out "big", elseif %total% > 15, write out "medium", else write out "small"
```

For a `%total%` of 18 this prints `medium`. What follows each branch is that branch's body. To ask a yes/no question and keep the answer rather than branch on it, use [compare](#compare).

## Combining conditions with and / or

Join two conditions with `and` (both must hold) or `or` (either):

```plang
- if %a% > 1 and %b% < 10, write out "both hold"
- if %a% > 10 or %b% < 10, write out "one holds"
```

## Testing whether a file exists

A path is true when it exists, so a condition can test one directly — `if '<file>' exists`, or a bare `if %path%`. It stats a local file, or sends an HTTP HEAD for a URL, behind the same consent prompt as any path access; a denied prompt reads as false.

```plang
- if 'here.txt' exists, write out "here.txt is there"
```

A common shape is to check first, then branch — load a file when it is there, fall back to defaults when it is not:

```plang
- check if 'config.json' exists, write to %found%
- if %found%, call LoadConfig, else call UseDefaults
```

`check if … exists` writes the path to `%found%`; because a path is true when it exists, `if %found%` is the existence test.

## compare
Compare two values with an operator and write the boolean result to a variable

- compare %a% > %b%, write to %isGreater%
- check if %myList% contains 20, write to %has20%
- check if %name% starts with "plang", write to %isPlang%
- check if %content% is empty, write out "nothing here"

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Left | the value before the comparison | item | no | — | the first value compared |
| Operator | `>`, `is`, `contains`, `is empty`, … (as in condition.if) | choice<operator> | yes | — | the comparison, a choice<operator> |
| Right | the value after the comparison | item | no | — | the second value |

**Returns:** a `bool`.

## if
Evaluate a condition and execute the then-branch actions; pair with elseif/else for full branching

- if %count% > 0, call ProcessItems
- if %content% is not empty
- if %flag% is true, call Go
- if %done%, return %result%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Left | the value right after `if` | item | yes | — | the value tested |
| Operator | `>`, `is`, `is not`, `contains`, `starts with`, `is empty`, `is in […]`, `is a <type>`, … | choice<operator> | no | — | the comparison, a choice<operator> |
| Right | the value after the operator | item | no | — | what Left is compared to |

**Returns:** a `bool`.

## elseif
Additional condition branch evaluated when the preceding if condition is false

- else if %a% > 5, write 'mid'

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Left | the value right after `else if` | item | yes | — | the value tested, the same as condition.if's |
| Operator | `>`, `is`, `contains`, `is empty`, … (as in if) | choice<operator> | no | — | the comparison, the same as condition.if's |
| Right | the value after the operator | item | no | — | what Left is compared to, the same as condition.if's |

**Returns:** a `bool`.

## else
Fallback branch that executes when all preceding if/elseif conditions are false

**Returns:** a `bool`.
