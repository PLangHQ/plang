# Lazy containers + structural `Clr` — implementation plan

## The problem
`list.Value()` / `dict.Value()` eagerly *render* the whole container (resolve every
`%ref%`/template, build a parallel native container). That:
- copies a whole list/dict on any change (a trillion-item list with one template → trillion re-houses),
- is the async→sync bridge the sync read surface (`Clr`, serializer, `Peek`) silently depends on,
- bakes nested template values to dead strings during `dict → record` conversion
  (`LlmMessage.Content = "%prompt%"` as a C# string, door gone).

The conversion hub `TryConvert` makes it worse: it lowers any plang value to raw CLR
up front (`item.Clr<object>()` at `Conversion.cs:~226`) then rebuilds from raw — a
type-switch + flatten that kills doors and types.

## The model (decided with Ingi)
- **`Value()` is lazy.** A container returns `this`; it does NOT render itself.
  A scalar/template still resolves (text/path doors unchanged).
- **No whole-container materialization, ever.** A list is **enumerated**; each row
  resolves/converts itself as the consumer pulls it, in the consumer's single pass.
  The only collection built is the one the consumer actually needs.
- **Conversion is `Clr`, polymorphic — the value converts itself.** No `As`
  conversion method, no `ValueClr`, no `TryConvert` hub on the plang-value path.
  - `item.Clr(Type)` (leaf) = `ClrConvert(Peek(), target)` — unchanged.
  - `dict.Clr(Type)` for a **record target** = build the object, per-prop
    `Slot(name).Clr(propType)`. One pass over props, no flatten, no JSON.
  - `Data.Clr<T>()` = `Peek().Clr<T>()` (sugar so a consumer writes `row.Clr<LlmMessage>()`).
  - Lists are NOT converted to other lists (no `list.Clr` record/list build that
    materializes) — they're enumerated; each row `Clr`s itself in the consumer.
- **Door preservation falls out of `Clr` for free.** `ClrConvert` line 333:
  `if (target.IsInstanceOfType(backing)) return backing;`. So a `text` asked for as
  `text` returns the SAME instance → the `%prompt%` door survives into a `text`-typed
  field; resolution happens later at the .NET perimeter.
- **Domain records hold plang types, not CLR primitives.** `LlmMessage.Role/Content`
  become `text` (was `string`), carrying context so a field resolves itself
  (`await msg.Content.Value(...)`) at the perimeter (the OpenAI HTTP call).
- **Enumeration yields `Data` rows, not bare items** — `Data` is the rich carrier
  (`Parent`, `Name`, `Type`, `Context`, the `.Value()` door). `list<T> : IEnumerable<Data>`.
- **`Row(i)` / `Slot(key)` set `Parent`** so a row can say who its parent is.

## Build order (each step verifiable; let unrelated stuff break as we go — agreed)
1. **Foundation — structural `Clr`, kept GREEN (fields stay `string`, `Value()` stays eager):**
   - `Data.Clr<T>()` / `Data.Clr(Type)` = `Peek().Clr(target)`.
   - `dict.Clr(Type)`: record target → per-prop `Slot(name).Clr(propType)`; else existing raw path.
   - Hub: replace the `value is item.@this { Clr + ReferenceEquals + recurse }` block
     (`Conversion.cs:~226`) with `value.Clr(target)`; route the `list<T>` element arm
     through per-row `Clr` too.
   - Verify full suite green (behavior-equivalent: with eager `Value()` + `string`
     fields, structural `Clr` gives the same result as the old flatten/JSON path).
2. **`list<T> : IEnumerable<Data>`** (drop the invented `.Rows()`); `Row`/`Slot` set `Parent`.
3. **`LlmMessage.Role/Content` → `text`** (carry context). Ripples: `[Store]`
   serialization, `[LlmBuilder]` schema (builder LLM should see `text`, not `string`),
   signing/wire.
4. **Flip `Value()` lazy** (container returns `this`); migrate consumers to
   `foreach (row) ... row.Clr<T>()` / `row.Value()` per item. The LLM `messages` path
   end-to-end first (OpenAi loop), then the rest (AsT tests, DeepResolution, etc.).
5. Sweep the remaining `Value().Clr()`-on-container consumers; green.

## Thread: JSON out of object construction → `type.Create` (decided with Ingi)
JSON is a serialization format; turning a raw CLR value into its plang type is a
**type-system** job, not json's. Today object construction routes through
`json.Parse` / `json.BornFromRaw`, and dict→record "serializes-then-deserializes" —
all wrong.

- **`type.Create(raw)`** is the one factory: raw CLR → plang value (`"a"`→`text`,
  `5`→`number`, `Dictionary`→`dict` O(1) alias, `List`→`list`). It lives in
  `app.type`. `Data.Lift` (`data/this.cs:194`, via `convert.OwnerOf`) already *is*
  this — promote/rename it to `type.Create`, give it the type-system home.
- **`json.Parse` legit sites stay** (real JSON input): `Wire.cs:423`,
  `list/Json.cs`, `dict/Json.cs` (the `[JsonConverter]`s), `Fluid.cs`,
  `CommandLineParser.cs`. Their **leaf** creation delegates to `type.Create`.
- **`json.Parse` smell sites become `type.Create`** (object construction): the
  `Data` ctor (`:281`), `SetValue` (`:400`), source (`:847`), `type/this.cs:415`,
  and `Row`/`Slot`'s `BornFromRaw` (`list:93`, `dict:99`). Delete `BornFromRaw`.
- **A `JsonElement` reaching the `Data` ctor is a BUG → throw.** The wire/json
  boundary must convert `JsonElement → plang` *before* constructing a Data; the
  ctor must never see a `JsonElement`. (`json.Parse` in the ctor was the silent
  net catching strays — replace with a loud throw so we see the offender.)
- **Audit before pulling `json.Parse` from the ctor:** scan `new Data(...)` sites
  for a `JsonElement` value reaching them; convert those at the boundary.
- Not strictly blocking the lazy-container work — the current ctor (`Lift(json.Parse)`)
  passes a raw slot through fine, so `new Data("", slot)` already works. Sequence
  this thread WITH the structural-`Clr` work (same conversion area).

## Thread: conversion core — kill TryConvert↔ClrConvert (decided with Ingi)
The conversion hub is tangled: `item.Clr(target) → ClrConvert → catalog.TryConvert`,
and `TryConvert`'s OwnerOf-build arm does `wrapper.Clr(target)` — so LOWER calls the
hub and the hub calls LOWER. Terminates only by luck for scalars; loops forever for
containers (a family builds a plang value that never satisfies a CLR container
target). This is also why B (containers in `OwnedClrTypes`) stack-overflows:
`OwnerOf` is dual-purpose — "raw value's family" (LIFT) AND "target type's family"
(BUILD) — and a container family yields a plang value, not the CLR target.

**The fix — three directions, one owner each, NO cross-calls:**
1. **LIFT**  raw CLR → plang value   — owner `type.Create(raw)` (done).
2. **LOWER** plang value → CLR type  — owner `value.Clr(target)`, **terminal**.
   A container lowers itself by lowering each CHILD (`row.Clr<X>()` / entry per
   prop) — never the hub. Base: identity / IConvertible ChangeType / fail.
3. **CONVERT** value → plang family X — owner `X.Convert(value)`, terminal (e.g.
   `number.Convert(text)` parses), produces the target plang value directly.

Universal entry becomes a **dispatch by target**, no recursion:
```
Convert(value, target):
  target.IsInstanceOfType(value) → value                 // identity
  target is item-derived (PLANG)  → Family(target).Convert(value)   // BUILD, terminal
  else (CLR target)               → value.Clr(target)               // LOWER, terminal
```
Build never lowers; lower never builds. Then B is safe (a list registers `IList`;
lowering list→List<object?> is `list.Clr`, terminal; `OwnerOf` only feeds BUILD).

**Known hard part:** LOWER of dict→record for COMPLEX records (Goal) currently leans
on JSON-deserialize-with-custom-options (`GoalReadOptions`, converters) — a naive
per-prop loop won't replicate it. So record-lowering must route to the type's own
reconstruction (FromWire / its Convert), not a generic prop loop. Scope this before
making the base `ClrConvert` fully terminal.

**Also a smell (Ingi):** `list`/`dict` have THREE ways to build from raw — the
ctor (O(1) alias), `FromRaw` (element-wise), and the `Convert` hook. Collapse to one
owner as part of this.

Build order: (1) container self-lowering `Clr` (LOWER for collections); (2) record
LOWER via the type's own reconstruction; (3) drop `TryConvert` from `ClrConvert`
(base terminal); (4) collapse `TryConvert` to the 3-way dispatch; (5) then B.

## Findings (this session)
- **Recursion is dead with inline-`Clr`.** Once `list`/`dict` lower themselves inline
  (terminal), `wrapper.Clr(target)` bottoms out — re-probed B (containers in
  `OwnedClrTypes`): no stack overflow, suite green. BUT B is a **detour, not a win**:
  it routes a CLR-container target through `OwnerOf→Convert` (build a plang container)
  then `wrapper.Clr` (lower it) — the build-then-lower smell, just non-looping —
  whereas without B that target uses the direct element-convert arm. So B was reverted;
  the real fix is the dispatch collapse (LOWER a CLR target directly, never build-then-lower).
- **Convert hooks must throw, not return null (Ingi).** A failed conversion silently
  returning `null` hides the error. Today `null` is overloaded as "decline / fall through
  to another arm" in the hub, so it can't change piecemeal. With the 3-way dispatch the
  owner is chosen by target type → its `Convert` converts-or-throws, never declines. So
  "throw not null" lands WITH the dispatch collapse, not before.

## Finding: the LOWER/CONVERT boundary is FAMILY-based, not CLR-vs-plang
Tried the naive dispatch "plang value + CLR target → LOWER (value.Clr)". It broke:
- `text → int` (CLR target) threw `FormatException` — because `text→int` is NOT a
  lower, it's a **CONVERT** (`number` parses text, returns an error gracefully). Only
  `text → string` (its OWN backing) is a LOWER.
- `image.Is(path)` facet broke (collateral).
So the dispatch can't key on "is the target CLR or plang". The real boundary:
- **LOWER** = value → its OWN family's CLR backing (`text→string`, `number→long`,
  `list→List`, `dict→Dictionary`). Terminal, the value's own.
