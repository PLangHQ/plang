# clr + kind navigators — code draft (OBP-minded)

**Companion to `plan.md`.** These are **suggestions** — coder owns the final shape (see "You own this" at the end). Headers name the `plan.md` section they implement. Signatures are sketches; the intent above each block is what must survive.

## A rule for the recurring "plang types?" question (Kind, Handles, From, …)

Several comments ask whether `Kind`/`Handles`/`Type`/`From` should be plang types (`text`, `@bool`) instead of `string`/`bool`. The line I'd draw:

- **Values that flow through the runtime → plang types.** What `Navigate`/`Convert` take and return is `Data` (a value crossing the plang boundary). That stays plang.
- **Plumbing — registry keys and predicates → CLR (`string`/`bool`).** `Kind` is a dictionary key; `Handles`/`From` are C# predicates the registry calls in a hot loop. They never flow through plang as values, and the existing `type.@this.Kind` is already `string`. A plang `text`/`@bool` here would force `.Value()`/`.ToBoolean()` unwraps at every lookup and buy nothing.

So below: `Kind`/`Handles`/`Type`/`From` stay CLR. Making the **type system's** kinds plang-`text` everywhere is a real idea, but it's a change to `type.@this.Kind` and every consumer — a separate, type-wide branch, not clr-navigators. Flagged, not done here.

---

## plan §1 — `clr` carries `(obj, kind)`; navigation delegates

```csharp
// app/type/clr/this.cs
public sealed class @this : global::app.type.item.@this, global::app.module.IContext
{
    public object Value { get; }
    public string? Kind { get; }          // producer-stamped format ("json"/"yaml"/…); null → derive from CLR type

    public @this(object value, global::app.actor.context.@this context, string? kind = null)
    {
        Value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        Kind = kind;
        if (value is global::app.data.@this)
            throw new System.InvalidOperationException("A Data may not be carried in a clr — nested Data is not a supported shape.");
    }

    protected internal override global::app.type.@this Mint()
        => new("item", Kind
                       ?? Context.App.Type.ResolveName(Value.GetType())
                       ?? Value.GetType().FullName
                       ?? Value.GetType().Name);

    // Peek returns THIS — the clr, which IS a plang type (an item). Never the raw JsonElement.
    // The signature is object? only because the base item.Peek() is object?; the value is always
    // the plang type. (The raw host is reachable ONLY via the explicit Clr<T>() exit.)
    public override object? Peek() => this;

    // A registered navigator exists for this value → it owns the whole path (the handoff, plan §4).
    public bool Navigable => Context.App.Type.Navigators.For(Kind, Value.GetType()) is not null;

    // The handoff target: a whole path, walked by the navigator. Only reached when Navigable.
    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, global::app.variable.path.@this path)
        => Context.App.Type.Navigators.For(Kind, Value.GetType())!.Navigate(Value, path, parent, Context);

    // Per-hop fallback (v1): a POCO with no registered navigator keeps the EXISTING reflection,
    // one key at a time, driven by the generic walker. v2 relocates this into a "*" navigator.
    public override global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
        => /* existing reflection body, unchanged (single key → property) */;

    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    // Output / Write unchanged.
}
```

**Comments addressed:** `Peek() => this` returns the clr (a plang type), not `object`/the raw JsonElement — the `object?` is just the base virtual's return type. `Kind` stays `string` (plumbing rule + parity with `type.@this.Kind`). **OBP:** clr selects + delegates; no `is JsonElement` switch. v1 wrinkle: reflection lives inline (per-hop `Navigate(parent, key)`) while json lives in the registry — two homes, retired in v2 when reflection relocates into a `*` navigator.

---

## plan §2 + §3 — the navigator interface + registry

The interface lives at **`app/type/INavigator.cs`** (not under `clr/`) — navigation is not clr-specific; over time other types adopt it (Ingi). `Navigate`/`Enumerate` take the **raw `obj`** (a `JsonElement`, a POCO) because the navigator's job is to turn a raw host into plang values — its input is raw, its output is `Data`. Passing an `item` would just force an immediate unwrap. It takes the **`path` object** (already tokenized once by `app.variable.path.Parse`) — no re-parse, no ToString.

