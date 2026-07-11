# Decision — `Courier<T>` is deleted; every name in the chain is `Create`; the doors go branch-free

**From:** architect. **Settled with Ingi (2026-07-11).** Rules the `Courier<T>` thunk on `type/this.cs:445`. Nothing structural changes — the two-door split stays exactly as you built it; the vocabulary and the bind mechanics change.

## Why the name existed, and why it dies

The two thunk factories were parameterless (`Create<T>()` returning a delegate), so the context/data difference lived only in the **return type** — invisible to overload resolution (CS0111), forcing a second method name. `Courier` reads as an architectural concept a reader will hunt for; the truth is "same door, second overload, the compiler made me." The fix moves the difference into the **parameter list**, where the compiler can see it — the same trick the `ICreate` pair itself uses.

## The shape (you own the final form)

```csharp
    // Never null: starts as the one-shot binder, which swaps itself for the closed thunk
    // (or the decline) on first use. Every call thereafter is a single delegate invocation.
    private System.Func<object?, global::app.actor.context.@this?, item.@this?> _byContext;
    private System.Func<object?, global::app.data.@this, item.@this?> _byData;

    // in the constructor(s) — field initializers can't reference `this`:
    _byContext = Bind;
    _byData    = Bind;

    public item.@this? Create(object? raw, global::app.actor.context.@this? context) => _byContext(raw, context);
    public item.@this? Create(object? raw, global::app.data.@this data)              => _byData(raw, data);

    // the one-shot binders — same overload trick, one verb:
    private item.@this? Bind(object? raw, global::app.actor.context.@this? ctx)
    {
        _byContext = Creatable is { } clr
            ? _openByContext.MakeGenericMethod(clr)
                .CreateDelegate<System.Func<object?, global::app.actor.context.@this?, item.@this?>>()
            : static (_, _) => null;
        return _byContext(raw, ctx);
    }

    private item.@this? Bind(object? raw, global::app.data.@this data)
    {
        _byData = Creatable is { } clr
            ? _openByData.MakeGenericMethod(clr)
                .CreateDelegate<System.Func<object?, global::app.data.@this, item.@this?>>()
            : static (_, _) => null;
        return _byData(raw, data);
    }

    // The one eligibility check both binds share: the entity's ClrType when it is an
    // ICreate<clr> family — ICreate<clr> SPECIFICALLY (a subtype implementing ICreate<base>,
    // e.g. FilePath : ICreate<path>, can't close Create<subtype>); null for a primitive/host
    // entity, whose doors decline so the collection perimeter falls to the next rung.
    private System.Type? Creatable
        => ClrType is { } clr
           && typeof(item.@this).IsAssignableFrom(clr)
           && System.Array.Exists(clr.GetInterfaces(),
                  i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.ICreate<>)
                       && i.GenericTypeArguments[0] == clr)
           ? clr : null;

    // Both generic thunks are Create<T> — the context/data difference lives in the parameter
    // list, where overload resolution can see it (a parameterless factory pair differing only
    // by RETURN type is CS0111 — the reason a second name once existed here). Logic-free:
    // the raw rides straight into the type's own Create.
    private static item.@this? Create<T>(object? raw, global::app.actor.context.@this? ctx)
        where T : item.@this, global::app.type.item.ICreate<T>
        => T.Create(raw, ctx);

    private static item.@this? Create<T>(object? raw, global::app.data.@this data)
        where T : item.@this, global::app.type.item.ICreate<T>
        => T.Create(raw, data);

    // The two opens, disambiguated by parameter types (not by name — both are Create):
    private static readonly System.Reflection.MethodInfo _openByContext = Open(typeof(global::app.actor.context.@this));
    private static readonly System.Reflection.MethodInfo _openByData    = Open(typeof(global::app.data.@this));

    private static System.Reflection.MethodInfo Open(System.Type second)
        => System.Array.Find(
               typeof(@this).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
               m => m.Name == nameof(Create) && m.IsGenericMethodDefinition
                    && m.GetParameters()[1].ParameterType == second)!;
```

## The pieces, and why each is shaped that way

