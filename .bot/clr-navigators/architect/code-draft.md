# clr + kind navigators — implementation spec

A `clr` value carries a foreign `object` (a `JsonElement`, an `XElement`, a POCO) and a **kind** naming its shape. It becomes navigable and convertible **lazily**, through per-kind implementations resolved from a registry — no eager materialization into `dict`/`list`.

This spec is the build target for the branch. Section order follows `plan.md`. Signatures are the intended shape; where a name or seam is genuinely open it is called out under "Open for the implementer" at the end.

---

## Conventions this spec follows

- **Type identifiers are `text`.** A type's name and kind, a navigator's kind, a converter's target — every identifier in the type system is a plang `text`, not a C# `string`. `text` implements value `Equals`/`GetHashCode` and an implicit `string` operator, so it works as a dictionary key and compares against string literals directly. This is a type-system-wide convention (`type.@this.Name`/`.Kind` are `text` too — see "Companion change: type identifiers" below).
- **Values cross as `Data`.** Anything a navigator or converter takes or returns as a *value* is `Data`. Its inputs and outputs are plang, not raw CLR.
- **CLR interop stays C#.** A method that matches *C# reflection types* (`System.Type`) is C# by nature — it is about the CLR, not about plang values, so it uses `System.Type`/`bool`.
- **`Peek()` returns the plang value.** `item.@this.Peek()` returns `item.@this` — never `object?`, never C# `null`. Absence is `@null.@this.Instance`. (See "Companion change: Peek".)

---

## clr carries `(object, kind)` and delegates navigation (plan §1)

`clr` holds its foreign object and a stamped kind. It does not navigate itself — it resolves the navigator for its kind/object from the registry and delegates. There is no `is JsonElement` switch anywhere on `clr`.

```csharp
// app/type/clr/this.cs
public sealed class @this : global::app.type.item.@this, global::app.module.IContext
{
    public object Value { get; }
    public global::app.type.text.@this? Kind { get; }   // stamped format ("json"/"yaml"/…); null → derived from the CLR type

    public @this(object value, global::app.actor.context.@this context, global::app.type.text.@this? kind = null)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        Kind = kind;
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException("A Data may not be carried in a clr — nested Data is not a supported shape.");
    }

    // type = item (the lattice apex); kind = the stamped format, else the CLR identity.
    protected internal override global::app.type.@this Mint()
        => new("item", Kind?.Value
                       ?? Context.App.Type.ResolveName(Value.GetType())
                       ?? Value.GetType().FullName
                       ?? Value.GetType().Name);

    // Peek returns THIS — the clr, a plang value. The foreign object is reachable only via Clr<T>().
    public override global::app.type.item.@this Peek() => this;

    // A single key (from the generic per-hop walk) and a whole path (from the handoff) both
    // resolve the navigator and delegate. A key is a one-segment path.
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
        => Navigate(parent, global::app.variable.path.@this.Parse(key));

    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, global::app.variable.path.@this path)
        => Nav().Navigate(Value, path, parent, Context);

    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate()
        => Nav().Enumerate(Value, Context);

    private global::app.type.INavigator Nav()
        => Context.App.Type.Navigators.For(Kind, Value.GetType());   // never null — the "*" navigator is the catch-all

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    // Output / Write unchanged.
}
```

**Usage.** A producer stamps the kind when it builds the value:

```csharp
// llm.query / file.read of .json / an http json response:
var value = new global::app.type.clr.@this(jsonElement, context, kind: "json");
```

A plang program then navigates it as any other value — the json navigator resolves the path:

```plang
- read file.json, write to %doc%          / %doc% is clr(kind=json)
- write out %doc.users[0].email%          / json navigator walks users → [0] → email → text
```

---

## The navigator interface and registry (plan §2 + §3)

The interface lives at `app/type/INavigator.cs` — navigation is not clr-specific; other value types adopt it over time. A navigator turns a raw `object` into plang values: its input is the foreign object, its output is `Data`. It receives the already-tokenized `path` (parsed once by `app.variable.path.Parse`) and walks it — no re-parse. It resolves any plang variable it meets in the path through `context.Variable`, because only the navigator knows what in its own path language is a variable versus syntax.

