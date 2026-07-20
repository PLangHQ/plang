# Phase B — the graph tree: types, storage, run chain, readers (for coder review)

**Status: settled with Ingi through design (2026-07-20); coder to comment before building.** This is the code-level design that resolves the two open questions in `phaseB-tree.md` (run-drive owner + collection storage). Where they disagree, this file wins. `phaseB-tree.md`'s Why, `.pr` shape, demolition-by-gate, and builder split still hold.

> **You own this.** The type shapes, bodies, and reader cases below are the settled *design* — the structure (two typed list classes owning `Run`, the delegation chain, `child` on the control-flow action, `Decision` retires) is the ruling. Method bodies, exact navigation wrap, naming inside, and the builder prompt are yours. Comment on anything that doesn't hold when you trace it — especially the `Handled` mechanism (§6) and the navigation wrap (§4).

## 1. Why

Phase B blocked on "where does the step loop live." The answer: the flat-list-plus-`indent` is a flattened tree. Make it a real tree and the skip-state (`skipBelowIndent`) vanishes — a control-flow action owns its body as `Child`; running it either enters `Child` or falls through to the next action. `Decision` retires, the "2 ways to do conditions" collapse to one, and the collection `steps.@this` becomes a proper `list<step>`. The tree *is* Phase B done right.

## 2. The tree — who holds what

```
goal
 └─ Step : step.list                 the goal's steps            (list<step>)
     └─ each step
         └─ Action : action.list      the step's actions          (list<action>)
             └─ each action
                 └─ Child : step.list  ONLY control-flow actions;  (list<step>)
                     └─ each step …      the branch / sub-step body — recurses
```

- a **goal** holds a `step.list` (`Step`),
- **each step** holds an `action.list` (`Action`),
- **each control-flow action** holds a `step.list` (`Child`; empty on ordinary actions).

`child` lives on the **action**, not the step. Both nesting forms land there: inline `if/elseif/else` (the step's `Action` is a chain of condition actions, each with its `Child`) and indented sub-step blocks (the `condition.if` action's `Child` holds the block). `list<step>` appears twice (`goal.Step`, `action.Child`) — one type, `step.list`. `list<action>` once — `action.list`. `goal.Child` (sub-goals, `list<goal>`) and modifier lists stay **plain** lists — they are never sequence-run.

## 3. The two list types — standalone, typed storage, own their `Run`

Not subclasses of `item.list` (that stores `List<object?>` and yields `Data` — a `Data` unwrap on the hot run path). Standalone classes holding the typed list, looping it directly. Each owns its sequence-run (Rule 5 — the collection owns its iteration), uniformly; the only difference is the break condition.

```csharp
// goal/step/list/this.cs — the steps collection
public sealed class @this
{
    private readonly List<step.@this> _steps;
    public @this(List<step.@this> steps) => _steps = steps;

    public int Count => _steps.Count;
    public step.@this this[int i] => _steps[i];
    public IReadOnlyList<step.@this> Steps => _steps;      // for the navigation boundary (§4)

    public async Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var step in _steps)
        {
            result = await step.Run(context);
            if (result.ShouldExit() || result.Returned) break;   // a return exits the goal
        }
        return result;
    }
}
```

```csharp
// goal/step/action/list/this.cs — the actions collection (the twin)
public sealed class @this
{
    private readonly List<action.@this> _actions;
    public @this(List<action.@this> actions) => _actions = actions;

    public int Count => _actions.Count;
    public action.@this this[int i] => _actions[i];
    public IReadOnlyList<action.@this> Actions => _actions;

    public async Task<data.@this> Run(actor.context.@this context)
    {
        data.@this result = context.Ok();
        foreach (var action in _actions)
        {
            result = await action.Run(context);
            if (result.ShouldExit() || result.Handled) break;   // a fired condition took its branch
        }
        return result;
    }
}
```

