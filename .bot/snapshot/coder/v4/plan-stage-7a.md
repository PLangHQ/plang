# snapshot — coder v4 — stage 7a: the frame kinds (81, 83)

## The change

- `call.goal`, `call.step`, `call.action`, each a frame kind in its own file beside `call.binding` and `call.isolated`.
  Each takes its constructor from `call.@this` (goal + names; step; action + variables) and its own node constructor and
  `Create` for a stored frame (as binding has, until 7c replaces the storage). `call.list`'s three `Push`es make the
  right kind; the base `Call` type goes.
- Each kind does its own rest:
  - an action's frame: the rest of its step's code, the stopped action again or from the next (a C#-composed action,
    in no step, has nothing left);
  - a step's frame: the rest of the list that holds its step, the goal's steps or a body's, from its list position.
    The goal answers it: `step.list.Rest(step, context)` runs what follows the step in the list that holds it, looked
    for through the bodies under it;
  - a goal's frame: nothing.
- The base `Rest` answers Ok; its null-field fork goes. `IsGoal` is false on the base, true on the goal's frame.
- Placing a stored frame finds its step by its goal-wide index across bodies (`step.list.At(index)`), not the list
  position.
- `goal.Resume(context, index)` goes (only the null-field Rest called it).
- Boy scout: `step/this.Resume.cs`'s catch answers through `Error.FromException`.

## Tests

- `AResume_AfterAStepWithABody_GoesOnAtTheStepItStoppedIn` (known red) goes green.
- New: a step inside an `if` body resumes the rest of its body, then the rest of its goal.
- Surface re-pinned citing "snapshot 81".
