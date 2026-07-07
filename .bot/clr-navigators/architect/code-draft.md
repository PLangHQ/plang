# clr + kinds — implementation spec

A `clr` value carries a foreign `object` (a `JsonElement`, an `XElement`, a POCO). What can be done with it — navigate it, convert it, load it from raw — is owned by its **kind**. A kind is a first-class entity in the type system, reached at `context.App.Type.Kind[k]`. Navigation, conversion, and even "what kind is this object" all resolve through the kind registry. Nothing materializes a structured value into `dict`/`list` up front.

This spec is the build target for the branch. Section order follows `plan.md`. Signatures are the intended shape; genuinely open choices are listed under "Open for the implementer".

---

## Conventions

- **A kind owns the behavior for its values.** `context.App.Type.Kind["json"]` is the json kind; it knows how to navigate a json value, load one from raw text, and (later) build one from a source. Adding a format is adding a kind.
- **Identifiers are `text`.** A type's name and kind, a kind's key — every identifier is a plang `text`, not a C# `string`. `text` has value `Equals`/`GetHashCode` (it keys a dictionary directly) and an implicit `string` operator (`kind == "json"` works). This is type-system-wide: `type.@this.Name`/`.Kind` are `text` too (a mechanical companion change).
- **Values cross as `Data`.** Anything a kind takes or returns as a value is `Data`.
- **`System.Type` is confined to the CLR bridge.** The one place that maps a live CLR object's type to a kind (`Type.KindOf(clrType)`) is the CLR↔plang bridge — by definition it speaks `System.Type`. No other surface does: kinds and their capabilities are addressed by `text`, never `System.Type`.
- **`Peek()` returns `item.@this`.** Never `object?`, never C# `null`. Absence is `@null.@this.Instance`.

---

## clr carries `(object, kind?)` — kind is derived, not required (plan §1)

A `clr` holds its foreign object. Its kind is optional at construction: when not stamped, the clr asks the type system what kind its object's CLR type is (`JsonElement → json`, else `*`). The creator does not need to know the kind — that is the type system's knowledge. `clr` does not navigate itself; it delegates to its kind.

```csharp
// app/type/clr/this.cs
public sealed class @this : global::app.type.item.@this, global::app.module.IContext
{
    public object Value { get; }
    public global::app.type.text.@this? Kind { get; }   // optional; derived from Value's CLR type when null

    public @this(object value, global::app.actor.context.@this context, global::app.type.text.@this? kind = null)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        Kind = kind;
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException("A Data may not be carried in a clr — nested Data is not a supported shape.");
    }

    // The effective kind: stamped, else asked of the type system by the object's CLR type.
    private global::app.type.text.@this EffectiveKind => Kind ?? Context.App.Type.KindOf(Value.GetType());

    // type = item (the lattice apex); kind = the effective kind.
    protected internal override global::app.type.@this Mint() => new("item", EffectiveKind);

    // Peek returns THIS — the clr, a plang value. The foreign object is reachable only via Clr<T>().
    public override global::app.type.item.@this Peek() => this;

    // A single key (generic per-hop walk) and a whole path (handoff) both go to the kind. A key is a one-segment path.
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
        => Navigate(parent, global::app.variable.path.@this.Parse(key));

    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, global::app.variable.path.@this path)
        => Context.App.Type.Kind[EffectiveKind].Navigate(Value, path, parent, Context);

    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate()
        => Context.App.Type.Kind[EffectiveKind].Enumerate(Value, Context);

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    // Output / Write unchanged.
}
```

**Usage.** A producer builds a `clr` and does not have to state the kind for a known shape:

```csharp
var value = new global::app.type.clr.@this(jsonElement, context);   // kind derived: json
```

```plang
- read file.json, write to %doc%          / %doc% is a clr; its kind resolves to json
- write out %doc.users[0].email%          / the json kind walks users → [0] → email → text
```

---

## A kind owns navigate / enumerate / load / build (plan §2 + §3)

`context.App.Type.Kind` is the kind registry (hung off `App.Type`, beside `Readers`/`Conversions`). `Kind[k]` returns the kind, falling back to the `*` kind when `k` is unknown. A kind is one class per format; the base gives a plang-path navigation and errors for capabilities a kind does not provide.

