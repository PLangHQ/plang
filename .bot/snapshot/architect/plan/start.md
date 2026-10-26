# snapshot

Cut from runtime2 after `windows`. Replaces `test/plan/snapshot/start.md` (2026-10-02), which this cuts down to what Ingi decided on 2026-10-26 and what's left in the code.

## Why

A run's exact state, saved to disk or a db, and run later (Ingi: "everything. this is not for web requests, we save this to disk or db and run later").

```
- save snapshot, write to %snapshot%
- save %snapshot% to 'run.snapshot'
… later, in a fresh process …
- read file 'run.snapshot', write to %snapshot%
- resume %snapshot%
```

**Today:** the last two steps fail. Nothing turns a file's text back into a snapshot (`snapshot.Create` is `value as @this`, `snapshot/this.Wire.cs:42–43`); only the C# helper `SnapshotFromWire` can. The six `SnapshotWireTests` reds are this. The parts that take part are a fixed list on the app (`app/this.Snapshot.cs:7–9`), each writes a section it names by hand, the call stack keeps only the live caller chain and loses each frame's own variables (parameters, `%item%`, a handler's `%error%`), and resume walks a second chain (`_restoredChain`, `ResumeChain`'s recursion).

**After:** a part that takes part says so itself; `snapshot.save` walks the app tree and stores each; the tree is saved as it lives; resume starts each part where it was.

## Ingi's decisions (2026-10-26)

- A class that takes part implements `ISnapshot` with two members: `Store` (it writes itself into the snapshot) and `Start` (it comes back from what it stored). The snapshot knows nothing of who takes part.
- `snapshot.save` walks the live app tree from `app` and stores each node that is an `ISnapshot`: no list on the app, no hand-written section names.
- The tree is saved as is: each part at its own place, and starting walks the saved tree.
- The call stack is the run's (`context.call`): `root`, each frame's `children` beneath it, `current` as a reference into that tree. Starting restores the exact same state.
- Everything, one size. A web request's resume is a callback, later (not here). The app's world to a file (the "app snapshot") waits for a user.
- `start %callback%` stays as it is (callbacks later).
- Standing (item-doors 311): a snapshot never carries a `[Sensitive]` value; a resumed run that needs one asks again.

## Stages

1. **`ISnapshot` is `Store` and `Start`; `snapshot.save` walks the tree.** Today's five participants move: Code's providers (`module/code/this.Snapshot.cs`), Variables (`variable/list/this.Snapshot.cs`), Kept (`kept/list/this.cs`), the app's mode (`app/this.Snapshot.cs:47–61`), the call stack. The list (`:7–9`), `Section`, the private `Capture` loop and the app's own sections go. Each part's place in the saved tree is its path from `app`. Code's restore loads its DLL through the async path verb, never `.GetAwaiter().GetResult()` (`:116`).
2. **The call stack stored as it lives, and started exactly.** Root, each frame's children, current as a reference; each frame with its position (goal by name and hash, step, action) and its own variables (`call/this.cs:179–180`). Starting pushes the frames as they were and goes on from current: `Position`, `_restoredChain`, `RestoredChain`, `BottomFrame` and `ResumeChain`'s recursion go. A goal gone or changed fails hard (`CallbackGoalNotFound`, `CallbackGoalHashMismatch`, `CallbackActionMismatch` stay); the PrPath fallback for old snapshots goes.
3. **One door from text to a snapshot.** A snapshot read from its own text (a file, a db row) is born through the snapshot type's reader, so `resume %snapshot%` takes what a file read gave. `SnapshotToWire`, `SnapshotFromWire`, `ResumeFromWire` go (middlemen). The six `SnapshotWireTests` reds go green.
4. **Never a `[Sensitive]` value.** The snapshot is written in the Store view today (`snapshot/this.Wire.cs:31`), which writes a `[Sensitive]` member and a `sensitive` value whole (`View.cs:125–152`). The snapshot's write masks them; a resumed run that needs one asks again. `IdentityPrivateKeyLeakTests` pins it, plus a probe for a `sensitive` variable.
5. **`snapshot.save` and the proof in plang.** The verb answers the snapshot (a value); saving it is the developer's step. Plang tests: save, write to disk, read in a fresh app, resume at the step, with the same variables and frames (a call's parameters and a loop's `%item%` among them). The three unbuilt ask tests in `test/plan/snapshot/module/callback/` move out to the callback plan's list, unbuilt.
6. **Docs.** `Documentation/v0.2/snapshots.md` and `callbacks.md` describe types that are gone (`ISnapshotted`, `ICallback`, `AskCallback`, `ErrorCallback`, `RestoredFrame`); rewritten to what is, callbacks marked "later".