```csharp
// app/type/INavigator.cs
namespace app.type;

public interface INavigator
{
    global::app.type.text.@this Kind { get; }     // "json"; "*" for the catch-all
    bool Handles(System.Type clr);                // does this navigator claim an unstamped object of this CLR type?

    // Walk `obj` by `path`, producing plang values: a container → clr(kind); a leaf → its scalar.
    global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path, global::app.data.@this parent, global::app.actor.context.@this ctx);

    // A container's children, for foreach — each element a Data.
    System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx);
}
```

`Handles` takes `System.Type` and returns `bool` because it matches C# reflection types — the CLR-interop surface, not a plang value. `Kind` is a plang identifier, so `text`.

The registry resolves a navigator by kind (the fast path), then by the object's CLR type (an unstamped `JsonElement` still finds the json navigator), then the `"*"` catch-all:

```csharp
// app/type/navigator/this.cs
namespace app.type.navigator;

public sealed class @this
{
    public static readonly global::app.type.text.@this Any = "*";
    private readonly System.Collections.Generic.Dictionary<global::app.type.text.@this, global::app.type.INavigator> _byKind = new();
    private readonly System.Collections.Generic.List<global::app.type.INavigator> _all = new();
    private global::app.type.INavigator _default = null!;   // the "*" navigator

    public @this(System.Collections.Generic.IEnumerable<global::app.type.INavigator> navigators)
    {
        foreach (var n in navigators)
        {
            _all.Add(n);
            _byKind[n.Kind] = n;
            if (n.Kind == Any) _default = n;
        }
    }

    public global::app.type.INavigator For(global::app.type.text.@this? kind, System.Type clr)
    {
        if (kind is not null && _byKind.TryGetValue(kind, out var byKind)) return byKind;
        foreach (var n in _all) if (n.Handles(clr)) return n;
        return _default;                       // "*" — always resolves
    }

    public void Register(global::app.type.INavigator n) { _all.Add(n); _byKind[n.Kind] = n; }   // for DLLs loaded at runtime
}
```

**Discovery.** A navigator is found by the one thing that matters — it implements `INavigator`. There is no namespace filter; a navigator declares its own `Kind`, so nothing needs to be inferred from where the file sits:

```csharp
var navigators = assembly.GetTypes()
    .Where(t => typeof(global::app.type.INavigator).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
    .Select(t => (global::app.type.INavigator)System.Activator.CreateInstance(t)!);
App.Type.Navigators = new global::app.type.navigator.@this(navigators);
```

**Usage (runtime extension).** Loading a DLL of extra navigators/readers/converters is the existing `code.load` seam, surfaced as a plang action:

```plang
- add type mytype.dll     / code.load sweeps the assembly and calls Register for each INavigator / ITypeReader / IConverter it finds
```

---

## The json navigator (plan §3) — and the `*` navigator

Two navigators ship: `json` (walks a `JsonElement`) and the `"*"` catch-all (walks any object by reflection). Both walk the plang path language over `path.Segments`, so their common shape is a shared base; only the one-hop descend and the child-value construction differ per navigator.

```csharp
// app/type/navigator/walk.cs — shared walk for navigators that use the plang path language
namespace app.type.navigator;

public abstract class walk : global::app.type.INavigator
{
    public abstract global::app.type.text.@this Kind { get; }
    public abstract bool Handles(System.Type clr);
    public abstract System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx);

    public async global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
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

    protected abstract (bool found, object? node) Step(object obj, string key, global::app.actor.context.@this ctx);
    protected abstract global::app.data.@this Data(string name, object? node, global::app.data.@this? parent, global::app.actor.context.@this ctx);

    // A bracket key that is a plang variable resolves via ctx.Variable; a literal ("0") passes through.
    protected static async global::System.Threading.Tasks.ValueTask<string> Key(
        global::app.variable.path.Segment.Index i, global::app.actor.context.@this ctx)
        => i.IsLiteral ? i.Inner.ToString()
                       : (await ctx.Variable.Get(i.Inner.ToString())).Peek().ToString() ?? i.Inner.ToString();
}
```

