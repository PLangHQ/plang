# Snapshots

A snapshot is a run's state as a value: kept somewhere (a file, a db row) and resumed later, in this process or a fresh one.

```plang
- save snapshot, write to %snapshot%
- save %snapshot% to file 'run.plang'
… later, in a fresh app with the same identity …
- read file 'run.plang', write to %snapshot%
- resume %snapshot%
```

`snapshot.save` answers the snapshot (a `Data<snapshot>`), signed by the app; keeping it is the developer's own step. `snapshot.resume` checks the signature, reads the snapshot onto an app of its own and goes on from where it was saved. Both verbs live in `PLang/app/module/snapshot/` (`save.cs`, `resume.cs`).

## What a snapshot is

A snapshot holds the app, written in its own view (`View.Snapshot`, `PLang/app/View.cs:30`) whoever writes it (`snapshot/this.cs`, `Write`): it is the one member, written under `app`. There is no interface a part implements and no walk over the app. Each object writes its own members that are tagged `[Snapshot]` (`View.cs:484`), and a type with any tagged member shows only those in this view (`View.cs:220–224`); a type with none shows what Store shows, minus `[Sensitive]` members.

A value that something finds (an actor, the app, an event) is written as what finds it, wherever it is pointed at: an actor by its name (`actor/this.cs:65`), the app by its name (`this.cs:48`), an event by its item and its name (`event/this.cs:29`). The value is written whole only where it lives, at its `[Snapshot]` member (`View.cs:226–258`).

What takes part is what is tagged:

- the app: its `property` (what a program added), its `actor` list and its code (`Code`) (`this.cs:53,176,279`);
- Code (`module/code/this.Snapshot.cs`): the DLLs a run took in (`dll`), the providers registered in-process (`provider`) and each kind's default (`default`);
- each actor's events (`actor/this.cs:49`, `event/list/this.cs:241`): for each item a program bound an event on, its moments holding those events (`events.md`);
- each actor: its `property`, its events and its `Context` (`actor/this.cs:24,49,76`; `actor/list/this.cs:22,27` writes `System` and `User` whole, anywhere else an actor is its name);
- each actor's context: its `Variable` and its `call` (`actor/context/this.cs:42,49`); the variables are written as a `row` (`type/item/variable/list/this.Snapshot.cs:14`);
- the call stack and its frames (below).

What nothing tags is not stored: it is the app's own, made again from its files when the app is born (modules, goals, channels, the cache, the filesystem), or it is how the app was started.

## The call stack

A context's `call` (`call/list/this.cs:105`) writes `Current`: the frame the run stopped in, and its callers (`call/this.cs:80`, `Caller`). A frame's children are never written (`call/this.cs:28`). A frame writes:

- its kind, as its plang type: a goal's frame, a step's frame, an action's frame, and the others the stack holds (`call/goal`, `call/step`, `call/action`, `call/binding`, `call/isolated`);
- where it stands, each kind its own part: a goal's frame its `goal` (`call/goal/this.cs:22`), a step's frame the `index` of its step in its goal (`call/step/this.cs:36`), an action's frame the `index` of its action in its step, and the `Module` and `Name` of the action that stood there (`call/action/this.cs:44–54`);
- its own variables, its `Parameter` list (`call/this.cs:304`);
- what a program added to it (`property`), its `Tags`, the error it observed (`Error`), and the `Event` running in it (`call/this.cs:31,75,107,152`).

A goal is written as what finds it: its `Name` and its `Path`, the `.goal` file the goal list loads it from (`goal/this.cs:32,121`). A goal has no hash in the snapshot.

Reading a frame back, the step is found at its saved `index` in its caller's goal, and the action at its saved `index` in its step, as the goal is now (`call/step/this.cs:22–27`, `call/action/this.cs:26–33`). A place past the end is refused, naming how many the goal or the step holds: `StepNotFound` and `ActionNotFound` (404).

A save is refused when it can't be taken whole:

- inside a task: `SnapshotInTask` (400), because a task's variables and frames are reached by nothing a snapshot saves (`this.Snapshot.cs:16–19`);
- while a task runs: `SnapshotWhileTaskRuns` (409), because its frames go on in the task's own stack, which nothing would run again (`call/list/this.Snapshot.cs:43–54`);
- a value the snapshot can't keep (an event it can't make again): the error of that value, whole (`this.Snapshot.cs:22–25`).

## Resume

`resume %snapshot%` goes on with the run in an app of its own (`snapshot/this.Resume.cs`): a sub app made by the asker's actor, layered over the asker's app. Each object reads itself back onto it (`snapshot/this.cs`, `Read`), the code the run took in is taken in again, and then the run goes on in the actor of the asker's role, from the frame it stopped in, outward (`call/list/this.Snapshot.cs:87`): the stopped action again and the rest of its step, the rest of each goal, the action after each call, up to the frame the restored tree went under. The asker's variables, settings and calls are never touched; the step answers what the run answers, and the app ends when the run ends. A snapshot that held no call answers `NoFrameToResume` (400).

