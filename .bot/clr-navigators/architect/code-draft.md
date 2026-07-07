# clr + kind navigators — code draft (OBP-minded)

**Companion to `plan.md`.** These are **suggestions** — coder owns the final shape (see the "You own this" note at the end). Every snippet uses `obj` (not hostObject), `tail` (not rawTail), and the born-with `context` (no separate resolver param). Signatures are sketches; the intent above each block is what must survive.

Section numbers match `plan.md` §1–§9.

---

## §1 — `clr` carries `(obj, kind)`; navigation delegates

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

    // Behaviour lives on the navigator; clr only selects + delegates. `tail` is the whole
    // remaining path (a single key is just a one-segment tail — matches the base signature).
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string tail)
        => Nav().Navigate(Value, tail, parent, Context);

    // foreach over a container clr — the navigator walks its obj's children.
    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate()
        => Nav().Enumerate(Value, Context);

    private global::app.type.clr.navigator.INavigator Nav()
        => Context.App.Type.Navigators.For(Kind, Value.GetType());

    public override object? Peek() => this;                                   // still a closed box
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    // Output / Write unchanged.
}
```

**OBP:** registry selects, navigator behaves — no `is JsonElement` switch survives in `clr`. `Kind` is a stamped noun. The reflection body that lived here moves out to the `*` navigator (§5) — do not leave a private reflection path behind (that would be two homes).

---

## §2/§3 — the navigator registry + interface

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
    private INavigator _default = null!;   // the "*" reflection navigator

    // Discovered like readers (scan app.type.clr.navigator for INavigator impls); a code.load
    // DLL can Register more later — same seam as app.type.reader.Register.
    public @this(System.Collections.Generic.IEnumerable<INavigator> navigators)
    {
        foreach (var n in navigators)
        {
            _all.Add(n);
            _byKind[n.Kind] = n;
            if (n.Kind == Any) _default = n;
        }
    }

    // stamped kind (O(1)) → CLR-type match (unstamped JsonElement → json) → reflection default.
    public INavigator For(string? kind, System.Type clr)
    {
        if (!string.IsNullOrEmpty(kind) && _byKind.TryGetValue(kind!, out var byKind)) return byKind;
        foreach (var n in _all) if (n.Handles(clr)) return n;
        return _default;
    }
}
```

**OBP:** registry = selection only (`For`). No type-switch — `Handles` is asked of each element. Hung off `App.Type.Navigators`, mirroring `App.Type.Readers` / `App.Type.Conversions`.

---

## §4/§5 — the walk is shared; the *descend* is per-kind

json (over `JsonElement`) and reflection (over a POCO) both speak the **plang path language** — same tokenize-and-loop, differing only in one-hop descend, container-test, and the kind they stamp on sub-containers. So the walk (and the option-(b) variable resolution) lives **once** in a shared base; each kind is a thin override. A future jsonpath/css navigator that speaks a *different* language implements `INavigator` directly instead of extending this base.

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

reflection navigator — the `*` default; the body relocated out of `clr.Navigate`:

```csharp
// app/type/clr/navigator/reflection.cs
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

## §6 — the parser handoff (`data/this.Navigation.cs`)

When the value being navigated is a `clr`, hand the **whole untokenized tail** to it and stop — the navigator walks the rest. Native `dict`/`list` and item types keep the existing per-hop walk (and its rich `IndexNotSet` diagnostic) for now.

```csharp
public async System.Threading.Tasks.ValueTask<@this> Navigate(global::app.variable.path.@this path)
{
    if (path.IsEmpty) return this;

    // Handoff: a clr owns its whole tail (its navigator speaks its own path language + resolves vars).
    if (_item is global::app.type.clr.@this)
        return await _item.Navigate(this, path.ToString());   // ToString() reconstructs the raw tail

    var (head, tail) = path.Split();
    // … existing Infra / Call / Index / Member per-hop walk unchanged …
}
```

**OBP:** one branch, at the top; the clr doesn't get its tail pre-tokenized into the generic segment kinds. `app.variable.path.Parse` stays the single plang tokenizer — the navigator re-parses the *same* language from the raw tail (a jsonpath/css navigator would parse a *different* language). No second plang tokenizer.

---

## §7 — the reader pivot (keep external json as clr)

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

## §8 — OpenAi stamps the kind

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

## §9 — Convert: (fromKind → toKind), behind the existing door

The per-type `Convert(value, kind, ctx)` hook already takes `kind` (today "a hint"). Make it load-bearing: the door reads the **source** kind off the value and the **target** kind from the request, and dispatches to a per-pair converter. `as dict` (json→dict) is one arm; `md→html` is another.

Value-facing call (the carrier owns the op):

```csharp
// on Data — routes to the door; source kind read from this.Type, target kind = the request
public System.Threading.Tasks.ValueTask<@this> Convert(string toKind, global::app.actor.context.@this ctx)
    => ctx.App.Type.Conversions.To(this, toKind, ctx);      // whole Data in; the door decomposes, not the caller
```

Per-pair converters, one file each, discovered like readers/navigators:

```csharp
// app/type/convert/kind/IConvert.cs  — a (from → to) content transform
public interface IConvert { string From { get; } string To { get; } System.Threading.Tasks.ValueTask<global::app.data.@this> Convert(global::app.data.@this value, global::app.actor.context.@this ctx); }

// app/type/convert/kind/md.html.cs  (illustrative — md → html)
public sealed class md_html : IConvert { public string From => "md"; public string To => "html"; /* … render markdown → html … */ }
```

**OBP:** one door (extend `Conversions`, don't stand up a parallel path); behavior per (from,to) file (variant-per-file, feedback_obp_variant_design); the value is passed **whole** to `Convert`, not decomposed to `.Value` at the call site. `json→dict` reuses the existing `catalog/Conversion` arm — wire it in as the `(json, dict)` converter rather than duplicating it. Chains (md→html→pdf) are later; a missing hop must fail loud (`log` + error), never silently pass the source through.

---

## Guards (§ "The throw guards" in plan)

```csharp
// source.Value — container must never come back a scalar (the exact round-trip loss that caused this bug)
if (declaredType.IsContainer && materialized is not (dict or list or clr))
    throw new … ("a container value materialized to a scalar leaf — round-trip loss at <slot>");
```

**OBP:** fails loud at the point of loss, not three hops later as `IndexNotSet`.

---

## You own this (coder)

Every signature, file path, and name here is a **suggestion** to make the design concrete — you own the final form. Highest-value places to exercise judgement: (1) whether the shared `path` base is worth it or the json/reflection navigators are cleaner standalone; (2) the exact reader-pivot seam (`Read` vs `Parse` vs `source.Build`) — trace it before editing, it's the riskiest; (3) the `IConvert` naming/home (I reused `convert`'s namespace; a better word than `From`/`To` pair is welcome — scan for verb+noun). The design intent that must survive: **clr stays clr; the per-kind navigator owns its path language + variable resolution; convert is fromKind→toKind behind the existing door; the reader pivot must not turn a `%ref%` into a clr.** If a shape here fights the code, push back.
