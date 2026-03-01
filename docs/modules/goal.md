# Goal Module

Call other goals from within a goal. This is how you organize PLang code into reusable pieces.

## Actions

### call

Execute another goal.

```plang
/ Call a goal in the same file
- call !SayHello

/ Call with parameters
- call !Greet name='World'

/ Call a goal from another file
- call helpers/Utilities

/ Call and capture the return value
- call !CalculateTotal items=%items%, write to %total%
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| GoalName | goal | yes | Goal to call (prefix with `!` for same-file goals) |

## Goal Naming

- **`!GoalName`** — calls a goal defined in the same `.goal` file
- **`path/to/File`** — calls the first goal in another `.goal` file
- **`app /AppName/Start`** — calls an installed PLang app

## Passing Parameters

Parameters are passed as key=value pairs:

```plang
Start
- set %greeting% = 'Hello'
- call !Show greeting=%greeting%, time=%Now%

Show
- write out '%greeting% PLang world at %time%'
```

## Return Values

Goals return their last step's result. Capture it with `write to`:

```plang
Start
- call !Add a=5, b=3, write to %sum%
- write out 'Sum: %sum%'

Add
- add %a% and %b%, write to %result%
```

## Run and Forget

Call a goal without waiting for it to complete:

```plang
Start
- call !BackgroundTask, run and forget
- write out 'Continuing immediately'

BackgroundTask
- wait 5 seconds
- write out 'Background done'
```

## Examples

### Modular Program

```plang
Start
- call !LoadConfig
- call !ProcessData
- call !SaveResults

LoadConfig
- read 'config.json' into %config%

ProcessData
- write out 'Processing with %config.mode% mode'

SaveResults
- save %results% to file 'output.json'
```

### Calling External Goals

```plang
Start
- call helpers/Validation
- call helpers/Transform
```

This calls the first goal in `helpers/Validation.goal` and `helpers/Transform.goal`.
