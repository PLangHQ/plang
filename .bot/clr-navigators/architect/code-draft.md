# clr + kinds under type — implementation spec

A `clr` value carries a foreign `object` (a `JsonElement`, an `XElement`, a POCO). What can be done with it — navigate it, load it from raw, build it by conversion — is owned by its **kind**, and a kind lives **under its type**: `context.App.Type[t].Kind[k]`. Navigation, conversion, and kind-derivation all resolve through it. Nothing materializes a structured value into `dict`/`list` up front.

This is the build target for the branch. Section order follows `plan.md`. Signatures are the intended shape; genuinely open choices are listed under "Open for the implementer".

---

## Conventions

- **A `(type, kind)` owns the behavior for its values.** `context.App.Type["item"].Kind["json"]` is the json kind under `item`; it navigates a json value, loads one from raw, and (later) is where a target kind builds one from a source. Navigation kinds live under `item` (the apex type, whose kinds are the formats); convert kinds live under their target type (`Type["text"].Kind["html"]`, `Type["audio"].Kind["mp3"]`).
- **`Type[t].Convert` uses the type's default kind.** `Type["audio"].Convert(md)` picks audio's default kind and converts on it; `Type["audio"].Kind["mp3"].Convert(md)` is explicit.
- **Values cross as `Data`.** Anything a kind takes or returns as a value is `Data`.
- **Reuse the one owner.** One json parse owner (`object/serializer/json.cs:Read`), one bracket-variable resolver (`Segment.Index.ResolveKey`), one CLR→kind map (`KindHooks` + `ResolveName`). Do not add parallels.

Deferred to their own branches after the unblock is green (see v1 scope): identifiers→`text`, `Peek`→`item`, and Convert (ships with the first real converter).

---

## clr carries `(object)` — type and kind derived (plan §1)

A `clr` holds its foreign object. Its type and kind are derived from the object, not supplied: `JsonElement → (item, json)`, a POCO → `(item, *)`. A producer may stamp a kind for an ambiguous raw form, but a structured host needs none. `clr` does not navigate itself — it delegates to its `(type, kind)`.

```csharp
// app/type/clr/this.cs
public sealed class @this : global::app.type.item.@this, global::app.module.IContext
{
    public object Value { get; }
    public global::app.type.text.@this? StampedKind { get; }   // optional; only for ambiguous raw forms

    public @this(object value, global::app.actor.context.@this context, global::app.type.text.@this? kind = null)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        StampedKind = kind;
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException("A Data may not be carried in a clr — nested Data is not a supported shape.");
    }

    // The effective (type, kind): the apex type item, and the kind asked of the type system
    // by the object's CLR type (reuse KindHooks/ResolveName — no third CLR→kind path).
    private string EffType => "item";
    private string EffKind => StampedKind?.Value ?? Context.App.Type.KindOf(Value.GetType());   // JsonElement → "json", else "*"

    protected internal override global::app.type.@this Mint() => new(EffType, EffKind);

    public override global::app.type.item.@this Peek() => this;   // the clr is a plang value; the object is reachable only via Clr<T>()

    // Both a single key (generic per-hop walk) and a whole path (handoff) go to the kind.
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
        => Navigate(parent, global::app.variable.path.@this.Parse(key));

    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, global::app.variable.path.@this path)
        => Context.App.Type[EffType].Kind[EffKind].Navigate(Value, path, parent, Context);

    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate()
        => Context.App.Type[EffType].Kind[EffKind].Enumerate(Value, Context);

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    // Output / Write unchanged.
}
```

**Usage.** A producer builds a `clr`; the kind is derived, not passed:

```csharp
var value = new global::app.type.clr.@this(jsonElement, context);   // (item, json)
```

```plang
- read file.json, write to %doc%          / %doc% is a clr (item, json)
- write out %doc.users[0].email%          / Type["item"].Kind["json"] walks users → [0] → email → text
```

---

## A kind (under its type) owns navigate / enumerate / load / build (plan §2 + §3)

`type.@this` already carries a `Kinds` name-vocabulary. It gains a `Kind[k]` **accessor** returning the behavior owner for `(this type, k)`. A kind's behavior is one class per format; the base provides a default plang-path walk and errors for capabilities a kind does not provide.

```csharp
// on app/type/this.cs — a type's kinds, addressable
public global::app.type.kind.@this Kind[string name] => Context.App.Type.KindOf(this, name);   // resolves the (this, name) behavior
```

```csharp
// app/type/kind/<base> — the behavior of a (type, kind). Reconcile with the existing kind value token
// (app/type/kind/this.cs) per "Open for the implementer".
public abstract class behavior
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
            var key = seg is global::app.variable.path.Segment.Index i
                ? await i.ResolveKey(ctx.Variable)                        // reuse the one resolver — no second Key(...)
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

    // Load a raw payload into a value OF this kind — DELEGATES to the single reader, no second parse.
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(object raw, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Kind}' has no loader");

    // Build (convert) a value OF this kind FROM a source (audio from text). The outbound owns it.
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"cannot build '{Kind}' from {source.Type?.Name}");
}
```