```csharp
// app/type/kind/this.cs — a kind: the behavior for values of one kind
namespace app.type.kind;

public abstract class @this
{
    public abstract global::app.type.text.@this Kind { get; }

    // Navigate a value OF this kind. Default: walk the plang path segment-by-segment. A kind whose
    // path language is NOT plang (a future jsonpath / css kind) overrides Navigate wholesale.
    public virtual async global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path, global::app.data.@this parent, global::app.actor.context.@this ctx)
    {
        object? node = obj;
        foreach (var seg in path.Segments)
        {
            var key = seg is global::app.variable.path.Segment.Index i ? await Key(i, ctx)
                                                                       : ((global::app.variable.path.Segment.Member)seg).Name;
            var (found, next) = Step(node!, key, ctx);
            if (!found) return ctx.NotFound(seg.Raw);
            node = next;
        }
        return Data(parent.Name, node, parent, ctx);
    }

    protected virtual (bool found, object? node) Step(object obj, string key, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' is not navigable");
    protected virtual global::app.data.@this Data(string name, object? node, global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' is not navigable");
    public virtual System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' is not enumerable");

    // Load a raw payload (string / bytes) into a value OF this kind (json parses to a clr; md → text).
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(object raw, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' has no loader");

    // Build (convert) a value OF this kind FROM a source value (audio from text). The outbound owns it.
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"cannot build '{Kind}' from {source.Type?.Name}");

    // A bracket key that is a plang variable resolves via ctx.Variable (a literal "0" passes through).
    protected static async global::System.Threading.Tasks.ValueTask<string> Key(
        global::app.variable.path.Segment.Index i, global::app.actor.context.@this ctx)
        => i.IsLiteral ? i.Inner.ToString()
                       : (await ctx.Variable.Get(i.Inner.ToString())).Peek().ToString() ?? i.Inner.ToString();
}
```

Discovery is "is a `kind`" — no namespace filter (a kind declares its own `Kind`, so nothing is inferred from where the file sits):

```csharp
var kinds = assembly.GetTypes()
    .Where(t => typeof(global::app.type.kind.@this).IsAssignableFrom(t) && t is { IsAbstract: false })
    .Select(t => (global::app.type.kind.@this)System.Activator.CreateInstance(t)!);
App.Type.Kind = new global::app.type.kind.registry(kinds);   // indexes by k.Kind; Kind[unknown] → the "*" kind
```

`Type.KindOf(clrType)` is the CLR bridge that answers "what kind is a `JsonElement`" — the one place `System.Type` is spoken. Built-ins seed it (`JsonElement → json`, `XElement → xml`, anything else → `*`); a loaded DLL adds its own.

**Usage (runtime extension).** A DLL of extra kinds is loaded and swept by the existing `code.load`, surfaced as a plang action:

```plang
- add type mytype.dll     / code.load registers each kind (and reader / converter) the assembly defines
```

---

## The json kind and the `*` kind (plan §3)

Two kinds ship. Both navigate the plang path, so both inherit the base `Navigate` and supply only the per-hop `Step`, the child `Data`, and `Enumerate`. The json kind also supplies `Load` (raw json text → a clr).

```csharp
// app/type/kind/json.cs
namespace app.type.kind;
using System.Text.Json;

public sealed class json : @this
{
    public override global::app.type.text.@this Kind => "json";

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName)) return (true, byName);
        if (e.ValueKind == JsonValueKind.Array && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength()) return (true, e[n]);
        return (false, null);
    }

    // container → a clr (its kind derives to json again); scalar → its raw CLR (the Data ctor lifts string→text, long→number)
    protected override global::app.data.@this Data(string name, object? node, global::app.data.@this? parent, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)node!;
        return e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? new global::app.data.@this(name, new global::app.type.clr.@this(e, ctx), parent: parent, context: ctx)
            : new global::app.data.@this(name, Scalar(e), parent: parent, context: ctx);
    }

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) yield return Data("", item, null, ctx);
        else if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) yield return Data(p.Name, p.Value, null, ctx);
    }

    // raw json text → a clr(json). The parse is the validation ("is this valid json").
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(object raw, global::app.actor.context.@this ctx)
    {
        using var doc = JsonDocument.Parse((string)raw);
        return new(ctx.Ok(new global::app.type.clr.@this(doc.RootElement.Clone(), ctx)));
    }

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, _ => null,
    };
}
```

```csharp
// app/type/kind/reflection.cs — the "*" kind: any object, by reflection
namespace app.type.kind;

public sealed class reflection : @this
{
    public override global::app.type.text.@this Kind => "*";

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        System.Reflection.PropertyInfo? prop = null;                 // bottom-up + DeclaredOnly + IgnoreCase
        for (var t = obj.GetType(); t != null && prop == null; t = t.BaseType)
            prop = t.GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                                     | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.DeclaredOnly);
        return prop == null ? (false, null) : (true, prop.GetValue(obj));
    }

    protected override global::app.data.@this Data(string name, object? node, global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => node is global::app.data.@this d ? d : new global::app.data.@this(name, node, parent: parent, context: ctx);

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        foreach (var p in obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            yield return new global::app.data.@this(p.Name, p.GetValue(obj), context: ctx);
    }
}
```

