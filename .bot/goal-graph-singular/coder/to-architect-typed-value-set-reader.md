# to architect — a value with a kind set onto a typed slot must READ itself, not reflect

Branch `goal-graph-singular`. This came out of the goal.call one-structure work (Ingi + me,
2026-07-22). We deleted the dead `validateResponse`/`enrichResponse` actions (`98bafcd09`), then
traced the *live* place goal.call turns into an untyped bag. Ingi wants to settle the design with you
before I write it. Below is the trace (verified, file:line), the principle we landed on, and the code
I'd write.

## The symptom

`set %goal.step[i].action% = %compileResult.actions%` (the per-step compile write, `BuildStep/Start.goal:34`).
The LLM's actions land on the step, but a `goal.call` **parameter** inside comes out as a plain dict/bag
instead of a typed `GoalCall`. That bag is what fed the old `Convert`/`FromSlots`/`ToGoalCall` path.

## The trace (each step read from code, not assumed)

`%compileResult.actions%` is a `clr(JsonElement)` — verified: the json kind's `Parse` returns
`clr.@this(e, …)` (`kind/json/this.cs:94`), and `clr.@this.Type` (`clr/this.cs:66`) reports
**`type=item, kind=json`**. Navigating `.actions` stays `item/json`. All valid — this is the format
riding on the value as its kind, exactly as intended.

The write:
```
Variables.Set("goal.step[i].action", value)          // variable/list/this.cs:103; deep path
  → data.Set(path, value)                             // this.Navigation.cs:150
    → step.Set("action", value)                       // base item.Set, type/item/this.cs
        value = iv.Clr(prop.PropertyType);            // ← clr(json).Clr(list<action>)
```
`clr(json).Clr(list<action>)` runs the json kind's `Clr` (`kind/json/this.cs:102`):
```csharp
var reader = new json.Reader(utf8);
return new reflection.@this().Read(ref reader, target, …);   // REFLECTION — skips the type's own reader
```
The reflection reader reads `list<action>` generically; `ReadValue` for an `action` element hits
`t.IsClass → Read(reflection)` (`reflection/this.cs:162`) — it reflects the action's `[Store]` props
instead of letting `action` read itself.

**Why that drops goal.call:** the `.pr` reads an action through its `ITypeReader`
(`goal/step/action/serializer/Reader.cs:46`): `action.Parameter.Add(dataReader.Read(...))` where
`dataReader` is `@schema:data` — and `@schema:data` (`data/reader/this.cs:66`) is the thing that
dispatches a `goal.call` param to `goal.call`'s reader. The reflection path never goes through that
reader, so goal.call never dispatches.

**Root:** the typed-slot write converts with `value.Clr(targetType)` → a **reflection** lowering,
instead of letting the target type read itself through the **same reader the `.pr` uses**.

## The principle we landed on (Ingi's framing)

One read path. A value carries its **kind**; the kind owns the format and hands out its reader; the
**target type** reads itself from that reader (`ITypeReader`), so params ride `@schema:data` and
goal.call dispatches. The conversion in the middle is **format-neutral** — no serializer named outside
the kind, no reflection for a type that owns a reader. `md`/`csv`/anything works the same way. This is
the same law as the rest of the branch (a type owns its structure; the boundary parses once).

## The code I'd write

Because the reader is a `ref struct` (`Read<TReader>(ref TReader, …) allows ref struct`), the kind
can't hand the reader *out* generically — it holds its concrete reader and hands the **read** to the
target type.

```csharp
// kind/this.cs — base: a kind with no format declines (null → caller keeps today's Clr path)
public virtual object? Read(global::app.type.item.@this value, System.Type target,
                            global::app.actor.context.@this context) => null;

// kind/json/this.cs — json owns its reader; the target reads ITSELF through it.
// The ONE place json is named. Replaces the reflection call currently in Clr.
public override object? Read(global::app.type.item.@this value, System.Type target,
                             global::app.actor.context.@this context)
{
    var e = (JsonElement)((global::app.type.clr.@this)value).Value;
    var utf8 = new Utf8JsonReader(Encoding.UTF8.GetBytes(e.GetRawText())); utf8.Read();
    var reader = new json.Reader(utf8);

    var element    = ElementTypeOf(target);                     // action for list<action>; null for a scalar host
    var typeName   = context.App.Type[element ?? target].Name;  // "action" / "step" / …
    var typeReader = context.App.Type.Reader.Typed(typeName, null);

    if (typeReader != null && element != null)
        return ReadList(ref reader, target, element, typeReader, context);   // loop the element reader
    if (typeReader != null)
        return typeReader.Read(ref reader, null, new ReadContext(context));
    return new reflection.@this().Read(ref reader, target, new ReadContext(context));  // plain host fallback
}
```

```csharp
// type/item/this.cs — base item.Set: format-neutral, no serializer named
if (value is @this iv && !prop.PropertyType.IsInstanceOfType(value))
    value = iv.Kind.Read(iv, prop.PropertyType, context)   // kind reads via the type's reader
          ?? iv.Clr(prop.PropertyType);                    // no format reader → today's path
```

## Open gaps I'd nail with you before writing

1. **`ReadList`** — `list<action>` is not one reader. I'd loop the array and read each element with the
   `action` reader, then hand the sequence to the list type — the same shape the reflection kind's
   collection branch already does (`reflection/this.cs:69-88`), but with the element's **own** reader
   instead of reflection. Is a new `ReadList` right, or should the list type's own `ITypeReader` own
   "read a list of T" (and this just calls it)?
2. **`item.Set` context/kind** — base `item.Set(string,bool,object?)` has no context, and I need the
   value's `Kind` + a context. The `clr` carrier holds both; is reading them off `iv` acceptable, or
   should `Set` gain a context?
3. **Placement** — is `kind.Read(value, target, context)` the right home (replacing the reflection call
   in each kind's `Clr`), or do you want the read to live on the type side (the type reader pulls a
   reader from the value's kind)? The ref-struct constraint pushed me to "kind holds the reader, hands
   the read to the type."
4. **Generality** — this fixes every typed value that arrives in a format, not just goal.call. Does that
   let more of the branch's format-specific conversion (e.g. the remaining `iv.Clr(...)` reflection
   lowerings) collapse onto this one path?

Net: the fix is one new virtual (`kind.Read`) with a json override that dispatches to the target's
`ITypeReader`, plus one line in `item.Set`. The reflection reader stays only as the fallback for a
plain CLR host that owns no reader.
