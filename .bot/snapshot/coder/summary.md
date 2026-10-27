# snapshot — coder summary

**Version:** v4 (third round: stage 7c done on `snapshot-7c-wip`, landing held for codeanalyzer's review 118; stages
10, 11 after)

## What this is

A run's exact state as a value, kept on disk or in a db and resumed later, in this process or a fresh app:

```plang
- save snapshot, write to %snapshot%
- save %snapshot% to file 'run.plang'
… in a fresh app …
- read file 'run.plang', write to %snapshot%
- resume %snapshot%
```

The plan: `.bot/snapshot/architect/plan/start.md`; 7c's: `.bot/snapshot/coder/v4/plan-stage-7c.md`. The doc of what
is: `Documentation/v0.2/snapshots.md`.

## Stage 7c (snapshot 91–122) — a run's frames read back as themselves

- **The state is taken once, as bytes** (113): `app.Snapshot` (async, an approved locked change) walks the parts and
  writes them once in `View.Snapshot`, answering those bytes. `save` signs them; an ask's pending state and an
  error's `Callback(context)` hold them and open them into the node a resume goes on from.
- **The stack is its frames** (99–101): written as `frames`, root to the frame the run stopped in, each frame by its
  tags. Start reads them in order, each row made under the frame entered before it (its kind's `Create` from its
  record, not pushed), then entered under the restored app's setting; a failed row ends what was entered.
- **Found values are written as their key and read back by their own snapshot reader** (103–105): goal
  `{name, path, hash}` (found through the `.pr` its `.goal` is built to, loaded by the goal list, hash compared), step
  `{goal, index}`, action `{step, index, module, name}`, module `{name}`. A key member no reader knows is refused
  (`SnapshotFormatOutdated`).
- **A read asks its view for the reader** (103, 107): readers are keyed by (type, kind, view) (`ITypeReader.View`);
  `View.Reader` is the one door; the wire is born holding its view; `item.Read` reads in the view it is given; the
  general read onto a host reads each member by its type's reader in its view, taking members as their declaring
  class has them (`Tagged`).
- **A frame's own variables are its `Parameter`** (113, 119): the property list it holds, keyed by name
  (`%!call.scope.parameter.order%`); `call.Variable` went.
- **Also** (111, 114, 117, 120): a boy-scout pass over the files 7c touched (renames and smells; locked rows to the
  Ingi list); `sign.StoreView` replaced by `sign.View`, the one member; the wire's plang kind is `type.list.Plang`;
  five bool reads ask `ToBooleanAsync()`.

## Known reds and held work

- `AGoalKeptInWhatAProgramAdded_IsLoaded_InAnAppThatMustLoadIt` — pins the nested-load gap (a found value inside a
  non-found member isn't loaded before the token walk opens it; then a structure entry has no raw bytes for
  `json.Entry`). Logged by the architect.
- The running `%!event%` tests: `CallStackEventTests`, skipped for stage 10.
- `ReadmeIsACopyOfStartMd`: docs', fixed on runtime2. `TheCoresSurface_IsThePinnedOne`: the landing re-pin.

## Next

1. The landing (held for 118): rebase onto `origin/snapshot`, merge `origin/runtime2` (docs), re-pin the surface golden
   once (citing 77, 81, 101, 103, 113, 117, 120, 121), one sweep, fast-forward `snapshot`.
2. Stage 10 (the run's other state: settings, the actor's hold, the running event), stage 11 (`sign snapshot`).

## Code example

```csharp
// a goal read in the snapshot view: its key in, the live goal out — found, not rebuilt
if (path is not global::app.type.item.path.file.@this file)
    throw Refused($"the saved goal '{name}' names no .goal to find it in", "GoalKeyIncomplete", 400);
if (context.App.goal.list[file.Pr]?.Named(name) is not { } goal)
    throw Refused($"the saved goal '{name}' is not in {file}", "GoalNotFound", 404);
if (!string.Equals(goal.Hash ?? "", hash, StringComparison.OrdinalIgnoreCase))
    throw Refused($"the saved goal '{name}' is not the one there now …", "GoalHashMismatch", 409);
return goal;
```

## Earlier rounds (decisions 1–76)

Stage 1 (ISnapshot and the walk), 2 (the call stack, since replaced by 7c), 3 (text), 4 (sensitive values), 5
(`snapshot.save`, the plang proof), 6 (docs), 8 (`property`), 9 (signed in its own view). See git history and the
architect's summary for the detail.
