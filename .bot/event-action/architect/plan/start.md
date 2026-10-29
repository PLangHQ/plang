# event-action

Cut 2026-10-29 from runtime2 6cfe782d7 (snapshot's merge). Ingi: "lets do the event-action". Designed with Ingi in this branch's decisions 3–13 (`.bot/event-action/architect/summary.md`); it replaces the first draft (decision 2) and `.bot/snapshot/architect/plan/branch-event-action.md`. The coder places it at `test/plan/event-action/`. The expected C# is `expected.md` beside this file.

## Why

When an event fires, today's code works out where it is heard every time: each goal, step and action lists its "levels" by number (`Level(depth)`, five overrides), and every start walks them twice, asking two sources at each. Ingi: "I think we might have wrong code structure if we need that kind of identifier … list should be ready to use at execution time (or constructed for the first time on first execution because everything is lazy)."

What runs when an event fires was called a binding (snapshot 234: "the 'binding' is not needed in there"). It is an **event**: `before goal Process, call Log` is an event, and a goal's `before` is a `list<event>`.

```
Start.goal
- before each goal, call Audit
- before goal Process, call Log
- after llm.query, call ShowCost
```

**Today:** each start walks the levels by number; a binding fires for every actor unless `Scope` and a reference check stop it; plang's own goals are kept out by `IsSystem` at every run; there is no way to bind something for every app (the old `/system/events/` was deleted, 776a1c5c6).

**After:** Process's first run makes its lists for the running actor, Audit then Log, and every later run runs them as they stand; ShowCost is in each `llm.query` step's list. Nothing is asked at run time.

## The design (decisions 3–13)

- **What's bound is an `event`** (4). Its kinds: `call` (what on.event binds; it points to the on.event step that made it; on.event binds only a goal call), `clause` (on.error, on.cache, on.timeout), `code` (C#: debug's watch, the test runner, coverage, mocks), `inherited` (a maker's, taken in by a sub app). Every kind has `Start` (7); the "not inside its own call" check moves to the context, `context.Enter(event)`, a scope that is null when the event is already running in this flow.
- **Events live on the actor that bound them** (4): `actor.event`, a `list<event>` in bind order. A goal run as another actor never sees them, so `Scope` and the actor check go. `%!app.event%` reads the running actor's, as `%!app.call%` reads `context.call` (`app/this.cs:126`).
- **Both actors take in `/system/event/`** (8, 16): `event.setting.system: load | skip`, `load` the default (`--event.setting.system=skip` turns them off). The root app runs that folder once as the system and once as the user when it starts, so its events are each actor's own and bound first. A sub app inherits them as it inherits every event (`Sub: inherit | none`, snapshot 251). An app's own `system/event/<name>.goal` replaces the os's file of the same name. The first os event writes out `This is system event` to the channel it is given (Ingi: "it should just write out to the channel it is given, what ever that channel is").
- **The running event's data is `%!event!data%`** (14), the Data whole (value and error), as `%!data%` is; `%!event!result%` goes.
- **An error that was handled is seen through error's `after`** (14): `after error on each action, call Log` runs once the error is handled (ignored, recovered or still failing), `%!event!data%` the failure. No new event name.
- **A step binds with today's form** (12): `Event=%!<item>.on.<moment>%`, `When=before|after`. The path decides where the event goes (7): bound on a type (`%!app.type.goal.on.start%`, each goal) it is matched by `item.Is(type)` (`item/this.cs:395`), the only question; bound on one item (a goal, a catalog action, a channel, a screen element) it goes straight into that item's lists; bound on a module, into each of its actions'.
- **An app's events leave plang's goals alone** (9, 10): "each goal" bound by the app's own goals is the path pattern `/**` without `/system/**`, matched when a list is made; the same holds for the steps and actions of a `system/` goal (an app's `after llm.query` doesn't run for the builder's). An event bound from `/system/event/` (the os's or the app's own copy) covers every goal. `IsSystem` goes.
- **A snapshot keeps each actor's events at the actor's place**, as the live tree holds them (saved = the structure); the lists are never saved and are made again on first run.
- **Every goal, step and action has its own lists per actor, made on its first run** (3, 10): they are absent until then; the first run finds every event that applies and makes them; after that they are always ready (a list with nothing in it is "matched, nothing to run"). A later event joins every made list it applies to; `on.unbind` leaves them. Making a list and adding an event take one gate; a list runs as it stands, with no lock. Lists hold events by reference, never a copy. Only things that stay around keep a list (goals, steps, actions, types, channels, the app, a screen element); a variable is made new at every set (`variable/list/this.cs:213`), so how its events are found is plang-tester's to measure and suggest first (15, `.bot/event-action/tester/variable-events.md`).
- **`on` is an item's moments** (13): `before` and `after` (its start), and the named moments (`load`, `error`, `create`, `set`, `write`, `read`, `ask`, declared ones like `closed`), each itself an `on` with its own `before` and `after`. `%!app.type.goal.on.start%` reaches the `on` itself. Today's `event` class, its ten name-only subclasses, `declared` and `on.Made` go.
- **Order** (4, 11): `before` and `after` run in bind order. An action's own clauses are in the same list at fixed places: (1) the app's before events, (2) timeout's deadline, (3) cache lookup (a hit is the answer), the action runs, (4) cache stores the real result, (5) the step's own `on error`, (6) the app's `on error` events, (7) the app's after events. A passed deadline is a `Timeout` error that `on error` takes.
- **Running is one line per side** (5): goal.Start reads `context.Actor.@event[this]` and runs its `before`, its steps, its `after`; `before.Start(item, context)` takes no result.
- **Caching stays per action** (11); several actions cached as one is a goal: `call LoadData, cache for 10 minutes, write to %result%`.
- **The goal itself, in this branch (boy scout)** (6): `Enter` folds into `Start`; `Leave` moves onto the goal's frame (`call/goal/this.cs:38`); a goal of the parent app (`%!app.parent.goal["Claude"]%`) is its own kind whose `Start` sends the call up the link, so `Elsewhere` and goal.Start's fork go. Return early (a CLAUDE.md proposal).

