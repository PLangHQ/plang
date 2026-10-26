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

## Each goal, each step, each action

`before each goal call X` is heard for the goals of your app, not for plang's own. The goals under `/system/` (the builder and the shortcuts) run when your program runs, and a binding of yours does not hear them. The same holds for `each step` and `each action`: a step or action of a plang goal does not trigger a binding of yours, so `before action file.read call X` does not run for the file reads plang's own goals make.

Run: a program that binds `before each goal, call Log`, calls its own goal `Mine`, and then calls the system goal `/system/shortcut/Goal`; `Log` records the name of the goal it heard. After both calls it had recorded `Mine`, the last goal it was called for (the test `EachGoalIsTheAppsOwn`, 1 of 1 pass). Read: `PLang/app/goal/this.cs:392-397`, `goal/step/this.cs:118-123`, `goal/step/action/this.cs:156-158`.

## Remove a binding

`on.unbind` removes one. Keep what the binding step answers to unbind it later (`os/system/modules/on/unbind.description.md`). Not run here.
