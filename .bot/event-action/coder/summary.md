# coder — event-action

## Version
v1: the plan placed, the pushback ruled (architect 20–24), stage 1 built.

## What this is
What runs when an event fires used to be a "binding", and each of an item's moments (start, load, error …) was its own class. Decisions 4 and 13 make it one word and one shape: what is bound is an **event** (kinds `call`, `clause`, `code`, `inherited`), and an item's moments are one class, **`on`**: `before`, `after`, and each named moment itself an `on`. Stage 1 is the word and the folders; stage 2 moves the events onto the actor and makes the per-item lists on first run.

## What was done (stage 1)
- `PLang/app/event/`: `binding/**` is gone. `this.cs` is the bound event (abstract: `Name`, `When`, `Item`, `Actor`, `Scope`, `Start`, `Remove`, its snapshot key `{action, n}`, `Clone() => this`, type always `event`). Kinds: `call/` (on.event's goal call; `%!event%` is the event itself, with `!item` and `!data`), `clause/`, `code/` (C# handlers: debug, test timing, coverage), `inherited/` (runs the maker's as the event running here).
- `event/on/`: `this.cs` is one `on` (`[PlangType("on")]`), with the walk (`Before`, `After`, `IsBound`, `Catch`) moved off the deleted moment classes; `side.cs` (the base of `before.cs`/`after.cs`); `empty.cs` (the shared, closed moments, still here until stage 2); `serializer/Reader.cs` (refuses a written moment) and `serializer/Snapshot.cs` (reads an item's moments back).
- `event/list/` (was `on/list/`): the app's events, today's content; `Items()` and `Of(action)` number an event for its key. `event/serializer/Snapshot.cs` reads `{action, n}`.
- `module/on/event.cs` answers an `event`, and its `Event` is `data<on>`; `module/on/unbind.cs` takes `Event`.
- Error keys: `EventNoAction`, `EventNoGoalCall`, `SnapshotEventVariable`, `EventNotYours`.
- The snapshot dedupe (architect 22): a saved event is compared only against events inherited at birth, one for one. Pins in `SavedBindingsTests`: `OneStep_BoundTwiceOnOneMoment_ComesBackAsTwo`, `OneStep_BindingTwoMoments_ComesBackWithBoth`, `AMakersOne_AndASavesTwo_ComeBackAsTwo`, plus the existing `TheSameBinding_TakenInAndReadBack_FiresOnce`.
- Teaching `os/system/modules/on/{event.description,event.notes,event.examples,unbind.description,module.description}.md`: binding → event, `!result` → `!data` (a failure is `%!error%`). `loop/*` checked: its "binding" is a variable's, untouched.
- Re-pinned: `surface_golden.txt` (decisions 4, 13; architect 20–23), `pick_golden.json` (deterministic: the recorded answers are frozen, and only the rename and teaching moved).
- Plan placed at `test/plan/event-action/{start.md,start.goal}`; the stage 1 test `module/on/UnbindTakesTheEvent` is written, not built (the builder needs the TypeSafe key: plang-builder builds it).

## Next
- plang-builder: build `UnbindTakesTheEvent`, and rebuild the 14 `.pr` files whose on.event `Event` still records type `event` (they run, but the catalog says `on` now).
- Stage 2: the actor's events, the door `context.Actor.@event[item]` (a `ConditionalWeakTable`, a composed action asking with its catalog action), `%!x.on%` through item.Get, the variable name map (architect 24, the tester's option 3). Before code, the architect wants the name and body of the type-bound event's "does it take this item" member.

## Code example
```csharp
// on.event: the moment reached by the path, the event added to its side, answered
var on = Context.App.@event.Add(item).Add(moment.Name);
return Context.Ok<global::app.@event.@this>(on[when.Value].Add(side =>
    new global::app.@event.call.@this(side, __action!, Context.Actor!, scope.Value)));
```
