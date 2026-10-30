# event-action

Cut 2026-10-29 from runtime2 6cfe782d7. Designed with Ingi in this branch's decisions 3–36 (`.bot/event-action/architect/summary.md`; the replaced ones are listed at its top). This text is what holds now; `picture.md` is the same on one page. Stage 1 is built (2c7b8f78d). **Stage 2 waits for Ingi's green light** (36: "we need to move slower").

## Why

When an event fires, today's code works out where it is heard every time: each goal, step and action lists its "levels" by number (`Level(depth)`, five overrides), and every start walks them, asking two sources at each. Ingi: "I think we might have wrong code structure if we need that kind of identifier … list should be ready to use at execution time (or constructed for the first time on first execution because everything is lazy)."

What runs when an event fires was called a binding (snapshot 234: "the 'binding' is not needed in there"). It is an **event**: `before goal Process, call Log` is an event.

```
Start
- before each goal, call Audit
- before each goal in api/*, call CheckIdentity
- after llm.query, call ShowCost
```

**After:** the first time a goal runs for the user, its list loads every event that applies to it (Audit; CheckIdentity when the goal is under `api/`), and every later run runs that list as it stands. ShowCost is in `llm.query`'s list, which every `llm.query` step runs.

## The design

### What's bound (built in stage 1)

