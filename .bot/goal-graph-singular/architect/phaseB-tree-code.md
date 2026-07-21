# Phase B tree — the code (settled + readover-corrected, 2026-07-20)

The actual C# for the settled design. This is the code layer under `phaseB-tree-design.md` + `phaseB-tree-resolutions.md` — read those for the *why*; this is the *what*. **Corrected after a flaw readover** — the five+one flaws found are called out inline (⚠) so they don't get lost.

> **You own this.** Bodies are the shape, not holy writ; names use the target `Run` (the `RunAsync`→`Run` drop is part of this work). Aliases: `Step = app.goal.step.@this`, `Action = app.goal.step.action.@this`. **Wire keys are singular** throughout (`step`/`action`/`name`/`child`) — the rename runs first (§0), and the tree regenerates the `.pr` anyway, so the reader/writer read/write singular keys, never the old plural.

## 0. Namespaces are singular — the folder rename runs *first/with* the tree

No `steps`/`actions` plural anywhere. Concept folder singular + `list/` subfolder for the collection (OBP `X`/`X.list`):

```
goal/step/this.cs            → app.goal.step.@this             (step element; moves up from goal/steps/step/)
goal/step/list/this.cs       → app.goal.step.list.@this        (step.list — replaces steps.@this)
goal/step/action/this.cs     → app.goal.step.action.@this      (action element; moves up from …/actions/action/)
goal/step/action/list/this.cs→ app.goal.step.action.list.@this (action.list)
```

The plural *wrapper* folders (`steps/`, `actions/`) delete; the elements move up. **This restructure is the first move**, not an afterthought — the tree's whole payload is the `X.list` collections, and they must be born at singular paths (creating `goal.steps.step.actions.list` then renaming is churn). So: run the namespace rename (`goal.steps.step`→`goal.step`, `…actions.action`→`goal.step.action`, wire keys `steps`/`actions`→`step`/`action`) up front, then the tree code below lands entirely singular. (Refs to `steps/this.cs`, `steps.RunAsync`, etc. in this doc point at *current* HEAD — the demolition targets — and are correctly plural.)

## 1. `step.list` — the step NODE (`[i]` + `.list`, owns `Run`)

`goal.step` is a **node, not "a list"** — a singular name that reads as a step: `[i]`/`.list` reach the collection, and `.current` (the running step) is a **nav derivation from the callstack**, not node state (see below). The node itself is minimal — no `.current`, no `AsyncLocal`.

```csharp
// goal/step/list/this.cs
public sealed class @this
{
    private readonly List<Step> _steps;
    public @this(List<Step> steps) => _steps = steps;

    public Step this[int i] => _steps[i];                    // goal.step[0]
    public IReadOnlyList<Step> list => _steps;               // goal.step.list  (IEnumerable → list kind → navigable)
    public int Count => _steps.Count;

    public async Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var step in _steps)
        {
            if (context.CancellationToken.IsCancellationRequested)   // ⚠ flaw-2: was steps.RunAsync:139
                return context.Error(new app.error.Error("Operation was cancelled", "Cancelled", 499));
            result = await step.Run(context);
            if (result.ShouldExit()) break;                  // ShouldExit folds Returned (A6)
        }
        return result;
    }
}
```

**`.current` is callstack-derived, not stored (coder pushback, accepted).** A node-held `AsyncLocal` cursor forks from `app.goal.current` (which reads the callstack) and disagrees under `Child` nesting — the outer list's cursor is the condition, while what's *running* is the deep action. So `%goal.step.current%` / `%step.action.current%` resolve at the **nav boundary** from `context.CallStack` (nav always has a context), exactly like `%goal.current%`:

