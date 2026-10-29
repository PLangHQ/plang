# snapshot — coder v2: 1b, the call stack stored as it lives and started

v1 reviewed (stage 1 accepted, four fixes in 654497b96). Ruled: decision 5 (call.list's Store/Start replace
Section/Capture/Restore; `RestoredChain`, `BottomFrame` and the second chain go). The frames' kinds read back through
their type's `Create` wait for 1c.

## Store (ruled; no question)

The call stack's node:

```
{ root: "a1b2c3d4", current: "e5f6a7b8",
  frames: [                                   // root first, each after its caller
    { id, type: call|binding|isolated,        // the frame's own type writes its kind
      caller: "<id>"?,                        // a reference by id, never the frame
      goal: { name, hash }, step: <index>?, action: { index, module, name }?,
      variables: { … each the Data whole },   // its own (_variables), sensitive left out as the store does
      arguments: [names], for: <index>?,      // the action a binding was pushed for (a tool)
      errors, handled, tags, event?, kept } ] }
```

- The frames written are the live ones, reached from `Root` through `Children` (the stack's tree as it lives, a popped
  frame under history is history, not state: only the chain from root to current and its live siblings).
- Not stored: diffs, timing, `_items` (as in v1's plan).

## Start — two facts that change the ruled shape

1. **`current` can't be put back by Start.** `_current` is an `AsyncLocal`. A value set inside an awaited async method
   (`snapshot.Start` → `call.list.Start`) is undone when that method returns, so `Resume`'s flow would see the old current.
   Start can put back the shared tree (root, each frame's caller and children); current has to be set in the flow that runs
   on from it.
   - **Proposal:** Start installs the tree and holds the stored current; the run going on is the call list's own door,
     `context.call.Resume(context)` (replacing `snapshot.Resume`'s chain and recursion), which sets current in its own
     flow and goes outward.
2. **The frames can't be born back without 1c.** A frame's kind (call, binding, isolated) is its type, and reading it
   back without a switch is ICreate on the three, which is 1c. Store can land now. Start can't, so landing Store alone
   would leave `resume` with nothing to read.
   - **Proposal:** 1b lands Store and Start together once 1c is ruled. Until then I write Store and the resume loop
     against an internal frame constructor, and keep them uncommitted.

## Going on from current outward (the resume loop)

From the stored current, each frame in turn, innermost first:
- an action frame: run its action again (the ask takes its answer), then the rest of its step;
- a step frame: the rest of its goal's steps;
- a goal frame: done; dispose it (current back to its caller);
- a calling action frame (`goal.call`): its action is done, so go on to the next action of its step.

`goal.Resume`/`step.Resume` push a new step frame today. On a restored tree they must run in the restored frames, so the
loop takes their places (no second frame pushed). Exact loops (`foreach` saved mid-items) stay with Ingi: until then a
calling `foreach` goes on after itself, as today.

## Ruled (architect, after this plan)

- (1) the call list's `Resume`, internal, only snapshot.Resume calls it; brought to Ingi as 1f; the name may change with
  his question 5.
- (2) 1b's commit waits for 1c; no temporary switch on the stored kind.

## State

Written, uncommitted, green (after the architect's 1b review):
- Store: the tree nested as it lives — `root`, each frame's `children` under it (no `caller`: a frame's caller is the
  frame it sits under), `current` the one reference, by id. A frame's goal by its address and hash. Its errors and the
  event running in it are stored. A running task's frame among the children refuses the save (`SnapshotWhileTaskRuns`).
  The error's thrown chain is nested the same way, each frame's one child the frame it called.
- Start: walking down from the root, each frame born again under the frame it sits under through an internal
  constructor taking its saved id; the goal through `app.goal.Get(address)`.
- Resume: outward from current, each frame's own `Rest` (a binding has none); `goal.Resume`/`step.Resume` run in the
  frames that came back.
- Gone: `Position`, `RestoredChain`, `BottomFrame`, the old `Capture`/`Restore`/`Section`.
- A stored binding or isolated frame answers `FrameKindUnread` until 1c; `_for` comes with 1c.

## Blocked

- 1c (ICreate on the frame kinds).