- **An event** (4): `call` (what on.event binds: a goal call only), `clause` (on.error, on.cache, on.timeout), `code` (C#: debug's watch, the test runner, coverage, mocks), `inherited` (a maker's, taken in by a sub app).
- **`%!event%` is the bound event** (21): `%!event.name%`, `%!event.when%` (27); `%!event!item%` what it ran for; `%!event!data%` the value so far, a failure being `%!error%` (23). A snapshot keeps it by `{action, n}` (22); a resume brings back every saved event, matched one for one against the inherited ones (22).
- **`on` is an item's moments** (13): `before` and `after` are its start's; `load`, `error`, `create`, `set`, `write`, … are each an `on` with their own `before` and `after`. Its plang type is `on` (26).
- **A step binds with today's form** (12): `Event=%!<item>.on.<moment>%`, `When=before|after`.

### Where events live and how lists fill (stage 2)

- **Events live on the actor that bound them** (4): `actor.event`, every event the actor bound, in bind order. `%!app.event%` reads the running actor's, as `%!app.call%` reads `context.call`. A goal run as another actor never sees them, so `Scope` goes.
- **Whose list** (10, 28, 24): a goal has its own; a step has its own; an action's list is its **catalog action's** (`llm.query`, shared by every llm.query step and by a composed llm.query); a variable's is its own when an event is bound on it (a variable equals another by its name, 39), else the variable type's: a set never makes a list for an unbound name (73). In plang a variable's own moments are its named path, `after %count% is set` → `%!app.variable["count"].on.set%`; `%count.on…%` is its value's (75). `%!app.variable["x"]%` is the variable whether or not it's set (77).
- **Pull and push** (3, 10, 34): a list is not loaded until its first run; then it loads every event that applies to it, in bind order, and never asks again. When the runtime runs an on.event step, the new event is added to every loaded list it applies to. An unloaded list is never added to; it gets the event when it loads. One bind door, `Add(event)`, and one store of lists by item (73).
- **One question, answered by the thing bound on** (34, 39): "does this event apply to this item?", asked at a load and at a push: `@event.Item.Has(item)`. NEW `Has(item)` on item's base: the base answers itself (one goal, one catalog action); a type, `item.Is(this)` (each goal, each action); a module, whether the item is one of its actions (whole-module binding, 33); a pattern, whether the goal's path matches; a variable, by its name. `Is(type)` stays as it is. No `ReferenceEquals`.
- **By pattern** (35, 37, 84): `before each goal in api/*, call CheckIdentity` binds on `%!app.goal["api/*"].on.start%`, `api/` beside the goal whose step binds it; `/api/*` is from the app root (Ingi, 84): the goal list answers a **pattern** for a name with a wildcard (never the goals that match today, so a goal loaded later under `api/` gets it too). The pattern answers `Has` by the goal's path, with the one glob matcher that exists (`type/item/permission/this.cs:117`, private today; one matcher, not a second).
- **Plang's own goals** (31, 32, 44): one list; `event.setting.goal: app | all`, `app` the default. Under `app`, inside a goal under `system/` (the os's or the app's copy), an event bound on a type or a pattern ("each goal", "each action", `api/*`) is skipped, system events included; an event bound on one named item (debug's watch on a goal, `before goal /system/error/show`) runs. Under `all`, everything runs. Asked when a side runs and holds an event (`Skips(context)` on the actor's events), so a setting changed mid-run is heard at the next side. `goal.IsSystem` stays, the one member asked (also by the goal list's `os: false` listing). `event/setting/this.cs` holds `goal` (choice word `event.goal`).
- **Memory** (24, 43): a list is kept by what stays around (a goal, a step, a catalog action, a type, a channel, the app, a name with events), in a plain map owned by the actor's events, so it dies with the actor and its sub app. Held memory grows with binds, never with runs or sets.
- **`keep`, `loop` and `clause` are kinds of `action`** (43): actions with a role (the builder's step line, ad7b306d4); their types say they are actions, so "each action" reaches `variable.set`, `foreach` and the clauses.
- **Running** (5): one line per side: the actor's list for this item, `before.Start(item, context)`, the work, `after.Start(item, result, context)`. `before` and `after` both run in bind order (4).

### The rest

- **Clauses and order** (11, 14, 28): an action's own clauses run around its catalog action's shared list, in this order: the app's before events → timeout's deadline → cache lookup (a hit is the answer) → **the action** → cache store (a real result only) → the step's `on error` → the app's `on error` events → the app's after events. A passed deadline is a `Timeout` error that `on error` takes. Caching stays per action (11).
- **A handled error** (14): `after error on each action, call Log` runs once the error is handled; the failure is `%!error%` (23).
- **The C# kind and the check** (7): `event/code/` holds the C# handler; `context.Enter(event)` replaces `TryEnterEvent`/`ExitEvent`, so every kind's method is `Start` and `Handle` goes. Exceptions aren't swallowed.
- **System events** (8, 16, 17, 53): `event.setting.system: load | skip`, `load` the default. An actor's events load lazily, on their first ask or add (nothing at an app's start): a sub app's actor takes in its maker's events, then the `system/event/` files run as that actor — the root's from its own folder where it has the file, else the os's; a sub app's only its own, each replacing the inherited events from the file of that name. The door that can load is async; a load in progress doesn't load again. The first os event, `os/system/event/Hello.goal`, writes out `This is system event`, bound on the app's start.
- **Sub apps** (snapshot 251, 18): `Sub: inherit | none` on on.event, `inherit` the default; no `lock`.
- **A call finds its goal in the app its caller belongs to** (18): an os goal's call in the running app (its `system/` copy first, then the os's); a maker's goal's call in the maker's app. Reverses snapshot 265. By `goal.list`, a birth fact.
- **The goal** (6): `Enter` folds into `Start`; `Leave` moves onto the goal's frame; a goal of the parent app is its own kind (`Elsewhere` goes). Return early.

## Open for Ingi before stage 2

None. Decided: how a step names a pattern (37) and which side answers the question (39). The collision check (38) is done (40).

## Stages

1. **Done** (ec64ee712, 2c7b8f78d): binding → event; `on`; `%!event%`; on.unbind's `Event`; the teaching; the dedupe.
2. **Done** (eb5d47121, 2bdcaab2f). **Events on the actor; lists by pull and push; the one question; patterns; variable names; `event.setting.goal`.** `Level` and its overrides, the walk, `IsBoundAt`, `Scope`, the class's shared empty moments (`on/empty.cs`), `item.on`, and the per-item scan go; `%!x.on%` answers the running actor's `on`. Folded in from stage 1's review (26): an event's `ToString` goes, "start is the on itself" in one place, no `ReferenceEquals` left in `event/`, on.event's walk up the parents goes.
3. **Done** (ee67e2da5). **Clauses around the list**, in the order above; the error fold stays its own.
4. **Done** (f7eb594c4, 839ccf6c9). **The C# kind; `context.Enter`; `Start` for every kind.**

