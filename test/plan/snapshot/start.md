# snapshot

A run's exact state, saved to a file or a store, and gone on with later (Ingi: "everything. this is not for web
requests, we save this to disk or db and run later"), even after its program changed, or after a program moved the point
it goes on from.

```
- save snapshot, write to %snapshot%
- save %snapshot% to file 'run.plang'
… later, in a fresh app …
- read file 'run.plang', write to %snapshot%
- resume %snapshot%
```

`start.goal` beside this is the plan's index: each behaviour a comment, the step under it running the plang test that
proves it; a behaviour proved only in C# names its test there.

## What a snapshot is

- **The app in the snapshot view.** A snapshot holds the app, written in its own view whoever writes it (a .plang file,
  the wire): each object writes its own tagged members (`[Snapshot]`), and anything that points at an object writes what
  finds it (an actor by its name, a goal by its name and its `.goal`). Read back, each object reads itself onto the app
  the run goes on in.
- **Signed by the app.** A save is signed by the app's identity, in the snapshot's view; a resume takes only a snapshot
  one of the app's own identities signed, as it is.
- **The call stack as it stood.** The frames from the root to the one the run stopped in, each writing what it keeps:
  a goal's frame its goal, a step's frame its own place in its goal (`index`), an action's frame its own place in its
  step (`index`) and the module and name of the action that stood there. A frame's step or action is found from its
  place, under its caller, as the goal is now.
- **The event in play.** A run saved while a bound call runs keeps `%!event%`: the event is written as what finds it
  (its item and its name) and comes back as the resumed app's own event of that name.
- **Plain rows bare.** A saved row whose token already says its type (text, number, bool, dict, list, with nothing more:
  no kind, no template, no properties) is written as its token; any other keeps its type. Only in the snapshot view —
  the store (`.pr`) and the wire are untouched. The app's event list rows keep their type label until typed lists come
  (a later branch).
- **Never a secret.** A `[Sensitive]` value is never saved: it comes back empty (null).

## Going on

- **Resume** goes on in an app of its own (a sub app of the asker's actor), from the frame the run stopped in, outward:
  the stopped action again and the rest of its step, the rest of each goal, the action after each call. A call saved
  mid-step is finished, then the rest of its step runs once.
- **A changed goal** is the goal as it is now: the run goes on at its saved place in it. When another action stands
  there than the one saved, that is said on the actor's error channel (`'Order' step 1, action 0 is math.add now, not
  variable.set as it was saved: going on there`) and the run goes on. A place past a goal's or a step's end is refused,
  naming how many it holds (`StepNotFound`, `ActionNotFound`). A goal's key has no hash.
- **A sub app** takes in its maker's bindings at birth and fires them for its own user; its save holds only what it
  bound itself. A resume's app is one: a binding the resuming app made fires in the resumed run.

## Editing a snapshot

- The stop point is two numbers in the snapshot, each its frame's own, both settable from a program:
  `%snap.app.actor.list.user.context.call.current.caller.index%` (the step) and
  `%snap.app.actor.list.user.context.call.current.index%` (the action).
- An edit changes the snapshot alone, never the program that runs: a frame's number is the frame's, and a step's own
  place (`step.index`), like every member of an object apps share, is read-only to a program (`ReadOnly`).
- A program vouches for its own edit by signing the snapshot again (`sign %snap%, write to %snap%`); an edit no one
  signed again is refused (`SnapshotSignatureInvalid`). Signing is `signing.sign`'s job: it hashes the data in its own
  type's view, the one a verify re-hashes it in. **Decision 72's `snapshot.sign` is superseded by 287:** a snapshot has no
  signing action of its own.
- An edited snapshot kept again is the saved bytes plus the edit.

## Proved in C# only

- The changed-goal warning needs the goal rebuilt between the save and the resume: `CallSnapshotTests`.
- The signature's refusals (another app's, an archived identity's, a hand edit): `SignedSnapshotTests`.