A run saved by `save snapshot` stopped inside that action, so a resumed run goes on from the save itself: `save` answers again (a snapshot of the resumed run), then the steps after it run.

`Parallel` (`resume %snapshot%, don't wait`) starts the resumed run as a task of the asker's actor and answers at once; the task's wait gives what the run answers (`snapshot/this.Resume.cs:34–40`).

### A changed goal

The run goes on at its saved place in the goal as it is now. When another action stands there than the one saved, the actor's error channel says so (`'Order' step 1, action 0 is math.add now, not variable.set as it was saved: going on there`, `call/action/this.cs:80–84`) and the run goes on.

### What resume checks first

Before anything is restored, the resume checks what it was given (`module/snapshot/resume.cs`):

- no signature: `SnapshotUnsigned` (403);
- a signature that doesn't hold (a value changed after it was signed): `SnapshotSignatureInvalid` (403), holding verify's own error;
- signed by an identity that is none of this app's non-archived ones (another app's, a revoked one): `SnapshotNotTheApps` (403). The identity the app signs as now and every other it holds that isn't archived open a snapshot, so a rotated default still opens the older ones.

A value that isn't a snapshot is answered: the typed ask's own reason when it gave one (a dict: "a snapshot is made from a snapshot or its text", `snapshot/this.Wire.cs`), else `NoSnapshot` (400), naming where one is kept.

## Signed, in its own view

`save` takes the state once: the app is written in `View.Snapshot`, held as those bytes, and signed by the app's identity through the signer, naming that view (`this.Snapshot.cs:24–37`). Whatever carries it later (a `.plang` file, a db row) carries exactly those bytes. A save never answers unsigned.

`View.Snapshot` is Store's view with no secret in it: a `[Sensitive]` member is left out (`View.cs:222–224`), a sensitive value is written as null (`View.cs:342`: a resumed run asks again rather than going on with a fake value), and a Data's own name is not written (`View.cs:346`: it is named by the key it is held under). A row whose token already says its type (text, number, bool, dict, list, with no kind, template, properties or signature) is written as that token alone (`View.cs:237–242`).

A signature records the view it was hashed in, and verify re-hashes there (`module/signing/code/Ed25519.cs:39,113`). Signing a snapshot again after an edit is `signing.sign`'s job: `sign %snap%, write to %snap%`.

## Editing a snapshot

A snapshot is navigable: `%snap.app.actor.list.user.context.variable.row.x%` reads the user's saved `x`, and `set %snap.app.….x% = 2` edits it (`snapshot/this.Variables.cs`). The stop point is two numbers, each its frame's own: `%snap.app.actor.list.user.context.call.current.caller.index%` (the step) and `%snap.app.actor.list.user.context.call.current.index%` (the action). An edit changes the snapshot alone, never the program that runs. An edit no one signed again is refused (`SnapshotSignatureInvalid`). A snapshot opened and edited is written again first and read back from what it wrote (`snapshot/this.cs`, `Read`).

## In a file

A snapshot file is `.plang`, plang's own format: it keeps the whole Data, its signature included, and the read verifies it. `save %snapshot% to file 'run.plang'` and `read file 'run.plang', write to %snapshot%` are the whole road.

In text a snapshot is its own written form (`snapshot/this.Wire.cs`), made again by its own reader (`snapshot/serializer/Reader.cs`) in the snapshot's view. The reader keeps the app as the bytes the app wrote, until a resume reads them back or a navigation opens them. Text keeps no signature, so a resume refuses a snapshot read from bare text (`SnapshotUnsigned`).

## Templates

A template comes back only from bytes the gate trusts (`type/item/source.cs`). The snapshot's reader does not know where the bytes came from, so a template inside a snapshot comes back as written (`snapshot/this.cs` summary).

## What isn't stored

- **A sensitive value**: a variable holding one, or a `[Sensitive]` member inside a value, is left out or null. A resumed run that needs one asks again.
- **A frame's children**: only the run's own chain is written (`call/this.cs:28`).
- **What the app makes again from its files**: modules, goals, channels, the cache, types, the filesystem.
- **A restored error's run**: an error that came back from a stored state keeps its key, message, status and causes. It holds no run, so it has no callback (`NoCallback`, `error/this.cs:157–168`).

## See also

- `PLang/app/snapshot/` — the snapshot (`this.cs`), its navigation (`this.Variables.cs`), its text (`this.Wire.cs`), its reader (`serializer/Reader.cs`), resume (`this.Resume.cs`).
- `PLang/app/this.Snapshot.cs` — the save, signed.
- `PLang/app/call/list/this.Snapshot.cs` — the stack's resume.
- `PLang/app/module/snapshot/` — `save` and `resume`.
- `test/plan/snapshot/start.md` — the plan's behaviours and the tests that prove them.
- `Documentation/v0.2/callbacks.md` — callbacks.
