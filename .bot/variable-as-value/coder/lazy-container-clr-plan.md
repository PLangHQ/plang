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

## Watch-outs
- The 2×O(n) trap: never build an intermediate collection then walk it again. A
  `dict.Clr` record build is one object (fine); a `list`→`list` materialization is NOT (banned).
- `ClrConvert(text, typeof(text))` identity arm (line 333) is load-bearing for door survival — verified.
- `Peek` (not `Value`) inside `Data.Clr`: we want the structural value (the dict), with
  field doors intact; resolution is deferred to the perimeter.
- Naming: keep the existing `Data.As<T>()` (lazy typed VIEW, returns `Data<T>`) as-is —
  conversion is `Clr`, so no collision, no generator change.