## Stages

1. **The word and the folders.** `event/binding/**` → `event/{this, call, clause, code, inherited}`; the actor's events at `event/list/`; `on` as decision 13 says. on.event answers an `event`; on.unbind takes the event (its parameter's name is the coder's to propose: `Event` already names on.event's moment). Error keys and messages that say binding say event. The teaching in `os/system/modules/on/*` and `loop/*` (check each) changes, so the decider fixtures and page goldens re-pin; the surface golden re-pins under this decision. Boy scout (snapshot 39): each event kind holds an actor, so it clones as itself.
2. **Events on the actor; lists made on first run.** `actor.event`; the door `context.Actor.@event[item]`; placement by path; `item.Is(type)`; the path pattern for "each goal"; the fire sites (goal, step, action, variable, channel, type, the goal list's load, the screen element) each one line per side. `Level` and its overrides, the walk (`event/this.cs:95–191`), `IsBoundAt`, `Scope`, `IsSystem` and the per-item scan go; `app.event` moves to the actor.
3. **Clauses in the list.** on.error, on.cache and on.timeout are placed in an action's list at their places (11) when the list is made; the error fold (the first that takes the failure answers) stays its own.
4. **The C# kind and the context's check.** `event/code/`: the base keeps only what every event shares; `_handler` and the two-constructor split go; `context.Enter(event)` replaces `TryEnterEvent`/`ExitEvent`.
5. **`/system/event/`.** `os/system/event/Hello.goal`, which binds on the app's start (so it fires once per run, never per goal) a call that writes out `This is system event` — there only to prove loading works (Ingi: "it is fine if it constantly writes out"); the root app runs the folder as each actor at start, unless `event.setting.system` is `skip`. Two proofs: (a) the os event fires in an app; (b) an app's own `system/event/Hello.goal` replaces it, so only the app's fires.
6. **`Sub: inherit | none`.** A choice on on.event, `inherit` the default, held on the event; a sub app takes in only the events that reach. A new on.event property re-pins PickListTests, and ModulePageTests if the notes change. The notes say a handler inherited into a sub app calls the sub app's goals (snapshot 265), and "enforce in before; an after observes".
7. **The goal.** `Enter`, `Leave`, the parent's goal kind (6).

**Boy scout, in files this branch moves:** `on.unbind` leaves no emptied entry; two events from one on.event step both come back from a snapshot (`event/this.cs:89`'s dedupe by action).

**Noted, not built:** an action added by `module.add` after a module's event was bound misses it; a goal with events from two types (each goal and each item) runs each type's group in turn.

## Demolition

Dies in this branch:
- [ ] `event/binding/**` (moved; nothing kept beside it).
- [ ] `item.Level` and its overrides (`goal/this.cs:340`, `step/this.cs:126`, `action/this.cs:160`, `variable/this.cs:28`, `channel/this.cs:40`).
- [ ] The walk and `IsBoundAt` (`event/this.cs:95–191`, `item/this.cs:478`); `error.Catch`'s walk (`on/error.cs:15–39`).
- [ ] `Scope` (`binding/Scope.cs`, on.event's parameter) and `ReferenceEquals(context.Actor, Actor)`.
- [ ] `goal.IsSystem` (`goal/this.cs:179`) and its uses in the `Level` overrides.
- [ ] Today's `event` class, `on/{start, load, create, set, remove, write, read, ask, hit, miss, declared}.cs`, `on.Made`.
- [ ] The class's shared closed events (`on/empty.cs`) if `%!x.on%` answers the running actor's `on` (the coder's shape).
- [ ] `app.event` on the app (`app/this.cs:140`) and `origin.event` → the actor's.
- [ ] `binding._handler` and its constructor; `TryEnterEvent`/`ExitEvent`.
- [ ] `goal.Enter`, `goal.Leave`, `goal.Elsewhere`.

Stays:
- `When` (before | after); the error fold; `on.unbind`; `item.Is(type)`; `Sub` (snapshot 251).

## Proof (`start.goal`)

Each test goes red without its stage's change.

1. on.event answers an event; on.unbind takes it off and it no longer runs. The surface golden and the decider fixtures re-pinned.
2. `before each goal` and `before goal Process` both run for Process, in bind order; the first bound runs first in `after` too. A goal run as the system doesn't run the user's events. An app's `before each goal` doesn't run for a `system/` goal. `after llm.query` runs for each `llm.query` step. An event bound after a goal's first run joins its list. `%!app.event%` lists the running actor's events.
3. On a cache hit the app's before events still ran; `on error ignore` never puts an empty result in the cache; the step's own `on error` takes a failure before the app's `on error` event sees it.
4. The debug watch, the test runner's timing, coverage and a mock each still run, and none is in a snapshot.
5. The os's `Hello.goal` event fires in an app, and not with `event.setting.system=skip`; an app's own `system/event/Hello.goal` replaces it.
6. An event bound with `Sub=none` doesn't run in a sub app; one without does; a snapshot keeps `Sub`.
7. `return false, 2 levels` ends two goals; a parent app's goal call goes up the link with its parameters.

Boy scout pins: unbinding the last event on an item leaves no entry; two events from one step both run after a resume.

## OBP check

| pattern / smell | where | resolution |
|---|---|---|
| jump between references | `Level(depth)` and the walk | each item's lists are made once and run as they stand |
| one door | `Has`, `For`, `Is`, `ReferenceEquals` in the first sketch | `item.Is(type)` only; an event on one item is placed, not asked |
| a type per kind, decided at birth | the base binding doubling as the C# handler; the parent's stand-in goal | `code` is its own kind; the parent's goal is its own kind |
| a type per kind (inverse) | ten moment classes that differ only by name | one `on` |
| one concept, one birth method | goals, steps and actions each walking differently | all three: own lists, made on first run |
| verb+noun | `ReferenceEquals`, `TryEnterEvent` | gone; `context.Enter` |
| return early | `goal.Leave` | `if (!result.Returned) return result;` |

## For the coder

Push back on anything. The shape of `context.Actor.@event[item]`, how `%!x.on%` reaches the running actor's `on`, and on.unbind's parameter come to me before code. A new member on a locked surface (events, call memory, `item`'s base) comes to me first; Ingi has approved the design in decisions 3–13.
