# Ruling 6 mechanics — the json kind already owns it; remove the detour, don't add a mapping

Branch: `wire-source-split`. Coder mapping of Ruling 6 ("the `json` kind points to no type —
json content materializes as `clr`"). Ingi's steer: *isn't json a kind — does it need mapping?*
It does not. The mapping I asked about is the wrong framing.

## The kind already IS the json behavior

`type/item/kind/json/this.cs` fully owns json — nothing is missing:

```csharp
public override ValueTask<data> Load(object raw, ctx)                 // raw text/bytes → clr(json)
    => new(ctx.Ok(object.serializer.json.Read(raw, "json", ...)));    // the single parse owner
public override Type? ClrForm => typeof(JsonElement);                 // a clr(JsonElement) IS this kind
public override Descend(...)   // navigate by key/index
public override Enumerate(...) // foreach
public override Clr(...)       // lower to a target
public override Output(...)    // write raw json inline
```

A json value should just **be `clr(json)`** and navigate/enumerate/lower/write through its own
kind. There is no type to point at, no reader to register, no `TypeOf` route to invent. This is
literally the OBP "kind IS the behavior" model already built.

## The regression is a wrong DETOUR, not a missing mapping

`source.Read` (the value-dispatch I introduced in §1) materializes through the **type-reader**
registry:

```
source.Read
  → App.Type.Reader.Reader("binary", "json", ctx)          // reader/this.cs
  → binary→kind narrow: new kind("json").Type.Name          // kind/this.cs:50 → TypeOf("json") = "object"
  → narrows to the legacy OBJECT type
  → object/serializer/Reader.cs  → parser.ReadSlot(String)  // returns the json STRING, UNPARSED
```

The json kind's `Load` (which returns `clr(json)`) is never called. The OLD serializer-dispatch
(`Json.Read`) happened to parse; value-dispatch takes the object detour instead. That single
detour is the ~34 regressions (Runtime narrowing `ContentKindInference`/`IsDict`/`JsonContainer`,
Modules `Query_*` where params arrive as unparsed text and `text.Clr` throws, config-nav → null).

## So Ruling 6, concretely

1. **`TypeOf("json")` stops answering `object`.** `kind/this.cs:50` (`TypeOf(Name) ?? Format.TypeOf(Name) ?? "binary"`) then resolves the json kind's `.Type` to `binary` — a `{binary, json}` value stays `{binary, json}`, it does not narrow to the legacy type. `object` shrinks (removal stays separate).
2. **A json-kinded content source materializes through its KIND's `Load` → `clr(json)`**, not the type-reader. The kind then owns navigate/enumerate/clr/output on that `clr`.

## The one mechanic I want your eyes on — where source defers to the kind

`source.Read` today unconditionally uses the type reader. The question is where a kinded value's
materialization routes to `Kind.Load` instead. Two shapes:

- **A — source asks the kind first when the kind owns a non-default `Load`.** A small branch in
  `source.Read`: if `_type.Kind` has its own decode (json, and future kinds), `await
  _type.Kind.Load(_value, Context)`; else the type reader. Risk: reads as a fork
  (`kind.Load` vs type-reader) unless framed as "the kind is the first materialization owner."
- **B — the type reader for `binary`/kinded delegates to the kind.** Keep `source.Read` calling
  the reader; the binary reader (or the narrow step) routes a kind-that-owns-Load through
  `Kind.Load`. Keeps `source.Read` uniform; moves the choice into the reader/narrow layer.

Both remove the object detour. A puts the kind-vs-reader choice on the source; B hides it in the
reader layer. My lean is **A** but framed as "the kind is the materialization owner, the type
reader is the family fallback" so it's one owner, not a fork — but this is the semantic call I
don't want to make solo (it's the same "who owns materialization: kind or reader" question the
whole value-dispatch rests on).

`Load`'s signature is `ValueTask<data>` (async) while `source.Read` is sync today — so routing to
the kind makes `source.Read`/`source.Value`'s parse async at this node (Value is already async;
Read is the sync helper). Flagging that as a real consequence to confirm.

## Not in this: the object removal
`object` shrinking is a side effect; its full removal stays the separate task with the stopper.
The dict-nested cluster (`DictOfTypedEntries`, `PlanDict`, `AsT_DictWithNestedVars`) is a
different, smaller root — I take it independently.

— coder