The whole branch passed codeanalyzer v5, tester v5, security v2 and the auditor's re-check (084d73b49); docs 8b49a0d6a. Decisions 1–99 in `summary.md`.
5. **Done** (f71d37fa7 … fe4167c02; codeanalyzer PASS 5d0ac4382, tester v4 PASS 034559e84). System events: `event.setting.system`, the folder, both proofs; inheritance at the first ask (60); a sub app over its maker's folder runs no system/event file (66); variable events on one store (73–77).
6. **Done** (b8551ff48, b8db6b8f0; teaching 18ac4ce83): `Sub: inherit | none`; the event answers `IsInheritable`.
7. **Done** (1f615b6c6): the goal: `Enter` and `Leave` folded, `app.parent.goal` its own kind, `Elsewhere` gone. Also 84 (18507e237, teaching fe1447d57): a pattern rooted as a path.
8. **Done** (86d90db97): a call finds its goal in its caller goal's app: `goal.App` born in the reader (an os goal with none), `goal.Find(name, asker)`. Security probes it.

Noted, not built: an action added by `module.add` after a module's event was bound gets it at its list's load (pull), so this gap closes; a goal with events from two types runs them in bind order (one list).

## Demolition

- [x] `item.Level` and its overrides.
- [x] The walk and `IsBoundAt`; `item.IsBoundAt`.
- [x] `Scope` (deleted in stage 5), `event.For`, `ReferenceEquals(context.Actor, Actor)`.
- [x] `item.on`; `item._on` (moved onto the action, the auditor's B10).
- [ ] **Not done:** `on/empty.cs` and its static `Emptied` (a per-class cache of the empty moments: the action's clause fallback, and "is this a moment's name"). Removing it needs the moment names on the item's type (`[Events(...)]`, the locked events surface). On the backlog, not done here.
- [x] `app.event` reads the asker's actor's; `origin.@event(role)` answers the maker's actor's.
- [x] `Handle`, `TryEnterEvent`/`ExitEvent`. `event.code`'s `_handler` is the C# kind's own handler, so it stays.
- [x] `goal.Enter`, `goal.Leave`, `goal.Elsewhere`.
- Stays: `When`, `on.unbind`, `item.Is(type)`, `goal.IsSystem`, the error fold.

## Proof

Each test goes red without its stage's change; `start.goal` lists them.

2. `before each goal` and `before goal Process` both run for Process, in bind order; `after` too. A goal run as the system doesn't run the user's events. Under `app`, `before each goal` doesn't run for a `system/` goal, and a loop of 1,000 that reads `%!error%` runs Log once; under `all`, 1,001 times. `after llm.query` runs for each llm.query step. `before each http action` runs for http.get and http.post, not file.read. `before each goal in api/*` runs for `/api/Orders`, not `/Process`. An event bound after a goal's first run runs at its next start. The tester's four variable tests (a watch on `count` leaves another name's set near the nothing-bound bytes; 10,000 distinct names hold no more with an event bound; an event on `count` fires on its next set; unbinding the last one removes the entry).
3. A cache hit still ran the app's before events; `on error ignore` never puts an empty result in the cache; the step's own `on error` takes a failure before the app's `on error` event sees it.
4. The debug watch, the test runner's timing, coverage and a mock still run, and none is in a snapshot.
5. The os's `Hello.goal` event fires in an app, and not with `skip`; a sub app's own `system/event/Hello.goal` replaces it.
6. An event bound with `Sub=none` doesn't run in a sub app; a snapshot keeps `Sub`.
7. `return false, 2 levels` ends two goals; a parent app's goal call goes up the link with its parameters.
8. A platform's `before each goal, call Meter`, Meter calling `Record`, runs the platform's Record inside an uploaded sub app that has its own; an app's own `system/…/Record.goal` still replaces the os's.