### Second round (Ingi, 2026-10-26, decisions 42–67)

Ingi's rule (67): when a resume starts executing at step X in goal Y, the state is what it was at the save, except what was changed on purpose and re-signed (a value edited by hand, a goal fixed).

7. **The snapshot laid out like the app; the call stack a chain of what each goal frame wrote.** No whole-Data wrapping (42): a node is a plain object, a part's field its bare value, variables whole Data in a list. `app.id` saved; a snapshot resumes only in its own app (43). The frame is a tagged bag (its `Write` override goes; 52, 53): the trace `[Out, Debug]`, its place, `tags`, `Variable`, `Event`, the error in play and the frames its action pushed `[Store]`. A goal call's place is `goal {name, prpath, hash}` and a list of steps, outermost first, each holding its action `{index, module, name}` (47, 56). The stack writes one record per goal frame from the first goal call to where it stopped (45, 50, 51); on start it reads each goal and step place by its own type, finds the goal by its .pr and name, checks its hash (49), and pushes through a `Push` that takes the saved record, the frame reading its own record at birth (62, 63); pushed frames through their kind's own door (15, 46). The node constructors, `Placed`, `Stored`, the key reads, ids, `children` and `current` go. Expected code: `plan/expected-stage-7.md`.
8. **What a program adds to a thing is its `property`, saved with every value** (66, 67). `kept.list` → `item.property.list`; each keeper (app, actor, call, goal, step, module) declares its `property` node; the general write and read carry a value's added properties wherever plang saves a value; the app's hand-written kept goes. Each keeper's `%!x.property%` is read in a test.
9. **Signed, in its own view** (58, 59, 64, 65). `View.Snapshot` (`[Store]` members, `[Sensitive]` left out, read at rest); a signature records its view, and only a recorded `snapshot` view reads at rest; `snapshot.save` writes the snapshot once in its view, signs it with the app's identity, and answers a wire over the bytes; a snapshot file is `.plang`; resume verifies always, against the app's non-archived identities; a sensitive leaf comes back null. `ASensitiveMember_IsNotInTheSnapshotsText` goes green.
10. **The run's other state** (43, 44, 48). Settings where assigned, latest value; `debug.setting.enabled` and the debug setting's `Start`; the bindings a program made (`actor.on`); channels set at run time; a timer started; a timeout running. The coder proposes each before code; a locked addition comes to Ingi.
11. **Re-sign, and resume a fixed goal from the same step** (55, 57, and Ingi's answer on the re-sign flow).

Later, one at a time with Ingi: question 3 (a loop goes on at its item; an action holding a running call comes back where it was), the added properties on goal, step and module (4b), the verb (5).

## Out

Callbacks (`start %callback%`, the `Data.Snapshot` slot, an ask over a connection that isn't kept); the app snapshot to a file; the error's diff history as its own type (`error.Callback` keeps its throw-time projection through the new door).

## Open for Ingi (not blocking)

- The verbs: `snapshot.save` (new) and today's `resume %snapshot%`. Does the resume verb stay `resume`, or become `start` like the member?
