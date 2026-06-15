# Epic: remove the `clr` carrier (compare-redesign)

**Decision (Ingi, 2026-06-15):** remove the `clr` class — "it was a hack I didn't
agree on." `clr` (`app/type/item/clr.cs`) is the rung-2 carrier for values PLang
has no real item type for. Every value must become a real `item.@this`; the
fallback, `SetValueDirect`, and `Lower<T>` all retire with it.

This subsumes test cluster **B2** (CultureInfo signing cycle) and likely **J**
(LLM `{Cacheable,Prior,IsLeaf}` leak) — both are "a carrier/item reflected as a
transparent property bag." See `test-failure-clusters.md`.

## Why the cycle happens (the symptom that started this)
Signing normalizes a Data in `View.Out`. A `clr` from `Wrap()`/Compress sits in
the graph. `clr` is a **transparent type** (no `[Out]` tags → `Tagged.PropertiesFor`
ships every public property in Out mode), so its `Context` back-reference ships:
`clr.Context → context.App → app.Culture → CultureInfo.Parent → cycle`.
`app.Culture` has **zero readers/writers repo-wide** — dead engine config reached
only via the back-reference chain. No `culture:item` type is warranted. Remove clr
→ the chain's entry point is gone.
(Diagnostic kept: `this.Normalize.cs` cycle error now prints the property path.)

## The six jobs `clr` does (each needs a home)
| # | Job | Sites | Becomes |
|---|---|---|---|
| 1 | Rung-2 fallback for unowned CLR objects | `data/this.cs:252` (Lift) | every domain type → real `item.@this`; non-item at Lift = hard error |
| 2 | Stamped raw container (dict/list w/ `%ref%`) | `data/this.cs:503` | narrow to `dict.this`/`list.this` (exist) |
| 3 | `SetValueDirect` fallback (non-item) | `data/this.cs:548` | dies — see SetValueDirect below |
| 4 | Declared-label carrier (value + declared name it doesn't own) | `type/this.cs:451,463,482` | label onto the Data `Type` slot, not a value wrapper |
| 5 | Compress courier (seals inner Data w/ category) | `data/this.Transport.cs:92` | new `archive : item` type (Ingi blessed; like `encryption`) |
| 6 | Wire reconstruction of a labeled carrier | `data/Wire.cs:269,289` | follows from #4/#5 |

## Inventory — what actually rides as `clr` (live, all suites, 2026-06-15)
Instrumented the clr ctor, ran all suites. Distinct carried types:
- **~80% module `code.*` providers** — `signing.code.Ed25519`, `http.code.Default`,
  `llm.code.OpenAi`, `ui.code.Fluid`, `identity/crypto/condition/builder/assert/data.code.Default`.
  **NOT data — services.** They ride the action's `item` and should not. Removing
  them from the item path is the biggest single win and almost certainly the J leak.
  **(Ingi: "remove item of actions" — providers must not be in the action's `item`.)**
- **BCL leaves**: `System.Guid` (304), `System.DateTime` (32) — should be real items. ✅ DONE (below)
- **Already-items re-wrapped**: `binary.this`, `image.this`, `dict.this`, `table.this` (~20) — bug in the declared-label path (#4)
- **PLang domain**: `goal.this`, `goal.steps.this`, `data.this` (nested) (~35) → real `:item` (job #1)
- **Genuinely foreign** (~12): anonymous types, `Func<>`, `Object`, test POCOs → real item or hard error

So "every domain type becomes :item" is mostly: (1) get providers off the action
`item`, (2) BCL leaves → items, (3) fix the re-wrap bug. The true per-domain-type
migration is the small tail.

## Decisions locked this session
- **Job #1 shape:** every domain type becomes `:item` (no generic fallback). A
  non-item reaching Lift becomes a hard producer error.
- **`SetValueDirect` retires with clr** (Ingi). Two uses: (a) set an already-built
  item w/o re-Lift — collapses into normal Lift (lifting an item is identity);
  (b) wrap raw in clr — the hack, gone. ~10 callers (`Wire.cs`, `this.Transport.cs`,
  `this.cs:446/537/1163`, `this.Navigation.cs`).
- **`Lower<T>` retires with clr.** Its item arm is just `it.Clr<T>()`; its
  raw-passthrough arm (`T t => t`) exists only because the door still hands raw CLR
  for rung-2 during the transition. `Lower<object>` is a footgun (returns the
  wrapper, since item IS object). Once clr is gone: `Lower<T>(x)` ≡
  `(x as item)?.Clr<T>()`. 30 prod + 50 test sites. **Cannot remove before clr.**

## Suggested order (keeps the tree buildable each step)
1. **Providers off the action `item`** (kills ~80%; trace why `code.*` instances
   land in a Data value — likely the `[Code]` injection path. Start here; likely fixes J).
2. **Jobs 2 & 4** (mechanical): narrow stamped containers; move declared label to `Type` slot. Fixes the re-wrap bug.
3. **Job 5**: `archive : item` for the compress courier.
4. **Job 1**: domain tail → `:item`.
5. **Delete**: clr fallback → clr class → `SetValueDirect` → `Lower<T>` → wire reconstruction (#6).

## DONE — providers off the action value (commit 8fbc6334d)
The "80% of clr" bucket was a red herring re: *leaks* — the registry holds `ICode`
fine; only the action **return values** wrapped providers in a Data (→ throwaway
clr), and that value was never read. Fixed:
- `code.@this.Register(Type, ICode)` returns `Data.Ok()` (no value), not `Ok(provider)`.
- `code.@this.List(Type)` returns `IReadOnlyList<ICode>` (typed C#); the `code.list`
  action projects provider **names → list<text>** for PLang. `Get<T>` was already
  a typed tuple (never a Data) — unchanged.
- Verified provider clr-wraps **1221 → 0**; suites flat, zero regressions.
- NOTE: this was clr-COUNT/OBP cleanup, **not** the J leak. J is a separate root:
  raw `JsonSerializer.Serialize(itemValue)` in `llm/code/OpenAi.cs:105,984` on an
  item lacking a `JsonConverter` → leaks base item props `{Cacheable,Prior,IsLeaf}`.
  dict/list have converters and serialize fine; the leak is items that don't.

## DONE this session (BCL leaves, bucket #2)
- **DateTime**: `datetime` now owns `System.DateTime` too (`datetime/this.Owns.cs`);
  conversion already existed (`this.Convert.cs:20`). A C# `DateTime` lifts to
  `datetime.@this` backed by `DateTimeOffset` (validated). Aligns Lift with the
  existing Canonical `[DateTime]="datetime"` (legacy) entry.
  - **TODO filed** (`Documentation/Runtime2/todos.md` 2026-06-15): the
    `DateTime→DateTimeOffset` offset for Unspecified/Local is machine-local
    (nondeterministic for signed data) — make it runtime config, pick default (UTC likely).
- **Guid**: new `guid : item` leaf type (`app/type/guid/` — this/Owns/Convert/Parse/Json/serializer),
  mirrors `duration`. Owns `System.Guid` (name was already in `primitive.@this`).
  A C# `Guid` lifts to `guid.@this`, round-trips (validated).
  - Two tests updated to the new reality (guid is an item; `.Clr<T>` yields raw):
    `ValueConversionHookTests.ResidualLeaf_BoolGuidEnum` (`Lower<object>`→`Lower<System.Guid>`/`<DayOfWeek>`),
    `TypeEntityHomeTests.DataType_OnStampedData_ResolvesViaAppTypeIndexer` (compare `Name`, not `ClrType`).
  - Suites back to baseline (Types 13, Runtime 57), zero regressions.
