# Phase B tree — the code (settled + readover-corrected, 2026-07-20)

The actual C# for the settled design. This is the code layer under `phaseB-tree-design.md` + `phaseB-tree-resolutions.md` — read those for the *why*; this is the *what*. **Corrected after a flaw readover** — the five+one flaws found are called out inline (⚠) so they don't get lost.

> **You own this.** Bodies are the shape, not holy writ; names use the target `Run` (the `RunAsync`→`Run` drop is part of this work). Aliases: `Step = app.goal.step.@this`, `Action = app.goal.step.action.@this`. Wire keys shown are pre-sweep (`steps`/`actions`/`action`); the singular sweep flips them with the writer.

## 0. Namespaces are singular — the folder rename runs *first/with* the tree

No `steps`/`actions` plural anywhere. Concept folder singular + `list/` subfolder for the collection (OBP `X`/`X.list`):

```
goal/step/this.cs            → app.goal.step.@this             (step element; moves up from goal/steps/step/)
goal/step/list/this.cs       → app.goal.step.list.@this        (step.list — replaces steps.@this)
goal/step/action/this.cs     → app.goal.step.action.@this      (action element; moves up from …/actions/action/)
goal/step/action/list/this.cs→ app.goal.step.action.list.@this (action.list)
```

The plural *wrapper* folders (`steps/`, `actions/`) delete; the elements move up. **This restructure is the first move**, not an afterthought — the tree's whole payload is the `X.list` collections, and they must be born at singular paths (creating `goal.steps.step.actions.list` then renaming is churn). So: run the namespace rename (`goal.steps.step`→`goal.step`, `…actions.action`→`goal.step.action`, wire keys `steps`/`actions`→`step`/`action`) up front, then the tree code below lands entirely singular. (Refs to `steps/this.cs`, `steps.RunAsync`, etc. in this doc point at *current* HEAD — the demolition targets — and are correctly plural.)

## 1. `step.list` — NEW (typed storage, navigable, owns `Run`)

```csharp
// goal/step/list/this.cs
using System.Collections;

public sealed class @this : IReadOnlyList<Step>              // IReadOnlyList → the list kind claims it (A1)
{
    private readonly List<Step> _steps;
    public @this(List<Step> steps) => _steps = steps;

    public int Count => _steps.Count;
    public Step this[int i] => _steps[i];
    public IEnumerator<Step> GetEnumerator() => _steps.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var step in _steps)
        {
            // ⚠ flaw-2: cancellation was in steps.RunAsync:139 (return the error at step level)
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new app.error.Error("Operation was cancelled", "Cancelled", 499));
            result = await step.Run(context);
            if (result.ShouldExit()) break;                 // ShouldExit already folds Returned (A6)
        }
        return result;
    }
}
```

## 2. `action.list` — NEW (the chain resolution; B2(a))

```csharp
// goal/step/action/list/this.cs
using System.Collections;

public sealed class @this : IReadOnlyList<Action>
{
    private readonly List<Action> _actions;
    public @this(List<Action> actions) => _actions = actions;

    public int Count => _actions.Count;
    public Action this[int i] => _actions[i];
    public IEnumerator<Action> GetEnumerator() => _actions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        for (int i = 0; i < _actions.Count; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();   // ⚠ flaw-2: was step.RunAsync:162
            var action = _actions[i];
            result = await action.Run(context);                        // setup (file.exists/compare) & non-cond dispatch
            if (result.ShouldExit() || result.Handled) break;          // ⚠ flaw-3: keep Handled (legit event-handled stop)
            if (action.IsCondition && await result.ToBooleanAsync())
            {
                result = await action.Child.Run(context);              // gate fired → run the branch body
                break;                                                 // skip the rest of the chain
            }
        }
        return result;
    }
}
```

`IsCondition` matches only `if`/`elseif`/`else`, so a step's leading `file.exists` / `condition.compare` / `variable.set` (A4 — verified in 1663 `.pr`) just run in order; only the gate fires a `Child`. This also **fixes the latent `skipBelowIndent` bug** (`steps/this.cs:132` checks `Actions[0].Module=="condition"`, which is false for `[file.exists, condition.if]`).

## 3. `action.Child` + `condition.if.Run` collapse

```csharp
// action/this.cs — NEW property (branch body; empty on every non-control-flow action)
[Store, Debug, Default]
public app.goal.step.list.@this Child { get; init; } = new(new List<Step>());
```

```csharp
// condition/if.cs — Run() collapses to exactly what Elseif already is; Orchestrate + simple-form blocks delete
public async Task<data.@this> Run()
{
    var evalResult = await Evaluator.Evaluate(this);
    if (!evalResult.Success) return evalResult;
    var b = await evalResult.ToBooleanAsync();
    if (await Negate.ToBooleanAsync()) b = !b;
    return Data(b);
}
```
`condition.if` no longer touches `Step`, siblings, or the guard. `action.Run` is unchanged bar the `RunAsync`→`Run` rename (no fire block — fire lives in `action.list.Run`).

