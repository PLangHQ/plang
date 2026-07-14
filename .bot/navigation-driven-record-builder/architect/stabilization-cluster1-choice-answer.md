# Cluster 1 ruling — text→choice: `Parse(symbol)` + the ICreate faces; `FromName` dies

Answer to `coder/stabilization-findings.md` Cluster 1, settled with Ingi 2026-07-14.

> **You own this.** Sketches traced against `97e40b557`; the code wins.

## How it was missed (for the record)

The Stage-2 relocation list names 13 types + GoalCall — **choice isn't on it** (`architect/plan.md`, Stage 2 first bullet). Its old parse hook died with the hub and never got a new home; the default ICreate pass-through was the silent survivor. A list omission, not a design gap — and the census sweeps can't catch an *absence*; the guard that would have caught it is a per-type "text→X" coverage test, which choice never had.

## The trace — your open questions answered

- **(b) Symbols: there is no dual table, and nothing died.** `Operator` is not an enum — it's the named-set class (`condition/Operator.cs`), and its `Registry` keys ARE the vocabulary: `"=="`, `"!="`, `">"`, `"contains"`, `"is"`, … (`:25-43`). `Choices()` returns exactly those keys (`:61`), so the LLM authors symbols already. For a CLR enum, the symbol is the member name. One token axis. **Ingi's ruling: that token is called the SYMBOL — the name-vocabulary goes.**
- **(a) Unknown symbol = malformed value, not a decline** — the declines-vs-errors policy (`defining-plang-types.md` §2) applies verbatim: wrong type declines (null); a string that names no option **throws where there is no `data.Fail`** and the courier converts. One wrinkle your sketch would have hit: `Enum.Parse`/the Operator ctor throw `ArgumentException`, which is NOT in `source.Value`'s catch list (`source.cs:159`) — a bad symbol in a declared slot would escape as a raw exception instead of `MaterializeFailed`. The parse must throw **`FormatException`** (base64's `Parse` precedent) so the born path lands it named to the binding.
- **(c) Owner: choice.** Target owns convert-from; `IRawNameResolvable` is for slots that *name* bindings (variable), not values that parse from text.
- **`FromName` dies** (Ingi's call): one caller (the Reader's reflection hook), a `context` parameter its body never reads (`choice/this.cs:84-92`), and a name that misdescribes the token. Third instance of the named-factory-beside-Create smell on this branch (`FromString`, `EncodeLazily`, now this).
- Your sketch's `raw.Clr<object>()?.ToString()` is the blind-ToString smell (§6) — the sanctioned string face is `RawText` (text answers its content; an unparsed `source` answers its raw — which means an *authored, still-lazy* operator resolves too; anything else has no string face and declines).

## The shape

### `choice/this.cs` — `Parse` replaces `FromName`; the three ICreate faces land

```csharp
/// <summary>The one symbol→choice resolution home — the wire form is the option's SYMBOL
/// (an enum member's name, a named-set registry key like "=="). Shared by the ICreate core
/// and the wire reader. THROWS FormatException on an unknown symbol (no data.Fail in scope;
/// the born path turns it into MaterializeFailed named to the binding).</summary>
public static @this<T> Parse(string symbol)
{
    try
    {
        object member = typeof(T).IsEnum
            ? System.Enum.Parse(typeof(T), symbol, ignoreCase: true)
            : _nameCtor?.Invoke(new object?[] { symbol })
              ?? throw new System.InvalidOperationException(
                  $"choice<{typeof(T).Name}>: not a named-set type — needs an enum or a ctor(string).");
        return new((T)member);
    }
    catch (System.Exception ex) when (ex is System.ArgumentException or System.Reflection.TargetInvocationException)
    {
        // ctor.Invoke wraps the named-set ctor's ArgumentException in TargetInvocationException — unwrap to one story.
        throw new System.FormatException(
            $"'{symbol}' is not a {typeof(T).Name} option. Valid: {string.Join(", ", ValidValues)}", ex);
    }
}

/// <summary>THE PURE CORE — pass-through; a raw member wraps; a string face parses
/// (malformed throws per the error policy); anything else declines.</summary>
public static @this<T>? Create(object? raw)
{
    if (raw is @this<T> self) return self;
    if (raw is T member) return new(member);
    var symbol = raw as string ?? (raw as global::app.type.item.@this)?.RawText;
    return symbol is null ? null : Parse(symbol);
}

/// <summary>The courier — converts Parse's throw to data.Fail with the option list;
/// declines fail typed.</summary>
public static @this<T>? Create(object? raw, global::app.data.@this data)
{
    try { if (Create(raw) is { } made) return made; }
    catch (System.FormatException ex)
    {
        data.Fail(new global::app.error.Error(ex.Message, "ChoiceInvalid", 400)
            { FixSuggestion = $"Valid options: {string.Join(", ", ValidValues)}" });
        return null;
    }
    data.Fail(new global::app.error.Error(
        $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name} — choice<{typeof(T).Name}> cannot be created from it.",
        "CreateItemDeclined", 400));
    return null;
}
```

No context face override — resolution is context-free (`Choices(context?)` only feeds the LLM vocabulary listing; the default face delegating to the core is correct).

### `choice/serializer/Reader.cs` — re-point the reflection hook

The cached `GetMethod("FromName")` becomes `GetMethod("Parse")`; the invoke drops the dead context arg (`new object?[] { symbol }`). An unknown symbol on the wire now throws `FormatException` → `MaterializeFailed` named to the binding, which is the validation story a declared choice slot should always have had. Doc-comment wording in the file: "the chosen option's NAME" → symbol.

### The symbol vocabulary sweep (mechanical)

`IChoice.Name` → **`IChoice.Symbol`** ("the chosen option's symbol — the bare wire form"), plus its consumers (`AreEqual`'s `ic.Name` arm in the same file; grep `IChoice` for the serializer/Normalize touch points). `defining-plang-types.md` §7's choice line: "reads the option name off any reader" → symbol (one word).

## Demolition

- `FromName(string, context)` — the method, the Reader's `"FromName"` reflection string + its "has no static FromName(name, context)" error text, and the dead context threading.
- `Name` as the option-token word on choice's surface (`IChoice.Name`, doc comments) — replaced by Symbol. `Names(context?)` (private, feeds ValidValues/Choices) keeps its job; rename to match if you touch it.

## Pins

- `Data<choice<Operator>>` slot fed the text `"=="` → resolves; the condition suite (`if`/`compare`/`elseif`) recovers — this is the acceptance signal for the cluster.
- Unknown symbol via the courier (`as`/typed slot from memory) → `ChoiceInvalid` fail carrying the valid-options FixSuggestion; via a declared wire slot → `MaterializeFailed` naming the binding.
- Enum choice from text: `"GET"` → `choice<HttpMethod>` (case-insensitive).
- The apex lift's enum rung (`HttpMethod.GET` → choice) — untouched, still green.
- `%method% == "GET"` — comparison path unchanged (`Order`/`AreEqual` never route through the core).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `Parse(symbol)` | one verb, one resolution home, two real callers (core + reader) — the base64 pattern | ok |
| three ICreate faces | construction on the type, no hub, courier owns the Fail | ok |
| `RawText` as the string face | no blind ToString; source's raw resolves authored-lazy operators for free | ok |
| `FormatException` on malformed | throw-where-no-data.Fail; rides the born path's existing catch | ok |
| `Symbol` vocabulary | one token axis, named for what it is; no name/symbol dual table invented | ok |
| `FromName` deleted | named-factory-beside-Create smell, third instance on this branch | ok |
