# Next branch: what an event runs is an action (`event-action`)

Approved by Ingi on snapshot (decision 234: "yes, i agree on event", then "ok, yes" to its own branch). Cut from runtime2 after snapshot merges. The coder's finished folder move is saved as a patch (`.bot/snapshot/coder/event-action.patch`) to apply as the first commit.

## Why

`event/binding/action` read as "the binding's action", and since snapshot 10a a binding does have a member called `action`. What runs when an event fires is the event's action. Ingi: "I think the 'binding' is not needed in there", then "it should be removed the 'binding'".

## Layout (approved)

```
event/action/this.cs      what an event runs: actor, scope, start, remove   (was binding/this.cs)
event/action/Scope.cs                                                       (was binding/Scope.cs)
event/action/list/        an event's before and after                       (was binding/list)
event/action/call/        what an on.event step binds: it runs its call     (was binding/action)
event/action/clause/      what on.error, on.cache and on.timeout bind       (was binding/clause)
```

## plang's word

`on.event` answers an action instead of a binding. `on.unbind` stays: unbinding is something you do to an event. The teaching that says "binding" changes:

- `os/system/modules/on/{module.description, event.description, event.examples, event.notes, unbind.description}.md`
- `os/system/modules/loop/{foreach.description, module.guide, start}.md` (check each: some may use the word for a loop's item)

A teaching edit re-runs the decider fixtures and the page goldens (CLAUDE.md, "Teaching-edit rule"). The builder bot checks the builder still picks `on.event` for "before …"/"after …" steps. Stage two's criteria list every type's name, so the move alone re-pins `pick_golden.json`.

Docs that say "binding" (`apps.md`, `build-checks.md`, `architecture.md`, `building_plang_tests.md`, `execution-flow.md`) go to the docs bot.

## `app.event` by path (snapshot 244)

Ingi: "that is the path, app.event.goal.start.before". On snapshot, `%!app.event%` answers this app's events (all of them, Ingi: "lets just start with all the events"). This branch adds reaching one by path, each segment a member of the one before:

```
- before each goal, call Log      →  app.event.goal.start.before
- before goal Process, call Log   →  app.event.goal["/Process"].start.before
- after each file.read, call X    →  app.event.module.file.read.start.after
```

- on.event's `Event` examples change from `%!app.type.goal.on.start%` to `%!app.event.goal.start%` (teaching, re-pins).
- The app's list moves from `event/on/list/` to `event/list/`, following the convention that `app.X` is typed `X.list.@this`.

## A binding chooses whether it reaches sub apps (snapshot 249–250)

Ingi: "yes, each binding". On snapshot every binding reaches (the default), and a sub app takes each in at birth as an inherited binding. This branch adds the plang option on on.event (proposed `Sub: inherit | none`, name pending Ingi), held on the binding, and the sub app's birth takes in only the bindings that say `inherit`. A new action property is a catalog change: it re-pins PickListTests, and ModulePageTests if `on/event.notes.md` gets a line. The notes also get security's rule: enforce in `before`; an `after` observes. And Ingi's ruling (snapshot 265): a handler inherited into a sub app calls the sub app's goals, which the sub app can replace with its own, so policy that must hold in a sub app isn't built on an inherited handler's calls.

## Older problems that go with it (snapshot 230, items 7 and 8)

- **The base is also the C# handler kind** (`binding/this.cs:37`, `_handler!`, two constructors). The C# handler (debug's, the test runner's, mocks) becomes its own kind beside `call` and `clause`, and the base keeps only what every action shares.
- **Ten of the eleven event classes differ only by name** (`start`, `load`, …; `error` has behaviour). A difference that is only a name isn't a reason for a subclass.

OBP pattern: a compound name is a dot path; a type per kind, decided at birth.

## Tests

- The surface golden and `pick_golden.json` re-pinned under this branch's decisions.
- The builder bot's check of the on-module goals (before/after/on steps) passes as before.
- After the C# handler kind: a debug watch, a test runner binding and a mock each still fire, and none is saved in a snapshot.
