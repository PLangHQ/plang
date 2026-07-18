# FINAL mechanism — the graph becomes ITEMS (Ingi reversed Stage-1's hosts ruling, 2026-07-17)

**Supersedes `fork-b-answer.md`'s mechanism** (B's sweep/wire/migration content survives; the hosts-stay-hosts core does not) and the map §A subclass shapes. Stop the B prototype — this is the direction. The trace that flipped it (reviewed with Ingi): the decompose objection was a current-code artifact — items hold C# internals behind their faces, so the engine keeps typed access; `action.Parameters` already proves plang-in-the-graph at runtime scale (Data rows, door-read per execution, compact on the wire); and the `WriteReflected` bridge comment (type/item/this.cs:532 — "Deleted once they are items") was the codebase's own destination.

>>  **MIGRATION (Ingi, 2026-07-17): hand-edit the builder bootstrap `.pr` files — you are ALLOWED.**
>>  Normally editing a `.pr` by hand is forbidden. Ingi has explicitly permitted it for THIS branch, for the
>>  ~11 builder bootstrap files ONLY (`os/system/builder/**/.build/*.pr` + the `app.pr` boot markers). Rename
>>  the wire keys in place (see plan area 4); the new binary then rebuilds every other `.pr` from source. No
>>  migration script. Do NOT hand-edit non-builder `.pr` files — let them regenerate.

> **You own this.** Shapes below are the recipe applied (`Documentation/v0.2/defining-plang-types.md`); bodies, factoring, and the re-homing details are yours. Everything marked NEW.

## The model

`goal`, `step`, `action` (+ `modifier : action`) become **plang items** — `item.@this` + `ICreate` (honestly satisfied: the builder creates them from values every compile via the dict→slots door). The **item pattern applies in full**: C# internals for the engine (typed `int Index`, `string Text`, `List<step.@this> _step` — the inner loop reads them exactly as today, zero crossings), plang faces for the boundary. **The three bespoke collection classes DELETE** — a graph child collection is an internal typed list + a native-list face, not a class.

```csharp
// goal/step/this.cs — the shape (skeleton; the recipe fills it)
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    // ENGINE SURFACE — C# internals, typed, hot-loop safe (the value-type pattern: backing + faces)
    public int Index { get; init; }
    public string Text { get; init; } = "";
    public int Indent { get; init; }
    internal List<action.@this> _action = new();          // storage; the face wraps by ref
    public global::app.goal.@this Goal { get; set; } = null!;   // backref stays a typed ref

    // PLANG FACES
    protected internal override global::app.type.@this Type => new("step", typeof(@this));
    public override bool IsLeaf => false;
    public global::app.type.item.list.@this Action => /* native list over the SAME _action refs */;

    /// <summary>The step writes ITSELF — exactly today's bare [Store] shape, singular keys.
    /// This is what kills fork-A's wire break: an item OWNS its wire; nothing inherits the
    /// value-list's envelope. Byte-identical-modulo-keys is the golden.</summary>
    public override void Write(global::app.channel.serializer.IWriter w)
    { w.BeginObject(); w.Name("index"); w.Int(Index); w.Name("text"); w.String(Text); /* … */ w.Name("action"); /* each action.Write */ w.EndObject(); }

    public static @this? Create(object? raw, global::app.data.@this data) { /* dict → slots door (the builder's path) */ }
    // navigation: the base default reflects members — override Get only where precision pays
}
// goal/serializer/Reader.cs, step/…, action/… — ITypeReader per the recipe; format-agnostic;
// param rows ride the EXISTING @schema:data reader (sign-identical machinery). This REPLACES the
// reflection-kind read for the graph — the .pr load becomes a standard typed read.
```

## What this deletes (beyond fork-B's list)

- `steps.@this`, `actions.@this`, `modifiers.@this` — the classes entirely (not relocated: there is nothing to relocate; children are properties). The `goal/step/list/` folder slots from the earlier plan DON'T EXIST in this world.
- The `WriteReflected` raw-collection BRIDGE case (its own comment fulfilled) — after verifying no other raw-collection host rides it.
- `clr<goal>`/`clr<action>` carriers at the plang boundary → `Data<goal>`, `Data<step>` direct; the reflection-kind read path FOR THE GRAPH (it stays for genuine hosts like `app`).
- The fork-B wrap predicate at the apex for the graph (items enter rung 1); `ContainerFamily`'s `IList<>` probe drop still stands.
- The `IList<T>` facades, the `Count` question, the `private protected` seam for the graph (the seam survives only for `error.list`/`warning.list`).

## Method re-homing (the collection classes' members need homes — my leans, you own)

| Member (today) | Home |
|---|---|
| `steps.RunAsync` (the step loop, skipBelowIndent) | `goal.RunAsync` absorbs it (it already owns the lifecycle and calls it) |
| `steps.MergeFrom` / `HasIndentedChildren` | goal (merge is goal-level; indent is a question about the goal's sequence) |
| `steps.Nest` (per-step iteration) | goal; the per-step body onto **step** |
| `actions.Nest` (the catalog join) | **step** (it reshapes the step's action chain) |
| `actions.Chain` / `Branches` / `FirstConditionIndex` / `IsFirstCondition` | **condition module, as a `Decision` type** (NOT step — see `condition-decision-answer.md`; these are condition.if's logic, their verb names signal a missing type) |
| `actions.Value` | already sentenced — dies |
| `modifiers.RunAsync` (the wrap fold) + `Sort` | **action** (it wraps THIS action's dispatch; `action/this.cs:247` becomes its own fold) |
| Goal/Step backref stamping | construction-time wiring where possible; getter-stamp survives only where construction can't reach |

## What survives unchanged from the plan/map

- The singular sweep: namespaces (`app.goal.step.action.*`), properties (`Step`/`Action`/`Modifier`/`Child`/`Parameter`/`Default`/`Error`/`Warning`), wire keys, `ActionName→Name` (`action→name`), os/ files, LLM schemas, CLI keys — all areas 2-3 as planned.
- **The migration story** (rescoped — see the MIGRATION banner + plan area 4): item-owned `Write` reproduces today's bare shape, so hand-editing the ~11 builder bootstrap `.pr` keys is enough to boot; the fresh binary rebuilds all other `.pr`. The golden: item `Write` output byte-identical except renamed keys.
- `error.list`/`warning.list` thin subclasses; `Parameter`/`Default` as `List<data.@this>` (already the item world's shape — rows are Data); backref members stay typed refs (now internal to items); scalars stay C# internals (the sanctioned-crossing test governs the faces).
- Areas 0, 4, 5 acceptance gates — plus: the engine inner loop reads internals (perf pin trivially holds); `Data<goal>` replaces `Data<clr<goal>>` at the consumers (`goal/getTypes`-successor, `environment/run` — the sweep's compiler finds them).

## Verifies (the honest risks of the item world)

1. **The readers are the real new code** — goal/step/action ITypeReaders per the recipe. The param-row hop into the `@schema:data` reader must stay sign-identical (the W4 test pins it). One round-trip golden per level.
2. **The `Type` face + naming**: "goal"/"step"/"action" become REGISTERED type names (they're items now) — check collisions with existing vocabulary (the `goal.call` dotted registration, `getTypes`' "list<goal>" strings) and that `Is("goal")` asks behave.
3. **`WriteReflected` bridge deletion**: inventory what else rides the raw-collection case before deleting.
4. **`modifier : action` under items**: the subtype ruling stands; verify the modifier reader constructs the subtype by declared element type in the fold slot.
5. Fluid/`list.where`/`%goal.step[2]%` over item graphs — should be rung-1 trivial; one spike leg re-run proves it.
