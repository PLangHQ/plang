# Code draft — the settled shapes (companion to plan.md)

Representative shapes per stage, OBP-annotated. **You own the final code** — these pin intent, not lines. This supersedes the earlier draft (which still carried the navigate-pull record builder, async `Create`, and an invented `Shape` helper — all dropped).

---

## Stage 1 — the `*` (reflection) kind gains `Set` and a minimal `Read`

```csharp
// kind/behavior/reflection.cs — Set: the mirror of Navigate. The HOST'S CLASS declares the
// type (PropertyType); the kind reflects it at the leaf and converts the incoming value to it.
public override item.@this Set(object host, string key, object? value, context ctx)
{
    var prop = host.GetType().GetProperty(key, Public | Instance | IgnoreCase)
        ?? throw new OutputException($"{host.GetType().Name} has no property '{key}'.");

    prop.SetValue(host, Fit(value, prop.PropertyType, ctx));
    return /* the host — same live reference, caller keeps its clr carrier */;
}

// Fit — the incoming value fitted to the slot's declared type. Shown in full (no hidden methods):
static object? Fit(object? value, System.Type slot, context ctx)
{
    if (value is null || slot.IsInstanceOfType(value)) return value;      // already fits
    if (value is clr.@this { Value: JsonElement je })
        return Read(slot, je, ctx);                                       // json → host graph (the Read above)
    if (value is item.@this iv) return iv.Clr(slot);                      // a plang value LOWERS ITSELF
    return value;                                                         // raw CLR — let SetValue throw honestly
}

// Read (minimal in Stage 1, full .pr graph in Stage 2): construct a host of `target` from a
// format-agnostic reader — the mirror of Output's [Store] reflection.
public object Read(System.Type target, ref json.Reader reader, ReadContext ctx)
{
    // per [Store] property: List<T>  → BeginArray, Read(elementType) per element
    //                       List<Data> (action.Parameters) → the DATA READER's @schema:data path
    //                            (%var%-born / template / signing — byte-identical, untouched)
    //                       scalar/plang value → the type's own serializer reader
    //                       nested host → recurse Read
}
```

**OBP:** type knowledge lives in ONE place — the host's C# class declaration — reflected at the leaf, exactly like `Output` already does (`Tagged.PropertiesFor`). Read/Set/Output/Navigate become symmetric on the `*` kind. No STJ, no `GoalReadOptions`.

---

## Stage 3 — sync `Create` per type; the default shrinks; the entity delegate

```csharp
// item/ICreate.cs — the END-STATE default: pass-through, facet, decline. Nothing else.
static virtual TSelf? Create(@this value, data.@this data)
{
    if (value is TSelf self) return self;
    if (value.Facet<TSelf>() is { } facet) return facet;
    data.Fail(new error.Error(
        $"%{data.Name}% holds a {value.Mint().Name} — '{NameOf(typeof(TSelf))}' cannot be created from it.",
        "CreateItemDeclined", 400));
    return null;
}
// SYNC — dispatch stays `T.Create(await Value(), this)`: the await is in FRONT of the door;
// Create receives a materialized item. Signature unchanged from today.
```

```csharp
// number/this.cs — Stage 2 lands number ALREADY kind-owned (same move as the relocation;
// golden rule, no interim switch). number.Create only RESOLVES the storage kind and delegates:
public static new @this? Create(item.@this value, data.@this data)
{
    if (value is @this self) return self;
    // storage kind:  declared (data.Type.Kind — already a kind token)
    //             →  else the source's own (a number keeps its kind; a literal sniffs its shape —
    //                today's string-sniff, relocated to where the kinds live)
    //             →  else the app default: long (a SETTING, not hardcoded)
    var kind = data.Type?.Kind ?? ...;
    return kind.Build(value, data);      // each precision owns its build at type[number].kind[<k>]
}
// Dies with the switch family: NumberKind enum, KindFromName (the obpv name goes with it),
// CoerceToKind, the two serializer switches, FromDoubleAsKind.
```