json navigator — descends a `JsonElement`, stamps `kind=json` on sub-containers, resolves scalar leaves to their plang scalar:

```csharp
// app/type/navigator/json.cs
namespace app.type.navigator;
using System.Text.Json;

public sealed class json : walk
{
    public override global::app.type.text.@this Kind => "json";
    public override bool Handles(System.Type clr)
        => clr == typeof(JsonElement) || typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(clr);

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName)) return (true, byName);
        if (e.ValueKind == JsonValueKind.Array && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength()) return (true, e[n]);
        return (false, null);
    }

    protected override global::app.data.@this Data(string name, object? node, global::app.data.@this? parent, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)node!;
        return e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? new global::app.data.@this(name, new global::app.type.clr.@this(e, ctx, kind: "json"), parent: parent, context: ctx)
            : new global::app.data.@this(name, Scalar(e), parent: parent, context: ctx);   // ctor lifts string→text, long→number, …
    }

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) yield return Data("", item, null, ctx);
        else if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) yield return Data(p.Name, p.Value, null, ctx);
    }

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, _ => null,
    };
}
```

`"*"` navigator — the catch-all, navigates any object by reflection (the behaviour that previously lived inline on `clr`):

```csharp
// app/type/navigator/reflection.cs
namespace app.type.navigator;

public sealed class reflection : walk
{
    public override global::app.type.text.@this Kind => @this.Any;   // "*"
    public override bool Handles(System.Type clr) => true;           // last-resort; For() tries specific navigators first

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        System.Reflection.PropertyInfo? prop = null;                 // bottom-up + DeclaredOnly + IgnoreCase
        for (var t = obj.GetType(); t != null && prop == null; t = t.BaseType)
            prop = t.GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                                     | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.DeclaredOnly);
        return prop == null ? (false, null) : (true, prop.GetValue(obj));
    }

    protected override global::app.data.@this Data(string name, object? node, global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => node is global::app.data.@this d ? d : new global::app.data.@this(name, node, parent: parent, context: ctx);  // ctor lifts a nested POCO → clr(*), a scalar → its wrapper

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        foreach (var p in obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            yield return new global::app.data.@this(p.Name, p.GetValue(obj), context: ctx);
    }
}
```

**Usage.** Shipping both navigators is the first end-to-end test of the registry: `%doc.users[0].email%` exercises `json`; `%result.CustomProperty%` on a POCO returned from a C# action exercises `"*"`. Adding a format later is one file — drop `app/type/navigator/yaml.cs` (`Kind => "yaml"`, `Handles` claims `YamlNode`); it is discovered and resolvable with no other change.

The child-value factory is named `Data` because it produces the child `Data`. Container → `clr(kind)`; scalar → its plang scalar. Neither navigator returns a `clr` wrapping a scalar.

---

## The parser handoff (plan §4)

When the value being navigated is a `clr`, hand it the whole `path` object and let its navigator walk the rest. `app.variable.path.Parse` has already tokenized once — the path object is passed as-is, never re-serialized to a string.

```csharp
// app/data/this.Navigation.cs
public async global::System.Threading.Tasks.ValueTask<@this> Navigate(global::app.variable.path.@this path)
{
    if (path.IsEmpty) return this;

    if (_item is global::app.type.clr.@this c)
        return await c.Navigate(this, path);      // the clr's navigator walks the whole path

    var (head, tail) = path.Split();
    // … existing Infra / Call / Index / Member per-hop walk for native dict/list and item types …
}
```

`app.variable.path.Parse` remains the single tokenizer for plang paths; a navigator walks the resulting `path.Segments`. (A navigator whose path language is *not* plang — a future jsonpath or css navigator — reads the raw form off the path itself.)

**Usage.** The whole chain the builder needs:

