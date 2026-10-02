# Loop Module
Iterate over a collection, executing the remaining step actions once per item

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

## foreach
Iterate over Collection, binding each element to Item (and its key or index to Key) and executing the remaining step actions

- foreach %items%, call ProcessItem item=%item%
- foreach %rows%, write out %item%
- foreach %products% as %product%, call Handle
- foreach %prices% as %price% with key %sku%, write out "%sku%: %price%"

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Collection | `foreach %list%`, `for each %order% in %orders%` | item | yes | — | the list or dict to walk |
| Item | `as %product%` (else it is %item%) | variable | no | item | the variable each element is bound to |
| Key | `with key %sku%` | variable | no | — | the variable the key or index is bound to |

**Returns:** a summary of the loop: `{count, complete}` — how many elements it ran over, and whether it finished (false if cancelled). The work per element is its own action, so there is usually nothing to write the summary to.