**Usage (runtime extension).** A DLL of extra kinds is loaded and swept by the existing `code.load`, surfaced as a plang action:

```plang
- add type mytype.dll     / code.load registers each kind (and reader) the assembly defines
```

---

## The json kind and the `*` kind, under `item` (plan §3)

Both navigate the plang path (they inherit the base `Navigate`), supplying only the per-hop `Step`, the child `Data`, and `Enumerate`. The json kind's `Load` delegates to the single json reader.

```csharp
// app/type/kind/json.cs — the (item, json) behavior
using System.Text.Json;

public sealed class json : behavior
{
    public override global::app.type.text.@this Kind => "json";

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName)) return (true, byName);
        if (e.ValueKind == JsonValueKind.Array && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength()) return (true, e[n]);
        return (false, null);
    }

    // container → a clr (its (type,kind) derives to (item, json) again); scalar → its raw CLR (the Data ctor lifts string→text, long→number)
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

    // raw json → a clr. Delegates to the single parse owner (object/serializer/json.Read) — no JsonDocument.Parse here.
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Load(object raw, global::app.actor.context.@this ctx)
        => new(ctx.Ok(global::app.type.@object.serializer.json.Read(raw, "json", new global::app.type.reader.ReadContext(ctx))));

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, _ => null,
    };
}
```

```csharp
// app/type/kind/reflection.cs — the (item, *) catch-all: any object, by reflection
public sealed class reflection : behavior
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

**Usage.** Shipping both is the first end-to-end test of `Type[t].Kind[k]` — `%doc.users[0].email%` exercises `Type["item"].Kind["json"]`, `%result.CustomProperty%` on a POCO exercises `Type["item"].Kind["*"]`. A new format is one file: `app/type/kind/yaml.cs` (its own `Step`/`Data`/`Load`) is discovered and resolvable with no other change.

The child factory is named `Data` because it returns the child `Data`. Container → `clr`; scalar → its plang scalar; never a `clr` wrapping a scalar.

---

## The parser handoff (plan §4)

When the value being navigated is a `clr`, hand it the `path` and let its `(type, kind)` walk the rest. The `path` handed over is already **the tail relative to the clr** — the variable store resolves the root and calls `GetChild` on the remainder, so a clr never sees the root variable name.

```csharp
// app/data/this.Navigation.cs
public async global::System.Threading.Tasks.ValueTask<@this> Navigate(global::app.variable.path.@this path)
{
    if (path.IsEmpty) return this;

    if (_item is global::app.type.clr.@this c)
        return await c.Navigate(this, path);      // → Type[c.type].Kind[c.kind].Navigate(...)

    var (head, tail) = path.Split();
    // … existing Infra / Call / Index / Member per-hop walk for native dict/list and item types …
}
```

**Trace.** For `%user.address.zip%`, `Variables.Get` splits `root = "user"`, resolves the `user` clr, and calls `user.GetChild("address.zip")`. So `Type["item"].Kind[k].Navigate` receives the path `address.zip`. `app.variable.path.Parse` remains the single tokenizer; a kind walks `path.Segments`.

**Usage.** The chain the builder needs:

```plang
- llm.query ..., write to %plan%                         / %plan% is a clr (item, json)
- foreach %plan.steps%, call BuildStep planStep=%item%   / Type["item"].Kind["json"].Enumerate yields each step as a clr
  / inside BuildStep: %planStep.index% → Type["item"].Kind["json"] → number
```

---

## Blocker 1 — the apex must not mask a richer type (plan §5)

The `%plan%` slot is `variable.set(Name=%plan%, Value=%!data%, Type=object)` (`plan.pr:652`). `Type=object` stamps the plan's Data as the apex type, dropping its intrinsic `item/json`; the wire then carries `object` and read-back has nothing to reconstruct from.

Fix at the source: **declaring a value's type as `object`/`item` (the apex) must not overwrite the value's intrinsic type** — "this is an object" is always true and carries no information. `variable.set(Type=object)` on an `item/json` value leaves it `item/json`, so the wire carries `item/json` and the existing kind-routing reconstructs the clr on read-back.

```csharp
// app/module/variable/set.cs — the Type clause
// When the declared type is the apex (object/item) and the value already has a more specific
// intrinsic type, keep the value's type — do not re-stamp it to the apex.
if (declared.IsApex && value.Type is { } intrinsic && intrinsic.MoreSpecificThan(declared))
    /* keep value.Type */;
else
    /* existing mint-to-declared path */;
