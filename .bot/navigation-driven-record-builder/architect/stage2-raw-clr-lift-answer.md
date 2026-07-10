# Decision — the raw-CLR lift is NAVIGATED: `app.type[clrType].Create(raw, data)`; `OwnerOf` dies fully

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-born-native-lift-of-raw-clr.md`. Your trace was right — the lift rode two dying mechanisms; the fix is neither of them, and it's not a perimeter switch either (my first draft — Ingi killed it as the enum-of-types mistake again: central, closed, plugin-hostile).

## Your three questions

1. **Home:** this is the **scalar arm of the already-ruled Data-ctor reroute** (the Json-sweep blast map: `data/this.cs:226/:325` — containers → `FromRaw`, JsonElements → `clr(json)`, and now scalars → the lift below). It lands **with the hub deletion** — the lift must exist the moment `OfStatic` dies.
2. **The CLR→plang map:** it's the **collection's clr-keyed index** — `ctx.App.Type[System.Type]` (exists today), built from each type's own **`OwnedClrTypes` declaration**. Decentralized and plugin-extensible: a `code.load` type joins by declaring its CLR shapes on itself; no central list, no reflection-`Discover`. **`OwnerOf`/`_ownership`/`BuildOwnership` die completely** — this updates the plan's "[relocate/stays]" to dead; the declarations feed the collection's index instead.
3. **Raw-CLR-in-a-Data is a real perimeter, not a test artifact.** C# handlers and `code.load` hand raw values to Data constantly; the model's answer is born-native — `new Data("a", 9)` yields `number(9)`, never `Clr(9)`. The tests stay as written and go green via the lift. The `Clr` carrier remains only for genuinely unowned POCOs (rung 2).

## The shape — three layers, each honest

```csharp
// ── type.Create(raw) — the polymorphic perimeter: no switch, pure navigation ──
if (raw is item.@this v) return v;                        // already native — pass through
return ctx.App.Type[raw.GetType()]?.Create(raw, data)     // ① the collection's clr-keyed door
    ?? new Clr(raw, ctx);                                  //    genuinely unowned → rung 2, as today

// ── type.@this — the entity door takes OBJECT (the entity IS the runtime boundary;
//    a boundary taking object is the sanctioned crossing; an item is an object, so the
//    as-clause / kind-delegation / settings-binding callers use the SAME door) ──
public item.@this? Create(object? raw, data.@this data) => (_builder ??= Bind(ClrType))(raw, data);

// ── Builder<T> — the ONE place the raw→plang bridge lives ──
static Func<object?, data.@this, item.@this?> Builder<T>() where T : item.@this, ICreate<T>
    => (raw, d) => T.Create(raw as item.@this ?? new Clr(raw, d.Context), d);
    //             already an item → straight to the plang-pure door
    //             raw CLR → one-line bridge; immediately unwrapped by the core's own
    //             Clr<object>() — never stored, never returned
```

- **`T.Create(item)` stays plang-pure** — the clr-leak ruling intact; the per-primitive arms stay on their owners (number's `int i => i, long l => l` source-fidelity arms, bool's `bool` arm, text's `string` arm — a perimeter switch would have duplicated every one of them centrally).
- **The transient `Clr` wrap** — one line, inside the one closure. Note vs the "never a clr wrapping a scalar" comment: that rule is about *produced* values (never hand back `Clr(5)` as a result); this wrap is a momentary mechanism into the door that immediately produces the native value. Not a violation; say so in a comment at the wrap.
- **`json.Parse` leaves the Data-ctor path** (that reroute was already ruled; this specs its scalar arm) — a raw int never touches the DOM walker.

## What dies with this

`convert.OwnerOf` / `_ownership` / `BuildOwnership` (fully — the plan's "[relocate/stays]" is now dead; the `OwnedClrTypes` declarations feed the collection's index) · the hub involvement in the lift (`OfStatic`, already dying) · the Data-ctor's `json.Parse` calls for scalars (the ruled reroute's scalar arm).

## Acceptance

- `new Data("a", 9)` → `number(9)`; `9.Compare(10)` → `Less` (the forward-looking `CompareRedesign` tests go green — the coder's own success signal).
- A `code.load` type declaring `OwnedClrTypes` lifts through the same door (the extensibility pin — the reason the switch died).
- An unowned POCO still lands as `Clr` (rung 2 unchanged).
- Grep-zero: `OwnerOf`, `json.Parse` in `data/this.cs`.
