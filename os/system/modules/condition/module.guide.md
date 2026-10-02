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
