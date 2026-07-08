# FromSource — a navigation-driven, target-owned record builder

**Status:** spec seed for the architect (written by coder). Decision made with Ingi
(2026-07-08): build the **general** mechanism (Option B), not a per-type patch — "it
solves a whole lot of things."

**Branch:** `navigation-driven-record-builder` (off `variable-as-value`).

---

## 1. The problem it fixes

The clr(json) direction (clr-navigators) made LLM results ride as **clr(json)** — a lazy
JsonElement carrier, navigated by its kind, never materialized. That is correct. But it
exposed a gap: **writing a clr(json) onto a plang-typed slot has no conversion door.**

Concretely, the current builder blocker:

```
- set %goal.Steps[step.Index].Actions% = %compileResult.actions%
```

`%compileResult.actions%` is a clr(json) array (each element `{module, action, parameters}`).
`goal.Steps[i].Actions` is `actions.@this` — a real plang item type
(`: item.@this, ICreate<@this>, IList<action.@this>` = `list<action>`). The write path
tries to **lower** the clr(json) via `.Clr(actions.@this)`:

```
clr.Clr(target) → ClrConvert(JsonElement, actions.@this)
  → throws  "JsonElement cannot lower to actions — the type must own this Clr projection"
     (PLang/app/type/item/this.cs:364)
```