```
current step   = context.CallStack.Current?.Action?.Step     // %goal.step.current% and bare %goal.step% sugar
current action = context.CallStack.Current?.Action           // %step.action.current%
current goal    = …Action.Step.Goal                          // = %goal.current%
```
Correct under nesting (actions push, so `Current.Action` is what's executing), zero per-node state. **For Phase B this nav is optional** — nothing in the tree reads `.current`; add the resolver when a real `%…current%` consumer arrives.

**Access lines up C# ⇄ PLang:** `[i]`/`list` = `%goal.step[0]%`/`%goal.step.list%`. `%goal.step.list%` hands back `_steps` (`IEnumerable` → list kind claims it → `.list[0]`/`.where` work) — replaces coder's A1 (`list` property, not `IReadOnlyList` on the class). Runtime-nav to confirm: `%goal.step[0]%` resolves the node's `this[int]` indexer, else `%goal.step.list[0]%`.

## 2. `action.list` — the action NODE (the chain resolution; B2(a))

Twin of `step.list` — minimal node (no `.current`; that's the callstack derivation from §1), plus `IndexOf` for coverage (C1); its `Run` is the chain resolution.

```csharp
// goal/step/action/list/this.cs
public sealed class @this
{
    private readonly List<Action> _actions;
    public @this(List<Action> actions) => _actions = actions;

    public Action this[int i] => _actions[i];                // step.action[0]
    public IReadOnlyList<Action> list => _actions;           // step.action.list
    public int Count => _actions.Count;
    public int IndexOf(Action a) => _actions.IndexOf(a);     // coverage key (C1)

    public async Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var action in _actions)
        {
            context.CancellationToken.ThrowIfCancellationRequested();   // ⚠ flaw-2: was step.RunAsync:162
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

## 2b. `modifier.list` — the third node (fixes the naked `List`, pulls lifecycle out of `action`)

**Today:** `action.Modifiers` is a **public bare `List<modifier.@this>`** (`action/this.cs:38`) — the naked-collection smell the whole `X.list` pattern kills. The wrap-fold is **inlined on `action.RunAsync`** (`:164-183`), and — the part Ingi flagged — `action` loops over the modifiers firing *their* `AfterAction` (`:180-182`), acting as a middleman for their lifecycles. (`modifiers.@this` the collection was deleted in the earlier modifier-wrap-ownership pass, which is *why* the fold got inlined — it had no home.)

**Change:** make `action.Modifier : modifier.list` — the third `X.list` node. Modifiers **wrap** (not sequence-run), so its owned operation is `Wrap`, not `Run`:

```csharp
// goal/step/action/modifier/list/this.cs — node; owns the compose fold, NOT the lifecycle
public sealed class @this
{
    private readonly List<Modifier> _modifiers;
    public @this(List<Modifier> modifiers) => _modifiers = modifiers;

    public Modifier this[int i] => _modifiers[i];               // action.modifier[0]
    public IReadOnlyList<Modifier> list => _modifiers;          // action.modifier.list
    public int Count => _modifiers.Count;

    public async Task<(Func<Task<data.@this>>? Wrapped, IError? Error)> Wrap(Func<Task<data.@this>> inner, actor.context.@this ctx)
    {
        var execute = inner;
        for (int i = _modifiers.Count - 1; i >= 0; i--)         // compose right-to-left; no lifecycle loop
        {
            var (wrapped, err) = await _modifiers[i].Wrap(execute, ctx);
            if (err != null) return (null, err);
            execute = wrapped!;
        }
        return (execute, null);
    }
}
```

**Each modifier fires its OWN lifecycle** — the `foreach … After.Run` loop leaves `action` and moves into the modifier's `Wrap` (Ingi: a modifier should own its lifecycle, not have `action` fire it):

```csharp
// modifier/this.cs — the wrapper it returns fires this modifier's AfterAction as it unwinds
public async Task<(Func<Task<data.@this>>? Wrapped, IError? Error)> Wrap(Func<Task<data.@this>> inner, actor.context.@this ctx)
{
    // …Resolve params → IModifier (as today)…
    var innerWrapped = mod.Wrap(inner, ctx);
    Func<Task<data.@this>> wrapped = async () =>
    {
        var result = await innerWrapped();
        await ctx.LifecycleFor(this).After.Run(ctx, AfterAction, this, result);   // ← this modifier, its own result
        return result;
    };
    return (wrapped, null);
}
```

```csharp
// action/this.cs RunAsync — the modifier branch collapses; the AfterAction loop is DELETED
var (composed, err) = await Modifier.Wrap(() => DispatchAsync(context), context);
if (err != null) return context.Error(err);
data = await composed!();       // each modifier fires its own AfterAction as the chain unwinds
```

Result: all three collections own their operation — **`step.list.Run` · `action.list.Run` · `modifier.list.Wrap`** — the naked `List` is gone, and the stray lifecycle loop leaves `action`. Bonus correctness: each modifier's `AfterAction` now fires with **its own layer's result** (not the old loop's shared final `data`); coverage still sees one fire per modifier, so nothing regresses.

**Flag:** this **un-does** the modifier-wrap-ownership inline (which existed only because the collection was deleted) — a conscious reversal, consistent with the tree bringing collections back as owning nodes. Wire (`modifiers`→`modifier` key), reader (born-with the action), and `modifier : action` subtype are unchanged.

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
case "step":                                            // singular wire key ("steps" is gone)
    var steps = new List<Step>();
    reader.BeginArray();
    while (reader.NextElement())
        steps.Add((Step)_step.Read(ref reader, kind, ctx with { Goal = goal }));   // born with goal
    reader.EndArray();
    goal.Step = new goal.step.list.@this(steps);
    break;
// (goal's sub-goals ride the singular "child" key → list<goal>, a separate case)

// goal/step/serializer/Reader.cs — born with the goal from ctx; threads itself to its actions
var step = new Step { Goal = ctx.Goal! };              // ← born with the goal, set once at birth
case "action":                                          // singular wire key ("actions" is gone)
    var actions = new List<Action>();
    reader.BeginArray();
    while (reader.NextElement())
        actions.Add((Action)_action.Read(ref reader, kind, ctx with { Step = step }));   // action born with step
    reader.EndArray();
    step.Action = new goal.step.action.list.@this(actions);
    break;

// goal/step/action/serializer/Reader.cs — born with the step; Child steps born with the goal (ctx.Goal rode down)
// the action's own name reads case "name" (was "action"); the branch body is case "child" below
// B1: lazy the step reader to break the step→action→step ctor cycle
private goal.step.serializer.Reader? _stepReader;
private goal.step.serializer.Reader StepReader => _stepReader ??= new();

var action = new Action { Step = ctx.Step! };          // ← born with the step
case "child":                                          // action's "child" = branch body → list<step>
    // ⚠ the `child` wire key is level-scoped: a GOAL's "child" is sub-goals (list<goal>); an
    //   ACTION's "child" (here) is its branch body (list<step>). Same key, different type — do NOT unify.
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
The tree serializes recursively: `goal.Output` (writes `step`) → `step.Output` (writes `action`) → `action.Output` (writes `name` + `child`) → `step.Output` … — all singular keys. The action's own name field writes `name` (was `action`), the rest (`parameter`/`default`/`modifier`) singularize with the sweep.

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
- **⚠ C2 — `step.Index` MUST be globally unique within the goal** (the key relies on it; coder C2). The compiler does **NOT** re-index children per level — a step keeps its parse-order flat index (`step[2](if){child:[step[3], step[4]]}`, never `[0,1]`), and the LLM numbers synthesized inline-body steps continuing the sequence. So the key is unique for every condition (same-step if/elseif/else differ by `IndexOf`, everything else by `Index`). Chosen over stamping a tree-path on the step — a coverage-only field on a runtime object is the `Hits` smell we killed. **Invariant the builder upholds: every step, parsed and synthesized, gets a unique `Index` in the goal.**
- **Declared** branches (for "which weren't covered") still come from `test.discover` walking the tree's condition actions — same derived key, no `Decision`, no seeded chain.
- **Dies:** the `branchIndex`/`branchLabel`/`branchChain` stamping (`if.cs`); the `Properties["branch*"]` reads (`run.cs:109-128`); `RecordBranch(site,int)`/`RecordBranchLabel`/`RecordBranchChain` + `_branches`/`_branchLabels`/`_branchChains` → collapse to the single `_covered` (+ declared). `Merge` narrows to unioning `_covered`.
- **Stays:** a keyed, mergeable store (not a tree-walk) — corrected from the earlier "no registry" claim.

## 9. The builder — LLM inline + a deterministic Fold for substeps; both land on `action.Child`

`%goal%` is the **source-parsed** goal (`build.goals` reads the `.goal` files → `%goals%` → `BuildGoal goal=%item%`). Parsed steps (text + indent, **no actions**); `BuildStep` attaches compiled actions; a deterministic **Fold** re-parents substeps; `goalsSave` writes the tree. **Both condition forms land on `action.Child`** — one holder, one fire (`action.list.Run`), substep and inline byte-identical in shape.

Why a Fold (settled with Ingi, reversing the earlier "no fold"): substep structure is known at **parse** (indent), but the gate **action** it must attach to doesn't exist until **compile** (the LLM fills it). So there is no fold-free way to land substeps on `action.Child` — the parser can't reach the action, and asking the LLM to swallow real neighbour steps is the smell. The Fold bridges that gap; crucially it **moves real steps, never synthesizes** (the `FoldChain synthesized steps = obpv` objection doesn't apply — those steps have their own `BuildStep`, `Text`, `LineNumber`). It runs at **build time**, produces the `.pr`; the runtime just reads the finished tree.

**Access — `goal.step` is a node (§1):** bare `%goal.step%` → `.current`, `%goal.step[i]%` → item i (indexed, what the builder uses), `%goal.step.list%` → the collection. C# `goal.Step[i]` ⇄ PLang `%goal.step[i]%`, 100% aligned. The builder is written **in PLang**, so the singular sweep renames its accessors too (`os/system/builder/**/*.goal` + templates):

| plural (today) | singular |
|---|---|
| `%goal.Steps[planStep.index]%` | `%goal.step[planStep.index]%` |
| `set %goal.Steps[step.Index].Actions% = %compileResult.actions%` | `set %goal.step[step.Index].action% = %compileResult.actions%` |
| `set %goal.Steps[step.Index].Formal% = …` | `set %goal.step[step.Index].Formal% = …` |
| `foreach %parentGoal.Goals%` | `foreach %parentGoal.child%` |
| `build.validate actions=%goal.Steps[step.Index].Actions%` | `… actions=%goal.step[step.Index].action%` |

`BuildStep/Start.goal` and `BuildGoal/Start.goal` are full of these — the sweep must cover the builder `.goal` files, not just the C# and `.pr` keys.

### The two producers — LLM (inline) + Fold (substeps)

**1. LLM Compile (`Compile.llm` + `BuildResponse`) — inline bodies as `child`.** Today inline `if %x%, call Y` compiles to two flat peer actions in one step (`[condition.if, goal.call]`, `Compile.llm:22,31`). The change: the LLM emits each branch *body* as the condition action's **`child`** — a step with **LLM-authored `text`** + its action(s) — leaving *setup* actions (`file.exists`, the compound `condition.compare`+`set`) as leading siblings (A4). `Synthetic=true` marks the inline-born step. This is the **only** thing the LLM nests, and the eval-risk surface (schema + prompt + goldens).

**2. Parser (flat) + deterministic Fold — substeps.** The parser keeps substeps **flat, carrying `Indent`** (real steps, own `BuildStep`). After compile, the Fold re-parents a step's deeper-indented followers into its **gate action's `Child`** (the `IsCondition` action; A4: `[file.exists, condition.if]` → the `condition.if`; indented under a non-condition → build error, never dropped).

```csharp
// goal.Fold — deterministic, post-compile, pre-save. Flat+indent → tree; MOVES real steps, no synthesis.
static app.goal.step.list.@this Fold(IReadOnlyList<Step> flat)
{
    var top = new List<Step>();
    for (int i = 0; i < flat.Count; )
    {
        var step = flat[i];
        int j = i + 1;
        while (j < flat.Count && flat[j].Indent > step.Indent) j++;   // gather the deeper block
        var block = flat.Skip(i + 1).Take(j - i - 1).ToList();        // its substeps (real steps)
        if (block.Count > 0)
        {
            var gate = step.Action.list.FirstOrDefault(a => a.IsCondition)
                ?? throw context.BuildError($"indented steps under non-condition '{step.Text}'");   // A4
            gate.Child = Fold(block);                                  // recurse — nesting composes
        }
        top.Add(step);                                                // real step keeps its identity
        i = j;
    }
    return new app.goal.step.list.@this(top);
}
```
Coder already built + tested this (`goal.Fold`, `FoldTests 3/3`, commit `5ea5ba9bc`) — reverted only because "no fold" was absolute; **un-revert it**. Home is coder's: `goal.Fold` method or a `build.fold` action — it's a build-time transform, not a runtime method (name it a single verb, not `FoldChain`/`FoldBlock`).

`BuildGoal/Start.goal` flow:
```
- foreach %plan.steps%, call BuildStep/Start     (compile per step — flat, %goal.step[i]% indexed; pre-fold)
- foreach %parentGoal.child%, call BuildSubGoal
- <Fold> goal.Fold / build.fold Goal=%goal%      ← re-parent substeps into gate-action Child (post-compile, pre-save)
- build.goalsSave Goal=%goal%                     (writes the tree via item Output)
```

### Step identity — `Index` is an ID, `LineNumber` is the source key, `AllSteps` for lookup

The tree nests steps and synthetic inline bodies consume indices, so a step is no longer at `goal.step[Index]`. Three rules keep this sane:

- **`Index` = stable global ID** — unique across the goal incl. synthetic bodies (pre-order). Coverage keys on it (C2). **NOT the source position.**
- **`LineNumber` = the source line** — real steps only. **All source-facing tooling** (IDE cursor, `--debug={"step":…}`, "error at line X") keys on `LineNumber`, never `Index`. Synthetic inline bodies share the `if`'s `LineNumber` (line fragments, not independently addressable — correct: cursor on that line lands on the `if` step).
- **`goal.AllSteps()` = pre-order flat projection** (computed off the tree, not stored) — recovers the flat list the IDE/debug want:
  ```csharp
  IEnumerable<Step> AllSteps()             // pre-order == Index order
  {
      foreach (var step in Step.list) {
          yield return step;
          foreach (var a in step.Action.list)
              foreach (var s in a.Child.AllSteps()) yield return s;   // one child source (Fold) → single recursion
      }
  }
  // IDE cursor:  AllSteps().First(s => s.LineNumber == cursorLine)
  // debug/ref:   AllSteps().First(s => s.Index == n)
  ```
  The tree is the execution structure; `AllSteps` is the flat lookup view. `%goal.step[i]%` (top-level position) is a different thing from `Index` — the builder's `%goal.step[planStep.index]%` still works because compile runs **pre-Fold** (flat).

### Index uniqueness (coverage C2)

`step.Index` must be **globally unique within the goal** (the coverage key relies on it, §8). Parser numbers parsed steps in flat parse order (unique, no re-index per level); the LLM/compiler numbers synthesized inline-body steps continuing the sequence.

### Other builder touches

- **`stepForLlm.template` `HasSubSteps` hint** (its one consumer, `:4` "runtime handles branching via indent") — obsolete under the tree; the template now teaches the child-emission shape instead.
- `build.validate`/`goalsSave` follow the tree (`Output` recurses `child`).
- **A3 display indent:** `goal.ToText`/`ToString` (`goal/this.cs:68,211`) render from `step.Indent` — with the field gone, derive indent from tree depth at render.

## Demolition (net)

**Delete:** `steps.@this` (whole); `Decision` (whole); `condition.if.Orchestrate` + simple-form block; `step.Indent` + wire `indent`; `GoalSteps` alias; branch stamping in `if.cs`; `Properties["branch*"]` reads in `run.cs`; `Coverage`'s `_branches`/`_branchLabels`/`_branchChains` + `RecordBranch*`; `discover.SeedBranchChains`'s `Decision` use; `skipBelowIndent`/`HasIndentedChildren`.
**Change:** `goal.Steps→Step` (`step.list`); `step.Actions→Action` (`action.list`); `action.Modifiers→Modifier` (`modifier.list`, §2b — naked `List` gone); the modifier `AfterAction` loop leaves `action.RunAsync` → each `modifier.Wrap` fires its own (§2b); `action` gains `Child`; `RunAsync→Run`; backref wire (§5); coverage observer (§8); `goal/this.cs` `Steps[]→Step[]`; `step.HasSubSteps` → `Action.Any(a => a.Child.Count > 0)`.
**Stays:** `goal.Run`/`step.Run` lifecycle; `condition.if`/`elseif`/`else` evaluation; `IsCondition`; `Coverage.RecordModuleAction`/`Merge` (narrowed); the item `Output`/`Reader` (extended); `goal.Child` (sub-goals).
**Builder adds (§9):** `goal.Fold` **un-reverted** (deterministic post-compile substep re-parent, moves real steps); LLM inline-`child` emission (`Compile.llm`/`BuildResponse`); `goal.AllSteps()` pre-order flat view (`Index`=stable ID, `LineNumber`=source key for IDE/debug).

## Flaw readover — the six caught writing this up

1. **backref stamping** gone from the standalone lists → §5 born-with via `ReadContext` (NO `Wire`, NO `??=` — Ingi: any after-stamping is the late-stamp smell).
2. **cancellation** dropped → restored in §1/§2 (return at step level, throw at action level).
3. **`|| Handled`** dropped from `action.list.Run` → restored (legit event-handled stop).
4. **bare-if empty `Child`** returns `Ok` not the bool → verify (§3).
5. **`Steps[0]`/`Steps.Count`** in `goal/this.cs` → `Step[0]`/`Step.Count`.
6. **Coverage `Merge`** breaks "no registry" → keyed store stays, `Cover` derives the key (§8).
