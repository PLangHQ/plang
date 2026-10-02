# v21 — task stage 2b: the `call` concept — trace first, then shape

Decision 530: `callstack` → the `call` concept (`context.call`, `.list` the frames, `.current` the one running); the two
call stacks become one chain; each frame holds its own variables. Stage 2 accepted (daad36895, 17da35927).

## Trace — the two stacks today

### 1. The action stack — `callstack.@this` (`app/callstack/this.cs`), one per actor (`actor.CallStack`; `context.CallStack => Actor.CallStack`)
One `AsyncLocal<call.@this?> _current` per stack instance. Frames (`callstack/call/this.cs`) are pushed by:

| push site | frame | holds |
|---|---|---|
| `goal/this.cs:379` `Push(goal)` | goal frame (Goal, no Step) | `Tags` (`%!callStack.Scope.Tags%`), `Children`, `Errors`, timing, the `Scope` of its steps |
| `goal/step/this.cs:115` (+ `.Resume.cs:19`) `Push(step)` | step frame (Goal, Step) | `Children` (its actions), `Errors`, timing |
| `goal/step/action/this.cs:190` (+ `snapshot/this.Resume.cs:40`) `Push(action, context.Variable)` | action frame (Goal, Step, Action) | `Event` (a bound call running), `Items` (`on.timeout`'s Deadline), `Diffs` of its store, `Position`/`Index` (resume), `Errors` |
| `goal/step/list/this.cs:163` | a goal frame for a step list run outside its goal | as the goal frame |

Stack-wide: `Audit` (every error this run), `Root`, `MaxDepth` (1500 = ~500 goal levels, three frames each), the
setting (`Timing`/`Diff`/`Tags`/`History`). Walks outward from `Current`: `Scope` (nearest goal frame), `Error`
(nearest unrecovered), `Event` (nearest running bound call), `Goal`, `Step`.

### 2. The variable stack — `variable.call.list.@this` (`type/item/variable/call/list`), one per variable store (`Variables.Calls`)
Its own `AsyncLocal<call.@this?> _current`. Frames (`variable/call/this.cs`) hold the names they were born with
(`_born`, `_entries`), a `Caller`, and the held action they were pushed FOR (`_for`, a tool's). Pushed by:

| push site | frame | born with |
|---|---|---|
| `module/goal/call.cs:101` `Push(bound)` | a goal call's | its parameters — pushed *before* `goal.Start` pushes the goal frame |
| `module/loop/foreach.cs:66` `Push(bound)` | one iteration | `%item%` (`%key%`/named) |
| `app/this.cs:508` | an error handler's | `%error%` |
| `channel/type/goal/this.cs:149` | a goal channel's goal | `%message%`, `%where%` |
| `module/llm/code/OpenAi.cs:407` | the response validator | `%response%` |
| `OpenAi.cs:518`, `:554` | OnToolCall | the tool call's state |
| `OpenAi.cs:530` `Isolate(parameters, tool.Held)` | a tool's goal | the model's arguments; **isolated** |
| `shortcut/this.cs:44` `Isolate(null)` | a shortcut goal | nothing; **isolated** |

`isolated` (`variable/call/isolated`): `Keeper => this` — every write under it stays in it.

### 3. How a name resolves today
- **Read** (`variable/list/this.cs` `Get`): `Calls.Current.TryGet` walks the variable frames up their `Caller`s; then the
  store's own memory; then (a task's store) its caller's store, live.
- **Write** (`Bind`): `Calls.Current?.Keeper(name)` — the nearest frame *born with* the name (an isolated frame: itself),
  else the store's memory.
- **A task (stage 2):** a child store whose `Calls` IS its caller's call list, so in the task's flow `Calls.Current` is
  the caller's frame. That is the leak you named: `Keeper` finds the caller's frame for any name it was born with — a
  task's `set %city% = "x"`, `city` a caller's parameter, writes into the caller's frame.

## Is one chain right? — yes, with three costs to name

**Why it fits.** Both stacks are AsyncLocal chains of the same flow, pushed and popped LIFO in the same async flow,
and they nest consistently: the variable frames sit *between* action frames (goal.call's parameter frame is pushed in
the goal.call action's frame and wraps the goal frame `goal.Start` pushes; foreach's iteration frame wraps the body's
frames). So every variable frame has a place in the one chain:
- a goal call's parameters ARE the goal frame's variables (`goal.Start(context, parameters)` pushes one frame);
- an iteration, an error handler, a channel goal, a validator, OnToolCall: a frame of its own in the chain, holding
  just its variables (no goal/step/action) — the same "a frame that binds names" the variable frames are today;
- isolated is a frame property: `Keeper => this`.

**Resolution up one chain:** read — each frame's own variables outward from `call.current`, then the memory of the
context whose chain it is (for a task: its caller's frames, then its caller's memory — the read-through, now one walk,
not a store hop); write — the nearest frame that keeps the name (born with it, or isolated), else this context's
memory.

**A task's call (closes the frame-held leak):** the task's goal frame is isolated with read-through — every write the
task makes lands in its frame (`Keeper => this`), reads fall through to its caller's chain and memory. The child store
and the task's frame become one place: a task's memory IS its frame, so the separate reads-through `Variables` from
stage 2 goes (`Own`'s copy binds into that frame). Pin: a task setting a name its caller's frame keeps leaves the
caller's value unchanged.

**The three costs (none makes it wrong):**
1. **Depth.** Variable-only frames would count toward `MaxDepth` and `.Current.Depth`. Two test goals read
   `.Current.Depth` (`test/app/callstack/DepthIncreasesOnGoalCall`, `DepthRestoresAfterCall`, `Inner.goal`). Shape:
   `Depth` counts goal/step/action frames only (what it means today); a binding-only frame doesn't deepen.
2. **The observed tree.** `Children`/`History`/timing/`Diffs` record frames for observability; a binding-only frame
   (an iteration) would join the tree. Shape: it joins as what it is (an iteration is a real place a failure happened —
   gain, not noise), timing/diffs off for it.
3. **Snapshot/resume.** The call stack snapshots its frames (`callstack/this.Snapshot.cs`, `call/this.Snapshot.cs`);
   variable frames are not snapshotted today, so a resumed run loses a call's parameters. One chain means the frames'
   variables ride the snapshot — a fix, and the snapshot plan's concern (queued behind task): I'd write the frames'
   variables as `[Store]` and leave resume's restore of them to that plan, saying so.

## Shape (after the trace is accepted)
- `app/call/` (the concept): `call.list.@this` (the chain owner on the context: one AsyncLocal current, `Audit`, `Root`,
  `MaxDepth`, the setting) at `context.call`; `call.@this` the frame (today's `callstack.call` + `variables`: born
  names, entries, `_for`, `Keeper`); isolated as a frame kind. `call.current`, `call.list` (the frames), `call.scope`.
- `context.call` (the context owns it; `Actor.CallStack` goes): a task's child context owns its own chain whose root
  frame's `Caller` is its caller's current frame.
- Variables: `Calls` goes; `Get`/`Bind`/`Supplies`/`Own` walk `context.call.current`.
- Plang-visible renames: `%!callStack…%` → `%!call…%` (`.Current` → `.current`, `.Scope` → `.scope`, `.Audit` →
  `.audit`), `%!app.callstack…%` → `%!app.call…%`.

### Readers that move (`%!callStack…%` / `%!app.callstack…%`)
- os: `os/system/shortcut/goal.goal:3` (`%!app.callstack.scope.caller.goal%`), `os/system/shortcut/step.goal:3`
  (`…scope.caller.step%`) — rebuilt by the builder (their `.pr`).
- test goals: `test/app/actor/context/ActorContext.test.goal:4`; `test/app/callstack/` — `Audit.test.goal:6`,
  `CrossFileChain.test.goal:5,6`, `DepthIncreasesOnGoalCall.test.goal:3`, `DepthRestoresAfterCall.test.goal:3,5`,
  `Diffs.test.goal:1`, `Inner.goal:3`, `TagBareLabelWritesTrue.test.goal:1,4`, `TagWritesPairsOntoCurrentCall.test.goal:1,4,5`,
  `ThrowItem.goal:1`; `test/module/variable/contextvars/advanced/ContextVars2.test.goal:9`. (`Tests/` doesn't exist.)
- builder templates (os/system/builder `.liquid`/`.llm`): none read it.
- C#: 198 `.CallStack` sites (PLang, PLang.Tests, PlangConsole) and 37 `.Calls`.
