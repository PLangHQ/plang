# Events

For someone writing a program. The question this page answers: how do I run a goal whenever something happens?

## Bind a goal to an event

```plang
Start
- set %marks% = 0
- before action output.write call Mark
- write out "one"
- write out "two"
- write out "marks: %marks%"

Mark
- set %marks% = %marks% + 1
```

Output:

```
one
two
marks: 2
```

`before action output.write call Mark` runs the goal `Mark` before each `write out`. `Mark` uses `set`, not `write out`, so it does not trigger itself.

## What you can bind to

The forms the `on` module teaches (`os/system/modules/on/event.description.md`):

- `before each goal call X`, `after each goal call X`
- `after step call X`
- `before action file.read call X`
- a channel writing, reading or asking: `before write on "audit" channel call X`

Of these only `before action output.write` and `after each goal` were run for this page. `test/module/on/event` has 3 tests that pass (subfolders for before/after step, before/after action, priority, override, remove and wildcard exist); which forms they cover was not checked.

## Be careful with each goal

`before each goal call LogBefore` printed `before a goal` many times before the first goal of the program had printed its own line: the event fires for every goal the runtime starts, not only yours. Why it fired that often was not traced. If you bind to each goal, count on more calls than your own goals.

## Remove a binding

`on.unbind` removes one. Keep what the binding step answers to unbind it later (`os/system/modules/on/unbind.description.md`). Not run here.
