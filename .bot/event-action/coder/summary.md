# coder — event-action

## Version
v2: stage 2 built (rulings 41–46). v1 was stage 1 (ec64ee712, 2c7b8f78d).

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

## Next
- plang-builder: rebuild BindOrder, LateEventJoins, ResumeTakesInTheBinding (wrong Event paths in their .pr) and build SystemGoalsLeftAlone, ListsTheActorsEvents, BindingStaysInItsTest.
- Stage 5: system events (`event.setting.system`, `os/system/event/Hello.goal`).

## Code example
```csharp
// a goal's start: its list for the running actor, one line per side
var on = context.Actor!.@event[this];
var answer = await on.before.Start(this, context);
var result = answer is { Success: false } or { Handled: true } ? answer : await Enter(context);
return await on.after.Start(this, result, context);
```
