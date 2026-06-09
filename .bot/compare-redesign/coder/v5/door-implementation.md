# Stage 2 door — code-level cutover runway

Stage 2's prose (architect `stage-2-value-door.md`) leaves the concrete C# to the coder.
This file pins the shape so the next session executes the cutover mechanically. It is the
one all-or-nothing change in 2→6; the build is red from the first edit until every
Data-receiver `.Value` site is migrated.

## Done in v5 (green, committed)
- Stage 1 `Comparison` enum (`PLang/app/data/Comparison.cs`).
- `ScalarValue` → `Peek()` (method) — `PLang/app/data/this.cs:247`; ~20 call sites across
  PLang/ and PLang.Tests/ migrated. This is Stage 2's "Peek()" deliverable, landed standalone.

## The door — exact shape

Replace the `public virtual object? Value { get; set; }` property (`this.cs:197`) with:

```csharp
private object? _value;       // a PLang item subtype (or null) — never raw CLR
private bool   _present;      // true once _value is materialised in memory

// THE door. ValueTask: zero-alloc sync-complete when in memory (the 99% case);
// async only when it must read (a file/url reference's first touch — Stage 3).
public ValueTask<object?> Value()
{
    if (_valueFactory != null) { _value = _valueFactory(); _valueFactory = null; _present = true; }
    if (_present) return new ValueTask<object?>(_value);
    return Load();                       // pending read+parse; allocates only here
}

protected virtual async ValueTask<object?> Load()   // override seam (was the `Value` getter override)
{
    _value = await ReadAndParse();       // folds Materialize() + ILoadable.LoadAsync()
    _present = true;
    return _value;
}

// Write side — the old `Value` SETTER becomes a method (every `.Value = x` → `.SetValue(x)`).
public void SetValue(object? value) { /* old setter body; sets _present = true */ }
```

### Override seams (don't miss these)
- **`Data<T>.Value`** (`this.cs:1486`, `new T? Value`) → becomes `async ValueTask<T?> Value()`
  hiding the base, OR drop it and let `Data<T>` consumers `(T?) await base.Value()`. Recommend
  the latter — a `new` async method that hides a base async method is a footgun. `GetParameter<T>`
  returns `Data<T>`; the handler does `var v = (T?) await Param.Value();` (or `await Param.As<T>()`
  once `As<T>` moves inside the door). Decide this first — it sets the handler migration pattern.
- **`DynamicData.Value`** (`this.cs:1563`, `=> _valueFactory()`) → override `Load()` (or fold the
  factory into the base `Value()` factory branch shown above — DynamicData then needs no subclass
  override, just `SetValue(factory)`). Architect calls DynamicData "the sync recompute case."

### Serializer touchpoint (the sync boundary)
`Wire.Write` reads `data.Value` synchronously (`Wire.cs:485, 528, 545`) + `data.Raw`/`RawUntouched`.
Serialization must materialise BEFORE the sync write (architect: "already the codebase pattern").
Two options — pick one and apply consistently:
1. Add an **internal sync** `object? Materialized()` = "return `_value` if `_present`, else throw"
   (never reads). Wire calls `Materialized()`; callers that serialize a pending reference must
   `await data.Value()` first. The throw-if-not-present is the OBP-clean version (no hidden I/O on
   the wire path).
