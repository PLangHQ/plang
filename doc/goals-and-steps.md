# Goals and steps

For someone writing a program. The question this page answers: how is a PLang program laid out, and in what order does it run?

## The layout

A program is a folder. In it, a `.goal` file holds one or more goals. A goal is a name on a line by itself, then steps. A step is a line that starts with `- `.

```plang
Start
- set %name% = "Ada"
- write out "Hello %name%"
- call Farewell

Farewell
- write out "bye"
```

`Start` is where `plang` begins. `plang Farewell` runs the goal `Farewell` by name instead.

Steps run from the top, one after another. A blank line between goals changes nothing.

## Goals in one file

Put as many goals in a file as you like. The build counts the file as one goal (`Found 1 goals`), builds the others as its sub-goals (`Building sub-goal: <name>`), and writes them all to one `.build/start.pr`.

A goal is reached by `call <name>`. A build printed `GoalUnreached` for a goal whose calling step had been built wrong, so no built step reached it.

## Lines under a step

A line indented under a step belongs to that step. Two uses:

- A condition's body: the indented steps run only when the condition holds. See [conditions](conditions.md).
- A step's modifiers: `on error retry 2 times` on its own indented line. See [errors](errors.md).

```plang
- call Flaky
    on error retry 2 times
    on error ignore
```

## Comments

A line that starts with `/ ` is a comment. The repo's own test goals start with them and build and pass.

## Running one goal

- `plang` runs `Start`.
- `plang Name` runs `Name`.
- A run never builds. Change a goal, then `plang build`, then run.

## Next

[Variables](variables.md), [conditions](conditions.md), [loops](loops.md), [calling goals](calling-goals.md), [errors](errors.md), [events](events.md), and [how a step's words become an action](how-a-step-becomes-an-action.md).
