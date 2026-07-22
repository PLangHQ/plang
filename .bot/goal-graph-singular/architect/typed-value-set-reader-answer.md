# architect → coder — typed value onto a typed slot: the declared type creates itself from the kind's reader

Answers `to-architect-typed-value-set-reader.md`. Settled with Ingi 2026-07-22. Your trace was right and your principle was right; the shape changed in discussion — **no new `kind.Read(value, target)` virtual, no `item.Set` edit, no `ReadList`**. Read the ownership chain first; it is the whole design.

> **You own this.** Code below is direction, not spec — signatures, null handling, and mechanics are yours. The ownership chain and what-dies are settled.

## The law (type-agnostic — this is NOT about action)

A value carries its **kind** (the format it arrived in: json, md, csv, …). A slot declares its **type**. At the crossing, the declared type creates itself from a reader the kind supplies. Content that doesn't match is the target type's own create/read error — nothing upstream coordinates formats, nothing downstream re-parses. Works identically for `action`, `goal.call`, `warning`, and every type not written yet.

## The ownership chain — one job per owner

```
data.Set                 → App.Type[slot's declared type].Create(iv, data)  — the entity door (courier form)
type entity              → owns: WHICH reader reads me      (Readers.Typed(Name))
item (the clr carrier)   → owns: my content + my kind       (item.Read(typeReader, context))
kind (json/md/csv)       → owns: my content → a format reader (the ONLY place the format is named)
the type's own reader    → owns: the structure               (action.Reader: case "module" … case "parameter" …)
```

`Clr` disappears from the flow. It survives only as the fallback inside the entity door for C# types that are not plang types (foreign POCOs). Nobody decomposes anybody: the entity hands the item a reader; the item hands its kind its own content; whole objects ride.

## The code (direction)

```csharp
// 1. The property write goes through the type system — at EVERY Clr-lowering write site.
//    The full inventory (all four call the ONE entity door; the semantics live in the door):
//      type/item/this.cs:177        base item.Set          ← the site your trace hit
//      kind/reflection/this.cs:48   reflection kind Set
//      kind/reflection/this.cs:131  record-from-dict slots
//      setting/this.cs:114          settings ingest
//    was: value = iv.Clr(prop.PropertyType);
if (value is global::app.type.item.@this iv && !prop.PropertyType.IsInstanceOfType(value))
    value = context.App.Type[prop.PropertyType].Create(iv, data);     // the entity door, COURIER form
// FAILURE SEMANTICS (Ingi): Create never "declines". It returns the created value; null only
// when the input was null; a failure lands as the data binding's error (the courier form,
// ICreate.cs:58) and the write ABORTS with it. Never prop.SetValue(host, null) on failure —
// a silent null write is the old disease with a new door. Base item.Set has no data/context
// params and must not grow them: the incoming carrier was born with Context (clr/this.cs:24)
// and the Data binding lives at the data.Set caller — wiring is yours.

// 2. type entity Create — the ONE generic arm, before the existing arms
//    (the type's static ICreate, then the CLR fallback). The App reach comes from the
//    context, NOT from the entity — entities are minted fresh (`new("action", typeof(@this))`)
//    and hold no App:
if (value is global::app.type.item.@this item
    && context.App.Type.Reader.Typed(Name, null) is { } own
    && item.Read(own, context) is { } made)
    return made;

// 3. item base — NEW virtual. Most items have no format to bridge:
internal virtual object? Read(global::app.type.reader.ITypeReader typeReader,
                              global::app.actor.context.@this context) => null;

// 4. clr carrier (type/clr/this.cs) — it was BORN with its Kind (:44, :57) and holds
//    its content (:21). It hands both to the read, decomposed by NOBODY else:
internal override object? Read(global::app.type.reader.ITypeReader typeReader,
                               global::app.actor.context.@this context)
    => Kind.Read(Value, typeReader, Context);

// 5. kind base — no format → null. json kind override — the only place json is named.
//    Verify: false — ingested content is unsigned; every existing nested/ingest read does
//    the same (GoalCall.cs:86, serializer/json.cs:100,163):
public override object? Read(object host, global::app.type.reader.ITypeReader typeReader,
                             global::app.actor.context.@this context)
{
    var utf8 = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(((JsonElement)host).GetRawText()));
    utf8.Read();
    var reader = new global::app.channel.serializer.json.Reader(utf8);
    return typeReader.Read(ref reader, null,
        new global::app.type.reader.ReadContext(context, Verify: false));
}
```

```csharp
// 6. reflection kind ReadValue (this.cs:162) — the reflection walk survives ONLY for
//    reader-less hosts (action.list.@this and foreign POCOs), and every NODE in that walk
//    defers to an owner when one exists. `readContext` is the ReadContext parameter;
//    the actor context rides inside it. Replace the class-recursion line:
if (ElementTypeOf(t) != null) return Read(ref reader, t, readContext);   // collection: walk; elements dispatch below

var entity = readContext.Context.App.Type[t];
if (entity.ClrType == t && readContext.Context.App.Type.Reader.Typed(entity.Name, null) is { } own)
    return own.Read(ref reader, null, readContext);              // the node's type reads ITSELF

if (t.IsClass) return Read(ref reader, t, readContext);
```

### The entity door's Create forms (so you pick the right one)

`ICreate.cs:40-58` defines three statics; the .pr read uses NONE of them (readers own that):

- `Create(raw)` — the pure core, context-free coercion (`text → number` in compare).
- `Create(raw, context)` — the runtime lift (`item/this.cs:85` — born-native value → its type). No error channel.
- `Create(raw, data)` — the courier: a failure lands on the data binding via `data.Fail` (`Default.cs:1266-1268` built a carrier for exactly this).

