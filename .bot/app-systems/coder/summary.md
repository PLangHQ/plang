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
- **Container crash (2026-09-28 ~09:38) — 8g recovered from the transcript** (c0c28a841). The uncommitted 8g work
  was lost with the container; every Edit/Write since the 8g trace commit was replayed from the session transcript
  into a scratch tree (all applied cleanly, file set identical to the pre-crash `git status`), then reinstalled via
  Edit/Write. Verified against a real baseline (stash → rebuild → full gate → restore).
- **8g — modifiers become clauses** (c0c28a841, ee28abc9d; decision 151) — `on.error`/`on.cache`/`on.timeout` are
  `clause.@this` (handler is an `IClause`), siblings after their action in the step's code, bound on it when the
  code is read (`action.list.Bind`), never started as steps. `action.Start` = frame → `Attempt` (before-start →
  dispatch → after-start) → the error outcome (`on.error.Catch`, first match in written order) → `%!data%`. A retry
  is `action.Attempt` (fresh deadline, fresh cache lookup). Recovery is on.error's `list<action>` property;
  durations are `duration` (and duration's reader now takes ISO 8601 as taught). `module.Modifier`, `modifier.list`,
  `IModifier`, `ModifierAttribute`, `cache.wrap`, `timeout.after` gone. Python twins, fixtures, templates,
  Properties.llm, teaching md, decider.json updated. `action.Step` is init-only (tests use `action.In(step)`).

## Known reds (deliberate)
- 5 `PickListTests` (decider golden fixtures: `event.on` left the catalog, on.error's signature changed) —
  regenerate with 8h's eval.
- plang: `EventAfterStep`, `EventAfterAction` compile to wrong event paths — teaching/eval, 8h's (decision 149).
- Pre-existing: the 2 Masks plang tests; `LoadAction_*` (stale fixture sources). The fresh container adds ~130
  environmental C# reds that are in the baseline too (sqlite dirs, playwright, etc.).

## Open — the TRANSITIONAL reader (8g's last item)
`action/serializer/Reader.cs` (`Trailing`, `"modifier"`/`"recovery"` cases) + `step/serializer/Reader.cs` drain
still load the two `.pr` files with nested modifiers: `os/system/builder/BuildGoal/.build/start.pr` (the builder's
own program) and `os/system/error/.build/show.pr`. They can't be rebuilt today: the decider never asks its common
yes/no questions (`%decider%` from `read decider.json` stays a lazy `file` item; `decider1.template`'s
`for c in decider.common` renders nothing), so `on.error` is never listed and the check refuses the LLM's clause.
Pre-existing — the pre-8g build (446833981) fails Show too. Asked the architect: keep TRANSITIONAL until 8h's
bootstrap, or fix the file-read→template issue in 8g. How to rebuild once unblocked: point
`PlangConsole/bin/Debug/net10.0/os` at a scratch copy of `os/`, move the two targets aside, run from `os/`
`TYPESAFE_API_KEY="$(cat /shared/hopkaup/secrets/typesafe.txt)" plang '--build={"files":[…]}'`, restore the link.

## Next
Finish 8g (above), 8h (the one eval for 8f+8g: goldens + bootstrap), then stages 9–12. Dead files for Ingi:
`os/system/events/*.goal`, `os/system/.build/run.pr`, `os/system/modules/event/Modules.goal`.

## Code example
```csharp
// a program's before-each-goal hook — the event is the bind target, reached by path
// - before each goal call LogBefore
on.event(Event=%!app.type.goal.on.start%, When="before", Action=goal.call(Name="LogBefore"))

// the goal and step in play come off the call stack
var step = context.CallStack.Step;          // was context.Step (stale after a goal call)
var took = context.CallStack.Current?.Duration;   // live inside the frame

// 8g — clauses are the action's siblings, bound when the code is read
// - read %path%, timeout after 5 seconds, on error call Fallback
file.read(Path=%path%); on.timeout(After="PT5S"); on.error(Recovery=[goal.call(Name="Fallback")])
```
