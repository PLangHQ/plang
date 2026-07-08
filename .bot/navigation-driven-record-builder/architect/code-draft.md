# Code draft — how the shapes should look (OBP)

Companion to `plan.md`. Representative code per stage-that-needs-code, annotated with *why* it's OBP-correct. Real signatures/conventions (`@this`, `global::` aliases, `ValueTask`, `data.@this`), but:

> **You own this.** These are the intended *shapes*, not the final lines. Names, helper placement, and micro-structure are the coder's call — the load-bearing part is the OBP shape (behavior on the owner, no decompose, reflection at the leaf, one door). Where a seam is genuinely subtle I say so rather than pretend it's settled.

Two refinements to `plan.md` fell out of writing this — flagged inline as **▶ refines plan**.

---

## Stage 0 — `Create` goes async (signature only)

```csharp
// item/ICreate.cs
public interface ICreate<TSelf> where TSelf : @this, ICreate<TSelf>
{
    static virtual ValueTask<TSelf?> Create(@this value, global::app.data.@this data) { /* Stage 2 body */ }
}

// a sync leaf implementor — NO async keyword, NO state machine, NO allocation churn:
// wrap the ready value in a completed ValueTask.
public static new ValueTask<@this<T>?> Create(item.@this value, data.@this data)
{
    if (value is @this<T> already) return new(already);
    if (value is @this list)       return new(new @this<T>(list, data.Context!));
    return new((@this<T>?)null);
}

// dispatch — data/this.cs:512
public async ValueTask<T?> Value<T>() where T : item.@this, ICreate<T>
    => await T.Create(await Value(), this);
```

**OBP note:** nothing structural — a signature sweep. Only types that actually await (records) grow the `async` keyword; leaves stay allocation-free.

---

## Stage 1 — a record pulls itself from a navigable source

### `action.Create` — the navigate-pull, hand-written (the shape every record follows)

```csharp
// goal/steps/step/actions/action/this.cs
public static async ValueTask<@this?> Create(item.@this value, data.@this data)
{
    if (value is @this already) return already;                      // pass-through

    // The source is anything navigable — a clr(json) object, a dict, a clr(POCO).
    // Wrap it as a Data so we can ASK it for children by name. One path for every source.
    var src = value as data.@this ?? new data.@this("", value, context: data.Context);

    // init-only record → pull first, construct last (can't await inside an initializer).
    var module     = await src.Value<text>("module");
    var actionName = await src.Value<text>("action");
    var modifiers  = await src.Value<modifiers.@this>("modifiers");
    var parameters = await Parameters(src, data.Context);

    return new @this
    {
        Module     = module?.ToString()     ?? "",
        ActionName = actionName?.ToString() ?? "",
        Modifiers  = modifiers              ?? new(),
        Parameters = parameters,
        Synthetic  = false,                                          // materialized from source
    };
}

// The Data-leaf seam. `parameters` is an array of {name,type,value} — these are Data
// LEAVES, not record fields. Each child is READ AS A DATA through the reader (the
// JsonElement door), NEVER converted to a value: %var%/template/signing stay byte-identical.
static async ValueTask<List<data.@this>> Parameters(data.@this src, context ctx)
{
    var node = await src.GetChild("parameters");
    var list = new List<data.@this>();
    foreach (var (_, child) in await node.EnumerateItems())
        list.Add(app.data.reader.@this.Read(child, ctx));            // hands the leaf to the reader
    return list;
}
```