```

No reader-side container heuristic is needed once the apex stops masking. (Converting a value *to* `System.Object` is already identity in `TryConvert`; the loss is the Data's *advertised* declared type, not the value — so the seam is the `variable.set` Type clause / mint path. Pin the exact line.)

A full-match `%ref%` still borns a `variable` in `type.Build` (`type/this.cs:265`), a different branch — this cannot turn a `%ref%` into a clr.

---

## The producer hands raw + kind; the kind loads it (plan §6)

A producer does not branch per format. `context.Ok(raw, kind)` routes to the kind's loader — which for json delegates to the single reader (no second parse):

```csharp
// app/module/llm/code/OpenAi.cs — result construction (fresh) and ParseResultValue (cached)
var result = await context.Ok(extracted, kind: format);   // Ok(raw, kind) → Type["item"].Kind[kind].Load(raw)
```

`json` → a clr; `md` → text; an unknown kind → text. Nothing downstream guesses the format. (Rename the local `effectiveFormat` → `format`.)

**Usage.** `llm.query ..., format="json"` → a clr (item, json); `format="md"` → text.

---

## The reader pivot (plan §5 support)

`object/serializer/json.cs:Read` decodes+parses json today. It returns a `clr` instead of walking to a `dict`:

```csharp
// app/type/object/serializer/json.cs (Read)
-   return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);   // walked → dict
+   return new global::app.type.clr.@this(parsed, ctx.Context);                     // wrapped → clr (kind derives json)
```

`Read` stays the single json parse owner; `item.serializer.json.Parse` (the universal DOM narrower — Data ctor, `type.Create`, dict/list/object readers, Fluid) is **not** removed. Authored `dict`/`list` literals use their own readers and stay native.

---

## Convert — the outbound `(type, kind)` owns it (plan §7) — deferred, shape only

Convert ships with the first real converter, not the v1 unblock. Shape, so the v1 structure leaves room for it:

```csharp
// app/data/this.cs — the target (type, kind), under its type, builds itself from this source
public global::System.Threading.Tasks.ValueTask<@this> Convert(global::app.type.text.@this kind)
    => _context.App.Type[TargetTypeOf(kind)].Kind[kind].Build(this, _context);
```

```csharp
var html = await Md.Convert("html");     // Type["text"].Kind["html"].Build(md)
var mp3  = await Speech.Convert("mp3");   // Type["audio"].Kind["mp3"].Build(text)
```

`Type[t].Convert(source)` uses the type's default kind; `Type[t].Kind[k].Convert(source)` is explicit. `Build` reuses the existing `type.@this.Convert` / `Conversions` door and returns the built value or an **error `Data`** when it can't build from the source. `json → dict` is `dict`'s build from a json source (reuse the existing `catalog/Conversion` arm).

---

## Guards (plan §9)

```csharp
// app/type/item/source.cs (source.Value) — a container must never come back a scalar (the round-trip loss behind the bug)
if (declaredType.IsContainer && materialized is not (dict or list or clr))
    throw new … ("a container value materialized to a scalar leaf — round-trip loss at <slot>");
```

The `Data` constructor already throws if a bare `Data` is assigned as a value (the double-wrap guard, `clr/this.cs:26` / `type/this.cs:445`).

---

## v1 scope

v1 unblocks `plang build`: the apex-doesn't-mask fix (§5) — likely the whole `IndexNotSet` clear on its own; `Type[t].Kind[k]` with the `json` and `*` kinds under `item` (navigate + enumerate, json `Load`); `clr` with a derived `(type, kind)` and pure delegation; the reader pivot + the parser handoff; the container-materializes-to-scalar guard; `context.Ok(raw, kind)`. Native `dict`/`list` and item types keep their existing per-hop navigation for now.

**Deferred to their own branches after green:** `identifiers → text` (deep — wire serializer, primitive tables, `Canonicalise`/`Compare`; `text` keys the registry fine without it), `Peek → item.@this` (a `source` contract change — `source.Peek()` returns raw CLR by design), and **Convert** (ships with the first real converter, on the target `(type, kind)`).

---

## Open for the implementer

- **The kind behavior vs the existing `kind` value token.** `app/type/kind/this.cs` is the kind *value token* (names a kind, maps kind→type via readers). This spec adds navigate/load/build behavior addressed as `Type[t].Kind[k]`. Reconcile: extend that token (unseal + subclass per format), or have it delegate to a registered per-`(type,kind)` behavior — your call; it decides the file layout. Reuse `KindHooks.Of` + `ResolveName` for `KindOf` (don't add a third CLR→kind path; and `KindHooks.Of` is a poor name — rename if it surfaces).
- **The apex-doesn't-mask seam (§5).** Pin whether it's the `variable.set` Type clause or the mint path that re-stamps to the apex; that is the one edit the whole branch turns on. Verify against `plan.pr:652`.
- **The base `behavior` template.** It removes the duplicate walk between `json` and `*`. If a base template reads as too much for two kinds, each can implement `Navigate` directly — the shared piece is only the segment loop.
- **`context.Ok(raw, kind)` vs a direct `Type[t].Kind[k].Load(raw)`.** The `Ok(raw, kind)` overload is sugar over the loader; confirm it reads better than callers invoking `Load`.

The intent that must survive: a `clr` stays a `clr`; a `(type, kind)` owns navigate/load/build for its values; convert's outbound owns it, reusing `type.Convert`; the apex never masks a richer type; and the reader pivot never turns a `%ref%` into a `clr`.
