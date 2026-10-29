# coder v1: event-action, the plan placed, the pushback, stage 1

Brief: `.bot/event-action/architect/briefs/coder-start.md`. Plan: `.bot/event-action/architect/plan/{start.md,start.goal,expected.md}`, decisions 1–18.

## Steps
1. Baseline (C# sweep + plang --test) → `.bot/event-action/coder/baseline-failures.txt`.
2. Push back to the architect before any code (sent 2026-10-29): stage 1/2 boundary, clauses between stages 2 and 3, the per-item map growing with composed actions and variables (weak table + catalog key), the running event's snapshot key and the dedupe by action and moment, on.event's `Event` holding an `on`, the one re-pin for Scope's teaching. Shapes: A the door `context.Actor.@event[item]` with `event.Applies(item)` per kind; B `%!x.on%` through item.Get's `on` key → the running actor's on, `item.on` / `on/empty.cs` go; C on.unbind's parameter `Event`; D a goal's list as its birth fact for stage 8.
3. Place the plan: `test/plan/event-action/{start.md,start.goal}`; the stage 1 test `module/on/UnbindTakesTheEvent` (red today: on.event answers a binding with no name or when).
4. Stage 1, after the architect's answer: `event/binding/**` → `event/{this, call, clause, code, inherited}`; the moment classes collapse into one `on` (decision 13); on.event answers an `event`; on.unbind takes `Event`; error keys and messages say event; the teaching in `os/system/modules/on/*` and `loop/*`; re-pin the surface golden, PickListTests and ModulePageTests; boy scout: each event kind clones as itself.

## Blocked on
- The architect's answer to the pushback (points 1, 6 and shape C gate stage 1).