**Usage.** Shipping both is the first end-to-end test of the kind registry — `%doc.users[0].email%` exercises `json`, `%result.CustomProperty%` on a POCO exercises `*`. A new format is one file: `app/type/kind/yaml.cs` (`Kind => "yaml"`, its own `Step`/`Data`/`Load`) is discovered and resolvable with no other change.

The child factory is named `Data` because it returns the child `Data`. Container → `clr`; scalar → its plang scalar; never a `clr` wrapping a scalar.

---

## The parser handoff (plan §4)

When the value being navigated is a `clr`, hand it the `path` and let its kind walk the rest. The `path` handed over is already **the tail relative to the clr** — the variable store resolves the root and calls `GetChild` on the remainder, so a `clr` never sees the root variable name.

```csharp
// app/data/this.Navigation.cs
public async global::System.Threading.Tasks.ValueTask<@this> Navigate(global::app.variable.path.@this path)
{
    if (path.IsEmpty) return this;

    if (_item is global::app.type.clr.@this c)
        return await c.Navigate(this, path);      // the clr's kind walks the whole tail

    var (head, tail) = path.Split();
    // … existing Infra / Call / Index / Member per-hop walk for native dict/list and item types …
}
```

**Trace — the navigator gets the tail, not the full reference.** For `%user.address.zip%`, `Variables.Get` splits `rootName = "user"`, resolves the `user` clr, and calls `user.GetChild("address.zip")`. So the json kind receives the path `address.zip`. `app.variable.path.Parse` remains the single tokenizer for plang paths; a kind walks the resulting `path.Segments`.

**Usage.** The chain the builder needs:

```plang
- llm.query ..., write to %plan%                         / %plan% is a clr, kind json
- foreach %plan.steps%, call BuildStep planStep=%item%   / the json kind's Enumerate yields each step as a clr
  / inside BuildStep: %planStep.index% → the json kind → number
```

---

## The reader pivot — external json stays a clr (plan §5)

`object/serializer/json.cs` `Read` currently walks a `JsonElement` into a native `dict`/`list`. It hands the raw off to the json kind's loader instead — which wraps it in a `clr`:

```csharp
// app/type/object/serializer/json.cs (Read)
-   return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);        // walked → dict
+   return (await ctx.Context.App.Type.Kind["json"].Load(rawJsonText, ctx.Context)).Peek();   // raw → clr(json)
```

`item.serializer.json.Parse` is **not** removed — it is the universal DOM narrower called by the `Data` constructor, `type.Create`, the `dict`/`list`/`object` readers, and Fluid to turn raw CLR / `JsonNode` values into native values. Only this reader path stops calling it. Authored `dict`/`list` literals (`%x% = {a:1}`) use their own readers and stay native.

The wire read routes a deferred value by its declared kind, defaulting to **text** when there is no kind:

```csharp
// app/data/reader/this.cs
-   deferredFormat = reader.Peek() == TokenKind.String ? Text.Mime : "application/plang";
+   deferredFormat = typeRef?.Kind is { } k ? Mime(k)                     // declared kind wins: (item, json) → clr(json)
+                    : global::app.channel.serializer.Text.Mime;          // no kind → text; the type decides
```

Text is the default because an undeclared value is safest as text; internal-wire values carry an `@schema` marker and are read by the schema reader, a different branch. A full-match `%ref%` still borns a `variable` and is never parsed — only genuine *content* of an `item`/`object` json type becomes a `clr`.

**Usage.** `read file.json` and an `http` json response both land as a `clr` (kind json) and navigate identically to the `llm.query` result — one representation for all external structured data.

---

## Producers hand raw + kind; the kind loads it (plan §6)

A producer does not branch per format. It hands the raw payload and the kind it asked for; the kind's loader builds the right value (json parses to a clr; md loads as text).

```csharp
// app/module/llm/code/OpenAi.cs — result construction (fresh) and ParseResultValue (cached)
var result = await context.Ok(extracted, kind: format);   // Ok(raw, kind) → Type.Kind[kind].Load(raw)
```

`context.Ok(raw, kind)` is the door: it routes to `Type.Kind[kind].Load(raw)`. json → a clr; md → text; an unknown kind → text (the `*`/text loader). Nothing downstream guesses the format — the producer named it once.

**Usage.** `llm.query ..., format="json"` → a clr(json); `format="md"` → text. `xml`/`yaml` load as their kind once those kinds exist.

---

