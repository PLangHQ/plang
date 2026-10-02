## Branching: if, elseif, else

`if` runs what follows it when its condition is true. To choose among several cases, chain `elseif` (or `or if`) and `else` (or `otherwise`) after it — the first branch whose condition holds runs, and `else` runs when none do:

```plang
- if %total% > 20, write out "big", elseif %total% > 15, write out "medium", else write out "small"
```

For a `%total%` of 18 this prints `medium`. What follows each branch is that branch's body. To ask a yes/no question and keep the answer rather than branch on it, use [compare](#compare).
