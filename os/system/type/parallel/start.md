# parallel
Whether work runs side by side, and how many at once: "in parallel" or "in parallel(cpu: 2)"; left out means not parallel.

# Parallel

`parallel` says whether the items of a loop (or an LLM's tool calls) run side by side, and how many at once.

- `in parallel` — run them together, up to a sensible default (about the machine's cores).
- `in parallel(cpu: 2)` — run at most 2 at a time.
- leave it out — they run one after another.

A program written before `parallel` was a type may say `true` (parallel at the default) or `false` (not parallel); both still read.

A `foreach … in parallel` answers a task: the loop hands back a task you `wait for` to let the parallel work finish.

```plang
Start
- foreach %orders% in parallel(cpu: 4), call Ship order=%item%, write to %tasks%
- wait for %tasks%
```
