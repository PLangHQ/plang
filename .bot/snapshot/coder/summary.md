# snapshot — coder summary

**Version:** v4 (second round: stages 8 and 9 done; stage 7 waits on Ingi's answer on the frame's shape; 10, 11 after)

## Second round (v4, decisions 42–76)

- **Stage 8, `property`** (2a019554a, 955b852dc): what a program adds to a thing is its `property`
  (`app.type.item.property.list`, an item: `["name"]`, `.list`, writes and reads itself, a snapshot part). The six
  keepers (app, actor, call, goal, step, module) declare a `[Store, LlmBuilder]` `property` node through
  `property.IOwner`; item's Get/Set fall through to it. Data's `Kept` → `Property`; the wire member is `property`.
- **Stage 9, signed in its own view** (d60c08ceb, 327ea4ba3, 751cf5de5, 627ad7f56): `View.Snapshot` (store, no
  `[Sensitive]` member, a sensitive leaf null, no Data name); a signature records its `view` inside the signed bytes,
  and only a recorded `snapshot` reads at rest; `snapshot.save` is walk → `wire.Of` → sign; `resume` refuses
  `SnapshotUnsigned`, `SnapshotSignatureInvalid`, `SnapshotNotTheApps`; a snapshot file is `.plang`; a file's content
  opening carries the read's signature and properties onto the binding (`Data.Carry`); writers refuse a value deeper
  than the readers' 64 levels (`ValueTooDeep`), a pasted slice counting its own depth.
- **Known red:** `FreshAppGoesOn` (plang) is refused at the save (`ValueTooDeep`) under `--test` until stage 7 saves the
  stack as a chain.
- **Next:** stage 7 (the chain and the frame's record), 10 (the run's other state), 11 (`sign snapshot`, re-sign).

## What this is

A run's exact state as a value, kept on disk or in a db and resumed later, in this process or a fresh app:

```plang
- save snapshot, write to %snapshot%
- save %snapshot% to file 'run.txt'
… in a fresh app …
- read file 'run.txt', write to %snapshot% as snapshot
- resume %snapshot%
```

The plan: `.bot/snapshot/architect/plan/start.md`. The doc of what is: `Documentation/v0.2/snapshots.md`.

## What was done

- **Stage 1, ISnapshot and the walk.** A part implements `ISnapshot` (Store, Start, both answering Data). `app.Snapshot`
  walks public ISnapshot members from `app`, each part at its member path, each in its own context
  (`snapshot/this.Store.cs`). Code records the DLLs a run took in (`code.dll.loaded`, `code.dll.added`), registrations
  and kinds' defaults (`code.default`).
- **Stage 2, the call stack as it lives** (1b + 1c): the stack is a nested tree (root, children, current). A frame
  writes goal {address, hash}, step, action {index, module, name}, its variables (its own node), arguments, errors,
  handled, event, tags, kept. It comes back through `call.Find` (async: opens entries, finds the goal, the setting) and
  then its kind's own `Create` (`call`, `binding`, `isolated`). Resume is a call: it stops at the resume action's frame.
- **Stage 3, text:** a snapshot in text is its own typed form; `read … as snapshot` reads it through its reader.
- **Stage 4:** a sensitive variable is not stored. A `[Sensitive]` member inside a value still writes (a pinned red,
  waiting on `View.Snapshot`, Ingi's).
- **Stage 5:** `snapshot.save` (`module/snapshot/save.cs`), and the plang proof
  (`test/plan/snapshot/module/snapshot/`): `SavesTheRun` and `FreshAppGoesOn` (resumes the committed `run.txt`,
  written by `Record.goal`; its `start.md` says when to run it again).
- **Stage 6:** `snapshots.md` and `callbacks.md` (callbacks later) rewritten to what is.
- **Security (snapshot 25):** every restored Data answers as its own actor, through one door, `snapshot.In(context)`.
  Before, it carried whoever read the text. This predates the branch.
- **Clones (snapshot 39):** item.Clone's deep copy copied a context, and with it the app, through a Data inside a dict
  or list. That crashed the full plang suite. Lists, dicts and snapshots now clone by their elements, and live or shared
  values are themselves. This predates the branch. The rest of the sweep is in the backlog.
- **Also:** the llm C# test recordings are keyed on the request body (they had answered nothing for a month). An llm
  tool call is a plang value (`tool.call`). Code's records go by plang names. The app's mode is not stored.

## Waiting on Ingi (via the architect)

- the text's shape (question 1);
- the removals and `call.list.Resume` sign-off (1d, 1e);
- `View.Snapshot` (2), with the sensitive-member red;
- the program as references (2b), with the event-through-text red, which is held uncommitted in
  `CallStackSnapshotTests.cs`;
- loops and held calls (3);
- refusals and "everything" (4, 4b);
- the resume verb (5);
- `%result%` in OnToolCall;
- the frame kinds' plang names;
- the origin of a read lost below containers (backlog).

## Code example

```csharp
// the door every restored Data comes through: cloned, born again in the receiving part's context
internal IEnumerable<global::app.data.@this> In(global::app.actor.context.@this context)
{
    foreach (var entry in Entries.Entries(context)) yield return entry.Clone().Copy(context);
}
```

## State

- C# sweep: the 3 known reds only (event-through-text, held; ReadmeIsACopyOfStartMd; ASensitiveMember).
- Plang suite: 308 total, 306 pass, 1 known fail (BareLiteralJudgedByForm), 1 skipped.