```plang
- llm.query ..., write to %plan%           / %plan% is clr(kind=json)
- foreach %plan.steps%, call BuildStep planStep=%item%   / Enumerate() yields each step as a clr(kind=json)
  / inside BuildStep: %planStep.index% → json navigator → number
```

---

## The reader pivot — keep external json as clr (plan §5)

`object/serializer/json.cs` `Read` currently walks a `JsonElement` into a native `dict`/`list`. It wraps it in a `clr(kind=json)` instead — one line:

```csharp
// app/type/object/serializer/json.cs (Read)
-   return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);   // walked → dict
+   return new global::app.type.clr.@this(parsed, ctx.Context, kind: "json");      // wrapped → clr(json)
```

`item.serializer.json.Parse` is **not** removed — it is the universal DOM narrower called by the `Data` constructor, `type.Create`, the `dict`/`list`/`object` readers, and Fluid to turn raw CLR / `JsonNode` values into native plang values. Only the reader path stops calling it. Authored `dict`/`list` literals (`%x% = {a:1}`) use their own readers and stay native.

The wire read routes a deferred value by its declared kind, defaulting to **text** when there is no kind:

```csharp
// app/data/reader/this.cs
-   deferredFormat = reader.Peek() == TokenKind.String ? Text.Mime : "application/plang";
+   deferredFormat = typeRef?.Kind is { } k ? Mime(k)                     // declared kind wins: (item, json) → clr(json)
+                    : global::app.channel.serializer.Text.Mime;          // no kind → text; the type decides
```

Text is the default because an undeclared value is safest as text (it is left for the type to interpret); internal-wire values carry an `@schema` marker and are read by the schema reader, a different branch. A full-match `%ref%` still borns a `variable` and is never parsed — only genuine *content* of an `item`/`object` json type becomes a `clr`.

**Usage.** `read file.json` and an `http` json response now both land as `clr(kind=json)` and navigate identically to the `llm.query` result — one representation for all external structured data.

---

## Producers stamp the kind (plan §6)

`OpenAi` stamps the kind from the requested format on both the fresh and cached result paths, so the two produce the same value:

```csharp
// app/module/llm/code/OpenAi.cs — result construction (fresh) and ParseResultValue (cached)
object? resultValue = format == "json" && TryParseJson(extracted) is JsonElement je
    ? new global::app.type.clr.@this(je, context, kind: format)   // kind is the format
    : extracted;                                                  // non-json → text (the string)
var result = context.Ok(resultValue);
```

The producer owns the kind because it knows what it asked for — nothing downstream has to guess. (The local is named `format`.)

**Usage.** `llm.query Scheme=..., format="json"` → `clr(kind=json)`; `format="md"` → `text`. `xml`/`yaml` become `clr(kind=…)` once those navigators exist.

---

## Convert — the outbound type owns it (plan §7)

A conversion is owned by the **target**, not the source. Converting `text(md)` to audio is owned by `audio` ("build audio from text"), so a source format never has to enumerate its possible targets. This matches how the existing per-type conversion already dispatches (`OwnerOf(target)`), extended so a target *kind* (e.g. `html`, a kind of `text`) can own a conversion too.

The value-facing call is on `Data` — everything at an action boundary is `Data`, which carries its own context, so no context argument is passed:

```csharp
// on app/data/this.cs
public global::System.Threading.Tasks.ValueTask<@this> Convert(global::app.type.text.@this toKind)
    => _context.App.Type.Conversions.To(this, toKind);   // toKind is "audio" or "text/html", parsed like a type name
```

A converter is keyed by the target it produces and knows how to build itself from a source. It returns an error `Data` when it cannot build from the given source — no separate boolean probe:

