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

## Watch-outs
- The 2×O(n) trap: never build an intermediate collection then walk it again. A
  `dict.Clr` record build is one object (fine); a `list`→`list` materialization is NOT (banned).
- `ClrConvert(text, typeof(text))` identity arm (line 333) is load-bearing for door survival — verified.
- `Peek` (not `Value`) inside `Data.Clr`: we want the structural value (the dict), with
  field doors intact; resolution is deferred to the perimeter.
- Naming: keep the existing `Data.As<T>()` (lazy typed VIEW, returns `Data<T>`) as-is —
  conversion is `Clr`, so no collision, no generator change.