Two subclasses, one per collection that is *run in sequence*; the break differs (`Returned` vs `Handled`) because the levels stop for different reasons — that difference is the reason they're two types, not one. `goal.Child` / modifier lists aren't run, so no third/fourth type.

## 4. Navigation — the graph is PLang-navigable

`%goal.step%`, `%step.action%`, `%goal.step[2].action%`, `list.where` over the graph must work. Typed storage is the source of truth; the plang-list view is a **boundary alias**, never a second storage. `step.list`/`action.list` expose their typed items (`Steps`/`Actions` above); at the `%…%` boundary those alias into a `list<step>`/`list<action>` value (`item.list`'s foreign-list-alias ctor is O(1), no copy). **Coder owns the exact wrap** — alias-on-access vs. a thin navigable surface on the list class — under the constraint: one storage (the typed `List<>`), the plang list is a view.

## 5. The run chain — each node runs its list, each list runs its elements

```
goal.Run(ctx)   →  lifecycle (events, call frame, cycle guard, return-depth) + await Step.Run(ctx)
step.Run(ctx)   →  lifecycle (before/after step, exception→ServiceError)     + await Action.Run(ctx)
action.Run(ctx) →  lifecycle + dispatch + modifier-wrap + (if fired) await Child.Run(ctx)   // §6
```

`goal.Run` (`goal/this.cs:247`) keeps its whole body — the one changed line is `await Steps.RunAsync(context)` → `await Step.Run(context)`. `step.Run` (was `RunAsync`, `step/this.cs:148`) keeps its lifecycle + try/catch; its body loop `foreach (action in Actions)` becomes `await Action.Run(context)` (the loop moves onto `action.list`). `RunAsync`→`Run` everywhere (drop `Async`; everything is async).

## 6. Fire-or-fall-through — the one piece to scrutinize

A control-flow action that fires runs its **own** `Child`; the branch is taken, so the step's action chain stops. Today `condition.if.Run` reaches into `Step.Actions` and orchestrates its *siblings* (an action bossing its peers — a smell). The tree kills that: the action runs only itself, and the chain is coordinated by the `Handled` signal the loop already reads.

**NEW — the tail of `action.Run`** (`this.cs`, before `return data`):

```csharp
// A control-flow action that fired runs its OWN body; the branch is taken, so the step's
// action chain stops here (a following elseif/else is never reached). Uniform — no type-switch:
// an ordinary action has an empty Child so never enters; a condition that evaluated false isn't
// truthy so it falls through to the next action.
if (data.Success && Child.Count > 0 && await data.ToBooleanAsync())
{
    data = await Child.Run(context);   // step.list.Run over the body
    data.Handled = true;               // "branch taken — stop the step's action chain"
}
return data;
```

**`condition.if.Run` collapses** to what `elseif` already is (evaluate, `Negate`, return the bool) — the whole `Orchestrate` block (`if.cs:37-136`) and the simple-form branch-Properties block (`:57-64`) delete. `condition.if` no longer knows `Step`, siblings, or branches.

Walk `[if, elseif, else]` in `action.list.Run`: `if` fires → runs `Child`, `Handled=true` → loop breaks (elseif/else skipped). `if` false → `Handled=false` → loop continues to `elseif`. Fire-or-fall-through, on the mechanism that already exists.

> **Ingi flagged `Handled` ("not sure you need this").** It is load-bearing here: it's the signal a fired condition sends so `action.list.Run` stops the chain (skips `elseif`/`else`). The alternative — `action.list.Run` itself checking "did the last action fire?" — puts `IsCondition`+truthy logic (a fork) into the loop. Keeping the fired action signal it via a result flag keeps the loop fork-free. Coder: confirm `Handled` (or a same-shaped result flag) is the clean carrier, or propose better.

## 7. The three readers

```csharp
// GOAL reader — the goal's top-level steps → goal.Step
case "step":
    var steps = new List<step.@this>();
    reader.BeginArray();
    while (reader.NextElement()) steps.Add((step.@this) _step.Read(ref reader, kind, ctx));
    reader.EndArray();
    goalStep = new goal.step.list.@this(steps);
    break;

// STEP reader — this step's actions → step.Action
case "action":
    var actions = new List<action.@this>();
    reader.BeginArray();
    while (reader.NextElement()) actions.Add((action.@this) _action.Read(ref reader, kind, ctx));
    reader.EndArray();
    stepAction = new goal.step.action.list.@this(actions);
    break;

// ACTION reader — a control-flow action's body → action.Child (a step.list again)
case "child":
    var child = new List<step.@this>();
    reader.BeginArray();
    while (reader.NextElement()) child.Add((step.@this) _step.Read(ref reader, kind, ctx));
    reader.EndArray();
    actionChild = new goal.step.list.@this(child);
    break;
```

Each reader walks the handed `ref reader` (the read-shape ruling — never `new` a `json.Reader`), builds the typed `List<>`, hands it to the list ctor. The tree recurses because `case "child"` re-enters the step reader.

## 8. Serialization (`Output`) — recurse `Child`, drop `indent`

`goal.Output` writes its `step` array (iterates `Step`, calls each `step.Output`) — unchanged in shape. `step.Output` drops the `indent` field and keeps writing its `action` array. `action.Output` gains: after its params/modifiers, if `Child.Count > 0` write a `child` array (iterate `Child`, call each `step.Output`). Every step carries `text` (LLM-authored for synthesized body-steps — §12). Byte-identity is not the golden (the shape genuinely changes); the golden is a semantic round-trip (write → read → write stable).

## 9. Demolition worklist

**Delete whole:**
- `goal/steps/this.cs` (`steps.@this`) — `RunAsync`/`skipBelowIndent`, `HasIndentedChildren`, `Nest`-loop, `Merge`-flat. Replaced by `step.list` + `action.list` + the tree.
- `condition/decision/this.cs` (`Decision`) — `Of`/`IsHead`/`Head`/`Split`/`Chain`, the `Branch` record. Nothing groups the action array anymore — the tree is the grouping.
- `condition/if.cs` `Orchestrate` (`:71-136`) + the simple-form branch-Properties block (`:57-64`).
- `step.Indent` (`step/this.cs:47`) + wire `indent` key + the `indent = leadingSpaces/4` persist (`goal/this.cs:447`) — indent becomes transient parse state consumed by the fold (§12).
- `GoalSteps` alias (`GlobalUsings.cs`).

**Change:**
- `goal.Steps : steps.@this` → `goal.Step : step.list`. `step.Actions : List<action>` → `step.Action : action.list`. `action` gains `Child : step.list`.
- `step.RunAsync`/`goal.RunAsync`/`action.RunAsync` → `Run` (drop `Async`).
- `action.IsFirst`/`IsIfHead` (`this.cs:94,104`, referenced `Decision.IsHead`) → gone. `IsCondition` (`:83`) stays (used nowhere structural now — verify; the fire gate is `Child.Count>0 && truthy`, not `IsCondition`).
- `step.HasSubSteps` (`:138`, `Goal.Steps.HasIndentedChildren`) → `Action.Any(a => a.Child.Count > 0)` (builder-facing; verify its consumers).
- `test/discover.cs:312` `Decision.Of(step.Actions).Chain` → walk the step's condition actions for the chain; `goal.ForEachAction` (`:310`) must **recurse `action.Child`**.
- `step.Merge`/`goal.Merge` → structural/recursive (match parent by `text`, children by position — child `text` is display/intent, LLM can reword it, so it's not the match key).
- Coverage `branchIndex`/`branchChain` move from `Orchestrate`/simple-form onto the **fired action** (its position in the step's condition chain + the chain labels); Properties-on-`Data` mechanism unchanged, `test/Coverage.cs` `RecordBranch`/`RecordBranchChain` surface unchanged.

**Stays (don't over-delete):** `goal.Run` lifecycle (events, `CallStack.Push` goal frame, cycle guard, return-depth); `step.Run` lifecycle (before/after, exception→`ServiceError`); `condition.if`/`elseif` evaluation (`Evaluator`, `ToBooleanAsync`, `Negate`); `else` (returns true); `test/Coverage.cs`; the goal/step/action `Output`+`Reader` (extended, not replaced); `goal.Child` (sub-goals).

## 10. The builder — two producers (coder owns; the new surface)

- **Indented blocks → deterministic, post-compile.** `goal/this.cs:446` parses `indent`; after compile (actions known), a deterministic pass folds a deeper-indented step into the preceding step's control-flow action `Child`. No LLM. `indent` stops being persisted.
- **Inline `if/elseif/else` → the LLM.** The compile that today emits the flat `[if, body, elseif, body, else, body]` action array instead emits condition actions each carrying `Child: [body step(s)]` with **LLM-authored `text`** per branch (the line is freeform — only the model parsing it knows each branch's boundary; a deterministic splitter can't). Schema + prompt + examples + goldens are yours; this is the eval risk.

## 11. OBP validation

| surface | shape | verdict |
|---|---|---|
| `step.list` / `action.list` | standalone, typed `List<>`, own `Run` | clean — collection owns iteration (Rule 5), uniformly; no `Data` on the hot path |
| `action.Child` | `step.list` on control-flow actions | clean noun (`Body` alt considered; `Child` chosen for parity with `goal.Child` + the tree framing) |
| fire block | `Child.Count>0 && truthy`, no `IsCondition` | no behavioral fork ✓ |
| `condition.if.Run` | evaluate only, no `Step`/sibling reach | removed a cross-object reach ✓ |
| `goal.Run`/`step.Run`/`action.Run` | one honest verb, `Async` dropped | clean |
| coverage on fired action | the action stamps its own chain position | no external orchestrator ✓ |
| navigation wrap (§4) | typed storage is truth, plang list is a view | clean *iff* one storage — coder must not introduce a second |

## 12. App-model plang-types audit

- `goal.Step` : `list<step>` · `step.Action` : `list<action>` · `action.Child` : `list<step>` — all plang lists (typed C# backing, plang-list view at the boundary). ✓
- `branchIndex` : `number` (stays). `indent` : was `int` — **removed**, not converted.
- every step has `text` : `text`; body-step `text` LLM-authored.

## 13. Open for coder to comment

1. **`Handled` as the fire signal** (§6) — confirm it's the clean carrier, or propose a same-shaped result flag.
2. **Navigation wrap** (§4) — alias-on-access vs. a navigable surface on the list class; keep one storage.
3. **`IsCondition`** — with the fire gated on `Child.Count>0 && truthy`, is `IsCondition` still needed anywhere structural, or does it retire too?
4. **The deterministic fold** (§10) — where it lives in the build pipeline (post-compile pass); name it without a `TreeBuilder`/verb+noun.

## 14. Acceptance

1. Tree `.pr` semantic round-trip (write → `Reader` → write stable) for the inline `if/elseif/else` form and the indented-block form.
2. Runtime behavior preserved: `if/elseif/else` fires the right branch; `branchIndex` + coverage output unchanged vs the flat baseline; indented block still gated by its condition.
3. Builder goldens: one inline `if/elseif/else` + one indented block; LLM emits the tree with per-branch `text`; the deterministic fold produces the block tree.
4. Grep-gates zero: `condition.decision` / `SplitAtConditions` / `ComputeBranchChain`; `indent` / `skipBelowIndent` / `HasIndentedChildren`; `steps.@this`.
5. `%goal.step%` / `%step.action%` navigable from PLang (Fluid + `list.where` + index).
6. Bootstrap: the ~11 hand-edited builder `.pr` files re-nested structurally by hand (Ingi-permitted, this branch); everything else regenerates from source.
