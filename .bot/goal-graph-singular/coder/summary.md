# Coder summary — branch `goal-graph-singular`

## What this is
Make the goal graph "singular" and turn `goal`/`step`/`action`/`modifier` into plang **items**
(`item.@this` + `ICreate`), delete the three bespoke collection classes
(`modifiers.@this`, `actions.@this`, `steps.@this`), flip `clr<goal>`→`Data<goal>`, do the
singular property/wire sweep, then the `.pr` migration + acceptance. Architect plan lives in
`.bot/goal-graph-singular/architect/` (`items-answer.md` = mechanism, `demolition-followup.md`
= the gated deletion worklist).

## What was done (latest — the 3 small architect directives, commit `8809cc1a4`)
All from `architect/branch-review-findings.md` "Post-Decision landing":

1. **`Decision.HeadIs` → `IsHead`** (Is must be a prefix) + inlined the private `Labels` label-chain
   builder into `Of`. Caller `action.IsFirst` updated.
   `PLang/app/module/action/condition/decision/this.cs`, `action/this.cs`.
2. **`step.Clone()` DELETED** — dead, zero production callers. Removed its two tests
   (`StepTests.Clone_*`, `ModifierRegistryTests.StepClone_*`).
   `PLang/app/goal/steps/step/this.cs`.
3. **`RunFrom` → `Resume`, `MergeFrom` → `Merge`** (obpv — the only name exemption is boolean
   Is/Has, no preposition carve-out). Renamed on `step`, `goal`, `steps` collection + the
   snapshot/build callers + tests; the two partial files renamed
   `this.RunFrom.cs`→`this.Resume.cs`; test `GoalRunFromTests`→`GoalResumeTests`.
4. **`action.Reflect` decompose** — `property.@this` gained a self-building ctor
   `new property.@this(PropertyInfo, type.list)` that reflects its own Name/Type/Nullable/
   Default/IsVariable (absorbed `UnwrapToValue` + `IsVariableNameSlot`, both inlined — no
   statics left). `Reflect()` is now just the filter loop, dropping rows by `row.Type.Name`
   ∈ {clr, goal, step, action, modifier}. `ReflectReturn` (verb+noun, single caller) inlined
   into the `Return` getter and deleted.
   `PLang/app/goal/steps/step/actions/action/property/this.cs`, `.../action/this.Schema.cs`.

### Tests (all green after the change; deltas are zero vs. baseline)
- Wire (Resume/snapshot): `GoalResumeTests` 5/5, `SnapshotResumeTests`+`StepLoopShouldExitTests` pass.
- Modules: `MergeTests` 12/12, `ModifierRegistryTests` 4/4.
- Data: `StepTests` 13/13.
- Catalog (Reflect output): `TypedPropertyCatalogTests` 5/5, `DescribeTests` 3/3,
  `ModulesDescribeReturnTypeTests` 7/7, `Stage4_BuilderCatalogTests` 3/3, `ParamDescParity`,
  `PropertyLeafParity`, `Stage5_ChannelActionsBuilderCatalog`, `Stage2_MechanicalTypings_Part1` all pass.
- **One pre-existing failure** (confirmed on clean HEAD via stash+rebuild, NOT mine):
  `Stage2_MechanicalTypings_Part2Tests.ModulesDescribe_BuilderRecordHandlers_AdvertiseConcreteReturnTypes`
  — `builder.types` absent from `Describe()` (NRE at the test's `types!`). Independent of this diff.

### Also already landed earlier on the branch (context)
- `modifiers.@this` deleted; `modifier.Wrap` owns the wrap fold; `Position` (was `Order`).
- `Decision` type created; condition methods left `actions.@this`.
- item base `Set` = reflect-default; `GoalCall` returns goal item direct (no `clr<goal>` wrap).
- Test-confidence sweep: `Make.Action` templates `%var%` params; `TestRunAssertions` diagnostic;
  RunActionTests 2→13; assert.isNull/isNotNull RESOLVE (async).

## What's left (the big, gated piece — increment 3)
Deleting `actions.@this`/`steps.@this` is **Gate 2** in `demolition-followup.md` — blocked until
**increment 3** lands:
1. **Explicit `Write`/`Output` for `step` and `goal`** (today they delegate to the reflection `*`
   kind — Finding #2). `action` already has explicit Store Output.
2. **Per-type `serializer/Reader.cs`** at goal/step/action level — reads native items, param rows
   ride the existing `@schema:data` reader. This REPLACES the reflection-kind read for the graph.
3. **Flip the `.pr` read** off the reflection kind onto the new readers.
4. **Then** delete the three collection classes + re-home (`steps.RunAsync`→`goal.RunAsync`,
   `actions.Nest`→`step`, etc. per items-answer table).
5. **Singular sweep** (LineNumber→Line, ActionName→Name, Steps→Step, Goals→Child, wire keys …).
6. **`.pr` migration**: hand-edit the ~11 builder bootstrap `.pr` keys (Ingi-permitted for THIS
   branch only), rebuild everything else from source. Golden: item `Write` byte-identical mod keys.

Architect flags the readers as "the real new code" and the honest risk. This touches the wire/type
boundary — **paused here for design alignment on the increment-3 approach before starting** (per the
discipline: discuss significant wire/type-shape changes first).

## Code example (the directive-4 pattern — row self-builds)
```csharp
// property/this.cs — the row reflects itself; the loop only filters
[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
public @this(PropertyInfo prop, global::app.type.list.@this types)
{
    var bare = System.Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
    bool isDataGeneric = bare.IsGenericType
        && bare.GetGenericTypeDefinition() == typeof(global::app.data.@this<>);
    Name = prop.Name;
    Nullable = /* Nullable<T> or nullable-ref via NullabilityInfoContext */;
    IsVariable = isDataGeneric && bare.GetGenericArguments()[0] == typeof(variable.@this);
    var value = isDataGeneric ? bare.GetGenericArguments()[0] : bare;
    Type = value == typeof(global::app.data.@this) ? types["object"] : types[value];
    Default = prop.GetCustomAttribute<DefaultAttribute>()?.Value;
}
// this.Schema.cs — Reflect() is now just the filter
foreach (var prop in handler.GetProperties(...)) {
    if (skip) continue;
    var row = new property.@this(prop, types);
    if (row.Type.Name is "clr" or "goal" or "step" or "action" or "modifier") continue;
    rows.Add(row);
}
```