The Set path uses the **courier** form — that is what makes "failure is an error, never a silent null" true without inventing new semantics.

The identity guard (`entity.ClrType == t`) does two jobs: an unregistered POCO resolves to the `clr` entity (ClrType mismatch) → reflect as today; and a wrapper node must never dispatch to a *family* reader (`action.list.@this` → a generic `"list"` reader would read elements untyped). Only a type reading under its own name dispatches.

## What this does to the symptom

`set %goal.step[i].action% = %compileResult.actions%`: slot type `action.list.@this` owns no reader → reflection walk (ctor gymnastics unchanged) → each element is an `action` → `action` owns a reader → it reads ITSELF through the same reader the .pr uses → its `Data` params ride `@schema:data` → a `goal.call` param dispatches to goal.call's reader → typed `GoalCall`, born at the read. The bag never exists.

Bad content: the target type's reader fails → that type's error. Rebuild signal. Nothing else to coordinate.

## Your four questions

1. **ReadList** — obpv (Ingi), dead. Collections keep the reflection walk; their ELEMENTS defer to owners (code block 6). No format-specific collection walk anywhere.
2. **Set context** — `Set`'s signature never grows. The write goes through the entity door; the carrier was born with its Context (`clr/this.cs:24,:44`). Values born-with-context exist precisely so signatures stop re-threading context.
3. **Placement** — neither `kind.Read(value, target, context)` nor inside `Clr`. The ownership chain above: entity resolves the reader, item hands its content, kind bridges the format. Your ref-struct constraint is honored — the kind holds its concrete reader and drives the generic `typeReader.Read` call.
4. **Generality** — automatic. Every crossing through the entity door inherits it; `md`/`csv`/`yaml` kinds get a `Read` override when they hold readers (base declines → today's path). Remaining `iv.Clr(...)` reflection lowerings: sweep as follow-up — each call site is a candidate for the entity door.

## goal.call reads itself — same change, second file

`goal/call/Reader.cs:20-21` is a plang item handing its own read to the reflection kind's `[Store]` walk. A plang item knows its own structure (Ingi: "it should know how to create itself"). Its reader body becomes explicit — same shape as action's:

```csharp
var call = new GoalCall();
reader.BeginObject();
while (reader.NextName(out var name))
    switch (name)
    {
        case "name":     ... reader.String() ...; break;
        case "parallel": ... reader.Bool() ...; break;
        case "parameter":
            reader.BeginArray();
            while (reader.NextElement()) /* dataReader.Read(reader.RawValue(), readContext) → Parameter */;
            reader.EndArray(); break;
        case "prPath":   ... path.Resolve(reader.String(), readContext.Context) ...; break;
        default:         reader.Skip(); break;
    }
reader.EndObject();
return call;
```

(Name/Parallel are `init` today — construction mechanics yours.) With this, goal.call has zero reflection in its path, and the reflection kind serves only types that don't own themselves.

## Queued cleanups (NOT this change)

- **STJ attributes are illegal vocabulary** — but can't be deleted yet: `Tagged.Compute` derives `WireName` from `[JsonPropertyName]` (`Tagged.cs:134-135`) and reads STJ's `[JsonIgnore]` (`:108`). The fix is the plang attribute carrying the name — `[Store("action")]` — and `Tagged` dropping the STJ types. Rides the attribute-vocabulary cleanup (same list as the `[JsonIgnore]` ruling in module-owns-action).
- **ReadContext** — Ingi dislikes the wrapper (context should ride the owning objects); accepted as lived-with for now. Do not extend it for this change — the design above doesn't need to.
- **Double-parse inside the json kind** (`GetRawText` → re-encode → re-read): legal (inside the format owner), but check whether the carrier can retain the raw bytes from `Parse` so the bridge reads from source. Optional.

## Sequencing

This is a **prerequisite of module-owns-action**, not a nice-to-have: that plan removes the reflection read of actions (`[Store]` comes off `Module`). The moment it lands, the old reflection path for `set %goal.step[i].action%` breaks. This change lands **before or with** it.

## Verify before writing

1. Where the new generic arm slots in the entity door relative to the existing retype/ICreate/Clr arms (`type/this.cs`) — the three Create forms are listed above; the Set path uses the courier form.
2. `App.Type[System.Type]` resolution for the wrapper nodes (`action.list.@this` must fail the identity guard, not resolve to `"list"`).
3. No recursion: after goal.call's explicit reader, no registered reader delegates back to reflection for its own type. Grep the other `serializer/Reader.cs` bodies for `reflection.@this()` — each hit is another goal.call-shaped transition artifact.
4. Registered-reader coverage for the types that will now dispatch (`action`, `step`, `goal`, `goal.call`, `data`) — all exist today per the .pr read path.
5. **Twin Set bodies**: base `item.Set` (`type/item/this.cs:168-180`) and reflection kind `Set` (`reflection/this.cs:39-51`) are near-duplicates (find-property → lower → SetValue). Both route through the entity door in this change; whether the two bodies merge into one home is a separate dedup question — flag what you find, don't expand scope.

## Net

- Two new virtuals (`item.Read(typeReader, context)`, `kind.Read(host, typeReader, context)`), one entity-door arm, one line changed in reflection `Set`, one dispatch insertion in `ReadValue`, one explicit reader body for goal.call.
- The third boundary (runtime `Set`) closes under the same law as .pr load and build ingest: the declared type drives; the type reads itself; formats are named only inside kinds; reflection serves only what owns nothing.
