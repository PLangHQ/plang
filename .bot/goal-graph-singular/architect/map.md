# goal-graph-singular — the full touch map, with code (pre-plan; plan.md follows Ingi's review)

The sweep: the goal graph goes singular everywhere — classes at the collection-node convention (`goal.step.list`), properties (`Steps`→`Step`, `Actions`→`Action`, `Modifiers`→`Modifier`), the `.pr` wire (`steps:[…]`→`step:[…]`), authored goals, templates, LLM schemas. Branched from `get-builder-running` @ `eeb9029b5`; merges back when green. All counts/`file:line` measured on this branch.

## The finding that sizes the whole thing

**WireName derives from the property name** (camelCase, `channel/serializer/filter/Tagged.cs:31`) — the serializer AND the reflection-kind reader both key off it. So the property rename IS the wire rename: **zero serializer/deserializer code changes.** The cost moves entirely to (a) the C# rename ripple and (b) the 806 tracked `.pr` files whose stored keys are the OLD names — the migration + bootstrap story below.

## Measured blast radius

| Area | Size |
|---|---|
| C# files referencing `app.goal.steps.*` namespaces | 99 |
| `.Steps` / `.Actions` / `.Modifiers` member accesses | 197 / 144 / 37 |
| GlobalUsings aliases | 6 (`GoalSteps`, `Step`, `ErrorOrder`, `CacheSettings`, `StepActions`, `ActionModifiers`) |
| Hardcoded wire literals in C# | 5 (3 real — dispositions below) |
| os/ authored files (goals/templates/llm) | 10 |
| Tracked `.pr` files carrying plural keys | **806** |
| Test snapshot JSONs with plural keys | 0 |

## A. The rewritten classes (real code — these are the non-mechanical core)

### A1. `app/goal/step/list/this.cs` (was `goal/steps/this.cs` — every member listed, none silently dropped)

```csharp
namespace app.goal.step.list;

/// <summary>The goal's step collection — a plang value (kills the item.Create⇄type.Create
/// bounce at rung 1) with the IList<Step> facade for C# consumers. Storage = the BASE's rows
/// (raw Step hosts, store-raw-type-on-read → elements lift to clr(step) on access — the plan's
/// list<clr<step>> shape). No second List<Step> beside the rows (stored-twice).</summary>
public sealed class @this : global::app.type.item.list.@this, IList<Step>, IContext
{
    public @this(actor.context.@this context) : base(context) { }            // born WITH context
    public @this(IEnumerable<Step> steps, actor.context.@this context) : base(context) { foreach (var s in steps) Add(s); }

    [System.Text.Json.Serialization.JsonIgnore]
    public global::app.goal.@this Goal { get; set; } = null!;               // backref, stamped by goal

    // --- the IList<Step> facade over base rows; the Goal-stamp behavior preserved verbatim ---
    public Step this[int index]
    {
        get { var s = (Step)Rows(index); s.Goal ??= Goal; return s; }        // Rows(i) = your accessor over the base storage
        set => …;
    }
    public int Count => …/* base count */;
    public bool IsReadOnly => false;
    public void Add(Step item) => …;        public void AddRange(IEnumerable<Step> items) => …;
    public void Clear() => …;               public bool Contains(Step item) => …;
    public void CopyTo(Step[] a, int i) => …; public int IndexOf(Step item) => …;
    public void Insert(int i, Step item) => …; public bool Remove(Step item) => …;
    public void RemoveAt(int i) => …;
    public IEnumerator<Step> GetEnumerator() { foreach (…) { s.Goal ??= Goal; yield return s; } }

    internal bool HasIndentedChildren(int index) { …identifier-only… }

    /// <summary>Per-step modifier nesting — body unchanged, one identifier: step.Actions → step.Action.</summary>
    public void Nest(global::app.module.list.@this modules)
    { foreach (var step in this) step.Action.Nest(modules); }

    public void MergeFrom(@this prior) { …body unchanged, indexer now the facade… }

    /// <summary>THE runtime inner loop — body unchanged (skipBelowIndent walk, ShouldExit,
    /// Goal-stamp). PERF VERIFY: iteration now reads through base rows — measure; if it shows,
    /// keep a private typed cache invalidated on mutation (the collection owns its cache).</summary>
    public async Task<data.@this> RunAsync(actor.context.@this context) { … }
}
```

**Dies:** the private `List<Step> _items` (storage moves to the base), the parameterless ctor + late-set `Context { get; set; }` (born-with-context; the base carries it — the late-stamp smell dies with the rewrite). **Verify:** every `new steps.@this()` construction site (GoalMapper/tests/reader) gets a context to hand.

### A2. `app/goal/step/action/list/this.cs` (was `actions/this.cs`, 175 lines)

Same reshape as A1 (base rows + `IList<action>` facade + born-with-context). Domain members ride with identifier-only bodies: `Nest(modules)` (the catalog join + modifier re-mint, from the modifier ruling), `GroupModifiersRecursive`'s successor, the `Step` backref + `DroppedLeadingModifier` warning path. **Gains the ICreate face** (layer 5 made honest): `set %goal.Step[i].Action% = %compileResult.…%` is a *list → action.list* creation — `Create(raw, data)` wraps/retags a native list's rows into this collection.

### A3. `app/goal/step/action/modifier/list/this.cs` (was `action/modifiers/this.cs`, 69 lines)

Same reshape. `Sort()` (by `Order`) and `RunAsync` (the right-to-left wrap fold + AfterAction events) ride with bodies unchanged over the facade. The `PrModifier` local alias dies (the namespace collapse makes it `modifier.@this`).

### A4. Element classes — identifier-level only