## Convert — the outbound kind owns it (plan §7)

Converting a value to another form is owned by the **target kind**, not the source: `text(md) → audio` is owned by `audio`, which knows how to build itself from text. The source never enumerates its possible targets. The call is on `Data` (everything at an action boundary is `Data`, which carries its own context), and dispatches through the kind registry:

```csharp
// app/data/this.cs
public global::System.Threading.Tasks.ValueTask<@this> Convert(global::app.type.text.@this to)
    => _context.App.Type.Kind[to].Build(this);      // the target kind builds itself from this source
```

The target kind's `Build` does the work (or returns an error `Data` when it cannot build from this source):

```csharp
// app/type/kind/audio.cs — audio owns "build audio from text"
public sealed class audio : @this
{
    public override global::app.type.text.@this Kind => "audio";
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx)
        => /* text → audio (TTS); ctx.Error(...) if source is not text */ default;
}
```

**Usage.** A handler that needs its input in a specific form asks for it; the outbound kind is found and builds it:

```csharp
// in an html-to-pdf handler with a Data<text> Md parameter sourced from read file.md:
var html = await Md.Convert("html");     // Type.Kind["html"].Build(md)
```

`json → dict` is the `dict` kind's `Build` from a json source (reuse the existing `catalog/Conversion` arm). Chained conversions (md → html → pdf) are out of scope initially; a target with no `Build` for the given source returns an error `Data`, never silently passes the source through.

---

## Guards (plan §9)

A container value must never come back a scalar — the round-trip loss behind the original blocker fails loudly at the point of loss instead of surfacing hops later:

```csharp
// app/type/item/source.cs (source.Value)
if (declaredType.IsContainer && materialized is not (dict or list or clr))
    throw new … ("a container value materialized to a scalar leaf — round-trip loss at <slot>");
```

The `Data` constructor already throws if a bare `Data` is assigned as a value (the double-wrap guard).

---

## v1 scope

v1 unblocks `plang build`: the `kind` base + registry (`context.App.Type.Kind`); the `json` and `*` kinds (navigate + enumerate, plus json `Load`); `clr` with a derived kind and pure delegation to its kind; `Type.KindOf` in the CLR bridge; the parser handoff; the reader pivot + `data/reader` default-text; `context.Ok(raw, kind)`; the guards; and the two companion changes below. Native `dict`/`list` and item types keep their existing per-hop navigation for now — routing them through the kind registry too is later.

`Build` (convert) is the next capability on the same kind registry — it ships with the first real converter (e.g. `audio`), not in the v1 unblock.

---

## Companion change: type identifiers are `text`

Kinds and type names are `text`, not `string` — so `type.@this.Name`/`.Kind` become `text`, and their consumers adjust. The payoff: the type system's own metadata is a first-class plang value like everything else, with one serialization path and inspection/comparison in plang. `text` keys the registry directly (value `Equals`/`GetHashCode`), and its implicit `string` operator keeps `kind == "json"` and interop working. Mechanical breadth (many call sites, shallow change), landed with this work so `clr.Kind` and the kind registry are not lone `string` islands.

## Companion change: `Peek()` returns `item.@this`

Every `Peek()` already returns `this`; tighten the base signature from `object? Peek()` to `item.@this Peek()`. A value is always a plang value, never C# `null` — absence is `@null.@this.Instance`. This removes null-checks at `Peek()` call sites and makes "navigation always yields a plang value" true by type. (`Data.Peek()`, a distinct surface on `Data`, is out of scope.)

---

## Open for the implementer

- **The reader-pivot seam.** Confirm the edit is in `object/serializer/json.cs` `Read` (not `Parse`, which stays). Trace `Read` vs `Parse` vs `source.Value/Build` before editing — the one place a wrong cut regresses every JSON read.
- **The base `kind` template method.** The base provides plang-path `Navigate` calling abstract `Step`/`Data`; kinds that navigate a *different* language (jsonpath, css) override `Navigate` wholesale. If a base template reads as too much, each kind can implement `Navigate` directly — the shared piece is only the segment loop + variable resolution.
- **`context.Ok(raw, kind)` vs a direct `Type.Kind[kind].Load(raw)`.** The `Ok(raw, kind)` overload is sugar over the loader; confirm it reads better than callers invoking `Load` directly.
- **`Build` error contract.** Returning an error `Data` when a source is unsupported (rather than a boolean probe) assumes the caller inspects the result. Confirm against how conversions are consumed.

The intent that must survive: a `clr` stays a `clr`; a kind owns navigate/load/build for its values; a conversion is owned by the outbound kind; and the reader pivot must never turn a `%ref%` into a `clr`.
