# Goals in other folders

For someone writing a program. The question this page answers: how do I call a goal that lives in a subfolder, or pick the goal by a variable?

## A goal in a subfolder

Put the goal in its own file in a subfolder, for example `tools/Shout.goal`:

```plang
Shout
- write out "from tools"
```

Call it from `Start.goal` with a path that starts with `/`, from the app's root:

```plang
- call /tools/Shout
```

Output: `from tools`. `plang build` found both goals (`Found 2 goals`), and the call saved as `Name="/tools/Shout"`.

Write the path without the leading slash, `call tools/Shout`, and the builder dropped the folder and saved `Name="Shout"`. The run then stopped:

```
GoalNotFound (404): Goal 'Shout' not found.
  at /Start.goal:2
    - call tools/Shout
```

That is what was seen, in one build. The module's teaching (`os/system/modules/goal/call.notes.md`) says a `Folder/Goal` name is allowed. A test in the repo that calls `sub/SubGoal` is switched off (its file ends `.goal2`). Use the leading `/`.

## Pick the goal by a variable

```plang
- set %which% = "Pick"
- call %which%

Pick
- write out "picked"
```

Output: `picked`. (`test/module/goal/dynamic` passes too.)

## A goal that does not exist

A call to a name no goal has stops the run with `GoalNotFound (404)`. `test/module/goal/missing` shows a handler for it and passes. All 8 tests in `test/module/goal` pass.

The `call` action: `os/system/modules/goal/call.description.md`.
