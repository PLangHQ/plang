# builder friction — snapshot

## Decider gap: a variable named like a module pulls that module's action (snapshot 32/33)

`save %snapshot% to %where%` built as snapshot.save in 4 of 4 builds (decider certain 0.95–0.97), so the writer's
file.save was refused (StepsBroken). snapshot.save takes no destination, and "to %where%" names one. The same step with
a literal path (`save %snapshot% to 'workspace/saved.snapshot'`) builds to file.save every time, so the decider keys on
the variable target plus the module-named variable.

One teaching line (snapshot/save.description.md: "`to <a place>` is file.save, whatever the value is") did not move it
(0.97); reverted.

Architect's rephrase `save %snapshot% to file %where%`: 1 of 3 builds landed file.save, the other 2 were refused ("has
no entry", the decider answered no action; or snapshot.save at 0.90). With the target `run.json`: 0 of 5 landed
(0.90–0.92 snapshot.save, or "has no entry"). Record.pr is therefore not committed.

WORKAROUND (architect, snapshot 34): rename the variable. With `%snapshot%` → `%run%` in Record and Sub, `save %run% to
file %where%` built file.save in 3 of 3 builds. The gap stays open: a plang developer will write
`save %snapshot% to file %x%`, and the module-named variable still pulls snapshot.save.

WANT: the fix belongs to the build, in its own branch: the variable's known type, or the step's destination, fed into the
module question (see the common-action pre-emption note). Not a teaching edit.

## 2026-10-29 — "before goal Work starts, call Log" builds as "before each goal"

EventComesBack step 0 `before goal Work starts, call Log` built `on.event(Event=%!app.type.goal.on.start%, When="before",
Action=goal.call(Name="Log"))`: the same as `before each goal, call Log` (ResumeTakesInTheBinding). The named goal Work is
dropped, so Log fires for every goal start (the test saw "Resumed", the last goal to start, not "Work"). One build, not
repeated. The coder rephrases the test step; the gap stays open: a developer who names one goal gets every goal.

WANT: on.event takes the goal (a filter on the event) or the Event carries it; the writer then has something to put "Work"
in. Teaching alone can't, since the event has no slot for it.
