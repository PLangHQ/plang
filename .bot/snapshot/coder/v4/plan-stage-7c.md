# Stage 7c: the stack saved as its frames (plan, with one problem for the architect)

Patterns: saved data maps by a view and its tags (1, 84: read, map, set); whoever numbers it finds it (a step by its
goal, an action by its step); one door (the typed ask, `data/this.cs:620`).

## What is uncontroversial

- The stack's Store writes its frames from the root to where the run is, a list, each frame as itself (no `children`,
  `current` or ids; the last frame is where the run stopped). A running task's frame is still refused.
- The frame's own `Write` override (`call/this.cs:35–70`) and `call/this.Snapshot.cs` (Store, Find, Placed, Stored,
  Incomplete) go. The frame's state is tagged members: `[Store]` variable, property, tags, event, errors, handled.
- The frame's hash check (`call/this.Snapshot.cs:93–105`, CallbackGoalHashMismatch) goes: one check, on the goal's key.

## The problem (traced)

A tagged member is written bare: `item.Members` (`type/item/this.cs:895–907`) writes `name: value`, no record, so
no type. Only a member that holds a Data is written as a record (`:897–901`).

1. **The goal.** `View.Type` (the `{goal, key}` kind) is asked only at a record (`wire/kind/plang/writer.cs:48`). A
   frame's `Goal` written as a member is `"goal": {name, pr, hash}`, with no type. The general read
   (`reflection/this.cs:176–198`) makes it by the declared type, `type.Make(dict)` → `goal.Create`, which declines (a
   goal is never converted from a value). Finding a goal is async (`goal.Select`, it may read a .pr); `Make` is sync.
2. **The step and the action.** Their keys (`{index}`, `{index, module, name}`) mean something only under their owner:
   `goal[index]`, then `step.Code[index]` checked by module and name. A step or an action can't be made from its key
   alone, so the general read can't set them either.

So the frame's place can't come back by read/map/set member by member: the goal needs an async ask, the step and the
action need the member before them.

## My proposal (for the architect, not built)

The frame holds its place as it is found: the goal first, then the step through the goal, then the action through the
step. The stack's Start, which is async, does it in that order:

```csharp
// call/list/this.Snapshot.cs — Start, one stored frame (record: the frame's dict, read by the general reader)
var goal   = await record.Get("goal", context)?.Value<goal>();       // the typed ask: (goal, key) → goal.Select
var step   = goal?[index];                                           // its owner numbers it
var action = step?.Code[index] checked by module/name;               // its owner holds it
var frame  = kind.Push(goal, step, action, ...);                     // the kind's own constructor, as a live push
new reflection.@this().Read(record, frame, context);                 // the rest, mapped: variable, property, tags, …
```

For the goal to reach the typed ask as `{goal, key}`, the frame's goal has to be written as a record, so the member
the snapshot shows is the goal as a Data (`data<goal>`), which the members walk writes whole (`:897–901`). That's a new
member on call (locked), so it's Ingi's. The alternative is for the snapshot view's `Members` to write a found member
as a record. That's a second shape for one walk, so I don't recommend it.

**How the frame holds its goal between the read (sync) and its Start (async):** it doesn't. The record holds the goal as
an unopened Data (a wire under `{goal, key}`) and the stack's Start opens it with the typed ask before the frame is
pushed. The frame is born with the live goal, as a live push is.

Tests (from the expected code): FreshAppGoesOn green; a binding's `%item%` and a handler's `%!event%` come back; a
frame's `%!call.scope.x%` comes back; a save inside a task still refused; no `children`, `current` or ids in the text.