> ⚠ **flaw-4 (verify):** a truthy bare `if %x%` with an empty `Child` now returns the empty-`Child` `Ok`, not the old simple-form bool. A bodyless `if` does nothing, so this is almost certainly fine — but confirm nothing downstream read `%!data%` as the bool after a bare if.

## 4. `step.Run` delegates; `goal.Run` seam

```csharp
// step/this.cs — Run() keeps lifecycle + try/catch; the action foreach becomes Action.Run
public async Task<data.@this> Run(actor.context.@this context)
{
    context.Step = this;
    var lifecycle = context.LifecycleFor(this);

    var beforeResult = await lifecycle.Before.Run(context, app.@event.Trigger.BeforeStep);
    if (!beforeResult.Success) return beforeResult;
    if (beforeResult.Handled) return beforeResult;

    data.@this result;
    try
    {
        result = await Action.Run(context);            // Action : action.list owns the chain loop
    }
    catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))
    {
        var typeName = ex.GetType().Name;
        var key = typeName == nameof(Exception) ? "StepError"
            : (typeName.EndsWith("Exception", StringComparison.Ordinal) ? typeName[..^"Exception".Length] : typeName);
        result = context.Error(new global::app.error.ServiceError(ex.Message, key, 400) { Exception = ex });
    }

    var afterResult = await lifecycle.After.Run(context, app.@event.Trigger.AfterStep);
    if (!afterResult.Success) return afterResult;
    return result;
}
```

```csharp
// goal/this.cs — inside Run (was RunAsync)
if (Step.Count > 0) goalEntryAction.Step = Step[0];    // ⚠ flaw-5: Steps[0]/Steps.Count → Step[0]/Step.Count
// …
var result = await Step.Run(context);                  // was: await Steps.RunAsync(context)
```

## 5. Backrefs are born-with — ⚠ flaw-1 (NO stamping, NO `Wire`)

`step.Goal ??= Goal` and a post-build wire are the **late-stamp** smell (Ingi). The step is *born with* its goal; the action *born with* its step. The parent rides the `ReadContext` (built to grow — see its doc-comment), and each child sets its backref in its own initializer, at creation. So **`Wire` never exists**, and every `??=` deletes (`goal/this.cs:47`, `steps/this.cs:24,47,113`, the `Actions` getter `step/this.cs:55` — the collections that did it are gone anyway).

```csharp
// type/reader/ReadContext.cs — grow it (the intended extension)
public sealed record ReadContext(
    actor.context.@this Context, string? Template = null, /* … existing … */,
    goal.@this? Goal = null,        // the goal being read — a step is born with it
    Step? Step = null);             // the step being read — an action is born with it
```

## 6. The three readers — thread the parent (born-with) + B1 lazy fix

```csharp
// goal/serializer/Reader.cs — the goal exists before its steps; each step born with it
var goal = new goal.@this { App = ctx.Context.App };   // scalars set as read (init→set, §note)
case "steps":
    var steps = new List<Step>();
    reader.BeginArray();
    while (reader.NextElement())
        steps.Add((Step)_step.Read(ref reader, kind, ctx with { Goal = goal }));   // born with goal
    reader.EndArray();
    goal.Step = new goal.step.list.@this(steps);
    break;

// step/serializer/Reader.cs — born with the goal from ctx; threads itself to its actions
var step = new Step { Goal = ctx.Goal! };              // ← born with the goal, set once at birth
case "actions":
    var actions = new List<Action>();
    reader.BeginArray();
    while (reader.NextElement())
        actions.Add((Action)_action.Read(ref reader, kind, ctx with { Step = step }));   // action born with step
    reader.EndArray();
    step.Action = new goal.step.action.list.@this(actions);
    break;

// action/serializer/Reader.cs — born with the step; Child steps born with the goal (ctx.Goal rode down)
// B1: lazy the step reader to break the step→action→step ctor cycle
private goal.step.serializer.Reader? _stepReader;
private goal.step.serializer.Reader StepReader => _stepReader ??= new();

var action = new Action { Step = ctx.Step! };          // ← born with the step
case "child":
    var child = new List<Step>();
    reader.BeginArray();
    while (reader.NextElement())
        child.Add((Step)StepReader.Read(ref reader, kind, ctx));   // ctx.Goal still set → child born with goal
    reader.EndArray();
    action.Child = new goal.step.list.@this(child);
    break;
```