`ClrConvert` is the LOWER door (plang→CLR). It is terminal on purpose ("LOWER never
re-enters the conversion hub"). Reaching it here means the caller asked LOWER to do a
**CONVERT** (clr(json)→plang record) — which is the type system's job, not the lower door's.
So the built goal's steps end up with no actions and validation fails
(`Step[0..N]: no actions`).

## 2. The model (settled with Ingi)

**The TARGET owns the conversion, and PULLS each property from a navigable source.**

`ICreate<T>` already says "I know how to create myself from some input." So `list<action>`
and `action` are the responsible parties — not the json. `action.Create(source)` walks its
OWN declared properties and, for each, asks the source:

> *"give me `module` as `text`"* → `source.GetChild("module").Value<text>()`

The source is a `Data` over *anything navigable* — a clr(json) object, a `dict`, a clr(POCO).
It already answers "child by key, converted to type T" (navigation `GetChild` + `Value<T>`).
So `action` never special-cases json vs dict vs POCO — **one conversion path for any
navigable source.** No STJ, no per-source converter.

Nested and list properties recurse the same way: a nested record property → the target
record's own `Create`; a `list<T>` property → `list<T>.Convert` (which iterates the source
sequence and runs each element through the target element's `Create`).

## 3. FromSource<T> — the shared builder

Because records are `init`-only, the builder can't set properties post-construction — it
must emit the object initializer. So this is **generated per record type** by the source
generator (which already knows each record's shape), not a runtime reflective helper.
Reflection is the fallback for hand-written types only.

Shape (conceptual — the generator emits the concrete per-type body):

```csharp
// generated on each [PlangType] record's Create
public static @this? Create(item.@this value, data.@this data)
{
    if (value is @this already) return already;                 // identity
    var src = value as data.@this ?? new data.@this("", value, context: data.Context);
    return new @this
    {
        Module     = await src.GetChild("module").Value<text>(),
        ActionName = await src.GetChild("action").Value<text>(),
        Parameters = await src.GetChild("parameters").Value<parameters>(),   // list/record recurse
        // ... one line per declared property, wireName + declared type known at build
    };
}
```

(Real signature is sync `Create` per `ICreate`; the async pulls are the open question in §6.)

## 4. How it affects the .pr READ PATH (the key analysis)

Today the `.pr` goal read is one STJ call:

```
goal/serializer/Reader.cs:  JsonSerializer.Deserialize<goal.@this>(text, GoalReadOptions)
GoalReadOptions = Wire.ReadOptions(ReadContext(context, "plang", Store, Verify:false))
```

`GoalReadOptions` is the **same** converter chain the Data wire reader uses. So the read
already splits into two halves:

- **Record skeleton** (Goal → Steps → Actions, scalar/nested fields) — walked by **STJ
  reflection**.
- **Data leaves** (step param values `{name, type, value}`) — built by the **Data reader**
  (`app/data/reader` — `%ref%`→variable, deferred source, template flag, signing), invoked
  *through* the STJ converter chain.

**FromSource replaces only the first half.** `Deserialize<goal>(text)` becomes
`FromSource<goal>(clr(json))`: goal pulls `name`/`steps`, Step pulls `actions`, action pulls
`module`/`action`/`parameters`. When FromSource reaches a **Data-typed property**, it hands
that json child to the **existing Data reader** — the exact path the Wire converters take
today. So the `%ref%`-born-as-variable / deferred-source / template / signing machinery is
**untouched**.

The boundary already exists and is clean:

| half | today | with FromSource |
|---|---|---|
| record skeleton (Goal/Step/action fields) | STJ reflection | FromSource navigation |
| Data leaves (param values) | Data reader (via Wire converters) | Data reader (unchanged) |

This is **the already-planned cleanup**, not a detour — `goal/serializer/Reader.cs` carries
the note:

> BRIDGE: goal is really a host CLR object… Final-stage cleanup (Ingi): goal rides as
> `clr`, this reader and the goal-as-type machinery go.

FromSource is that mechanism: a `.pr` becomes a clr(json) that builds itself; the STJ
`Deserialize<goal>` + `goal/serializer/Reader.cs` retire.

## 5. What already lines up (don't rebuild)

- **`list<T>.Convert`** (`app/type/list/this.Generic.cs`) — iterates a source sequence,
  `TryConvert`s each element to `T`, aggregates per-element errors. The list half is done.
- **Navigation** — `Data.GetChild` + `Value<T>` is the "answer a property ask" machine,
  including clr(json) navigation (clr-navigators). Done.
- **`ICreate<T>`** — the target-creates-itself contract. Done; records just need bodies.
- **The Data reader** (`app/data/reader`, `Wire.ReadOptions`) — the Data-leaf half. Reused
  as-is.

## 6. Open design decisions for the architect

1. **Generated `Create` vs. shared reflective `FromSource<T>`.** Coder lean: generated per
   type (strict, fast, init-friendly). Confirm; decide the generator emission contract.
2. **Sync `ICreate.Create` vs. async pulls.** `Value<T>()` is async (navigation can be
   I/O / resolve). `ICreate.Create` is sync today. Either make the create path async, or the
   generator emits a two-phase build (sync skeleton, async leaf resolution), or pulls that
   are known-sync stay sync. This is the main mechanical fork.
3. **The Data-leaf seam.** FromSource pulling a `Data`-typed property must produce the SAME
   deferred Data the Wire converter does today (lazy `%ref%`, template flag, signing) — i.e.
   "read this child *as a Data* via the Data reader," never "convert it to a value." Nail
   this so `%ref%` resolution + signing stay byte-identical.
4. **Migration order / coexistence.** Flip type-by-type; STJ and FromSource coexist during
   rollout. Suggested order: (a) write-path convert for `action`/`list<action>` (unblocks the
   builder), (b) Step/Goal, (c) retire `goal/serializer/Reader.cs` + STJ `Deserialize<goal>`.
5. **Wire-name mapping.** Property → wire key (JsonPropertyName / camelCase). Generator reads
   it; confirm it matches what the Store writer emits (round-trip).
6. **Error aggregation.** Per-property convert failures should aggregate like
   `list<T>.Convert` does (ErrorChain), not throw on the first — non-programmers read these.

## 7. Interim (parallel, on `variable-as-value`)

To keep the builder moving while the architect reviews this: a **scoped Option A** —
a hand-written `action.Create` (+ `list<action>` routing in the write path) that pulls its
own properties from the navigable source. ~30 lines, unblocks the "no actions" blocker,
throwaway once FromSource (B) subsumes it. It also proves the pull-from-navigable-source
pattern in the small before the general build.

## 8. Payoff

One uniform, plang-native deserializer: `clr(json)`/`dict`/`clr(POCO)` → any plang record,
target-owned, no STJ. Retires the goal-as-type STJ bridge, makes every record round-trip the
same way, and closes the clr(json)→typed-slot gap that the clr(json) direction opened.
