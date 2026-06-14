# Design note: the read builds values born-right, via the type (not static Lift)

**Status:** settled with Ingi this session (2026-06-14), for architect review.
Refines `clr-dissolution-design.md` role 2 (declared-kind at read) and aligns with
`deserialize-flow-design.md`. The `AtKind` approach explored earlier was the wrong
shape and has been reverted. No code yet.

## The rule

> The **format serializer** decodes wire bytes → a **plain CLR** value. The
> **type builds itself**, at its declared kind, from that plain value — via the
> **catalog instance**, not a static `Data.Lift`. One pass. Born right.

Three consequences fall out, all things we wanted gone:
- no `json.Parse` inside a type's read (that's JSON leaking into the type),
- no pre-lifted value reaching the type (so no "already-built" coercion exception),
- no static `Data.Lift` on the read path (a type owns its own construction).

## Why the current read is wrong (trace `set %n% = 5 as int` from `.pr`)

```
wire:  { type:{number,int}, value:5 }

Wire.ReadBody
  json.Parse(JsonElement 5) → number(long 5)        ← PRE-LIFT, kind-blind
  new Data(name, number(long), {number,int})
     ctor: Data.Lift(json.Parse(value)) → number(long)   ← static Lift + json.Parse AGAIN
     ctor: type.Judge(number(long))    → number(long)    ← can't fix (no-op)
  Wire kindDiffers patch → clr(number(long), label "int") ← the value is LONG, wears an "int" sticker
  %n% → long 5, type slot says int                        ← mismatch carried; clr exists
```

Two smells, both yours: (1) `json.Parse` runs *inside the type path* — the type is
doing JSON; (2) `Data.Lift` is a **static** kind-blind dispatcher doing type
*discovery* we don't need (the `.pr` already says `number`).

## The corrected read — the type creates itself

The creator already exists: **`type.@this.Convert(value, context)`** (`type/this.cs:159`):

```csharp
// "a type owns its own construction — callers ask the type to make the value"
public data.@this Convert(object? value, context ctx) {
    if (value is item { IsLeaf:true } leaf) value = leaf.Clr<object>();   // unwrap to raw
    var familyClass = ctx.App.Type[Name]?.ClrType;                       // catalog INSTANCE resolves the type
    var owned = ctx.App.Type.Conversions.Of(familyClass, value, Kind, ctx); // family builds, KIND-aware
    if (owned != null) return owned;
    ...
}
```

Registry-driven (`ctx.App.Type[...]`, an instance — not static), kind-aware
(passes `Kind`), per-type (the family's own hook builds it), and the type entity
"only routes — it holds no per-type knowledge." Exactly the shape you described.

So the flow becomes:

```
┌─ json serializer (the ONLY place JSON is understood) ──────────┐
│   decode value token  →  plain CLR  5L                          │
└──────────────────────────────┬─────────────────────────────────┘
                               ▼  plain CLR (no wrapper, no JsonElement)
┌─ the TYPE builds itself — format-agnostic, registry-driven ────┐
│   {number,int}.Deserialize(5L)                                  │
│      → this.Convert(5L, ctx)                                    │
│      → ctx.App.Type[number]  →  number family                  │
│      → number family builds 5L at kind "int"  →  number(int 5) │   ← BORN at int
└──────────────────────────────┬─────────────────────────────────┘
                               ▼  new Data(name, number(int))   (no Judge)
                         %n% → int 5, type slot int   (consistent, no clr)
```

## The change

`type.@this.Deserialize` (`type/this.cs:366`) — replace the static-Lift + json.Parse
fallback with the type building itself:

```csharp
// no reader: the TYPE constructs itself, kind-aware, via the registry — born right.
var built = Convert(raw, ctx!);
return built.Success && built.Peek() is item.@this it ? it : new item.absent(Name, Kind);
```

- Drops `Data.Lift(json.Parse(raw))` (static + JSON).
- `Convert` already unwraps a leaf and rebuilds at `Kind`, so the `raw is item.@this
  already` early-return is no longer a special case — the same path handles a
  pre-built value (unwrap → rebuild at kind). The "exception" disappears.
- Keep the reader-registry path (`Readers.Of(Name, Kind)`) for content formats
  (csv/xlsx/json-object) — those readers already get `Kind` and are born-right.

`Wire.ReadBody` value handling — hand the type **plain CLR** and let it build:

```csharp
// typed value: the declared type builds itself from the decoded plain value.
instance = typeRef.Deserialize(decodedPlainValue, ctx);   // born right
data     = new @this(name, instance);                     // no Judge, no Lift
```

- The `kindDiffers` → `clr` branch is deleted (the value is born at the right kind).
- The **`nameDiffers` → `clr`** branch (role 2 *name* — a domain value as a
  property-bag dict tagged `permission`) and the **courier** (`value is @this
  innerData`) branches **stay** — they're role-2-name / role-4, gated on the
  signing & schema work. This step removes only the *kind* clr.

## What this deletes / shrinks

- `json.Parse` inside `type.Deserialize` — gone (JSON stays in the serializer).
- The `kindDiffers` `clr` label in `Wire` — gone.
- The "already-built value" coercion exception — gone (one path handles all).
- `Data.Lift` on the **declared-type read path** — gone. `Lift` (static) survives
  ONLY for the no-declared-type (polymorphic `data` slot) discovery — and that
  discovery should itself move onto the catalog instance (`ctx.App.Type.From(value)`)
  so `Data.Lift` leaves `Data` entirely. Tracked as a follow-up.

## Scope / risk

In: `type.Deserialize` fallback, the `Wire` value handling (route through
`Deserialize`, drop the kind clr). Out (kept this branch): the `nameDiffers` clr,
the courier branch, signing — all gated on the next branch's signing/schema work.

Touches the core wire read, so verify: the `int`/precision round-trip is born
right; containers (dict/list, already native from Stage 11) pass through unchanged;
the full suite shows no regression. The signature is unaffected — only the
*in-memory* materialization changes; the wire bytes (`type:{number,int}, value:5`)
are identical.

## One line

The type already knows how to build itself at its kind (`type.Convert`, via the
catalog instance). The read just has to *call it* with plain CLR — instead of
pre-lifting with static `Data.Lift` + `json.Parse` and taping the result with a
`clr` kind-label. Born right, format-independent, no static, no exception.