- `goal.@this` (`goal/this.cs`): property `:45` becomes `public step.list.@this Step { get; }` (wire "step" derives); iterations at `:65, :129-133, :209`, `MergeFrom :231`, `RunAsync :282` (`Step.RunAsync`), the anchor `:270-276` — identifier renames, bodies untouched.
- `step.@this`: `Actions` → `public action.list.@this Action { get; }` (wire "action"); `Goal` backref unchanged.
- `action.@this`: `Modifiers` → `public modifier.list.@this Modifier { get; }` (wire "modifier"); `this.Schema.cs` references follow; `Modifiers.RunAsync` call site (`action/this.cs:247`) → `Modifier.RunAsync`.

## B. Namespaces + aliases (mechanical, compiler-guided)

`app.goal.steps.step.actions.action.*` → `app.goal.step.action.*` across 99 files — the `app.module.action` move playbook. The 6 GlobalUsings aliases update in place (`GoalSteps` → dies or becomes `StepList`; `StepActions` → `ActionList`; `ActionModifiers` → `ModifierList`; `Step`/`ErrorOrder`/`CacheSettings` re-point). 197+144+37 member-access renames ride the compiler.

## C. The 5 wire/name literals in C# (each with disposition)

| Site | Today | Disposition |
|---|---|---|
| `build/code/Default.cs:334,340` | `node?["steps"]` raw JsonNode reads of `.pr` content (the build-cache/merge path) | → `["step"]` — the ONE real deserializer-adjacent touch |
| `type/spec/render/this.cs:145` | `record["modifiers"] =` (example rendering) | → `"modifier"` |
| `build/code/Default.cs:720` | `new data.@this("actions", …)` param name feeding `build.actions` | rides D3 below |
| `build/actions.cs:6` | `[Action("actions")]` — the catalog getter action's NAME | decision D3 |

## D. os/ authored surface (10 files) + the LLM contract

- `%goal.Steps[…]%` → `%goal.Step[…]%`, `set %goal.Steps[i].Actions%` → `…Step[i].Action%`: `BuildStep/Start.goal`, `BuildGoal/Validate.goal`, `LlmFixer.goal`, `InstallUrl.goal`, `ReportBuilder/BuildGui.goal`.
- Templates reading `a.Parameters`/`step`/`goal.steps` faces: `stepForLlm.template`, `stepActionDetails.template`, `goalFormat.template`, `CompileUser.llm`.
- **D1 (decision, my lean = rename):** the LLM RESPONSE schemas — `Plan.goal:26 Schema={… steps: list<…>}`, `QueryAndVerify:46` + `FixValidation:63` `actions: list<{module…}>` → `step:`/`action:`. Renaming keeps ONE vocabulary end-to-end but busts the LLM cache and needs the compile-quality gate (goldens + one real build). Keeping them splits the vocabulary at the LLM seam. Ingi calls it.
- **D2 (decision, my lean = graph only):** handler PARAM names that happen to be plural (`validateStepActions.Actions`, `error.handle.Actions`, `build.actions`' param) are NOT the goal graph — plural list-params exist everywhere (`Test.Include`). Boundary: the sweep renames the GRAPH (classes/properties/wire/goal references), not handler param vocabulary. Ingi confirms.
- **D3 (decision):** the `build.actions` ACTION name — it's dying anyway per module-discovery 6c (dissolves into navigation); rename here is wasted motion. Lean: leave, let 6c delete it.

## E. The 806 `.pr` files — migration + bootstrap

The wire changes, so every tracked `.pr` (Tests/**/.build, os/**/.build incl. `os/system/builder/.build/build.pr`, `.build/app.pr`) is stale. **The bootstrap trap:** the new reader cannot read the old `build.pr` to run the builder that would regenerate it. Sequence:

1. **A one-shot key-rename script** (deterministic, reviewable — json key renames only: `"steps":` → `"step":`, `"actions":` → `"action":`, `"modifiers":` → `"modifier":`, applied at the graph nesting levels only — NOT to action parameter names (D2) or arbitrary string values). Runs over all 806; committed as its own commit so the diff is pure key-renames.
2. New binary builds green; `plang build` on Sanity + the os builder regenerates; diff confirms only expected changes.
3. The script is deleted in the same branch (one-shot, not kept machinery).

Rejected: a both-keys compat reader (violates no-backward-compat and leaves a fork in the reader).

## F. Recognition side (rides from the layer-4 ruling, unchanged)

`ContainerFamily` drops its `IList<>` interface probe (the classes it existed to claim are items now); the exact-generic residue aligns claim=build for foreign shapes; `GetTypeName`'s broad naming untouched. The apex gains zero rungs.

## G. Verifies + pins

- Naming index: nothing keyed on literal "steps"/"actions"/"modifiers" type names (grep `Is("steps")`-style + `_typeToName` consumers).
- The `.pr` READ path end-to-end: reflection kind constructs the new collections (ctor with context; Goal backref stamping survives) — one round-trip test per level (goal/step/action/modifier).
- The layer-4 repro (Fluid `{% for %}`) green; the layer-3 fix unaffected; `Nest` suite green.
- Hot-path perf: step iteration through base rows measured before/after.
- Full suite by-name diff vs this branch's baseline; `plang build` Sanity end-to-end.
- Grep gates at close: `goal.steps`, `\.Steps\b`, `\.Actions\b`, `\.Modifiers\b`, `"steps"` in production + os → zero.

## Open decisions for Ingi (blocking plan.md)

1. **D1** — LLM schema keys: rename (one vocabulary, cache bust + quality gate) or keep (seam split)?
2. **D2** — confirm the boundary: graph only; plural handler params stay.
3. **D3** — `build.actions` action name: leave for 6c's deletion?
