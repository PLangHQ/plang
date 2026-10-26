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

WANT: the fix belongs to the build, in its own branch: the variable's known type, or the step's destination, fed into the
module question (see the common-action pre-emption note). Not a teaching edit.