2. Make `Wire.Write` async and `await data.Value()`. Larger ripple (the STJ converter is sync).
Recommend (1): keep `Peek()` for the verbatim-passthrough raw path (`RawUntouched`), add
`Materialized()` for the parsed-value path. The `[JsonPropertyName("value")]`/`[Out,Store]` tags
move off `Value()` (a method has no STJ property) — Wire already writes the envelope by hand
(`this.cs:170-176`), so this is safe; just confirm `WireLocal`/`Wire.Write` no longer rely on
property reflection for the value slot (they don't — they read `data.Value`/`data.Raw` explicitly).

## Navigation goes async (`ValueTask`)
Chain `Data.GetChild`/`GetChildValue` (`this.Navigation.cs`) → `Variable.Get`
(`variable/list/this.cs:570` dotted-path) → `Variable.Resolve` (`:649`) → `Value()`. Each becomes
`ValueTask`-returning, sync-completing in memory, awaiting only the first content read. **Await
once** per site (no store-and-await-twice). Sync surfaces that must NOT navigate:
- `ToString`/`Equals`/`GetHashCode` (`this.cs:1328`, and `Data<T>`): read `_value` backing only.
- `ToBoolean` (`this.cs:1229`) reads `Value` — it's already paired with async `ToBooleanAsync`
  (`:1259`); `ToBoolean` must read the materialised backing only, push truthiness-needing-IO to
  `ToBooleanAsync` (already done for `IBooleanResolvable`).

## `GetParameter<T>` lazy (source generator)
- `Action.GetParameter<T>(name)` (net-new; only non-generic `GetParameter` exists,
  `goal/.../action/this.cs:220`) returns a **lazy `Data<T>`** — wraps the param `%var%`, sets
  context, does NOT call `As<T>`/navigate. The `AsT_Impl` body moves *into* the async `.Value()`.
- Source-gen emits `this.Param = Action.GetParameter<T>("param")` in the lazy partial
  (`PLang.Generators/Emission/Property/Data/this.cs:44,54,58`) instead of the eager
  `__ResolveData(name).As<T>(Context)`. Collapses `__ResolveData` away (it already returns
  `NotFound` and ignores `Context`).

## The ~42 `param.Value!` handler sites — `await → guard → use`
Today (eager): `if (!Path.Success) return Path; ... Path.Value! ...`
After (lazy):  `var p = await Path.Value(); if (!Path.Success) return Path; ... p ...`
The guard moves **after** the await — pre-await it inspects the unresolved Data and stops catching
bad-scheme/unset-`%var%`/convert failures (architect v4). Inventory the sites with:
`grep -rn "\.Value!" PLang/app/module --include=*.cs` (start at `file/read.cs:31`). Each migration is
await + guard-reorder + use, NOT a mechanical `.Value` → `await .Value()`.

## `.`/`!` resolver, `data.Type`, typed-at-creation, no-ToRaw
- `data.Type` getter (`this.cs:373`) → `return _type;` once the door guarantees `_value` is a typed
  `item` (the CLR-sniffing `leaf.ToRaw()...GetType()` block at `:390` is deleted, not migrated).
  Depends on "value always typed at creation" — close the few paths that still leave a bare C#
  `string`/`List`/`Dictionary` in `_value`.
- `.` = content / `!` = properties+envelope, `!` resolved chain-wide. Lives in `this.Navigation.cs`.
  `!` reserved core (`@schema`/`type`/`error`/`success`) first, then walk `.Is()` chain / `properties`
  bag. `%x!type%` = headline, `%x!type.list%` = chain.
- `text.Value` public-raw → private; generic `item.ToRaw()` removed (28 sites — most are Stage 6:
  comparison/assert/number.Convert deleted with typed Compare; sqlite/settings bind the json blob;
  openai=dict-nav, identity=STJ round-trip, fluid=text serializer). Gate as **warning** in Stage 2,
  flip to error in Stage 7.

## Migration worklist driver
After the door edit, `dotnet build PLang` — the compiler error list IS the Data-receiver `.Value`
inventory (views keep their sync `.Value`, so they won't error). Migrate file-by-file. Confirmed
empirically: 977 `.Value` reads total in PLang/, but the migrating subset is Data-receiver only.

## Verification owed (architect handed two)
- Confirm raw-CLR access is genuinely bounded to leaves (the no-`ToRaw` premise) — sample handlers.
- Confirm `number` over a boxed numeric is acceptable.
- Coder v3-C: confirm nothing reads envelope `name` on the wire read-path before dropping it
  (`FromWireShape`/`TypeFromWire` `this.cs:781,793`, nested-Data recognizer, `ResolveParameter`).
