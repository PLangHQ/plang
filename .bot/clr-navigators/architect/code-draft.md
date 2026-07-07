# clr + kind navigators — code draft (OBP-minded)

**Companion to `plan.md`.** These are **suggestions** — coder owns the final shape (see the "You own this" note at the end). Every snippet uses `obj` (not hostObject), `tail` (not rawTail), and the born-with `context` (no separate resolver param). Signatures are sketches; the intent above each block is what must survive.

Headers name the `plan.md` section they implement (a couple of plan sections get more than one code block).

---

## plan §1 — `clr` carries `(obj, kind)`; navigation delegates

`clr` stops *doing* navigation (no reflection, no switch) — it resolves the navigator for its kind/obj and delegates. Kind is stamped by the producer; unstamped falls back to the CLR identity.

```csharp
// app/type/clr/this.cs
public sealed class @this : global::app.type.item.@this, global::app.module.IContext
{
    public object Value { get; }
    public string? Kind { get; }          // producer-stamped format (json/yaml/xml); null → derive from CLR type

    public @this(object value, global::app.actor.context.@this context, string? kind = null)
    {
        Value   = value   ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        Kind    = kind;
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException("A Data may not be carried in a clr — nested Data is not a supported shape.");
    }

    // type = item (lattice apex); kind = the stamped format, else the CLR identity.
    protected internal override global::app.type.@this Mint()
        => new("item", Kind
                       ?? Context.App.Type.ResolveName(Value.GetType())
                       ?? Value.GetType().FullName
                       ?? Value.GetType().Name);

    // v1: a registered navigator (json) wins; otherwise the EXISTING reflection stays as the
    // fallback (Ingi: "reflection already works, just add json"). `tail` is the whole remaining
    // path (a single key is just a one-segment tail — matches the base signature).
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string tail)
    {
        var nav = Context.App.Type.Navigators.For(Kind, Value.GetType());   // null in v1 when only json is registered and this isn't json
        return nav is not null ? nav.Navigate(Value, tail, parent, Context)
                               : Reflect(parent, tail);                     // ← the current reflection body, unchanged
    }

    // foreach over a container clr — a registered navigator enumerates; else reflect over public props.
    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate()
        => Context.App.Type.Navigators.For(Kind, Value.GetType())?.Enumerate(Value, Context)
           ?? ReflectEnumerate();

    // Reflect / ReflectEnumerate = today's clr reflection, kept verbatim. v2 relocates them into
    // the `*` navigator and this method becomes pure delegation (no fallback).

    public override object? Peek() => this;                                   // still a closed box
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    // Output / Write unchanged.
}
```