**Note (the one consequence):** the goal is created before its scalars finish reading (steps come mid-object in the `.pr`), so the graph items' scalar props flip `init` → `set` — the reader populates a created-first object. That's ordinary deserialization (the reader *is* the construction), not post-hoc stamping; the backref is born-with. Pure-`init` immutability would instead need the ctor to build children from buffered raw (a raw-parse layer) — same born-with, more code. Lean: `set` + born-with via the context (the graph is read-only after load either way).
```

## 7. `Output` — action gains `child`; step drops `indent`

```csharp
// action/this.Item.cs — after the modifiers array (Child empty on non-control-flow → omitted)
if (Child.Count > 0)
{
    writer.Name("child");
    writer.BeginArray(Child.Count);
    foreach (var s in Child) await s.Output(writer, mode, context);   // each step writes itself → recursion
    writer.EndArray();
}
```
```csharp
// step/this.Item.cs — DELETE this line (indent is gone; display indent derives from tree depth, A3)
writer.Name("indent"); writer.Int(Indent);
```
The tree serializes recursively: `goal.Output` → `step.Output` (writes `actions`) → `action.Output` (writes `child`) → `step.Output` …

## 8. Coverage — observer derives; keyed store stays for `Merge` (⚠ flaw-6)

Coverage `Merge`s across App boundaries (`run.cs:239`), so action *references* can't be the key — a **stable derived key** is. Nothing is stamped on `Data`; the observer derives from the natural facts:

```csharp
// test/run.cs — the AfterAction observer (replaces the Properties["branch*"] reads at :109-128)
childApp.Test.Coverage.RecordModuleAction(action.Module, action.ActionName);
if (action.IsCondition && result != null && await result.ToBooleanAsync())
    childApp.Test.Coverage.Cover(action);
```
```csharp
// test/Coverage.cs — Cover derives a stable branch key from the action's tree position
public void Cover(Action a)
{
    var s = a.Step;
    var site = $"{s?.Goal?.Path}:{s?.Index}:{s?.Action.IndexOf(a)}";   // survives Merge (no object refs)
    _covered.GetOrAdd(site, 0);
}
```
- **Declared** branches (for "which weren't covered") still come from `test.discover` walking the tree's condition actions — same derived key, no `Decision`, no seeded chain.
- **Dies:** the `branchIndex`/`branchLabel`/`branchChain` stamping (`if.cs`); the `Properties["branch*"]` reads (`run.cs:109-128`); `RecordBranch(site,int)`/`RecordBranchLabel`/`RecordBranchChain` + `_branches`/`_branchLabels`/`_branchChains` → collapse to the single `_covered` (+ declared). `Merge` narrows to unioning `_covered`.
- **Stays:** a keyed, mergeable store (not a tree-walk) — corrected from the earlier "no registry" claim.

## 9. The builder (coder-owned) + display indent

- **Indented block → deterministic fold**, post-compile: fold a deeper-indented step into the preceding step's **gate** action (`IsCondition`, not `Actions[0]` — A4) `Child`. `indent` becomes transient parse state. Name the pass without a verb+noun `TreeBuilder`.
- **Inline `if/elseif/else` → LLM**: emit each branch *body* as `Child` steps (with per-branch `text`); leave *setup* actions (`file.exists`, the compound `condition.compare`+`set`) as leading siblings (A4).
- **A3 display indent**: `goal.ToText`/`ToString` (`goal/this.cs:68,211`) render from `step.Indent` — with the field gone, compute indent from tree depth at render.

## Demolition (net)

**Delete:** `steps.@this` (whole); `Decision` (whole); `condition.if.Orchestrate` + simple-form block; `step.Indent` + wire `indent`; `GoalSteps` alias; branch stamping in `if.cs`; `Properties["branch*"]` reads in `run.cs`; `Coverage`'s `_branches`/`_branchLabels`/`_branchChains` + `RecordBranch*`; `discover.SeedBranchChains`'s `Decision` use; `skipBelowIndent`/`HasIndentedChildren`.
**Change:** `goal.Steps→Step` (`step.list`); `step.Actions→Action` (`action.list`); `action` gains `Child`; `RunAsync→Run`; backref wire (§5); coverage observer (§8); `goal/this.cs` `Steps[]→Step[]`; `step.HasSubSteps` → `Action.Any(a => a.Child.Count > 0)`.
**Stays:** `goal.Run`/`step.Run` lifecycle; `condition.if`/`elseif`/`else` evaluation; `IsCondition`; `Coverage.RecordModuleAction`/`Merge` (narrowed); the item `Output`/`Reader` (extended); `goal.Child` (sub-goals).

## Flaw readover — the six caught writing this up

1. **backref stamping** gone from the standalone lists → §5 born-with via `ReadContext` (NO `Wire`, NO `??=` — Ingi: any after-stamping is the late-stamp smell).
2. **cancellation** dropped → restored in §1/§2 (return at step level, throw at action level).
3. **`|| Handled`** dropped from `action.list.Run` → restored (legit event-handled stop).
4. **bare-if empty `Child`** returns `Ok` not the bool → verify (§3).
5. **`Steps[0]`/`Steps.Count`** in `goal/this.cs` → `Step[0]`/`Step.Count`.
6. **Coverage `Merge`** breaks "no registry" → keyed store stays, `Cover` derives the key (§8).
