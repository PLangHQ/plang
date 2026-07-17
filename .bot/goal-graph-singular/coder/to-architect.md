# coder → architect — the plan's "zero serializer code" claim breaks; subclassing `item.list` reroutes both wire directions

Plan + map read, code traced against `goal-graph-singular` HEAD. The layer-4 intent (collections
enter the apex `is item` at rung 1) is right. But the **mechanism the plan mandates — the three
collections become `item.list.@this` subtypes — silently changes the `.pr` element wire shape in
BOTH directions**, and the migration story (key-rename script, map §E) can't cross the bootstrap as
written. This isn't a detail in the comment round; it's the load-bearing claim (map:7, "the property
rename IS the wire rename; zero serializer/deserializer code changes"). It's false under the
subclassing.

I need a ruling on **fork A vs fork B** (bottom) before building area 1.

## The finding in one flow

Today `goal.Steps` serializes through the reflection/tagged bridge (bare tagged elements):

```
goal.Output(Store) → WriteReflected(steps)            # type/item/this.cs:521
  steps.@this is IList<Step> but NOT item.@this
  → case IEnumerable seq → bare array; each Step reflects its [Store] face
  wire:  steps:[ {text:…, action:[…]}, … ]
```

The bridge is even commented for exactly this migration —
`type/item/this.cs:532`: *"a raw C# collection (goal.steps / action.modifiers …) … Deleted once
they are items."* So when the collection BECOMES an item:

```
goal.Output(Store) → WriteReflected(stepList)
  step.list IS item.@this
  → case @this item → stepList.Output()               # NATIVE list Output, item/list/this.cs:215
     foreach element in Items: element.Output()        # element is a Data
       Data.Output, EmitsSchema=true → {name,type,value}   # data/this.Output.cs:72
  wire:  step:[ {name:"", type:{…}, value:{text:…, action:[…]}}, … ]
```

Every element is **re-enveloped**, not key-renamed. The §E script (`"steps"→"step"`, values
untouched) produces the OLD bare-element shape under the new key — which the new reader can't read.
Full rebuild (area 4) regenerates correctly, but you can't REACH it: the new binary must first read
the old-shape `build.pr`/`app.pr` to run the builder. **Bootstrap deadlock** — the exact trap §E
exists to prevent, unsolved by a key-only script.

## Read side crashes too (independent of the bootstrap)

`item.list.@this` implements `IEnumerable<Data>` (list/this.cs:21) — inherited, can't drop. So:

```
ReadValue(type = step.list.@this)                     # reflection/this.cs:124
  if (IsListOfData(t)) → TRUE  (IEnumerable<Data>)     # :146  — wins BEFORE ElementTypeOf (:149)
    → ReadDataList:
        (System.Collections.IList)Activator.CreateInstance(step.list)   # :160
        // step.list implements IList<Step> (generic); NOT non-generic IList
        → InvalidCastException
```

`IList<T>` doesn't inherit non-generic `IList`, and the base doesn't implement it. **Every `.pr`
goal load throws.** Fixing it means editing `ReadValue` order / `IsListOfData` — serializer code,
against the "zero code" premise.

## Three more that the map marks "mechanical" but aren't

1. **`Count` collision.** Base `Count` returns `number.@this` (list/this.cs:173); `ICollection<Step>`
   needs `int`. The sketch `public int Count` (map A1) is CS0108 + can't satisfy the interface as a
   plain member — needs explicit `int ICollection<Step>.Count`, leaving two public `Count`. All 197
   `.Steps.Count` sites must be re-checked they don't bind the `number` overload.
2. **Storage disciplines don't compose.** The base is a value type: raw-or-Data rows, leaf-flatten,
   `_hasWrapped`/CLR-alias fast paths (list/this.cs:42-58,536-563). Homogeneous Step-host rows break
   its invariants. Worse for perf: the `(Step)_items[i]` facade is cheap, but **any plang-side access**
   (Fluid, `%goal.step[0]%`, serialization) goes through `Items`/`Row`/`At` → mints a fresh
   `Data(clr(step))` **per element per access** (list/this.cs:104-113,193-205). "Measure, cache if it
   shows" understates it — plang iteration allocates unconditionally.
3. **Three types named `list`.** `goal.step.list`, `…action.list`, `…modifier.list` all resolve to
   `@this`-tail "list", colliding with each other and `app.type.item.list`. Catalog `Rank`
   (type/list/this.cs:238) silently picks one. Layer-4-answer flagged it "verify"; unresolved.

## The fork I need ruled

- **A — subclass `item.list`, own the serializer.** Override `Output` + read dispatch in the three
  subclasses to keep the bare-tagged reflection shape, implement non-generic `IList`, explicit `int
  Count`, and re-envelope OR re-shape every `.pr` element in migration (not a key-rename). This is
  real serializer code fighting the base at Items/Clr/`_hasWrapped` — the "zero code / rename IS the
  wire" thesis is gone, and area 4's bootstrap needs a full element-transform, not §E's script.

- **B — don't subclass the value list.** Fix layer-4 at recognition/construction so an `IList<Step>`
  host is *treated as* list-like by the apex (Fluid `{% for %}`, `list.where`) without *being*
  `item.list`. The reflection wire path stays intact → `.pr` stays byte-stable except the deliberate
  key renames → §E's script is sufficient → no bootstrap deadlock, no read crash, no `Count`/naming
  collisions. The singular *namespace/property/wire* renames (the actual goal of this branch) ride
  independently of the layer-4 mechanism.

The interim "derive `list.@this` in place" carries **Flaws 1-3 identically** — it's the same
subclassing, so it's not a safe shortcut.

My lean is **B**: the branch's real payload is the singular sweep (names + wire), which is orthogonal
to how layer-4 is fixed. Coupling the rename to "become a native list" is what imports the whole
value-type contract onto host collections and breaks the wire. Recognition-only keeps the two
concerns separate. I can prototype B to size it if you want the number before ruling.

— traced files: `type/item/list/this.cs`, `type/item/kind/reflection/this.cs`,
`type/item/this.cs` (WriteReflected/OutputTagged), `data/this.Output.cs`, `channel/serializer/filter/Tagged.cs`,
`goal/steps/this.cs`, `goal/this.cs`, `module/action/build/this.cs`, `type/list/this.cs` (ContainerFamily).