**OBP (v1 vs v2):** registry selects, navigator behaves — no `is JsonElement` switch in `clr`. The one transitional wrinkle is the `?? Reflect(...)` fallback: reflection lives on `clr` **and** json lives in the registry — two homes. Accepted for v1 (Ingi: don't rebuild what works); v2 relocates reflection into the `*` navigator so `For(...)` always resolves and the fallback disappears. `For` returning null in v1 (only json registered) is the signal — see §2 note.

---

## plan §2 + §3 — the navigator registry + interface

Interface first. Note `Handles` is a **method doing real work** (like `Covers`/`HasAccess`), not a verb-named property — the element answers "do I recognize this obj shape?"

```csharp
// app/type/clr/navigator/INavigator.cs
namespace app.type.clr.navigator;

public interface INavigator
{
    string Kind { get; }                          // "json"; "*" for the reflection default
    bool Handles(System.Type clr);                // recognizes an UNSTAMPED obj of this CLR type

    // Walk `obj` by `tail` in this kind's own path language, resolving any plang variables
    // it meets via ctx.Variable. Container → clr(kind); leaf → its plang scalar; ctx.NotFound on miss.
    System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, string tail, global::app.data.@this parent, global::app.actor.context.@this ctx);

    // Walk a container's children for foreach — each element a Data.
    System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx);
}
```

Registry — mirror of the reader registry, discovered by namespace, keyed by kind with a CLR-type fallback:

```csharp
// app/type/clr/navigator/this.cs
namespace app.type.clr.navigator;

public sealed class @this
{
    public const string Any = "*";
    private readonly System.Collections.Generic.Dictionary<string, INavigator> _byKind = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.List<INavigator> _all = new();

    // Discovered like readers (scan app.type.clr.navigator for INavigator impls); a code.load
    // DLL can Register more later — same seam as app.type.reader.Register.
    public @this(System.Collections.Generic.IEnumerable<INavigator> navigators)
    {
        foreach (var n in navigators) { _all.Add(n); _byKind[n.Kind] = n; }
    }

    // stamped kind (O(1)) → CLR-type match (unstamped JsonElement → json). Returns NULL when
    // nothing matches — in v1 that means "not json", and clr falls back to its own reflection.
    // v2 registers a "*" navigator so this never returns null and the fallback disappears.
    public INavigator? For(string? kind, System.Type clr)
    {
        if (!string.IsNullOrEmpty(kind) && _byKind.TryGetValue(kind!, out var byKind)) return byKind;
        foreach (var n in _all) if (n.Handles(clr)) return n;
        return null;
    }
}
```

**OBP:** registry = selection only (`For`). No type-switch — `Handles` is asked of each element. Hung off `App.Type.Navigators`, mirroring `App.Type.Readers` / `App.Type.Conversions`. v1 registers only `json`; `For` returns null for anything else and `clr` keeps reflecting.

### How registration works today — and the navigator mirror (comment: "demonstrate this")

Registration is **discovery by namespace**, not a hand-maintained list. The reader registry (`app/type/reader/this.cs`, `IndexAssembly`) scans the App assembly: for every type whose namespace ends in `.serializer`, it takes the folder before `.serializer` as the **type name** and, if the class implements `ITypeReader`, instantiates it and indexes it by the reader's own `Kind`:

```csharp
// app/type/reader/this.cs  (existing — abridged)
foreach (var type in assembly.GetTypes())
{
    if (!type.Namespace!.EndsWith(".serializer")) continue;           // app.type.<name>.serializer
    var typeName = /* folder before ".serializer" */;                  // "item", "table", …
    if (typeof(ITypeReader).IsAssignableFrom(type) && !type.IsAbstract)
    {
        var instance = (ITypeReader)ctor.Invoke(null);
        _generatedTyped[(typeName, instance.Kind)] = instance;        // keyed by (type, kind)
    }
}
```

So a new reader = drop a class in the right folder; no registration code. The **navigator registry does the same**, scanning for `INavigator` under `app.type.clr.navigator`:

```csharp
// how App builds the navigator registry (mirror of the reader scan)
var navigators = assembly.GetTypes()
    .Where(t => t.Namespace == "app.type.clr.navigator"
                && typeof(INavigator).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
    .Select(t => (INavigator)System.Activator.CreateInstance(t)!);
App.Type.Navigators = new global::app.type.clr.navigator.@this(navigators);   // ctor indexes by n.Kind
```

Adding `app/type/clr/navigator/yaml.cs` (implements `INavigator`, `Kind => "yaml"`) then "just works" — discovered, indexed, resolvable — exactly like adding a reader. Same seam for a `code.load` DLL: a `Register(navigator)` method on the registry (mirroring `app.type.reader.Register`) lets a loaded module add its own.

---

## plan §3 (the navigators) — the walk is shared; the *descend* is per-kind

**v1 ships the `json` navigator only** — the `reflection` navigator below is the **v2** relocation of `clr`'s existing reflection (shown here so the shared shape is visible; do not build it in v1). Both speak the **plang path language** — same tokenize-and-loop, differing only in one-hop descend, container-test, and the kind they stamp on sub-containers. So the walk (and the option-(b) variable resolution) lives **once** in a shared base; each is a thin override. A future jsonpath/css navigator that speaks a *different* language implements `INavigator` directly instead of extending this base.

> **v1 note:** you only need `path` (base) + `json`. The `reflection : path` block is the v2 target — in v1, `clr`'s own `Reflect(...)` stays and does this job.

```csharp
// app/type/clr/navigator/path.cs  — shared walk for kinds that adopt the plang path language
namespace app.type.clr.navigator;

public abstract class path : INavigator
{
    public abstract string Kind { get; }
    public abstract bool Handles(System.Type clr);
    public abstract System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx);

    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, string tail, global::app.data.@this parent, global::app.actor.context.@this ctx)
    {
        object? node = obj;
        foreach (var seg in global::app.variable.path.@this.Parse(tail).Segments)
        {
            var key = seg is global::app.variable.path.Segment.Index i ? await Resolve(i, ctx) : ((global::app.variable.path.Segment.Member)seg).Name;
            var (found, next) = Step(node!, key, ctx);          // per-kind one-hop descend
            if (!found) return ctx.NotFound(seg.Raw);
            node = next;
        }
        return Wrap(parent.Name, node, parent, ctx);            // container → clr(kind); leaf → scalar
    }

    protected abstract (bool found, object? node) Step(object obj, string key, global::app.actor.context.@this ctx);
    protected abstract global::app.data.@this Wrap(string name, object? node, global::app.data.@this parent, global::app.actor.context.@this ctx);

    // Option (b) lives here, ONCE: a bracket key that is a plang variable resolves via ctx.Variable;
    // a literal ("0") passes through. The navigator owns WHEN to call it; the mechanics are shared.
    protected static async System.Threading.Tasks.ValueTask<string> Resolve(
        global::app.variable.path.Segment.Index i, global::app.actor.context.@this ctx)
        => i.IsLiteral ? i.Inner.ToString()
                       : (await ctx.Variable.Get(i.Inner.ToString())).Peek()?.ToString() ?? i.Inner.ToString();
}
```

json navigator — descend a `JsonElement`, stamp `kind=json` on sub-containers:

```csharp
// app/type/clr/navigator/json.cs
namespace app.type.clr.navigator;
using System.Text.Json;

public sealed class json : path
{
    public override string Kind => "json";
    public override bool Handles(System.Type clr)
        => clr == typeof(JsonElement) || typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(clr);

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName)) return (true, byName);
        if (e.ValueKind == JsonValueKind.Array && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength())
            return (true, e[n]);
        return (false, null);
    }

    // container stays clr(json); a scalar rides as its raw CLR — the Data ctor lifts it
    // (string→text, long→number, …). One rule; no clr(scalar) intermediate.
    protected override global::app.data.@this Wrap(string name, object? node, global::app.data.@this parent, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)node!;
        return e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? new global::app.data.@this(name, new global::app.type.clr.@this(e, ctx, kind: "json"), parent: parent, context: ctx)
            : new global::app.data.@this(name, Scalar(e), parent: parent, context: ctx);
    }

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Array)
            foreach (var item in e.EnumerateArray())  yield return Wrap("", item, null!, ctx);
        else if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject())     yield return Wrap(p.Name, p.Value, null!, ctx);
    }

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True   => true,
        JsonValueKind.False  => false,
        _ => null,
    };
}
```

reflection navigator — **v2 only** (the `*` default; the body relocated out of `clr.Navigate`). In v1, `clr` keeps this logic inline as `Reflect(...)`; this block shows where it lands later:

```csharp
// app/type/clr/navigator/reflection.cs   [v2]
namespace app.type.clr.navigator;

public sealed class reflection : path
{
    public override string Kind => @this.Any;      // "*"
    public override bool Handles(System.Type clr) => true;   // last-resort catch-all (For() tries it last)

    protected override (bool, object?) Step(object obj, string key, global::app.actor.context.@this ctx)
    {
        // bottom-up + DeclaredOnly + IgnoreCase — the walk that was in clr.Navigate.
        System.Reflection.PropertyInfo? prop = null;
        for (var t = obj.GetType(); t != null && prop == null; t = t.BaseType)
            prop = t.GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
                                     | System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.DeclaredOnly);
        return prop == null ? (false, null) : (true, prop.GetValue(obj));
    }

    protected override global::app.data.@this Wrap(string name, object? node, global::app.data.@this parent, global::app.actor.context.@this ctx)
        // node is a live CLR value — let type.Create lift it (a nested POCO → clr(*), a scalar → its wrapper).
        => node is global::app.data.@this d ? d : new global::app.data.@this(name, node, parent: parent, context: ctx);

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        foreach (var p in obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            yield return new global::app.data.@this(p.Name, p.GetValue(obj), context: ctx);
    }
}
```

**OBP:** the walk + variable-resolution isn't copied into each kind (Smell #5 avoided) — it's on the shared `path` base; only `Step`/`Wrap`/`Enumerate` (the genuinely per-kind bits) are overridden. `Handles` on `reflection` returns true but `For()` tries specific navigators first, so it only wins as the fallback.

---

## plan §4 — the parser handoff (`data/this.Navigation.cs`)

When the value being navigated is a `clr`, hand the **whole untokenized tail** to it and stop — the navigator walks the rest. Native `dict`/`list` and item types keep the existing per-hop walk (and its rich `IndexNotSet` diagnostic) for now.

```csharp
public async System.Threading.Tasks.ValueTask<@this> Navigate(global::app.variable.path.@this path)
{
    if (path.IsEmpty) return this;

    // Handoff: a clr with a registered navigator (v1: json) owns its whole tail — it speaks its
    // own path language + resolves vars. A clr WITHOUT one (a POCO in v1) is NOT handed off; it
    // falls through to the per-hop walk, which drives clr.Navigate(key) one key at a time (the
    // existing reflection). So the multi-segment handoff only reaches navigators that expect it.
    if (_item is global::app.type.clr.@this c && c.Navigable)
        return await _item.Navigate(this, path.ToString());   // ToString() reconstructs the raw tail

    var (head, tail) = path.Split();
    // … existing Infra / Call / Index / Member per-hop walk unchanged …
}
```

`Navigable` on clr (plan §1): `public bool Navigable => Context.App.Type.Navigators.For(Kind, Value.GetType()) is not null;` — true for json in v1, false for a POCO (which keeps the per-hop reflection).

**OBP:** one branch, at the top; the clr doesn't get its tail pre-tokenized into the generic segment kinds. `app.variable.path.Parse` stays the single plang tokenizer — the navigator re-parses the *same* language from the raw tail (a jsonpath/css navigator would parse a *different* language). No second plang tokenizer. `Navigable` is an adjective (single word), not a verb-noun.

---

## plan §5 — the reader pivot (keep external json as clr)

One line. The `(object/item, json)` reader wraps the DOM in a clr instead of walking it to a dict. Authored `dict`/`list` literals use their own readers and are **untouched** (they stay native — `%x% = {a:1}` is still a native dict).

```csharp
// app/type/object/serializer/json.cs  (Read)
-   return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);   // walked → dict (old)
+   return new global::app.type.clr.@this(parsed, ctx.Context, kind: "json");      // wrap → clr(json)
```

Route the deferred value by declared kind, not token shape:

```csharp
// app/data/reader/this.cs : ~79-80
-   deferredFormat = reader.Peek() == TokenKind.String ? Text.Mime : "application/plang";
+   deferredFormat = typeRef?.Kind is { Length: > 0 } k ? Mime(k)                    // declared kind wins
+                    : reader.Peek() == TokenKind.String ? Text.Mime                 // else token-shape fallback
+                    : "application/plang";
```

**Guardrails (parent-branch rule):** a full-match `%ref%` still borns a `variable` and is **never parsed** — that branch stays; only genuine *content* of an `item/object`-json type becomes clr. **Do not blanket-change** `item/serializer/json.Parse` (the Data ctor calls it on every value) — only the *reader* path (`object.serializer.json.Read`) wraps in clr. Coder: trace `Read` vs `Parse` vs `source.Value/Build` before touching, this is the riskiest seam.

---

## plan §6 — OpenAi stamps the kind

The fresh path must not hand a raw `JsonElement` to `context.Ok` (the Data ctor would walk it to a dict). Build the clr — stamped from the requested format — on both fresh and cached paths, so `fresh == cached`.

```csharp
// app/module/llm/code/OpenAi.cs — result construction
object? resultValue = effectiveFormat switch
{
    "json" => TryParseJson(extracted) is JsonElement je ? new clr.@this(je, context, kind: "json") : null,
    "md"   => /* text (kind=md) — a scalar, no navigator */ extracted,
    _      => extracted,   // prose → text
};
var result = context.Ok(resultValue);
// ParseResultValue (cached path): same — wrap the re-parsed JsonElement in clr(kind=json).
```

**OBP:** the producer owns the kind (it knows what it asked for) — the honest source of truth, not `data/reader` guessing downstream.

---

## plan §7 — Convert: the **outbound (target) owns it**, behind the existing door

**Ingi's correction:** the target owns the conversion, not the source. `text(md) → audio`: if `md` owned its outbound conversions it would have to know every format (audio, html, pdf, …); but `audio` only needs to know how to make itself **from** text. So the owner is the **outbound `(type, kind)`** — which is how the per-type `Convert(value, kind, ctx)` hook already dispatches: `catalog.Convert` → `OwnerOf(targetType).Convert(value, …)`, the *target* builds itself from the value. Make the `kind` param load-bearing so a target *kind* (html, a kind of text) can own it too.

Value-facing call — **no `context` param**, because everything at an action boundary is `Data`, which carries its own context (Ingi):

```csharp
// on Data — resolve the TARGET (type,kind) owner and hand it this value whole. Data has _context.
public System.Threading.Tasks.ValueTask<@this> Convert(string toKind)
    => _context.App.Type.Conversions.To(this, toKind);   // "audio" / "text/html" — parsed like a type name
```

So a handler with a `Data<text> Text` param just writes:

```csharp
var audio = await Text.Convert("audio");   // audio (the outbound) owns text→audio
```

The converter is keyed by the **target** `(type, kind)`, discovered like readers/navigators — the outbound owner:

```csharp
// app/type/convert/IConvert.cs  — "I am the outbound; build me from a source value"
public interface IConvert
{
    string Type { get; }             // the OUTBOUND type it builds ("audio", "text")
    string Kind { get; }             // the outbound kind ("*" for audio; "html" for text/html)
    bool From(global::app.data.@this source);   // can I build myself from THIS source? (audio: source is text)
    System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx);
}

// app/type/audio/convert.cs  — audio owns "make audio from text" (lives WITH audio, the outbound)
public sealed class convert : IConvert
{
    public string Type => "audio";
    public string Kind => global::app.type.clr.navigator.@this.Any;   // "*"
    public bool From(global::app.data.@this source) => source.Type?.Name == "text";
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx)
        => /* text → audio (TTS) */ …;
}
```

**OBP:** one door (extend `Conversions.To`, don't stand up a parallel path). The behavior lives **with the outbound** — `audio` owns text→audio, so adding a new output format = adding one owner, and no existing type learns about it (this is exactly why source-owns was wrong). The value is passed **whole** to `Build`, never decomposed to `.Value` at the call site. `json→dict` = `dict` owns "build dict from json" (reuse the existing `catalog/Conversion` arm, wired in as `dict`'s inbound-from-json). Chains (md→html→pdf) later; when no outbound owner can build from this source, fail loud (`log` + error), never silently pass the source through.

---

## plan §9 — Guards

```csharp
// source.Value — container must never come back a scalar (the exact round-trip loss that caused this bug)
if (declaredType.IsContainer && materialized is not (dict or list or clr))
    throw new … ("a container value materialized to a scalar leaf — round-trip loss at <slot>");
```

**OBP:** fails loud at the point of loss, not three hops later as `IndexNotSet`.

---

## You own this (coder)

Every signature, file path, and name here is a **suggestion** to make the design concrete — you own the final form. Highest-value places to exercise judgement: (1) for v1 you only need `clr.Kind`/`Navigable` + the registry + the `json` navigator + the reader pivot + OpenAi stamping — leave clr's reflection inline as the fallback; (2) the exact reader-pivot seam (`Read` vs `Parse` vs `source.Build`) — trace it before editing, it's the riskiest; (3) the `IConvert` shape (I put it **with the outbound type**, `app/type/audio/convert.cs`; confirm `Type`/`Kind`/`From`/`Build` reads right, and whether `From` should be a bool probe or the door just tries `Build`). The design intent that must survive: **clr stays clr; the per-kind navigator owns its path language + variable resolution; convert's outbound owns it behind the existing door; the reader pivot must not turn a `%ref%` into a clr.** If a shape here fights the code, push back.
