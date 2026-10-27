# snapshot — coder summary

**Version:** v5 (stage 7c on `snapshot-7c-wip`, both codeanalyzer reviews worked through; the landing held for two
answers from Ingi; stages 10, 11 after)

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

## The review rounds (snapshot 123–155)

codeanalyzer's review (36 findings) and re-check (review2) are worked through, each fix its own commit naming its
decision. The shape of what changed:
- **One door each**: a read asks its view for the reader (`View.Reader`/`Make` over one private `Registered`); the
  resume runs the action list from a position; the snapshot walk is one private type; a type's class doors are one
  private `door<TFunc>`; Data's copiers take the result whole (`Take`).
- **Births, not stamps**: a context's memory, a snapshot's context, a step's list (the list admits it), a goal's
  `.pr` (derived from its path), a Diff (one capture birth).
- **The load walk** (146–149): a snapshot's load walks the saved text by what each value says it is, and a record's
  member is read by the reader its type registers, else born by its own birth — a goal kept in a frame's property
  comes back, lazy, in an app that must load it.
- **Removed**: runtime code for tests (`Synthetic`, the variable memory's six, the dead callback errors, observed-error
  tracking), middlemen (`app.Start(goal)`), duplicate overrides (frame kinds' `Create(raw)`, binding's `Rest`).
- **Fixed on the way**: a SIGSEGV the error stream's deep clone caused (691f5355b, found by bisect); the sqlite store
  dropping a row it couldn't read (035ec67fd); keys verified strictly in their one form.

## Known reds and held work

- `ReadmeIsACopyOfStartMd` (docs', fixed on runtime2) and `TheCoresSurface_IsThePinnedOne` (the landing re-pin; every
  line it moves is in `.bot/snapshot-7c-wip/coder/landing-note.md`, by decision).
- plang: `BareLiteralJudgedByForm` (baseline); `FreshAppGoesOn` (Ingi: which frames a resume inside a run goes on
  through; no fresh app exists under `plang --test`).
- **Held for Ingi:** `kind.Read`'s signature (review 8, keep or revert); stored signatures with no view (review2 item 1,
  built and held in `stash@{0}`: refuse them, or keep verifying with the fact that their bytes named none). Also his:
  Copy/Clone, ICode, `Uninitialized`→`NotFound`, `IncomparableException`, the formal reader's type-name forks, the module
  registry's sentinel, the write-to-bytes door, the step half of item 10.

## Next

1. Ingi's two answers; finish item 1 from `stash@{0}` with its pins (a Debug-view signature verifies in Debug).
2. The landing: rebase onto `origin/snapshot`, merge `origin/runtime2` (docs), re-pin the surface golden once naming
   each line's decision from the landing note, one sweep, fast-forward `snapshot`.
3. 18's second half (the code module's provider records, one record type) after ICode lands.
4. Stage 10 (the run's other state: settings, the actor's hold, the running event), stage 11 (`sign snapshot`).

## Code example

```csharp
// a record's member in a view: read by the reader its type registers, else born from its slot by its own birth
if (Registered(type, kind, context) is { } reader)
    return slot.Read(reader, kind, context, this) as global::app.type.item.@this;
var born = new global::app.data.@this("", context: context);
return type.Make(slot, born) ?? throw new global::app.error.AppException(born.Error ?? …);
```

## Earlier rounds (decisions 1–76)

Stage 1 (ISnapshot and the walk), 2 (the call stack, since replaced by 7c), 3 (text), 4 (sensitive values), 5
(`snapshot.save`, the plang proof), 6 (docs), 8 (`property`), 9 (signed in its own view). See git history and the
architect's summary for the detail.
