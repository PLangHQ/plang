# Stage 10: the run's other state

**Goal:** a resumed run's app is the same app as the one that saved it: "app should be the same" (Ingi). Besides its variables, call stack, code providers and added properties, which are already saved, this means the state a program builds while it runs.

**Scope:** decisions 43, 44, 48, 115 and 116. The parts are listed below. Each part is traced and proposed by the coder before it's built, and each gets its own commit and review. Anything that needs a locked addition goes to Ingi first.

**Dependencies:** stage 7c and the resume stage (both landed). A snapshot is restored into a sub app of the resumer's (166), so every part here starts into that sub app.

## Parts, in order

### 10a. A program's bindings and the running event (115, 116)

**Today:** `actor.on` (`actor/this.cs:73`, internal) holds what an actor bound while a program ran. It's keyed by the item each binding is on, then the event's name, then before or after (`event/on/list/this.cs`, `module/on/event.cs:73–76`). A snapshot doesn't save it, because the part walk reaches only public `ISnapshot` members. So a resumed run's `on` bindings are gone, and a handler's `%!event%` can't be found (`NoWireContract`, the stage-10 event tests are skipped).

**After (expected shape):**

```csharp
// the actor's hold is a part of the snapshot, at its place under its actor
// (the coder says how the walk reaches an internal member, or proposes the member)

// the hold writes itself:
//   - each held item, by its key: goal, step, action and module have one; a type, a channel and the app need theirs, proposed;
//   - each event, by its name;
//   - each side's bindings, each binding by its own kind.
// binding.action writes {when, scope, call: <its goal.call action, by key or whole: proposed>}.
// A binding C# makes from a step (on.error, on.cache, on.timeout) is proposed kind by kind. A handler is a closure,
// so it is re-made from what the step said, never serialized.

// on restore, the hold is started into the sub app's actor: each item found by its key, its events made, and each
// binding re-made by its kind, before the frames are entered (115). The running event %!event% (frame.Event) is
// saved as { "on": <its item's key>, "name": … } and found again in the restored hold, actor.on[item][name].
```

**Checks:**
- The un-skipped stage-10 event tests pass.
- A binding a program made before the save fires after the resume.
- A handler's `%!event%`, with its `!item` and `!result`, comes back.
- A binding the sub app makes doesn't reach its maker.
- No closure is ever written.

### 10b. Settings, latest where assigned (44), and debug's setting (48)

Ingi: "the setting is just the setting at that point, so default setting for most, param at start or be code would show up where assign, only latest", and "it would not store --debug, but app.debug.enabled = true". The coder traces where a run's settings live (memory or the settings store) and what a sub app already reads through its maker, then proposes what's saved.

### 10c. Channels a program set at run time

The coder traces what a program can open or configure at run time, and what a resume needs.

### 10d. A timer started, and a timeout running

The coder traces what is running and what a resume should do with it: restart it with what's left, or start it fresh. This may come to Ingi as a question.

## Not in this stage

Stage 11: re-sign, and a fixed goal resumed from the same step (57).
