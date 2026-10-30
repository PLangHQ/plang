# coder — event-action

## Version
v3: stages 1–8 built and accepted; the review line's fixes through ruling 98. Codeanalyzer, tester and security have passed; the auditor's re-check of 99af7fa0b/756467fa6 is next, then docs and the merge (Ingi's go, 96). v2 was stages 2–4; v1 stage 1.

## The review line's fixes (latest first)
- 97 (99af7fa0b): the actor's events load runs in its own context under the actor's lifetime (`Child(actor, actor.CancellationToken)`); an asker cancelled while it waits only stops waiting (`Loaded(cancel)` → `WaitAsync`). goal.Start, action.Start, the variable write and channel write/read/ask answer a failed load as Data (its Error; a cancelled wait → `GoalError.Cancelled`). What a system event file sets stays in the load's context; what it binds is the actor's (98).
- 95: system events fail closed: a `system/event/` file that doesn't load or run fails the actor's load with its Error, all or nothing.
- 94 (the auditor's v1): a sub app's own `system/event/X.goal` replaces only its maker's event bound at `/system/event/X` (`event.IsBoundIn(address)`); probes pin the denial key and exact counts; `Takes` proven both ways (B4, B5); `Names` stops at the first variable it reaches (it read on into `.on` and loaded events); `on.Add(name)` asks `IsMoment`; `Join` finds its sides before joining; the os's Hello runs end to end in a root app; `_on` lives on the action; `pattern.Matches(goal)`; renames.
- 91/92 (security): an event skips a `system/` goal only when the goal is plang's own for it (`event.Skips(goal)`: the os's, or its own app's copy; `event.App`, an inherited event's is its maker's); a sub app's maker's events join before its own system/event files run; a failure an event raises is offered only to its own app's error events (`event.Takes(raised)`, `call.list.@event`).
- 88/89 (codeanalyzer): one door for finding a caller's goal (`goal.list.Find(name, caller)`); an event list goes only when empty.

## Backlog from the review line (the architect's)
- The six doors with the same two catches around `@event.On` (a door answering `data<on>` would allocate per ask: call-cost's ground).
- F4: a bare goal name falls through to any goal the app's list has read ("Sub apps and their makers").

## Stages 6–8 and the fixes since stage 5 (latest first)
- Stage 8 (86d90db97): a call finds its goal in the app its caller goal is. `goal.App` is born in the goal reader (the reading app's for a .pr in its own folder, its system/ copy too; none for one in the os's folder, judged by location); `goal.Find(name, asker)` = `(App ?? asker.App).goal.list.Find(name, this)`, asked by goal.Select and the call event. Proofs: `PLang.Tests/Runtime/App/Goals/CallFindsItsAppTests.cs`.
- Stage 7 (1f615b6c6): goal.Start runs its steps through `frame.Leave` (Enter, goal.Leave, Elsewhere gone); `call.goal.Leave` counts a return's levels down; `app.parent.goal` is its own kind of goal whose Start sends the call up the link with its parameters. 84 (18507e237): a goal pattern is rooted as a plang path (`api/*` in the binding goal's folder).
- Stage 6 (b8551ff48, b8db6b8f0): on.event's `Sub: inherit | none`; the event answers `IsInheritable`; the origin answers only the inheritable; a saved call event keeps `sub`.
- 73/77 (0562aa0ab, 6eae47d9c, a044fe4fa): one store for a variable's events (`Moments(variable)`, debug's watch through `Add(event)`); the type list keeps each class's lookup answer (a lookup by class was 13 KB each time); `variable.Select(key)` makes `%!app.variable["count"]%` the variable whether or not it is set; Cover asks the answer's variables (`variable.Names`).
- 66–75: the codeanalyzer's and tester's findings (lazy-load cost, the load claimed without a lock and all-or-nothing on failure, on's one answer, set.cs's one read value, equality asked of the value itself, path.Relative's system folder first).
- Examples to formal (63/65, a3942896b): 202 of 239 action examples in formal's untyped form.

## Next
- The auditor's re-check, then docs and the merge.

## What this is
What runs when something happens is an **event**, held by the **actor** that bound it. Before, every start walked an item's "levels" (type, module, catalog action, itself) and asked two sources at each. Now each goal, step and catalog action has a list per actor, loaded on its first run with every event that applies, and an event bound later joins the lists already loaded. The thing an event is bound on answers which items it runs for (`Has`).

## What was done (stage 2)
- `PLang/app/event/list/this.cs`: the actor's events (`actor.@event`, `%!app.event%` as the asker's): bind order, `this[item]` (pull: `Load` takes every event whose `Item.Has(item)`), `Add(event)` (push into loaded lists), `this[name]`/`Add(name, event)` for variable names (a name's list loads on its first set only when an event is bound on that name), `Remove`, `Inherit`, `Skips(context)`, the setting owner (`ISetting<event.setting>`), the snapshot member `on` and `Read`. The per-item lists are a `ConcurrentDictionary` owned by the actor (43: a weak table keyed by shared items kept sub apps alive).
- `event/this.cs`: an event is born with `Item`, `Name`, `When`, `Actor`; `Scope`, `For`, `ToString` gone. Kinds: `call`, `clause`, `code` (holds the item filter), `inherited`.
- `event/on/`: `on` loaded by an actor's list knows that list (`on.list`); `side` runs with no await when empty; inside plang's own goals under `event.setting.goal=app` the events bound on many are left out (`IsMany`), the ones on a named item run, an action's own clauses always run.
- `type/item/this.cs`: NEW `Has(item)`, `Is(pattern)`, `IsMany` (approved by Ingi, 39/42/45); `item.on`, `Level`, `IsBoundAt`, `Before` gone; the `on` door answers the asker's actor's list. `type/this.cs`, `module/this.cs`, `goal/pattern/this.cs` override `Has`/`IsMany`; `goal/this.cs` overrides `Is(pattern)`; variable equality by name.
- `goal/pattern/this.cs` (NEW): `%!app.goal["/api/*"]%`, made by `goal.list.Find` for a wildcard name; `Start` answers `NotAGoal`. `goal` unsealed, `Start` virtual. `type/item/path/Glob.cs`: the one glob matcher, moved out of permission.
- `event/setting/this.cs` (NEW): `goal: app | all` (`event.goal` choice).
- `[Kinds]` on action (keep, loop, clause, composed are kinds of action) and on channel (stream, file, noop … kinds of channel).
- Fire sites one line per side: goal, step, action (its catalog action's list, its clauses around it), app start, goal load, type create, channel write/read/ask, screen element click, window events, variable set/remove.
- Teaching: event.notes gains the pattern line. Re-pinned: surface, pick (read: the walk/Scope gone, the setting, the kinds, the pattern type).
- Tests: `Runtime/App/Events/ListTests.cs` (8), `Data/App/VariablesTests/VariableEventsTests.cs` (4, the tester's), the composed-in-a-system-goal pin, OnEventTests' named-vs-each pins; the event test files migrated to the actor's lists. 7 plang tests written under `test/plan/event-action/app/event/` (not built: the builder needs the decider key).

## Stages 3 and 4 (after stage 2's review, 2bdcaab2f)
- Stage 3 (ee67e2da5): `goal/step/action/this.cs` — an attempt is the actor's before events, the action's own clauses before (deadline, cache lookup), the dispatch, its clauses after (cache store); a retry is another attempt. Then its own on error, the actor's error events, the actor's after events last. Pins: `PLang.Tests/Modules/App/Modules/modifier/ClauseOrderTests.cs`, `CacheWrapTests.Wrap_CacheHit_StillRanTheAppsBeforeEvent…`.
- Stage 4: `actor/context/this.cs` — `Enter(event)` answers the event's entry into the flow (dispose leaves), null when it already runs there; `TryEnterEvent`/`ExitEvent` gone. Each side enters each event and passes by one already running. Every kind's method is `Start`; `Handle` gone. An inherited event's `%!event%` is the maker's event (its key numbers it in the maker's list, which the sub app's inherited events mirror). Pin: `PLang.Tests/Runtime/App/Events/CodeEventsTests.cs`.

## Stage 5 (v3, rulings 53–59): system events, the lazy load
- `event/list/this.cs`: the actor's events load on their first ask or add. The doors are `On(item)` (answers at once once loaded), `On(name)` (the list a set runs: the name's own or the variable type's, never null) and `Add`/`Add(name, event)`. `Load` waits for the app's System first (every goal is read as System; without this the User's load and System's load waited on the same `.pr` read), runs the system/event files as this actor (`Run`, the folders from `origin.Events`), then takes in the maker's events. The snapshot's read (`snapshot/this.cs`) loads the actors' events before it binds what it kept (`Read`).
- `type/this.cs`: `Create` keeps its signature; when `On(this)` is completed the birth goes on as before, otherwise `Awaited`.
- `event/setting/this.cs`: `System: load | skip`. `origin/`: `@event()` async, `Events(app)` (root: own then os; sub app: own). `Executor.cs`: test mode's session opens before any variable set, so the load opens the stores in memory.
- Every caller moved to the async door (goal, step, action, channel, goal list, element, window list, item's `on` door, variable Before/After, debug Bind/Watch, Coverage.Watch, `app.event(context)`). `event/Scope.cs` deleted.
- Tests: `Runtime/App/Events/SystemEventsTests.cs` (5), `VariableEventsTests` (the one answer), `CreateEventTests` (a birth after the load). Bare `Variable.Set` calls in tests are now awaited, since the first set can wait on the load.

## Next
- plang-builder: build `os/system/event/Hello.goal`; rebuild EventAfterAction and AfterEachLlmQuery (comments reworded).
- Open with the architect: the two "maker's later event" pins (inherit at first ask vs at birth); Hello's line on every root app's start; SystemGoalsLeftAlone (the count can't see sets made inside the shortcut's isolated call).

## Code example
```csharp
// a goal's start: its list for the running actor, one line per side
var on = context.Actor!.@event[this];
var answer = await on.before.Start(this, context);
var result = answer is { Success: false } or { Handled: true } ? answer : await Enter(context);
return await on.after.Start(this, result, context);
```