1. **`Create<T>` overloads on real arguments** — kills `Courier<T>`. `GetMethod(name, 1, …, Type.EmptyTypes, …)` can't disambiguate anymore (both share the name, neither is parameterless), hence the small `Open(secondParamType)` finder — runs twice ever, at type-init.
2. **`CreateDelegate<T>()` instead of factory-`Invoke`** — the cached delegate now points at the closed generic method itself rather than at a lambda a factory returned. One less indirection per call, same single reflective bind. (Nullability annotations don't affect the match — `ParameterType` compares by type identity.)
3. **Self-replacing bootstrap instead of `??=`** (Ingi: shave the per-call cost) — the field starts non-null pointing at `Bind`, which swaps itself for the closed thunk and forwards. Every later call is a bare delegate invocation: no null check, no branch, and the public doors become pure one-liners. Bind-on-first-use survives because `ClrType` is lazy (it can need the registry, which isn't up when `data.Type` mints fresh entities); if a registration-time eager bind ever becomes safe, the ctor assigns the closed thunk directly and the bootstrap never runs — same shape, no rework. Thread-safety unchanged from your `??=`: a first-call race double-binds the identical delegate, benign, assignment atomic.
4. **`Creatable` extracts the duplicated guard** — the 8-line `ICreate<clr>`-specifically chain (your `ac82fd38c` fix) is currently pasted in both doors; it now has two callers, so one private property answering "the closable clr, or null." Your fix's semantics carry over exactly — stated once.

## Vocabulary of the result

Public doors `Create`/`Create` → binders `Bind`/`Bind` → thunks `Create<T>`/`Create<T>` → `T.Create` — the compiler picks context-vs-data by the second argument at every layer. `Creatable` answers the one question. Fields `_byContext`/`_byData`/`_openByContext`/`_openByData` are named for the discriminating parameter, not a concept (fields can't overload — they carry the only unavoidable residue). Deliberately NOT `_context`: a context-named field on the deliberately context-free shared entity would read as the late-stamp leak it isn't.

## What stays

- The public entity pair — unchanged; it was never the problem.
- The **prose** "courier" in comments (`data/this.cs:312`, `channel/this.cs:259`) and the design docs — the metaphor explaining that carriers relay values without opening them is used correctly there. Only the API name dies.

## Forward note — the context door's shape when `type.Build` dies (settled 2026-07-11)

"The public doors are pure one-liners" is permanent for the **data door** and holds for the **context door only until the Build death lands.** The standing ruling ("the defer rule is the entity door's first branch") plus the FromRaw settlement (next section) then make the context door ladder-then-thunk:

```csharp
public item.@this? Create(object? raw, global::app.actor.context.@this? ctx, string? format = null)
{
    if (raw is null or item.@null.@this) return new item.@null.@this(Name, Kind?.Name);
    if (raw is string rawName && ClrType == typeof(app.variable.@this))
        return app.variable.@this.Resolve(rawName, ctx);
    if (raw is string or byte[])                        // the defer rule — capture, don't parse
        return new item.source(raw, Name, Kind?.Name, ctx!, Strict,
                               format ?? RawFormat(raw, ctx!), template: Template);
    return _byContext(raw, ctx);                        // already a value → the bound thunk
}
```

Everything in this ruling survives untouched underneath: the `Bind` bootstrap, the branch-free thunk invocation (now the door's tail), `Creatable`, the `Create<T>` overloads. The `format?` parameter lives on the context door only — the courier receives materialized values (no defer, no format), and the optional third parameter creates no overload collision; every existing `Create(raw, ctx)` call site compiles unchanged. This is not a violation of "the door is a one-liner" when it lands — the ladder IS the ruled first branch arriving, with Build's one honest parameter (`format`, the capture's encoding) migrating while Build's construction arms die.

## The FromRaw settlement (settled with Ingi, 2026-07-11)

Rides the same Build death — the pieces above are what make it possible.

### `data.FromRaw` dissolves

Its body is `new Data(name, type.Build(raw, ctx, format), ctx)` — once the defer branch and `format?` live on the entity context door, nothing of FromRaw's own remains: declared-type routing and the defer are the door's first branch; the wrap is the Data ctor. The four production callers become the two-call compose:

```csharp
// data/reader/this.cs:117 — was: Data.FromRaw(deferredRaw, typeRef, ctx.Context, format: deferredFormat)
var data = new Data(name, typeRef.Create(deferredRaw, ctx.Context, deferredFormat), ctx.Context);

// file/this.Operations.cs:86/:105 — was: data.FromRaw(bytes, type, Context, Raw, format)
return new Data(Raw, type.Create(bytes, Context, format), Context);

// channel/this.cs:302 — same pattern with StampType(context)
```

(~14 test files follow mechanically.)

### What `format` is, and why it rides the door

The **decode key for the deferred payload** — the MIME of the encoding the raw was captured in; it picks the serializer the source materialises through on first touch (`source.cs:189`). Derived by default (`RawFormat`: scalar → `text/plain`, container slot → `application/plang`, byte-backed → the kind's mime), **overridden when the perimeter knows the capture better** (a `.pr` slot's explicit format property, a file's own mime). Of the source mint's seven arguments, six are the type's own knowledge (Name, Kind, Strict, Template, the RawFormat derivation) — format is the ONE capture-site fact, so it's the one parameter the perimeter passes. Never on `ICreate` — the type's pure core never sees wire-raw.

### Why the defer is exactly `string or byte[]` — no defer-everything

A source defers a **decode**, and only encoded forms have one. `"5.5"` under `number{decimal}`, json under `dict`, png bytes under `image` — real, fallible work, worth skipping for slots never touched, with the failure surfacing at first touch (the KPR story). A raw CLR value (`int 9`) has no undecoded form: wrapping it in a source is one allocation PLUS the same lift later — deferral machinery costlier than the work deferred. And `Peek()` (ToString, debug, equality) would show an opaque source for every fresh `%x% = 5`, degrading every sync face for zero saved work. The `string or byte[]` test is the boundary of "has an undecoded form" — it is not a heuristic to widen.

### `list.@this.FromRaw` / `dict.@this.FromRaw` die too

Statics on the value types (`this.Convert.cs` files — the convert era), doing what the type's own `Create(object?, context)` door does: a second construction door. Callers reroute to `Create`: six `module/list` actions (`reverse`, `flatten`, `remove`, `set`, `unique`, `sort`) + `OpenAi.cs:227`. The emptied `this.Convert.cs` files delete.

### Sequencing

Entity-door defer branch + `format?` land (the Build death) → `data.FromRaw` deletes with its four callers rewritten → `list/dict.FromRaw` delete with theirs. The defer branch must exist before FromRaw dies — never a window where wire slots parse eagerly.

### Acceptance (FromRaw)

- Grep-zero: `FromRaw` across production (`this.Convert.cs` files gone).
- A deferred `.pr` slot still materialises through its slot-declared format (not the derived one) — pin one test on the override path.
- Lazy semantics unchanged: an untouched deferred slot never parses (existing laziness tests hold).

## Acceptance

- Grep `Courier` → prose comments only; no method, field, or type carries it.
- Name-diff on the suites: zero behavior change (rename + bind-mechanics swap).
