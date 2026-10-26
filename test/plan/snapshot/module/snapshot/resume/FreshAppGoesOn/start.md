# FreshAppGoesOn

`FreshAppGoesOn.test.goal` resumes `run.json` in its own app, a fresh one, and checks the run goes on from its save:
the context's `%name%`, Sub's parameter `%city%`, Record going on after the call, and Sub's step before the save not
running again.

`run.json` is committed. A run writes it, never a hand: `Record.goal` runs Start's shape (Record calls Sub, Sub saves),
and keeps the snapshot in `run.json`.

**When to run Record again:** when `Record.goal` or `Sub.goal` changes. The snapshot holds each goal's hash, so after a
change the resume answers 409 (`CallbackGoalHashMismatch`) naming the goal's address.

**How:** build the changed goals, then from `test/`:

```
../PlangConsole/bin/Debug/net10.0/plang plan/snapshot/module/snapshot/resume/FreshAppGoesOn/Record.goal
```

and commit `run.json`. The test points the resumed Record's own write into `workspace/` (ignored), so running the test
leaves `run.json` as it is.
