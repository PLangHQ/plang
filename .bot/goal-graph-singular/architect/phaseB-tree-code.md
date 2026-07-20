# Phase B tree — the code (settled + readover-corrected, 2026-07-20)

The actual C# for the settled design. This is the code layer under `phaseB-tree-design.md` + `phaseB-tree-resolutions.md` — read those for the *why*; this is the *what*. **Corrected after a flaw readover** — the five+one flaws found are called out inline (⚠) so they don't get lost.

> **You own this.** Bodies are the shape, not holy writ; names use the target `Run` (the `RunAsync`→`Run` drop is part of this work). Aliases: `Step = app.goal.steps.step.@this`, `Action = app.goal.steps.step.actions.action.@this`. Wire keys shown are pre-sweep (`steps`/`actions`/`action`); the singular sweep flips them with the writer.

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
// goal/steps/step/actions/list/this.cs
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

## 5. Backref wire — ⚠ flaw-1 (the standalone lists stamp nothing)

The old collections stamped `step.Goal`/`action.Step` (`goal/this.cs:47`, `steps/this.cs:24,47,113`, `step/this.cs:55`). The standalone lists don't, and cycle-detection (`goalEntryAction.Step`, `ContainsGoal` → `action.Step?.Goal?.PrPath`) + coverage need them. Getter-stamp can't reach nested `Child` steps (built bottom-up, before the goal exists). So a **post-build top-down wire** after the goal reader constructs the tree:

```csharp
// naming/home to settle (verb+noun placeholders); MUST run once after the goal is built
static void Wire(goal.@this g) { foreach (var s in g.Step) { s.Goal = g; Wire(s, g); } }
static void Wire(Step s, goal.@this g)
{
    foreach (var a in s.Action) { a.Step = s; foreach (var cs in a.Child) { cs.Goal = g; Wire(cs, g); } }
}
```
Open: whether this lives in the goal reader (post-build) or as a getter-stamp chain that threads the goal down. Construction-time is cleaner (one pass, no per-access cost) — pick the home, kill the verb+noun name.

## 6. The three readers + B1 lazy fix

```csharp
// goal/serializer/Reader.cs
case "steps":
    var steps = new List<Step>();
    reader.BeginArray();
    while (reader.NextElement()) steps.Add((Step)_step.Read(ref reader, kind, ctx));
    reader.EndArray();
    goalStep = new goal.step.list.@this(steps);
    break;

// step/serializer/Reader.cs
case "actions":
    var actions = new List<Action>();
    reader.BeginArray();
    while (reader.NextElement()) actions.Add((Action)_action.Read(ref reader, kind, ctx));
    reader.EndArray();
    stepAction = new goal.steps.step.actions.list.@this(actions);
    break;

// action/serializer/Reader.cs — B1: lazy the step reader to break the step→action→step ctor cycle
private goal.step.serializer.Reader? _stepReader;
private goal.step.serializer.Reader StepReader => _stepReader ??= new();

case "child":
    var child = new List<Step>();
    reader.BeginArray();
    while (reader.NextElement()) child.Add((Step)StepReader.Read(ref reader, kind, ctx));
    reader.EndArray();
    actionChild = new goal.step.list.@this(child);
    break;
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

1. **backref stamping** gone from the standalone lists → §5 post-build wire.
2. **cancellation** dropped → restored in §1/§2 (return at step level, throw at action level).
3. **`|| Handled`** dropped from `action.list.Run` → restored (legit event-handled stop).
4. **bare-if empty `Child`** returns `Ok` not the bool → verify (§3).
5. **`Steps[0]`/`Steps.Count`** in `goal/this.cs` → `Step[0]`/`Step.Count`.
6. **Coverage `Merge`** breaks "no registry" → keyed store stays, `Cover` derives the key (§8).