- **CONVERT** = value → a DIFFERENT family (CLR or plang): `text→int` (number parses),
  `text→datetime`, `dict→record`. Owned by the TARGET family's `Convert`, graceful.
The dispatch must ask "is `target` this value's own family?" to pick LOWER vs CONVERT —
that's the careful part of the collapse, and why it's not a one-arm change.

## CONVERT-arm migration — pattern + status
A `TryConvert` target arm migrates by giving the **target type its own discoverable
`Convert` hook**: `OwnerOf` already ends with `Discover(target)` (a type with a static
`Convert(object,string,ctx)` owns itself), so once the type has the hook, the existing
`OfStatic` build-arm invokes it and the special arm is deleted. The type gains its
builder; the hub loses an arm. **No big-bang** — one arm at a time.

- **choice<T> — DONE** (`choice<T>.Convert` added, arm removed, green).
- **list<T> — the clean answer is LAZY, not an eager `Convert`.** `list<LlmMessage>`
  is `type=list, kind=LlmMessage`; each element is `type=dict, kind=LlmMessage` — a
  dict that *returns as* LlmMessage. `list<T>.Create` is then an **O(1) re-tag**
  (stamp `kind=T`), NO element loop, NO `TryConvert`; the `dict→LlmMessage` build
  happens **per element, lazily, on `row.Value()`**, driven by `kind`. Touch 2 of a
  trillion rows → 2 materializations.
  - **This is OURS to build (not deferred).** The element carries `typeof(T)` (a real
    `System.Type`), NOT a `kind` string — `kind` is a string and can't faithfully name
    a domain type. The slot exists: `type` entity's `_clrType`, set via the
    `@this(string name, System.Type clrType)` ctor (`type/this.cs:771`). Concrete steps:
    1. `list.@this` gets `protected virtual type? ElementType => null`; `list<T>`
       overrides → a type entity carrying `typeof(T)`.
    2. rows stamp their `Data` with `ElementType` when set (so a row knows it returns as T).
    3. `.Value()`/`type.Build` materialize a native that doesn't match the stamped
       `ClrType` by converting it (`dict.Clr(typeof(T))` → the record build). Today
       `Build` passes a non-leaf native straight through (`type/this.cs` Build) — extend
       it: when `ClrType` is a domain type the native isn't, convert.
    4. `list<T>.Create` = O(1) re-tag (stamp `ElementType`); delete the eager `list<T>`
       arm in `TryConvert`. Materialization is per-element, lazy, on `row.Value()`.
- **record / string→json / FromWire / ctor-string arms** — same family-hook pattern,
  but they're the `dict→record` deserialize question (record owns `From` vs deserialize)
  and tie into the same kind-driven materialization. Sequence after the kind machinery.

## Watch-outs
- The 2×O(n) trap: never build an intermediate collection then walk it again. A
  `dict.Clr` record build is one object (fine); a `list`→`list` materialization is NOT (banned).
- `ClrConvert(text, typeof(text))` identity arm (line 333) is load-bearing for door survival — verified.
- `Peek` (not `Value`) inside `Data.Clr`: we want the structural value (the dict), with
  field doors intact; resolution is deferred to the perimeter.
- Naming: keep the existing `Data.As<T>()` (lazy typed VIEW, returns `Data<T>`) as-is —
  conversion is `Clr`, so no collision, no generator change.