```csharp
// app/type/INavigator.cs
namespace app.type;

public interface INavigator
{
    string Kind { get; }                 // "json"; "*" for the reflection default (plumbing → string)
    bool Handles(System.Type clr);       // recognizes an UNSTAMPED obj of this CLR type (predicate → bool)

    // Walk `obj` by `path`, producing plang values (container → clr(kind); leaf → its scalar).
    // Resolves any plang variable it meets (e.g. steps[step.Index]) via ctx.Variable — option (b).
    global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path, global::app.data.@this parent, global::app.actor.context.@this ctx);

    // Walk a container's children for foreach — each element a Data.
    System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx);
}
```

Registry — discovery is just "implements `INavigator`", no namespace gymnastics (see comment on the reader scan below):

```csharp
// app/type/navigator/this.cs
namespace app.type.navigator;

public sealed class @this
{
    public const string Any = "*";
    private readonly System.Collections.Generic.Dictionary<string, global::app.type.INavigator> _byKind = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.List<global::app.type.INavigator> _all = new();

    public @this(System.Collections.Generic.IEnumerable<global::app.type.INavigator> navigators)
    {
        foreach (var n in navigators) { _all.Add(n); _byKind[n.Kind] = n; }
    }

    // stamped kind (O(1)) → CLR-type match (unstamped JsonElement → json). NULL when nothing
    // matches — in v1 that means "not json", and clr falls back to its own reflection.
    public global::app.type.INavigator? For(string? kind, System.Type clr)
    {
        if (!string.IsNullOrEmpty(kind) && _byKind.TryGetValue(kind!, out var byKind)) return byKind;
        foreach (var n in _all) if (n.Handles(clr)) return n;
        return null;
    }

    // code.load DLL seam (see the plang action note below).
    public void Register(global::app.type.INavigator n) { _all.Add(n); _byKind[n.Kind] = n; }
}
```

### How discovery works — and why no namespace filter (comment: "this looks bad, why do we need it?")

You're right — for navigators there's **no** namespace check. A navigator declares its own `Kind`, so the only filter is the interface:

```csharp
var navigators = assembly.GetTypes()
    .Where(t => typeof(global::app.type.INavigator).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
    .Select(t => (global::app.type.INavigator)System.Activator.CreateInstance(t)!);
App.Type.Navigators = new global::app.type.navigator.@this(navigators);
```

