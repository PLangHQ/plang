# Architect summary — remove-goalcall

## 2026-06-09 — Initial design: remove `GoalCall`, one door into plang

**What this is.** `GoalCall` is a C# call-site type that overlaps `Goal` on identity and has accreted unrelated concerns (event context, an LLM-tool flag, the resolution logic). Ingi's goal: when you call a goal, you just use `Goal`. Through the design conversation we landed on: move the *arguments* off the reference and onto the `goal.call` action; let the reference be a `data.@this<Goal>` resolved lazily; funnel every goal invocation — including `app.Start` — through the single `goal.call` path. The architectural payoff is one C#→plang bridge with no exceptions.

**What was decided.**
- `goal.call` = `Goal` (data.@this<Goal>) + `Parameters` (List<data>) + hidden `PrPath`. Args set into the variable scope by the handler; `Goal` stays a shared stateless definition.
- Resolution: `app.Goal.Load(name, context, prPath?) → Data<Goal>` (absorbs `GetGoalAsync` + `LoadFromFile`). Named `Load` (not `Parse` — taken for text→Goal; not `Resolve` — `Load` is plainer and names the disk cost). Lives on the registry.
- Runner: `app.Run(goal, context)` (renamed `RunGoalAsync(Goal)`); `RunGoalAsync(GoalCall)` deleted.
- `Event`/`IEvent` deleted; `%!event%` survives by the firing path setting `context.Event` directly. `EventContext` stays.
- `goal.call(Actor: X)` now sets/restores `app.CurrentActor` around the run — closes a latent context-vs-CurrentActor split. Start delegates the entry via `goal.call(Goal: entry, Actor: User)`; System owns, User executes. No escape hatch.

**State.** Design settled in conversation. `plan.md` (spine) + `plan/call-sites.md` (full leaf-trace, ~30 production sites bucketed by disposition) written. Stage files **not yet carved** — waiting on Ingi's read-over and his answer on stage granularity (whether to pull Start-through-`goal.call` forward).

**Open for Ingi.** (1) Read-over of the plan. (2) Stage granularity — 4-stage cut as drafted, or surface the one-door property earlier? (3) Confirm coordination point with the tools-as-actions branch (where `Parallel` lands).

**Code example — the shape of the change.**
```
// before: the reference carries everything
public partial data.@this<GoalCall> GoalName { get; init; }   // name + prPath + parameters + parallel + event

// after: reference is just a Goal; args are the action's own
public partial data.@this<Goal>  Goal       { get; init; }    // resolved lazily via app.Goal.Load
public          List<data>       Parameters { get; init; }    // x=%y%, set into scope before running
[Store, Out] /* not LlmBuilder */ public partial data.@this<path>? PrPath { get; init; }  // build-time cache
```

Stage status:
| Stage | File | Status |
|-------|------|--------|
| 1 | Resolution + run primitives (`app.Goal.Load`, `app.Run`, Goal conversion hook) | pending |
| 2 | `goal.call` reshape + `CurrentActor` switch + `Build()` validation | pending |
| 3 | Reroute every other call site; delete `RunGoalAsync(GoalCall)` | pending |
| 4 | Delete `GoalCall`/`IEvent`/`path.GoalCall`; move `%!event%`; clean catalog | pending |
