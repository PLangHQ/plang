# Held for later branches (from snapshot)

Each item was raised on snapshot and held by Ingi or by me, with the decision that held it. None blocks snapshot's merge.

## Saved data

- **Typed lists (`list<on>`).** The app's event list writes each row with the label `app.event.on`, which the list never reads. With a typed list, the list says once what its rows are and the rows go bare. That needs the core list reader to read records for every typed list (the coder's NotTokens finding). (317, 321)
- **An empty dict writing nothing,** as an empty list already does. It changes the Out view too. (208)
- **The run's call at the snapshot's top** (`{app, call}`), so the edit path is `%snap.call…%`. Ingi: "I'm not sure, let's leave this long for now". (293, 294)
- **Finding a channel again after a resume.** A binding on a channel refuses the save today (NoWireContract). A channel knows only its name; its actor's list numbers it. (225)

## Code shape

- **The `kind["*"]` recipe and `Kept<T>`:** the same record-reading recipe at several sites, a missing door (P7-2, 205, 235).
- **`IsBound` in the doors that ask before making:** variable set/remove (`variable/list/this.cs`) and type creation (`type/this.cs:275`) still ask "is anything bound?" before they make what they would hand a binding. (254, 272)
- **The per-step parse in debug's `Variables(sb…)`:** it goes with `module-list-type`. (231, 233)
- **The bare-row rule's five type literals** (`View.Record`, snapshot view): no existing door answers "the type a bare token reads back as", so the literals stay until a type can say it. (324, 325)
- **A frame's place handed in at push:** the action frame finds its `index` lazily, on the first ask from a snapshot or resume. Handing it in at birth needs a parameter on `action.Start` and `call.list.Push`, the locked call surface. (325)

## The auditor's deferred minors (snapshot 341, `.bot/snapshot/auditor/final.md`)

- Snapshot navigation can throw out of `View.Make` (it should answer an error).
- `resume` creates a default identity as a side effect.
- Late stamps in `call` (`call.Under`, accepted at 324 with a reason) and in `step`.
- Dropped answers at `call/action:79`, `Resume.cs:47` and `test/this.cs:144`.
- The app event list's lookup is O(n), and `on.unbind` leaves an emptied `on` in the list.
- The code registry keeps partial state when a DLL is refused.
- The sign/verify view asymmetry (plausible, not reproduced).

## Branches already written up

- `plan/branch-event-action.md`: the event folders, the plang word binding → action, the `Sub: inherit | none` option, `on.Declared` and the `declared` class, the C# handler kind, ten event classes that differ only by name.
- `plan/branch-module-list-type.md`: a module's own list as a type.
