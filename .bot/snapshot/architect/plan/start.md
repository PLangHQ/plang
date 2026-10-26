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

## Out

Callbacks (`start %callback%`, the `Data.Snapshot` slot, an ask over a connection that isn't kept); the app snapshot to a file; the error's diff history as its own type (`error.Callback` keeps its throw-time projection through the new door).

## Open for Ingi (not blocking)

- The verbs: `snapshot.save` (new) and today's `resume %snapshot%`. Does the resume verb stay `resume`, or become `start` like the member?
