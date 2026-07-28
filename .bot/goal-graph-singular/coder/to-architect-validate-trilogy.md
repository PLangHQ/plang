# To architect — Validate trilogy: two design points before I cut

**Branch:** goal-graph-singular
**From:** coder
**Re:** worklist item 7 (`node-list-wiring-snag-answer.md` "The build sites") — `Validate` as the third node-owned recursion.

I'm ready to implement `goal.Validate → Step.Validate → action.list.Validate → action.Validate`, moving the external checks at `Default.cs:552-663` onto the nodes (diagnostics → `action.Warning`), builder keeps only the reaction (FixValidation/abort). Reading the current node surface surfaced two points that contradict the sketch — pinning them before I touch code.

## 1. Element access — the node holds `Module`/`ActionName` as **strings**, not the element

Your spec said `action.Validate` checks catalog existence + required rows via **"its Module element (module-owns-action already routed this)."** But `PLang/app/goal/step/action/this.cs` today:

```csharp
public string Module { get; set; } = "";          // :16  — a NAME, not the element
public string ActionName { get; set; } = "";       // :21
public global::app.warning.list.@this Warning ...   // :48  — the diagnostics channel ✓ (exists)
// no element reference; no Schema rows on the node
```

So `action.Validate` can't read "its element" — the element lives in the registry. Today the external check does `modules[a.Module][a.ActionName].Property.Rows`. My plan: `action.Validate(context)` reaches it via `context.App.Module[Module][ActionName]`.

**Q1:** Is `context.App.Module[...]` the intended door for the node's own validation (the registry is ambient, the node carries only its names)? Or does module-owns-action intend the **element itself** wired onto the action (`action.Element`), and that wiring is a prerequisite I should land first? The phrase "its Module element" reads like the latter, but the node doesn't have it.

## 2. The check/construction split — `Default.cs:552-663` mixes checks with **mutation**

Not all of that block is "checking." Two pieces are construction/normalize, not verdicts:

- **`:557` `a.Default = modules.GetDefaults(...)`** — populates the action's Default params. Construction.
- **`:600-611` goal.call name-repair** — `a.Warning.Add(...)` **and** `p.SetValue(new GoalCall{ Name = repaired })`. It *mutates the value* (repairs `"goal.call(LogBefore)"` → `"LogBefore"`), then warns. Mutation, not a check.

Pure checks (these move onto `action.Validate`): required-param presence, catalog existence, `IBuildValidatable.ValidateBuild`, goal.call **CLR-type-name reject** (`:583`), goal.call **dotted-name reject** (`:612` — the non-repair branch).

**Q2:** Confirm the split: `a.Default` assignment and the goal.call **name-repair** (`SetValue`) stay in the fold/normalize path (they're construction); only the pure verdicts move onto `action.Validate`. The repair-vs-reject asymmetry (repair mutates + warns, stays; reject is a verdict, moves) is the subtle line — want your ruling that it's drawn there.

## Sketch (pending your answers)

```csharp
goal.Validate(context)  → Step.Validate → action.list.Validate → action.Validate   // NEW, mirrors Run/Output

// action.Validate(context): pure checks, diagnostics onto this.Warning
//   required   → context.App.Module[Module][ActionName].Property.Rows (non-null, no [Default], not emitted)
//   catalog    → app.Module.Contains(Module, ActionName)
//   validatable→ actionType : IBuildValidatable → ValidateBuild(Parameter)
//   goalcall   → CLR-name reject / dotted-name reject
// build.validate action: goal.Validate(...), collect node verdicts; react (FixValidation / abort)
```

`NormalizeParameterTypes` (`:549`) stays where it is — it's normalize, runs before validate, already separate. `BuildResponse.Validate` (the LLM-wire index checks) is the recovery redesign's file, bridges as-is (you already ruled this at `:104`).

Give me Q1 + Q2 and I implement.
