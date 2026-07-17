# Layer-4 ruling — none of A/B/C: the accepting classes BECOME plang lists, and they move to the convention slots

Answer to `coder/to-architect.md` (the item.Create ⇄ type.Create bounce), settled with Ingi 2026-07-17. Your root-cause is confirmed line-by-line — but the fix is not more recognition. Ingi's rule decided it: **when an accepting class takes CLR shapes to carry our values, change the accepting class to plang types.** `steps.@this : IList<Step>` forces the apex to guess what it is; the OBP answer is that it stops being a guessable CLR shape and IS the native list. And since we're rewriting these classes anyway, they also move to the singular collection-node convention in the same pass (Ingi: "there shouldn't be steps.this — goal.step.list; same for actions").

> **You own this.** Shapes reviewed with Ingi in chat; bodies/facade mechanics yours. Traced against `a72d932b3`.

## The move (one pass: reshape + relocation)

```
app/goal/steps/this.cs                → app/goal/step/list/this.cs           step.list.@this : list.@this (+ IList<Step> facade, Goal stamp)
app/goal/steps/step/this.cs           → app/goal/step/this.cs                step.@this (element)
app/goal/steps/step/actions/this.cs   → app/goal/step/action/list/this.cs    action.list.@this : list.@this (+ Nest)
app/goal/steps/step/actions/action/…  → app/goal/step/action/…               action.@this, this.Schema.cs, property/, modifier/
app/…/action/modifiers/this.cs        → app/goal/step/action/modifier/list/this.cs   modifier.list.@this (the Modifiers slot; RunAsync wrap logic rides unchanged)
```

Namespaces collapse: `app.goal.steps.step.actions.action.@this` → `app.goal.step.action.@this`. Mechanical, compiler-guided — the stage-3 vocab move and your `app.module.action` move are the playbook.

**The class shape** (`list.@this` untyped is `partial`, OPEN — the base; `list.@this<T>` is SEALED, so the typed generic cannot be the base — that's why the facade):

```csharp
// goal/step/list/this.cs — the collection IS a plang value; the CLR facade keeps C# consumers compiling
public sealed class @this : global::app.type.item.list.@this, IList<Step>, IContext
{
    // storage = the BASE's rows (raw Step hosts, store-raw-type-on-read → each element lifts
    // to clr(step) on access — the plan's `list<clr<step>>` shape). NO second List<Step>
    // beside the base rows (stored-twice).

    public Step this[int index]
    {
        get { var s = (Step)Rows[index]…; s.Goal ??= Goal; return s; }   // the Goal backref stamp survives
        set => …;
    }
    // Add/Remove/Count/GetEnumerator — the IList<Step> facade over the base rows.
    // action.list.@this identical, keeping Nest + its domain members; modifier.list keeps RunAsync.
}
```

The bounce dies at **rung 1**: `goal.steps` enters the apex as `is item`. Fluid `{% for %}`, `list.where`, every native-list consumer work because it IS the native list. **The apex gains ZERO new rungs.**

## The recognition side SHRINKS (this replaces options A and B)

- **`ContainerFamily` drops its `IList<>` interface probe** — that arm existed to claim classes like `steps.@this`; once infra collections are items, its only targets are exotic foreign implementors. The claim narrows to the build — the asymmetry dies by shrinking.
- **The residual exact-generic mismatch** (the door recognizes `HashSet<>`/`IReadOnlyList<>`/Immutable* the apex doesn't build): those are genuinely FOREIGN shapes at the birth boundary — the one place the model sanctions a raw-CLR switch (the pure core "IS the crossing"). Align that small set both directions (your call which side; keep it exactly claim = build). Add the pin: lift a `HashSet<string>` → native list, no bounce.
- **`GetTypeName`'s broad recognition is a DIFFERENT question and stays broad** — naming (schema faces for foreign props) vs construction claims were conflated inside `ContainerFamily`'s breadth; only the construction claim shrinks.

## The hard boundary: folders/namespaces/classes ONLY — property + wire names untouched

`goal.Steps` (the property), `Actions`, `Modifiers` keep their names and `[Store]` wire faces this pass: authored goals reference them (`%goal.Steps[planStep.index]%`, `BuildStep/Start.goal:6`) and the `.pr` carries them — renaming means every `.pr` rebuilds and authored goals edit. That belongs to the parked lowercase-property sweep with a wire-migration story, not to a builder-recovery branch. Pin: a `.pr` built before/after diffs byte-identical.

## Verify items

1. **Naming index**: `steps.@this`'s namespace-tail was "steps"; the collection's tail is now "list". Expected moot — as a list subtype its `Type` face inherits list behavior (`{list, kind:"step"}` if you stamp the kind) and it likely needs NO registry name — but grep for anything keyed on the literal "steps"/"actions" names (`Is("steps")`-style asks, `_typeToName` consumers) before assuming.
2. **The `.pr` read**: the reflection kind constructs these collections by declared `PropertyType` — the ctor story (rows + context at birth, replacing the late-set `Context { get; set; }`), and the Goal-backref stamping must survive the read path.
3. **Hot-path perf**: steps iteration is the runtime's inner loop; the facade reads through base rows per access — measure; cache if it shows.
4. The two rungs the apex KEEPS in front (`IEnumerable<data>` / `IEnumerable<item>` instance-preserving narrows) still fire before anything else — don't reorder them behind the item check accidentally (a `steps` collection is both).

## Layer 5 — now coherent instead of a special case

`set %goal.Steps[i].Actions% = %compileResult.actions%` becomes a *list → action.list* creation — `action.list.@this` (an item now) gains its ICreate face (wrap/retag a list of actions), which is honest: the builder genuinely creates these collections from values. `error.Handle`'s `Clr<actions.@this>()` then likely heals because the value already IS actions-typed — trace it when you reach that layer; the standing direction holds: no native-list→infra reverse-birth via `Clr`, the consumer reads through doors or the value is born the right type.

## Pins

- The layer-4 repro: Fluid `{% for step in goal.steps %}` renders; the depth probe shows no bounce.
- `HashSet<string>` (foreign shape) lifts to a native list — the residue alignment proven.
- `.pr` byte-identical pre/post (wire names untouched).
- `Nest` suite still green (the modifier work rides the rename).
- The layer-3 fix (`IChannel` name via `Value()`) unaffected.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| infra collections as `list.@this` subtypes | the accepting class holds plang types; no central recognition of plang-shaped things | ok |
| singular + collection-node slots (`goal.step.list`) | the convention applied; `steps`/`actions` plural folders die | ok |
| `ContainerFamily` probe dropped | claim = build by construction; asymmetry dead by shrinking | ok |
| boundary switch only for foreign shapes | the sanctioned crossing, sized to what's real | ok |
| property/wire names untouched | authored goals + `.pr` stable; the rename sweep stays parked with its migration story | ok |
