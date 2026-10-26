# Calling goals

For someone writing a program. The question this page answers: how do I run another goal, give it values and get one back?

## Call

```plang
Start
- call Greet

Greet
- write out "Greet ran"
```

## Give it values

```plang
- call Visit city="Reykjavik", unit="metric"
```

Each `name=value` is a variable in the called goal. It ends with the call. See [variables](variables.md#where-a-variable-lives).

## Get a value back

```plang
- call Double n=21, write to %answer%
- write out %answer%

Double
- set %result% = %n% * 2
```

Output: `42`. A goal answers with `return`:

```plang
Early
- if %done% is true, return "early"
- return "late"
```

`call Early done=true, write to %answer%` leaves `early` in `%answer%`.

## Wait or not

A plain call waits for the goal to end. A call with `Parallel=true` answers a task at once and the next step runs while the goal is still going. `task.wait(Task=%task%)` waits for it. Both are tests that pass: `test/module/goal/call/PlainCallWaits.test.goal`, `ParallelAnswersATask.test.goal`. They use the action form (`goal.call(Name="Slow", Parallel=true)`); the words that make the builder pick it were not tried.

## A call that builds wrong

A step can build and still mean the wrong thing. In one build, `call Early done=true, write to %got%` was saved with the goal name `Early done=true`, and the run stopped:

```
GoalNotFound (404): Goal 'Early done=true' not found.
  at /Start.goal:14
```

A call built to the wrong goal can also show as a loop. In a test file, `call Add a=2, b=3, write to %sum%` once built with the name of the test's own goal, and the run stopped with `Error: Call stack overflow: exceeded 1500 frames`. A sibling test with the same step text built correctly.

Changing the step's variable name and building again fixed it; the rebuild printed `Properties rejected — retrying` and saved a right step. Look at the step in `.build/start.pr` when a run does not do what the words say. See [how a step becomes an action](how-a-step-becomes-an-action.md).

For the action itself: `os/system/modules/goal/call.description.md`.
