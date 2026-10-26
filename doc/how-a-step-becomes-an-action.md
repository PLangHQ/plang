# How a step becomes an action

For someone writing a program. The question this page answers: what does the build do with my words, and how do I see it?

## One step, one or more actions

The builder turns each step into actions of the form `module.action(parameters)`. A step with a second job gets a second action.

| Step | Becomes |
|---|---|
| `set %n% = 3` | `variable.set` |
| `write out "Hello %name%"` | `output.write` |
| `if %n% is 3, call Three` | `condition.if` (the call is inside it) |
| `foreach %person.langs%, call Say item=%item%` | `loop.foreach`, `goal.call` |
| `call Double n=21, write to %answer%` | `goal.call`, `variable.set` (Value `%!data%`) |
| `call Fails, on error write out "recovered"` | `goal.call`, `on.error` |
| `before action output.write call Mark` | `on.event` |
| `read missing.txt, on error 404, write out "no such file"` | `file.read`, `on.error` |

These came from the `.build/start.pr` of the scratch apps these pages were run on.

## See it

`plang build` writes `.build/<goal>.pr`: JSON, one entry per step with its `code` (module, name, properties). Read it, never edit it:

```bash
jq -r '.step[] | "\(.index) \(.text) => " + ([.code[]? | "\(.module).\(.name)"] | join(" ; "))' .build/start.pr
```

## Write the action yourself

A step may be written as the action: `variable.set(Name=%x%, Value=1)`, `goal.call(Name="Slow", Parallel=true); variable.set(Name=%task%, Value=%!data%)`. The repo's tests use this form. Two actions in one step are joined with `;`.

## Where the actions are

An action is in a module. `os/system/modules/<module>/` holds the teaching for each: `<action>.description.md`, `<action>.examples.md`. Four modules also have a generated page: [condition](../os/system/modules/condition/start.md), [file](../os/system/modules/file/start.md), [loop](../os/system/modules/loop/start.md), [screen](../os/system/modules/screen/start.md).

## When the build disagrees with you

- `Properties rejected — retrying: …` is the builder checking its own answer and trying again. Many appear in normal builds.
- A step it cannot fix fails the build: [when a step fails](when-a-step-fails.md).
- A step that builds but means something else shows up at run time. The run, not the build, tells you: see [calling goals](calling-goals.md#a-call-that-builds-wrong).
- One step can hold only the actions the builder can pair. Two `on error` clauses on one line did not build; one clause per indented line did ([errors](errors.md)).

A second `plang build` with nothing changed builds nothing.
