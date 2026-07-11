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

"The public doors are pure one-liners" is permanent for the **data door** and holds for the **context door only until the Build death lands.** The standing ruling ("the defer rule is the entity door's first branch") plus the FromRaw settlement (2026-07-11, write-up pending) then make the context door ladder-then-thunk:

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

## Acceptance

- Grep `Courier` → prose comments only; no method, field, or type carries it.
- Name-diff on the suites: zero behavior change (rename + bind-mechanics swap).