```csharp
// dict/this.cs — a container builds from a navigable source by asking it to ENUMERATE ITSELF
// (absorbs kind.behavior.dict.Convert — dict is a TYPE, its build belongs on dict.Create):
public static new @this? Create(item.@this value, data.@this data)
{
    if (value is @this self) return self;
    if (value.Clr<object>() is string s && string.IsNullOrWhiteSpace(s))
        return new @this(data.Context);                           // {} from blank (the .pr shape)
    if (value is clr.@this or @this)                              // clr(json) object / another dict
    {
        var d = new @this(data.Context);
        foreach (var (key, child) in value.EnumerateItems(data.Context))
            d.Set(key.ToString(), child);                         // children stay lazy Data — sync
        return d;
    }
    data.Fail(...); return null;
}
```

```csharp
// type/this.cs — the runtime door: the ENTITY owns closing its own builder. One shared
// logic-free thunk; lazy bind = the single reflective touch. No static helper class.
// type.Build DIES — FromRaw lands here; the defer rule is the FIRST branch:
public item.@this? Create(object? raw, data.@this data)
{
    if (raw is null)             return @null.Instance;
    if (raw is string or byte[]) return new source(raw, this);   // wire-raw → DEFER, parse on first touch
    var value = raw as item.@this ?? /* lift raw CLR */;
    return (_builder ??= Bind(ClrType))(value, data);            // already a value → T.Create, build now
}

Func<item.@this, data.@this, item.@this?>? _builder;
static Func<item.@this, data.@this, item.@this?> Bind(System.Type clr)
    => (Func<item.@this, data.@this, item.@this?>)typeof(@this)
        .GetMethod(nameof(Builder), NonPublic | Static)!.MakeGenericMethod(clr).Invoke(null, null)!;

static Func<item.@this, data.@this, item.@this?> Builder<T>() where T : item.@this, ICreate<T>
    => (v, d) => T.Create(v, d);
// users: `as <type>` clause, kind→Create delegation, settings binding.
// Deleted: convert.OfStatic/Of/Invoke/Discover (per-call MethodInfo.Invoke from a hub).
```

```csharp
// kind/behavior/html.cs — a REAL kind converter (cross-kind transform; async lives HERE).
// ⚠ PROOF-OF-CONCEPT ONLY — the `Kind?.Name is "md"` string-compare must NOT reach production;
//   the real dispatch shape is designed when kind converters land (TODO, that stage's work).
public override async ValueTask<data.@this> Convert(data.@this source, context ctx)
{
    if (source.Type?.Kind?.Name is "md" && await source.Value<text>() is { } md)
        return ctx.Ok(new text.@this(Markdown.ToHtml(md.ToString())) { Kind = "html" });
    return ctx.Error(...);                                        // decline what it can't render
}
```

---

## Stage 5 — module/action views (reflection at the leaf)

```csharp
// module view — backed by a namespace, holds NO copy:
public list<action.@this> Actions =>
    new(_reg.Names(_ns).Select(a => new action.@this(_reg.Type(_ns, a), _ctx)), _ctx);

// action view — backed by the live handler System.Type; the ONE place reflection happens:
public list<type.@this> Properties =>                             // keyed by property name
    _handler.GetProperties(Public | Instance)
        .Where(p => p.Name is not "EqualityContract" and not "Context")
        .Keyed(p => p.Name, p => _ctx.App.type[Unwrap(p.PropertyType)]);   // Data<T>/[Code]T → plang type
// consumers read type.Name — never a System.Type, never GetTypeName at a call site.
```

---

## OBP self-audit

| surface | check | verdict |
|---|---|---|
| `*`-kind `Set`/`Read` | mirrors of existing `Navigate`/`Output`; class declaration = the one type source | clean |
| `ICreate` default | 3 lines: pass-through/facet/decline; every type owns its own `Create` | clean |
| sync `Create` | await in front of the door; no signature change | clean |
| entity `Builder<T>` | on `type.@this` (owner closes its own builder); no static helper class; lazy, one reflective touch | clean |
| `dict.Create` | asks the source to enumerate itself; never reaches into `clr.Value` | clean (Rule #7) |
| kind converter | transform on the kind it produces (html owns md→html); async where I/O lives | clean |
