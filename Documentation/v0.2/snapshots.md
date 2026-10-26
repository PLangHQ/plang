# Snapshots

A snapshot is a run's state as a value: kept somewhere (a file, a db row) and resumed later, in this process or a fresh
one.

```plang
- save snapshot, write to %snapshot%
- save %snapshot% to file 'run.txt'
… later, in a fresh app …
- read file 'run.txt', write to %snapshot% as snapshot
- resume %snapshot%
```

`snapshot.save` answers the snapshot (a `Data<snapshot>`); keeping it is the developer's own step. `snapshot.resume`
puts it back and goes on from where it was saved. Both verbs live in `PLang/app/module/snapshot/`.

## Who takes part: `ISnapshot`

A part is a class that implements `ISnapshot` (`PLang/app/snapshot/ISnapshot.cs`), with two members:

- `Store(snapshot, context)` writes the part's own state into the node at its place;
- `Start(snapshot, context)` comes back from what that node holds.

Both answer Data: a part that can't store or start answers why, never a throw. Both default to doing nothing, so a part
that only holds parts (the actors, an actor, a context) writes nothing of its own.

The parts today: the app (what it keeps), Code (`module/code/this.Snapshot.cs`: the DLLs a run took in, the providers
registered in-process, each kind's default), the actors, each actor, each actor's context, a context's variables
(`type/item/variable/list/this.Snapshot.cs`) and its calls (`call/list/this.Snapshot.cs`).

A class that doesn't implement it is rebuilt from the app's own files instead: modules, goals, channels, the cache, the
filesystem.

## The walk

`app.Snapshot(context)` walks the live app from `app` (`snapshot/this.Store.cs`). A part's parts are its public members
whose declared type is an `ISnapshot`, depth first, in declaration order; no other member is read. Each part is stored
at the node of its member path from the app (`actor.list.User.Context.Variable`), never under a name it picks. A node
that holds nothing is left out.

Starting walks the stored tree the same way: each live part at a stored place starts from its node, in the order it was
stored (Code before the actors). A live part with no node held nothing when it was saved, and stays as it is.

**Each part is walked in its own context.** A part's answer carries a context, and the walk goes on into its parts in
that answer's context. A part answers in the context it was handed; an actor's context answers in itself. So a context's
variables and calls are stored and started in their own actor's context, both ways.

A save from inside a task is refused (`SnapshotInTask`): a task's context is no member of anything the walk reaches.

## Coming back as the right actor: the `In` door

Every Data a restore puts into live memory answers as the actor whose memory it goes into, never as whoever read the
snapshot's text. A part takes stored Data only through the snapshot's door, `snapshot.In(context)` (and `In(key,
context)`): each entry cloned, so the run never shares a value with the snapshot, and born again in the part's context.
What is still unread (a value through text) opens in that context too. `snapshot.Read<T>(key, context)` takes a list
entry the same way; a row that doesn't read is the answer, its own error whole.

A clone never copies a context. Lists, dicts and snapshots clone by their elements, each Data through `Data.Clone`
(which keeps its context); live or shared values (a frame, the stack, an error, an actor, a goal, a step, a channel, a
store, the app) are themselves when cloned.

## The call stack

A context's calls (`context.call`) are stored as they live: `root`, each frame with the frames it called under it in
`children`, and `current`, the id of the frame the run is in. A frame writes:

- `id`, and `type`, its kind as its plang type (`call`, `binding`, `isolated`);
- where it stands: `goal` (`address` and `hash`), `step` (its index) and `action` (`index`, `module`, `name`);
- its own variables, as a node of their own, so they come back through `In`;
- the names it was born with (`arguments`), its `errors`, `handled`, the `event` running in it, its `tags` and what it
  keeps.

A collection is written only when it holds something.

Starting a stored frame has two halves:

1. The async half, `call.Find`: each entry taken in through `In` and opened once, its goal found by its address through
   `app.goal` (it may load) and checked against the stored hash, and the setting it is born with (the stack's, as it is
   now). Each is narrowed into the read copy; the stored text is never touched.
2. Its kind's own `Create`: `call`, `binding` and `isolated` each implement `ICreate` of themselves, and the stored
   type dispatches to it. There's no switch on the kind. A frame comes back under the stack's current frame, entering as
   a push enters.

A goal that isn't there is the goal list's own answer. A goal whose hash changed is `CallbackGoalHashMismatch` (409).
A step or action index out of range is `CallbackGoalNotFound`, and a live action that isn't the stored one is
`CallbackActionMismatch`. A frame missing what it needs is `CallbackFrameIncomplete`. A goal with no address can't be
stored (`GoalHasNoAddress`), and a save while a task runs is refused (`SnapshotWhileTaskRuns`).

## Resume

`resume %snapshot%` is a call. The restored tree goes under the resume action's frame, the way a goal call's goal goes
under its caller. The run goes on from the frame it stopped in, outward, each frame running what is left of it (the
stopped action again, the rest of its step, the rest of each goal), up to the resume action's frame, where it stops. The
resuming goal then goes on once, after the resume. The resume's answer is the restored run's.

A run saved by `save snapshot` stopped inside that action, so a resumed run goes on from the save itself: `save` answers
again (a snapshot of the resumed run), then the steps after it run.

A value that isn't a snapshot is answered: the typed ask's own reason when it gave one (a dict: "a snapshot is made from
a snapshot or its text"), else `NoSnapshot` (400), naming the step that reads one.

## What isn't stored

- **The app's mode** (test, build, run): it's how the app was started, the host's choice, not the program's state. A
  restore leaves the app as it was started.
- **A one-time grant** ("y"): consent nobody gave in the run that would use it. A resumed run asks again.
- **A sensitive value**: a variable holding one is left out, and a resumed run that needs it asks again. A `[Sensitive]`
  member inside a value is not yet masked by the snapshot's write (a pinned red test,
  `ASensitiveMember_IsNotInTheSnapshotsText`).
- **A binding's held call** (`_for`): it is read only while that call binds its parameters, before anything runs under
  the binding.
- **What the app rebuilds from its files**: modules, goals, channels, the cache, types.
- **A frame's history**: its diffs and timing (a resumed frame runs from now).
- **A restored error's run**: an error comes back with its key, message, status and causes. It holds no run, so it has
  no callback (`NoCallback`).

## In a file

In text a snapshot is its own typed form (`snapshot/this.Wire.cs`): its entries, each the Data it is with its type. A
file whose format is text keeps it as that, so `save %snapshot% to file 'run.txt'` writes it, and `read file 'run.txt',
write to %snapshot% as snapshot` reads it back exactly through the snapshot's own reader. The read must say `as
snapshot`: a step that doesn't gets text.

A `.json` file holds a value's content in JSON, with no types, by that format's nature. A snapshot read back from one is
plain dicts and can't resume. A file whose extension has no format (`.snapshot`) takes plain bytes, so a snapshot is
refused there.

## Templates

A template comes back only from bytes the gate trusts (`type/item/source.cs:76–81`), by whose bytes they are: the
build's own `.pr` (every variable its row lists), a file or url the program read (the program's own variables), anything
else none. Raw text and the store are trusted for none, so a template restored from them reads as written.

Today a template inside a container comes back literal by every road, a file included: the bytes' origin is lost below
the snapshot's reader (`type/this.cs:469,475`, `type/item/wire/this.cs:49`, `type/item/serializer/json.cs:103,197`).
That gap is in the backlog.

## Open

- What a snapshot's text looks like to a reader: open (Ingi's question).
- Resuming a loop mid-items, and any action that holds a running call (a tool call, an event handler): open (question
  3). Today a resume under one runs that action again from its start.
- Which saves are refused because they can't come back exactly: open (question 4).
- The resume verb's name, and going on after the save instead of from it: open (question 5).

## See also

- `PLang/app/snapshot/` — the snapshot, its walk (`this.Store.cs`), its text (`this.Wire.cs`), resume (`this.Resume.cs`).
- `PLang/app/call/this.Snapshot.cs`, `call/list/this.Snapshot.cs` — a frame and the stack.
- `PLang/app/module/snapshot/` — `save` and `resume`.
- `Documentation/v0.2/callbacks.md` — callbacks (later).