```csharp
// app/type/IConverter.cs
namespace app.type;

public interface IConverter
{
    global::app.type.text.@this Type { get; }     // outbound type it builds ("audio", "text")
    global::app.type.text.@this Kind { get; }     // outbound kind ("*" for audio; "html" for text/html)
    global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(
        global::app.data.@this source, global::app.actor.context.@this ctx);   // built value, or an error Data
}

// app/type/audio/converter.cs — audio owns "build audio from text", living with the outbound type
public sealed class converter : global::app.type.IConverter
{
    public global::app.type.text.@this Type => "audio";
    public global::app.type.text.@this Kind => global::app.type.navigator.@this.Any;   // "*"
    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(
        global::app.data.@this source, global::app.actor.context.@this ctx)
        => /* text → audio (TTS); return ctx.Error(...) if source is not text */ default;
}
```

`Conversions.To(value, toKind)` parses `toKind` into a target `(type, kind)`, finds the registered `IConverter` for it, and calls `Build(value, ctx)`. If no converter is registered for the target, it returns an error `Data` naming the missing conversion. `json → dict` is `dict` owning "build dict from json" and reuses the existing `catalog/Conversion` arm.

**Usage.** A handler that needs its input in a specific format asks for it; the framework routes to the outbound owner:

```csharp
// in an html-to-pdf action handler, given a Data<text> Html parameter sourced from read file.md:
var html = await Md.Convert("html");     // (text, html) converter owns md → html
```

Chained conversions (md → html → pdf) are out of scope initially; a target with no converter for the given source returns an error `Data`, never silently passes the source through.

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

Everything above except the two "Companion change" items below is v1 — it unblocks `plang build` (the `%plan%` navigation) and proves the registry end-to-end with two navigators (`json` + `"*"`). `clr.Navigate` is pure delegation; there is no inline reflection left on `clr` and no fallback branch (the `"*"` navigator always resolves).

Native `dict`/`list` and item types (`goal`, …) keep their existing per-hop navigation for now; routing them through the registry too is a later step, not v1.

---

## Companion change: type identifiers are `text`

Making kinds and type names `text` (this spec's convention) means `type.@this.Name` and `type.@this.Kind` become `text` as well, and their consumers adjust. The payoff: the type system's own metadata is first-class plang values — uniform with everything else, one serialization path, inspectable and comparable in plang. `text` keys the registries directly (value `Equals`/`GetHashCode`), and its implicit `string` operator keeps `kind == "json"` and interop with `System.Type` names working. This is mechanical breadth (many call sites) rather than depth. It lands with this work so `clr.Kind`, `INavigator.Kind`, and `IConverter.Type`/`Kind` are consistent with the rest of the type system rather than a lone `string` island.

## Companion change: `Peek()` returns `item.@this`

Every `Peek()` in the type system already returns `this`. Tighten the base signature from `object? Peek()` to `item.@this Peek()`: a value is always a plang value, never C# `null`, and absence is `@null.@this.Instance`. This removes null-checking at every `Peek()` call site and makes "navigation always yields a plang value" true by type, not by convention. `Data.Peek()` (on `Data`, distinct from `item.Peek()`) is a separate surface and is out of scope here.

---

## Open for the implementer

- **The reader-pivot seam.** Confirm the exact edit is in `object/serializer/json.cs` `Read` (not `Parse`, which stays). Trace `Read` vs `Parse` vs `source.Value/Build` before editing — it is the one place a wrong cut regresses every JSON read.
- **The `walk` base.** It removes the duplicate walk between `json` and `reflection`. If it reads as over-abstraction for two implementations, inline the walk into each — the shared piece is only the segment loop + variable resolution.
- **`IConverter.Build` error contract.** Returning an error `Data` when a source is unsupported (rather than a boolean probe) assumes the door tries `Build` and inspects the result. Confirm that reads cleanly against how `Conversions` dispatches today.
- **Names.** `walk` (the shared base), `reflection` (the `"*"` navigator) — rename if a clearer word fits. The interfaces are `INavigator` and `IConverter` (agent nouns, matching `ITypeReader`).

The design intent that must survive regardless of these: a `clr` stays a `clr`; a per-kind navigator owns its path language and its own variable resolution; a conversion is owned by the outbound type; and the reader pivot must never turn a `%ref%` into a `clr`.
