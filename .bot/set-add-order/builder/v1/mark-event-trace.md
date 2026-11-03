# Why `before action output.write call Mark` builds Event=%!app.type.step.on.start%

Binary f3d89adf6 (runtime2 + set-add-order; on-event-examples d7606becd is in). Scratch apps, cache skip.

Captures (writer = BuildGoal/Properties): `mark-writer-user.txt` (the user block), `mark-writer-response.txt`.

## Decider: fine
user block line 4: `[1] - before action output.write call Mark => decider: on.event 0.98 (module on 0.98) => formal: on.event(Event, When, Action)`

## Writer prompt: the right teaching is there
user block (line 84 on): the on.event page holds the rule ("an action the step names as module.action ... -> %!app.module.file.read% ...
not %!app.type.action%") and the example `after action output.write call CaptureWrite` -> `%!app.module.output.write.on.start%`.

## Writer answer: wrong
mark-writer-response.txt line 3: `[1] on.event(Event=%!app.type.step.on.start%, When="before", Action=goal.call(Name="Mark"))`

## What decides it: the writer copies a whole example, and does not apply the rule
Event path built, fresh builds each:

| step text (goal called) | Event built |
|---|---|
| `after action output.write call CaptureWrite` (= example) | module.output.write.on.start 3/3 |
| `before action file.read call OverrideFileRead` (= example) | module.file.read.on.start 3/3 |
| `before action output.write call Mark` | step.on.start 5/5 (Mark app) + 0/12 module path in EvA-EvC (3 shapes) |
| `before action file.read call Count` | step.on.start 3/3 |
| `before action http.get call Count` | step.on.start 3/3 |
| `after action output.write call Count` | step.on.start 3/3 |
| `before action output.write, call Mark` | `%!app.on.start%` 4/4 |

Changing only the called goal's name (CaptureWrite/OverrideFileRead -> Count/Mark) flips 3/3 right to 3/3 wrong. The rule and the
`action module.name` sentence are in the prompt in both cases.

## Cause
The module/action path is taught only by example strings, and the writer reproduces an example only when the step text matches it
(name included). For an `action X.y` step with any other text it reaches for `%!app.type.step.on.start%` (the nearest "step" example)
or, with a comma, `%!app.on.start%` (the "before the app starts, call Say" example). The rule line is read as prose, not applied.
Not the decider (0.98/0.98), not the description (in words since d7606becd; it names "an action starting").

## Fix to propose (teaching, mine; nothing built)
Not another example of the same shape: the two `action ...` examples already there are the ones that get copied. Options:
1. Give the examples different goal names and different actions (3-4 `action module.name` examples with varied called names, with and
   without a comma), so a copy of the shape is a copy of the rule. Measure EvA-EvI-like steps before and after.
2. Or C#: a Cover/Agree check that refuses `%!app.type.step...%` when the step text names `action <module>.<action>` (the architect's call; the builder doesn't pattern-match step text, see feedback_plang_is_multilang, so this one is a no).
