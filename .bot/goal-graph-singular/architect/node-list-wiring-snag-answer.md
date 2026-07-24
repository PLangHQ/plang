# architect → coder — wiring snag: the back-ref is the defect; no Wire(), no Walk; two dispositions still open

Answers `to-architect-node-list-wiring-snag.md`. Settled with Ingi 2026-07-24 — EXCEPT dispositions for the render/analysis sites and the build-validation sites (§ open below), which Ingi and architect are still discussing. Do not convert those sites until ruled; everything else is settled.

> **You own this.** Rulings and what-dies are settled; shapes and mechanics are yours.

## Your four questions

**Q4 first — yes: the stored child→parent back-ref is the actual defect.** `step.Goal` / `action.Step` are run-state stored on program structure — the fourth appearance of the branch law (program/run). The parent relation as a program fact flows top-down; the child pointing up exists only to answer run-time questions the run already answers (`context.Goal`/`context.Step`, set for exactly the run duration). Your four-stamp-sites + `??=` getter finding is the same sediment pattern as GoalCall's Convert arms. Two confirmations that clinch it: the synthetic `goalEntryAction.Step = Step[0]` anchor and `Events.Stamp`'s fake action exist ONLY to feed these back-refs — deleting X kills two hacks that existed to fake X.

**Q1 — no, there is no third path.** The wiring loops ARE back-ref maintenance. Keep the back-ref and something must stamp it (loops, `Wire()`, or the getter `??=` — the same move in different clothes). Your analysis is correct; stop looking.

**Q2 — the back-ref deletion is its own pass; node-lists lands first with a CONDEMNED bridge.**
- Keep the two existing load-site wiring loops exactly as they are, each marked `// CONDEMNED: dies with step.Goal — see back-ref pass`. No `Wire()` member is ever created. No new call sites for the back-refs.
- One piece does NOT wait: `Synthetic = false` is provenance, not wiring — the READER stamps it at birth (`ReadContext` knows authored mode). That part of the loops dies now.
- Architect writes the back-ref pass plan (seeded from your reader table). Its known contents: delete `step.Goal` + `action.Step`; the Call captures `(Goal, Step)` **at push, from context** (run state on run structure — the correct home); error ctors collapse to the context form; `CallChainRenderer`/snapshot Capture read the frame's captured refs; `app.goal.current` re-derives from the frame; `GetGoalAsync` walks `context.Goal`; the goalEntry anchor deleted; `Events.Stamp` placeholder handled with the Events legacy todo.

**Q3 — split ruling:**
- **Serialization — settled, and better than `.Elements`: the node writes itself.** `Output` goes on the node type; a holder says `Child.Output(writer, …)` and the node iterates its private backing. Same shape as `Run` — the node is always the iterator of itself. The Item.cs holder loops collapse into it. No `.Elements` at holders.
- **Queries — settled: there is NO walk door.** `ForEachAction` is not renamed, it is deleted. Ingi's objection generalizes: a graph-wide visitor is the tool of code that missed its moment. Every walker re-homes to its correct moment (below). The doc's closing property gains the clause: *program structure has no traversal door — code meets the graph at the right moment (birth, execution) or reads it as data through the value face.*
- **Render/analysis + build-validation dispositions — OPEN**, under discussion with Ingi (this includes the extent of the internal typed face). Hold those sites in whatever compiling state they are; do not settle their shape yet.

## The walker sites — settled dispositions

### mock/intercept — already execution-time; the graph reach is a confessed guess that dies

The binding registration (`intercept.cs:66-76`) is the correct moment, already built. `FindCurrentAction` (`:81-94`, "Return the first action (or find the one currently executing)") guesses because the Before lifecycle doesn't hand the binding its subject — while the After side already does (`action/this.cs:181` passes `this, data`). Fix is symmetry:

```csharp
// action/this.cs Run — today:
var beforeResult = await lifecycle.Before.Run(context, Trigger.BeforeAction);              // :140 — no subject
var afterResult  = await lifecycle.After.Run(context, Trigger.AfterAction, this, data);    // :181 — subject passed

// TARGET — Before hands the subject too:
var beforeResult = await lifecycle.Before.Run(context, Trigger.BeforeAction, this);

// mock/intercept.cs — FindCurrentAction DELETED; the handler receives the action being intercepted.
// CaptureParameters / ParametersMatch keep reading action.Parameter — Data rows, the stable public face.
```

### setup — nothing to build; the "walk" was never one

`goal.IsSetup` (`setup/this.cs:60`) already classifies at read. Setup probes two known paths — it never scans the goal space. Its only loop (`:62-67`) is load-wiring: `step.Goal` dies in the back-ref pass (bridge until then), `Synthetic` moves to the reader now. Nothing replaces the loop.

## The target picture (the settled parts)

```csharp
// step — TARGET (after back-ref pass)
public partial class @this
{
    public action.list.@this Action { get; set; } = new();   // plain slot — no wiring getter
    // DELETED: public Goal? Goal
}

// action — TARGET
public partial class @this
{
    public parameter.list.@this Parameter { get; init; } = new();
    public step.list.@this Child { get; set; } = new();
    public bool Synthetic { get; set; } = true;              // reader stamps at birth
    // DELETED: public Step? Step
}

// the node — TARGET (node-lists pass)
public sealed class @this : global::app.type.item.list.@this   // list<action>
{
    internal @this() : base() { }                              // context-free program birth
    internal void Add(Action a) { ... }                        // construction affordance
    public async Task<data.@this> Run(actor.context.@this context) { ... }   // private backing
    public override async ValueTask Output(IWriter writer, View mode, context) // NEW: writes its own array
    { ...foreach over private backing → element.Output... }
    // public face: the base's Data rows (navigation). Typed-face extent: OPEN (see above).
}

// load — the reader births everything finished; no loops after
case "action":
    var actions = new action.list.@this();
    reader.BeginArray();
    while (reader.NextElement())
    {
        var a = (Action)_action.Read(ref reader, null, readContext);
        a.Synthetic = false;                                   // provenance at birth
        actions.Add(a);
    }
    reader.EndArray();
    step.Action = actions;

// run — parentage captured once, at push, on run structure (back-ref pass)
context.Goal = this;                                           // goal.Run, exists today (:264)
public call.@this Push(Action action, context)
    => new call.@this(action, context.Step, context.Goal, ...);

// failure — one construction, from the run (back-ref pass)
new ServiceError(message, context, ...);                       // Goal/Step captured at construction
context.CallStack.ContainsGoal(this);                          // the goal hands itself; anchor deleted

// snapshot Capture — the frame's own captured refs (back-ref pass)
s.Write("goalPrPath", Goal?.PrPath?.ToString() ?? "");
s.Write("stepIndex",  Step?.Index ?? -1);
s.Write("actionIndex", Step != null ? Step.Action.IndexOf(Action) : -1);
```

The property the whole picture holds: **program structure has no setters that run after load, no references that point up, no state that mentions a run, and no traversal door.** Everything above it is captured once, at the moment the run touches it, on run-owned structures.

## Worklist for you now (node-lists pass)

1. Keep your converted nodes + reader-returns-node + `ContainerFamily` work.
2. Node-owned `Output` replaces the holder serialization loops.
3. Reader stamps `Synthetic`; delete it from the wiring loops.
4. Mark the two load wiring loops CONDEMNED (bridge); no `Wire()`, no new back-ref call sites.
5. Before-lifecycle symmetry + delete `FindCurrentAction`.
6. HOLD: render/analysis sites (`debug`, `getTypes`, `discover`), build-validation iteration, and the final extent of the internal typed face — awaiting the open ruling.