The *reader* registry uses `EndsWith(".serializer")` only because it derives the **type name from the folder** (there's no `Type` property on a static `Read` method to read it from). That's an existing wart, not a pattern to copy — navigators self-declare `Kind`, so they just register. (Worth a follow-up: give readers a self-declared name too and drop their namespace dependency.)

### Loading external navigators/types from a DLL (comment: "add type mytype.dll")

Surface the `Register` seam as a plang action so a user pulls in a type/navigator pack from a DLL:

```plang
- add type mytype.dll     // loads the assembly, registers its INavigator / ITypeReader / IConvert
```

This wraps the existing `code.load` DLL load + a registry sweep (same scan as above, over the loaded assembly). Design note for the plan — not v1 core, but the seam (`Register`) is built now so the action has something to call.

---

## plan §3 (the json navigator) — v1 ships this one

v1 = **the json navigator only**; clr's existing reflection stays as the per-hop fallback (plan §1). json walks the already-parsed `path.Segments` (no re-tokenize — `app.variable.path.Parse` ran once), descends the `JsonElement`, and returns a child `Data`: a container as `clr(kind=json)`, a scalar as its plang scalar. The factory that produces that child is named **`Data(...)`** (it gives us `Data` back) — not `Wrap`.

```csharp
// app/type/navigator/json.cs
namespace app.type.navigator;
using System.Text.Json;

public sealed class json : global::app.type.INavigator
{
    public string Kind => "json";
    public bool Handles(System.Type clr)
        => clr == typeof(JsonElement) || typeof(System.Text.Json.Nodes.JsonNode).IsAssignableFrom(clr);

    public async global::System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        object obj, global::app.variable.path.@this path, global::app.data.@this parent, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        foreach (var seg in path.Segments)
        {
            var key = seg is global::app.variable.path.Segment.Index i ? await Key(i, ctx)      // resolve %var% via ctx.Variable (option b)
                                                                       : ((global::app.variable.path.Segment.Member)seg).Name;
            if (!Step(ref e, key)) return ctx.NotFound(seg.Raw);
        }
        return Data(parent.Name, e, parent, ctx);
    }

    public System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(object obj, global::app.actor.context.@this ctx)
    {
        var e = (JsonElement)obj;
        if (e.ValueKind == JsonValueKind.Array)
            foreach (var item in e.EnumerateArray()) yield return Data("", item, null!, ctx);
        else if (e.ValueKind == JsonValueKind.Object)
            foreach (var p in e.EnumerateObject()) yield return Data(p.Name, p.Value, null!, ctx);
    }

    // one hop: member on an object, integer index on an array
    private static bool Step(ref JsonElement e, string key)
    {
        if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var byName)) { e = byName; return true; }
        if (e.ValueKind == JsonValueKind.Array && int.TryParse(key, out var n) && n >= 0 && n < e.GetArrayLength()) { e = e[n]; return true; }
        return false;
    }

    // container → clr(kind=json); scalar → its raw CLR (the Data ctor lifts string→text, long→number, …)
    private static global::app.data.@this Data(string name, JsonElement e, global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? new global::app.data.@this(name, new global::app.type.clr.@this(e, ctx, kind: "json"), parent: parent, context: ctx)
            : new global::app.data.@this(name, Scalar(e), parent: parent, context: ctx);

    private static object? Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? (object)l : e.GetDouble(),
        JsonValueKind.True => true, JsonValueKind.False => false, _ => null,
    };

    private static async global::System.Threading.Tasks.ValueTask<string> Key(global::app.variable.path.Segment.Index i, global::app.actor.context.@this ctx)
        => i.IsLiteral ? i.Inner.ToString()
                       : (await ctx.Variable.Get(i.Inner.ToString())).Peek()?.ToString() ?? i.Inner.ToString();
}
```

**v2 note:** when reflection relocates from `clr` into a `reflection : INavigator` (kind `"*"`), json + reflection share the walk — extract a base then (the `Data(...)` factory and the segment loop are the shared bits; only `Step`/container-test differ). Not now: v1 has one navigator, so no base to extract yet.

**OBP:** `Navigate`/`Enumerate` take raw `obj` (walk the host) and return `Data` (values); the `path` object is walked, not re-parsed; the child factory is `Data(...)` (says what it returns). The value is never decomposed to `.Value` at a call site.

---

## plan §4 — the parser handoff (`data/this.Navigation.cs`)

A `clr` with a registered navigator owns its whole path — hand it the **`path` object** (not `path.ToString()`; it's already tokenized) and stop. A `clr` without one (a POCO in v1) is not handed off; it falls through to the per-hop walk driving `clr.Navigate(parent, key)`.

```csharp
public async global::System.Threading.Tasks.ValueTask<@this> Navigate(global::app.variable.path.@this path)
{
    if (path.IsEmpty) return this;

    if (_item is global::app.type.clr.@this c && c.Navigable)
        return await c.Navigate(this, path);          // whole path object → the navigator; no ToString, no re-parse

    var (head, tail) = path.Split();
    // … existing Infra / Call / Index / Member per-hop walk unchanged …
}
```

**OBP:** one branch, at the top. `app.variable.path.Parse` stays the single tokenizer — the navigator consumes the same parsed path, no second tokenizer. `Navigable` is an adjective, not a verb-noun.

---

## plan §5 — the reader pivot (keep external json as clr)

One line: the `(object/item, json)` reader wraps the DOM in `clr(kind=json)` instead of walking it to a dict. Authored `dict`/`list` literals use their own readers and are **untouched** (`%x% = {a:1}` is still a native dict).

```csharp
// app/type/object/serializer/json.cs  (Read)
-   return new global::app.type.item.serializer.json(ctx.Context).Parse(parsed);   // walked → dict (old)
+   return new global::app.type.clr.@this(parsed, ctx.Context, kind: "json");      // wrap → clr(json)
```

**Can we delete `item.serializer.json`? No.** `Parse` is the universal DOM narrower — the `Data` ctor (`data/this.cs:216, 315`), `type.Create` (`type.cs:483, 585`), the `dict`/`list`/`object` `Reader.cs`, and Fluid all call it to turn raw CLR / `JsonNode` into native values. Only the *reader* path (`object.serializer.json.Read`) stops calling it. It stays.

Route the deferred value by declared kind, and **default to text** when there's no kind (comment: "text should be default"):

```csharp
// app/data/reader/this.cs : ~79-80
-   deferredFormat = reader.Peek() == TokenKind.String ? Text.Mime : "application/plang";
+   deferredFormat = typeRef?.Kind is { Length: > 0 } k ? Mime(k)     // declared kind wins (item/json → clr(json))
+                    : global::app.channel.serializer.Text.Mime;      // no kind → text (let the type decide)
```

Agreed on the default: `application/plang` (the internal Data wire) is the wrong thing to *assume* for an undeclared value — internal-wire values are `@schema`-marked and handled by the schema reader, a different branch. An undeclared value is safest as text (the parent-branch "a plain string stays a string for the type to decide"). **Guardrails (parent-branch rule):** a full-match `%ref%` still borns a `variable`, never parsed — that branch stays; only *content* of an `item/object`-json type becomes clr. Coder: trace `Read` vs `Parse` vs `source.Value/Build` before editing — the riskiest seam.

---

## plan §6 — OpenAi stamps the kind

Drop the switch — just wrap in `clr` stamped with the format (comment). And `effectiveFormat` → `format`.

```csharp
// app/module/llm/code/OpenAi.cs — result construction (both fresh and cached/ParseResultValue paths)
object? resultValue = format == "json" && TryParseJson(extracted) is JsonElement je
    ? new global::app.type.clr.@this(je, context, kind: format)   // kind IS the format
    : extracted;                                                  // md / prose → text (the string)
var result = context.Ok(resultValue);
```

(v1 handles json → clr; xml/yaml join when their navigators land — until then non-json falls to text, which is fine.) **OBP:** the producer owns the kind — the honest source of truth, not `data/reader` guessing.

---

## plan §7 — Convert: the **outbound (target) owns it**, behind the existing door

The target owns the conversion, not the source (Ingi: `text(md)→audio` — `audio` owns text→audio; `md` needn't know audio). That is how the per-type `Convert(value, kind, ctx)` hook already dispatches (`OwnerOf(target)`). Make `kind` load-bearing so a target *kind* (html, a kind of text) can own it too.

Value-facing call — **no `context` param**; everything at an action boundary is `Data`, which carries its own context:

```csharp
// on Data — resolve the TARGET (type,kind) owner and hand it this value whole.
public global::System.Threading.Tasks.ValueTask<@this> Convert(string toKind)
    => _context.App.Type.Conversions.To(this, toKind);   // "audio" / "text/html" — parsed like a type name
```

So a handler with `Data<text> Text` writes: `var audio = await Text.Convert("audio");`

The converter is keyed by the **target** `(type, kind)` — the outbound owner (`Type`/`Kind`/`From` are registry plumbing → CLR, per the rule up top):

```csharp
// app/type/IConvert.cs
namespace app.type;
public interface IConvert
{
    string Type { get; }                                   // outbound type it builds ("audio", "text")
    string Kind { get; }                                   // outbound kind ("*" for audio; "html" for text/html)
    bool From(global::app.data.@this source);              // can I build from THIS source? (audio: source is text)
    global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx);
}

// app/type/audio/convert.cs — audio owns "make audio from text" (lives WITH the outbound)
public sealed class convert : global::app.type.IConvert
{
    public string Type => "audio";
    public string Kind => global::app.type.navigator.@this.Any;   // "*"
    public bool From(global::app.data.@this source) => source.Type?.Name == "text";
    public global::System.Threading.Tasks.ValueTask<global::app.data.@this> Build(global::app.data.@this source, global::app.actor.context.@this ctx)
        => /* text → audio (TTS) */ default;
}
```

**OBP:** one door (`Conversions.To`), behavior with the outbound owner — adding an output format = adding one owner, no existing type learns about it. `json→dict` = `dict` owns "build dict from json" (reuse the existing `catalog/Conversion` arm). The source is passed **whole** to `Build`, never decomposed. Chains (md→html→pdf) later; when no owner can build from this source, fail loud (`log` + error).

---

## plan §9 — Guards

```csharp
// source.Value — a container must never come back a scalar (the exact round-trip loss behind this bug)
if (declaredType.IsContainer && materialized is not (dict or list or clr))
    throw new … ("a container value materialized to a scalar leaf — round-trip loss at <slot>");
```

**OBP:** fails loud at the point of loss, not three hops later as `IndexNotSet`.

---

## You own this (coder)

Every signature/path/name is a **suggestion** — you own the final form. Highest-value judgement calls: (1) v1 is small — `clr.Kind`/`Navigable` + the `INavigator`/registry + the **json** navigator + the reader pivot + OpenAi stamping; leave reflection inline. (2) The reader-pivot seam (`Read` vs `Parse` vs `source.Build`) — trace before editing, riskiest. (3) `IConvert` shape (`From` bool-probe vs the door just trying `Build`; where `audio/convert.cs` lives). The intent that must survive: **clr stays clr; the navigator owns its path language + variable resolution (option b); convert's outbound owns it behind the existing door; the reader pivot must not turn a `%ref%` into a clr.** If a shape here fights the code, push back.
