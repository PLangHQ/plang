# coder — app-systems

**Version:** v9 (stage 8 + the overnight order; v8 = stage 8 trace, v9 = frames onward)

## What this is
app-systems makes every `app.X` the type X (a generic `type<X>` over its concept's `list<X>`), so the plang path,
the C# path and the file path agree. The architect's plan (`.bot/app-systems/architect/plan.md`) has 13 stages;
the architect's running log of rulings is `.bot/app-systems/architect/summary.md` (decisions 1–149). Each slice
is reported to the architect (plang-cd session) and reviewed before the next.

## What was done this session (newest last; all pushed, suites at baseline unless noted)
- **First identity made once** (3d6aea817) — identity creation re-entered through its own signed save (503 nested
  `identity.get`, ~5 s on every fresh app). The in-flight identity now answers.
- **Code registry owns defaults per interface; a failed sign fails the plang write** (6cd222ae0) — `ICode.IsDefault`
  gone (Ed25519 is ISigning and IKey); the plang kind no longer writes unsigned on a failed sign.
- **Call-stack frames** (9c398c543, decisions 132/136) — a frame per goal, step and action holding `Goal`/`Step`/
  `Action`; `context.CallStack.Goal/.Step` answer the goal/step in play (`context.Goal/Step`, `AnchorScope` gone);
  `Duration` is live; `--debug` prints each step's time; test timings read the frame; MaxDepth 1500.
- **Asks wait without a limit** (020a736e5, decision 126/137) — the run's cancellation ends an ask; the channel's
  `Timeout` is untouched (no reader now — Ingi's call whether it goes).
- **8e — `type.Create` is the birth and fires `on.create`** (0089a34a0) — the sync build is internal `Make`;
  `kind.Decode` births through `Create`. Silent `ValueTask`-as-object callers found with an `[Obsolete]` probe.
- **Navigation** (e71ff15b6) — `%!app.module.file.read%` (a module's action), `%!app.actor.user.channel.x%` /
  `%!channels.x%` (channel list is an item).
- **8f — the `on` module** (14115ddc1, 318f42fa3, 26a9d8222; decisions 142–149) — `on.event(Event, When, Action,
  Scope)` binds a program's call on an event reached by path (`%!app.type.goal.on.start%`,
  `%!channels.audit.on.write%`); `on.unbind`, `on.cancel`. `%!event%` is the frame's (the call binding holds it on
  the frame it fired in while its call runs; `!item`, `!result` are properties). A mock is a binding kind
  (`event/binding/mock`). The `event` module, lifecycle, `Trigger`, `moment`, `context.Events` are gone. An
  unresolved bind path warns at build.

## Known reds (deliberate)
- 4 `PickListTests` (decider golden fixtures) differ by `event.on` leaving the catalog — regenerate with 8h's eval.
- plang: `EventAfterStep`, `EventAfterAction` compile to wrong event paths — teaching/eval, 8h's (decision 149).
- Pre-existing: the 2 Masks plang tests; `LoadAction_*` (stale fixture sources).

## Next
8g (modifiers become events: `on.error`/`on.cache`/`on.timeout` bind on the action before them; trace first), 8h
(the one eval for 8f+8g), then stages 9–12. Dead files for Ingi: `os/system/events/*.goal`, `os/system/.build/
run.pr`, `os/system/modules/event/Modules.goal`.

## Code example
```csharp
// a program's before-each-goal hook — the event is the bind target, reached by path
// - before each goal call LogBefore
on.event(Event=%!app.type.goal.on.start%, When="before", Action=goal.call(Name="LogBefore"))

// the goal and step in play come off the call stack
var step = context.CallStack.Step;          // was context.Step (stale after a goal call)
var took = context.CallStack.Current?.Duration;   // live inside the frame
```
