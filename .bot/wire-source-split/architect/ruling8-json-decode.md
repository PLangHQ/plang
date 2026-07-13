# Ruling 8 — one json decode: sync `Parse` on the kind, `Load` its async face; `wire.Clr` graduates

Answers [`coder/json-decode-fork.md`](../coder/json-decode-fork.md). Settled with Ingi 2026-07-13.

**You own this.** Every code block below is a suggestion — bodies, factoring, and mechanics are yours. Members cited with `file:line` were verified against HEAD (f3a93b7ff) during the ruling; re-verify as you go.

## The answer in one line

Yes — ONE decode, owned by the kind. The sync/async wall that blocked you is signature-only: `Kind[json].Load`'s body is already sync (`JsonDocument.Parse`, `kind/json/this.cs:76`). Extract it as a sync `Parse` door on the kind base; `Load` becomes its async face. One parse, one discriminator (`Parse` returns null = decline), two faces.

## Flow

```
async entrance (unchanged):
  source.Value()                       source.cs:135 — the 6c rung stays as-is
    └─ Kind[json].Load(raw)            body becomes: Parse(raw) wrapped in ctx.Ok
         └─ clr(json) | native scalar

sync entrance (the fix — wire graduates instead of handing its raw slice):
  wire.Clr(typeof(Product))            NEW override
    └─ Kind[json].Parse(raw)           NEW sync door — the same body Load has today
         └─ clr(json)                  the graduated value
              └─ clr.Clr(Product)      clr/this.cs:136 → Kind.Clr
                   └─ json.Clr         kind/json/this.cs:90 → reflection read → Product

decline (csv, png, …): Parse returns null → falls to Read() → type reader (table, image) — unchanged.
```

## Code (suggestions)

Kind base (`app/type/kind/this.cs`; no existing `Parse` member — verified) — a kind that owns its decode answers sync; the default declines:

```csharp
// NEW
public virtual global::app.type.item.@this? Parse(object raw, actor.context.@this ctx) => null;
```

json kind — `Load`'s body MOVES into `Parse`; `Load` (base signature `kind/this.cs:124`) wraps it:

```csharp
// NEW (body is today's Load, kind/json/this.cs:70-85, returning items instead of Data)
public override global::app.type.item.@this? Parse(object raw, actor.context.@this ctx)
{
    if (raw is not (string or byte[])) return null;              // decline — unchanged
    var s = new global::app.type.item.text.@this(raw).ToString();
    // yours to place: the empty-string case (Load today answers ctx.Null()) and the
    // scalar-root lift (Load's ctx.Ok(Scalar(e)) lifted raw CLR; Parse returns items)
    using var doc = JsonDocument.Parse(s);
    var e = doc.RootElement;
    return e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
        ? new global::app.type.clr.@this(e.Clone(), ctx, this)
        : ctx.App.Type.Create(Scalar(e), ctx);
}

// NEW — the async face; the decline (null) rides through unchanged
public override global::System.Threading.Tasks.ValueTask<global::app.data.@this?> Load(
    object raw, actor.context.@this ctx)
    => Parse(raw, ctx) is { } item
        ? new(ctx.Ok(item))
        : new((global::app.data.@this?)null);
```

wire — graduate, then answer from the graduated value. NOT the throw from the memo's lean §3 — superseded; graduation is cheap and sync:

```csharp
// NEW override on wire (today it inherits source.Clr = ClrConvert(_value, target),
// source.cs:238 — which hands the ENCODED SLICE to the converter; that's the bug)
internal override object? Clr(System.Type target)
{
    var item = Type.Kind is { } k
        ? Context.App.Type.Kind[k.Name].Parse(Raw, Context)
        : null;
    return (item ?? Read()).Clr(target);
}
```

Same rung collapses the second entrance (6c's "both entrances" requirement, now shape-identical — a relayed json wire materializes to the SAME clr(json) that `Value()` gives):

```csharp
// wire.Write, non-owning branch (wire/this.cs:31-35) — Read() gains the kind rung
public override void Write(global::app.channel.serializer.IWriter w)
{
    if (_reader.Owns(w)) { w.Raw((string)Raw); return; }
    var item = Type.Kind is { } k
        ? Context.App.Type.Kind[k.Name].Parse(Raw, Context)
        : null;
    (item ?? Read()).Write(w);
}
```

If you factor the repeated rung, it's one private member on wire — name yours (one verb).

## The fork, re-counted — most "B sites" are NOT decoders and STAY

`item.serializer.json.Parse` has no string branch — a plain string passes through untouched (`item/serializer/json.cs:80-128`; the `data/this.cs:241-243` comment says so: "a plain string stays a string for the type to decide"). So these sites narrow **already-in-memory STJ/CLR graphs**; none decodes a json-kinded raw, none competes with the kind:

| site | what it narrows |
|---|---|
| `data/this.cs:243,349` | JsonElement/JsonNode handed to the Data ctor |
| `list/this.cs:330` | raw C# IDictionary/IList via SerializeToElement |
| `kind/dict/this.cs:62` | the explicit `as dict` (ruling 6) — input is the JsonElement out of a clr(json) |
| `kind/reflection/this.cs:125` | slot mechanics inside the [Store] host walk |
| `module/ui/code/Fluid.cs:187` | JsonNode at the 3rd-party boundary |
| dict/list typed readers (`dict/serializer/Reader.cs`, `list/serializer/Reader.cs`) | `.pr` wire mechanics for NATIVE containers — the json there is the channel's encoding, not the value's kind |

Two jobs, two owners: decode raw json-kinded text/bytes → the kind's `Parse`. Narrow an in-memory graph → `item.serializer.json`. The class stays; it just stops being thought of as a decoder.

## Deletes

- `type.@this.Convert(string)` (`type/this.cs:456-483`) — zero callers (grepped PLang, PLang.Generators, PlangConsole). obp-findings already kills its json arm; verify whole-member liveness and delete what's dead. The stale "falling back to the type's own Convert for a string raw" doc comment on source (`source.cs:95-98`) goes with it.
- `object/serializer/Reader.cs` — already condemned by 6b's trajectory (object is not a plang type). No new work here; just don't route anything new through it.

## In scope for this branch

The kind `Parse` extraction, the two wire overrides, the dead `Convert`. That's the whole collapse — not the "8+ call sites" chunk the memo feared.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `Parse` | one verb, caller's intent, no format name | ✓ |
| `Load` = async face over `Parse` | no second decode; the decline discriminator (null) unchanged from 6c | ✓ |
| `wire.Clr` | the value answers its own lowering by graduating — no courier peek, and today's raw-slice leak through inherited `ClrConvert` dies | ✓ |
| rung in both `Clr` and `Write` | if factored, one private member on wire — no helper class | ✓ |
| `item.serializer.json` | keeps one job (in-memory narrow); registry decoding of json-kinded raw ends | ✓ |
