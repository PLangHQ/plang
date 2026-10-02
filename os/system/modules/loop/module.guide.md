## The work per item is its own action

`foreach` repeats the rest of the step for each element of a collection. What you do with each element is a separate action after it — usually a `call` to a goal:

```plang
- foreach %products% as %product%, call ShowProduct product=%product%
```

`foreach %products% as %product%` binds each element to `%product%`; `call ShowProduct product=%product%` is the per-item work, passing the element on. Without `as`, each element is `%item%`.

## Dicts: binding the key too

Over a dict, each value binds to the item; add `with key` to bind its key:

```plang
- foreach %person% as %value% with key %field%, call ShowEntry field=%field% value=%value%
```

## Counting and running totals

A goal called from the loop updates the caller's variables, so a running total works:

```plang
- set %count% = 0
- foreach %items%, call CountItem
- write out "Total: %count%"
```

With `CountItem` doing `set %count% = %count% + 1`, a three-item list prints `Total: 3`.

## Strings are atomic

A string is one value, not a sequence of characters: `foreach %greeting%` where `%greeting%` is `"hello"` runs once, with the whole string — not once per letter.