**▶ refines plan:** `src.Value<text>("module")` is a small combinator = `GetChild(path).Value<T>()`. Cleaner than the two nested awaits the plan sketched. Propose adding `data.Value<T>(string path)` alongside `Value<T>()`. (Name check: it's the same verb `Value`, an overload — no verb+noun smell.)

**OBP notes:**
- **Behavior on the owner.** `action` builds `action`. No outside converter reaches in and assembles it.
- **No decompose (Rule #7/#8).** `Parameters` are handed to the reader *as Data* — the courier never opens `{name,type,value}` into scalars. The one place they'd be read as values is a leaf action that declares `Data<parameters>`, not here.
- **One path.** `src` is `Data`-over-anything; the pull is identical whether the source is clr(json), dict, or POCO.

### `list<T>.Create` — a navigable source enumerates itself

```csharp
// list/this.Generic.cs
public static new async ValueTask<@this<T>?> Create(item.@this value, data.@this data)
{
    if (value is @this<T> already) return already;
    if (value is @this list)       return new @this<T>(list, data.Context!);   // O(1) re-tag

    // A navigable carrier (clr(json) array, dict) ENUMERATES ITSELF via its kind — ask it,
    // don't demand a raw IEnumerable. Each element builds itself as T through T.Create.
    if (value is clr.@this or dict.@this)
    {
        var rows = new List<item.@this>();
        foreach (var (_, element) in value.EnumerateItems(data.Context))
            if (await element.Value<T>() is { } row) rows.Add(row);
        return new @this<T>(rows, data.Context!);
    }
    return null;
}
```

**OBP note:** the old `list<T>.Convert` demanded `value is IEnumerable` (a type-switch on the source's C# shape). Now it asks the value to enumerate itself (`EnumerateItems`, which the carrier delegates to its kind). Behavior moved from a caller switch onto the value. `list<T>.Convert` folds away — this is the door.

---

## Stage 2 — collapse to one `Create`

### The default `ICreate.Create` — pass-through, facet, record-navigate, else decline. No hub.

```csharp
// item/ICreate.cs
static virtual async ValueTask<TSelf?> Create(@this value, data.@this data)
{
    if (value is TSelf self)               return self;              // already it
    if (value.Facet<TSelf>() is { } facet) return facet;            // evolved from it (chain)

    // A SCALAR/leaf type OVERRIDES Create with its own parse (number below) — it never
    // reaches this default. A RECORD builds itself by pulling its declared properties from
    // the navigable source (the reflective fallback; codegen emits a typed body later).
    if (Shape.Record(typeof(TSelf)) is { } record)
        return (TSelf?)await record.Pull(value, data);

    data.Fail(new error.Error(
        $"%{data.Name}% holds a {value.Mint().Name} — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
        "CreateItemDeclined", 400));
    return null;
}
```

`Shape.Record` is the reflection-at-a-leaf: it reads `TSelf`'s declared properties + wire names **once, cached**, and `Pull` runs the same navigate-pull `action.Create` does by hand, generically. (This is exactly `action.Create` generalized; the Stage-1 hand body collapses into it unless it carries a real quirk.)

**OBP note:** no `convert.OfStatic`, no `TryConvert`, no `dict.Clr` STJ. The default knows only the two free cases + "records navigate." Everything type-specific is the type's own `Create`.

### A scalar owns its parse — `number.Create` (relocated verbatim from `number.Convert`)

```csharp
// number/this.cs
public static new ValueTask<@this?> Create(item.@this value, data.@this data)
{
    if (value is @this self) return new(self);                       // pass-through (every override opens with this)
    var raw = value.Clr<object>();                                   // the source's backing
    if (raw is null) return new((@this?)null);

    // precision kind from the TARGET descriptor (data.Type.Kind), else sniff the string / CLR type.
    // KindFromName / Build (string-sniff) / ClrToKindSafe / CoerceToKind / FromObject / Parse are
    // number's existing internals — unchanged by the relocation (the DOOR is what moves).
    NumberKind? k = KindFromName(data.Type?.Kind?.Name)
                    ?? (raw is string s ? KindFromName(Build(s)) : ClrToKindSafe(raw.GetType()));
    if (k is null && raw is string bare)                             // no precision → free-form parse
        return Parse(bare) is { } n ? new(n) : Fail(bare, data);
    if (k is null) return Fail(raw, data);

    try   { return new(FromObject(CoerceToKind(raw, k.Value))); }    // number's existing coercion
    catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
    { data.Fail(new error.Error($"Cannot read '{raw}' as {k}: {ex.Message}", "NumberConversionFailed", 400));
      return new((@this?)null); }

    static ValueTask<@this?> Fail(object v, data.@this d)
    { d.Fail(new error.Error($"'{v}' is not a number.", "NumberConversionFailed", 400)); return new((@this?)null); }
}
```

**OBP note:** the door moves onto `number` (virtual dispatch, no reflective hub) — the Stage-2 win. The numeric internals (`CoerceToKind`, `FromObject`, `KindFromName`, `Build`, `ClrToKindSafe`, `Parse`) move as-is; only the door (`Convert`→`Create`), the kind source (param→`data.Type.Kind`), and the return convention (`context.Ok/Error`→`return`/`data.Fail`) change. `CoerceToKind`'s `switch` over the precision kind is fine — the CLR numeric tower is a closed, fixed set (unlike open-ended formats), so "4 specials + `ChangeType`" is a contained leaf, not misplaced polymorphism. (Its name is a touch opaque — a clearer one wouldn't hurt, but that's cosmetic.) `Create` holds the "is this a number?" decline (null + `data.Fail`). This is "each type owns Create, no hub" — 14 types, this shape.

### Runtime construction (`Convert(kind)` and `.pr` name→type) — the non-generic face

The subtle seam. `Value<T>()` names `T` at compile time. A runtime caller holds a `type`/`kind` token (from a `.pr` name or a `data.Convert(kind)`), so it can't name `T`. It dispatches to the resolved type's static `Create` through one cached invoker:

The TARGETED runtime face — build THIS type (the entity's own ClrType) from `value`, target
preserved. Distinct from the POLYMORPHIC `type.Create(raw)` at `:439`, which infers the type
from raw and discards the target. **Three layers** — parse on the type, one shared thunk, a
delegate on the entity (option A: reflective thunk now, generated table = B later):

```csharp
// (1) THE PARSE — static, one per type, the logic (Decision A). Already drafted above (number).
//     text.@this.Create(value, data) { ... }

// (2)+(3) BOTH LIVE ON type.@this (app/type/this.cs) — the entity owns closing its OWN Create.
//         The collection (list<type>) only holds + indexes entities; it does NOT reach in to
//         stamp them. All three below are members of type.@this:

// the entity's runtime Create — invokes its OWN builder, closed on first use:
public ValueTask<item.@this?> Create(item.@this value, data.@this data)
    => (_builder ??= Bind(ClrType))(value, data);

// LAZY: first use resolves THIS entity's ClrType → Builder<clr> via MakeGenericMethod (the
// single reflective touch, option A), then cached. Lazy so a .pr-read descriptor
// {name,kind,strict} never forces ClrType before anyone constructs through it.
Func<item.@this, data.@this, ValueTask<item.@this?>>? _builder;
static Func<item.@this, data.@this, ValueTask<item.@this?>> Bind(System.Type clr)
    => (Func<item.@this, data.@this, ValueTask<item.@this?>>)typeof(@this)
        .GetMethod(nameof(Builder), BindingFlags.NonPublic | BindingFlags.Static)!
        .MakeGenericMethod(clr).Invoke(null, null)!;

// the thunk — private static on type.@this, logic-free; T.Create resolves statically INSIDE it:
static Func<item.@this, data.@this, ValueTask<item.@this?>> Builder<T>()
    where T : item.@this, ICreate<T>
    => (v, d) => T.Create(v, d);

// callers — dict lookup + DIRECT delegate call, no per-call reflection:
await app.type["text"].Create(value, data);              // .pr read (by name)
await app.type[typeof(text.@this)].Create(value, data);  // same cached entity (by CLR type)
await app.type[elementType].Create(value, data);         // write index-arm, target preserved
```

**OBP note — why this is NOT `OfStatic` renamed.** The smell was never "reflection exists" — it was *a free hub doing a per-call reflective type-switch to find behavior that belongs on the type.* Here: the logic is on the type (`text.Create`), the delegate is on the **entity** (`entity.Create`), and reflection is **once at registration** (the thunk), not per call. `Builder<T>` is one shared logic-free method; each entity holds the *closed result* for its own type. Compile-time (`Value<T>`), targeted-runtime (`type[clr].Create`), polymorphic (`type.Create(raw)`) all land on the same static `Create`. **This targeted door fixes the blocker** (the clr(json)→`Actions.@this` write holds `action.@this` as its target) and must exist before Stage 2 deletes `OfStatic`. Contrast the incumbent: `convert.Invoke` does `MethodInfo.Invoke` *every call* from a hub — the thing we're removing.

### `data.Convert(kind)` — the kind owns the transform

```csharp
// data/this.cs
public ValueTask<item.@this?> Convert(kind.@this to)
    => to.Convert(this, _context);             // the KIND owns its converter (already the shape today)

// kind/behavior/html.cs — a real converter (md → html); the html kind knows how
public sealed class html : @this
{
    public override kind.@this Kind => "html";
    public override async ValueTask<data.@this> Convert(data.@this source, context ctx)
    {
        // md → html: same TYPE (text), the KIND changes. A source kind it can't render → decline.
        if (source.Type?.Kind?.Name is "md" or "markdown" && await source.Value<text>() is { } md)
            return ctx.Ok(new text.@this(Markdown.ToHtml(md.ToString())) { Kind = "html" });
        return ctx.Error(new error.Error($"cannot render {source.Type?.Kind?.Name} as html", "KindConvertDeclined", 400));
    }
}
```

**▶ refines plan:** the plan calls `Convert(kind)` "a thin front over `Create`." More precisely: **`Convert(kind)` dispatches to the *kind's own converter* (`kind.behavior.Convert`), which the kind owns.** A kind that's just "build the type from raw" (mp3 = build audio) *delegates* to `Type.Create`; a kind that's a real transform of another kind (html from md) does the render itself. So Convert(kind) → the kind's converter (which may call Create), not always Create directly. This matches your "a converter belonging to the html kind knows md→html." I'll correct the plan's wording.

**OBP note:** the converter lives on the kind it produces (html owns md→html) — outbound owns it. `data.Convert` is a one-line courier to the kind; no conversion logic on Data.

### The write path — the value owns its child-write

```csharp
// variable/list/this.cs — SetValueOnObject collapses; the value sets its own child
// (the clr(json) arm at :389 is ALREADY this shape — the other arms join it)
var slot = await target.GetChild(propertyName);          // navigate to the slot
await slot.Set(rawValue);                                 // the slot's own type takes the value
```

**OBP note:** the seven reflection arms (bracket-index, `IList<T>`, CLR-property, `ConvertToDictionary`) die. The value at the slot owns "take this child" — a dict sets its key, a list its index, a clr(json) materializes via `kind.Set`. One write discipline; the lower-here/convert-there divergence (Smell #4) is gone.

---

## Stage 3 — `list<type>` is the registry; the index lives on the collection

```csharp
// app.type — the collection node (was type.catalog.@this); the registry IS list<type>
public list<type.@this> list { get; }                    // enumerate for the LLM

// select by name — the keyed index is ON the collection (it owns its own index),
// not a revived side-registry. O(1) lookup; the list is the single home.
public type.@this this[string name] => list.ByName(name)
    ?? throw new KeyNotFoundException($"No PLang type registered under '{name}'.");
```

**OBP notes:**
- **Registry = the collection.** No god-object stapling identity + fold + eight sub-registries. `app.type.list` is an instance of the native `list` value; `list` appearing as an element is data self-reference (harmless).
- **Index on the owner (Smell #1).** The name→entity map isn't a public side-registry with lookup rules enforced elsewhere — it's `ByName` on the collection that owns the elements.
- **Bootstrap** (item #1): born with `System.Context`, lazily populated (assembly reflection → entities, no name-lookup), runtime-extendable (`code.load`, module choices register after). Same lazy+extendable shape the catalog has today, re-homed onto the list.

---

## Stage 4 — `module`/`action`/`type` are views over reflection; reflection at the leaf

```csharp
// module/list/this.cs — app.module.list : list<module>, the ACTION modules
// (dispatchable verbs, not C# infra folders)

// module/this.cs — a VIEW over a namespace; holds NO copy
public sealed class @this : item.@this
{
    public list<action.@this> Actions =>                 // module = namespace
        new(_reg.Names(_ns).Select(a => new action.@this(_reg.Type(_ns, a), _ctx)), _ctx);
}

// action/this.cs — a VIEW over the live handler System.Type; reflection lives HERE, the leaf
public sealed class @this : item.@this
{
    public string Name => _reg.NameOf(_handler);         // class name

    // properties as list<type>, KEYED BY NAME. Data<text> Name → key "name", value type{name:"text"}.
    // The reflection (unwrap Data<T>/[Code]T → plang type) happens ONCE, here, the one place
    // that holds the System.Type. Consumers read type.Name — never a CLR type, never GetTypeName.
    public list<type.@this> Properties =>
        _handler.GetProperties(Public | Instance)
            .Where(p => p.Name is not "EqualityContract" and not "Context")
            .Keyed(p => p.Name, p => _ctx.App.type[Unwrap(p.PropertyType)]);
}
```

The compile prompt is then a render over these plang collections, not a C# schema builder:

```csharp
// no BuildTypeEntries / Describe() / StepActions — discovery is a projection, the prompt a template
var doc = await ui.Render("modulesAndActions.template", modules);   // Fluid over list<module>
```

**OBP notes:**
- **One source (no drift).** The handler class IS the definition; `module`/`action` are navigable views (the clr-navigator idea on type metadata). No `ActionDefinition` mirror.
- **Reflection at the leaf.** `System.Type` appears only inside the `action` view. `GetTypeName(typeof(x))` at any consumer site is deleted — consumers hold a `type` and read `.Name`.
- **Producer-hands-raw smell (Rule #5) removed.** The registry used to hand bare strings that catalog + `Describe()` re-reflected. Now it hands `list<module>` with types resolved.

**Name check (#6):** `Keyed(selector, valueSelector)` reuses dict/clr `(key→value)` enumeration — not a new keyed-list type; `action.Properties` navigates like a dict. Confirm the seam is dict-backed, not a fresh collection.

---

## Stage 5 — the `.pr` reads via navigate-pull (goal builds itself)

Same shape as `action.Create` — `goal` pulls `name`/`steps`, `step` pulls `actions`, `action` pulls its fields; Data-leaf param values ride the reader untouched. So the STJ `Deserialize<goal>` retires with no new machinery — it's the record navigate-pull already built in Stage 1/2, applied to the top of the tree.

```csharp
// goal reads through the one door — a .pr is a clr(json) that Creates itself.
var goal = await clrJson.Value<goal.@this>();            // was: JsonSerializer.Deserialize<goal>(text, GoalReadOptions)
```

**OBP note:** the record skeleton (goal→steps→actions) navigates; the Data leaves (param values) still go through `app/data/reader`. The boundary that was split between STJ-reflection and the Data reader becomes navigate-pull + the same Data reader — one direction changes, the leaf seam doesn't.

---

## OBP self-audit of this draft

| new surface | check | verdict |
|---|---|---|
| `data.Value<T>(string path)` | overload of `Value`; same verb | clean |
| `Parameters(src, ctx)` (in action) | private local; noun naming the property it builds | acceptable (not a public surface) |
| `reader.Read(child, ctx)` | single verb `Read`, existing name | clean |
| `Shape.Record` / `.Pull` | `Record` = the shape; `Pull` = one verb doing real work (navigate+construct) | acceptable — flag if a single word fits better |
| `type.Create(value, data)` runtime face | single verb `Create`; same door as `Value<T>` | clean |
| `NumberKind.Of/Sniff/Coerce` | number-internal; relocated from `number.Convert` | clean |
| `list.ByName` | verb+noun? — it's "the element named X," a lookup on the collection | borderline; alt `list["name"]` indexer — **coder's call** |
| `action.Properties` / `module.Actions` | plural noun properties (collections) | clean |
| `Keyed(sel, sel)` | one verb; reuses dict enumeration | clean — confirm seam (#6) |

No verb+noun compounds slipped into a load-bearing name. Two borderlines (`list.ByName`, `Shape.Pull`) flagged for the coder rather than defended.
