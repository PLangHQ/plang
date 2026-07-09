# For architect — Stage 2: is construction/coercion static-factory, or does item-purity push it to instances?

**From:** coder. **2026-07-09.** Ingi's call: this runs deeper than one type — investigate before we
relocate `Convert`→`Create` across ~12 types. Three of Ingi's principles collide with the shape your
`stage2-compare-coercion-answer.md` specified, and they're entangled.

## Context — the shape we were about to roll out

Your Stage-2 coercion answer (settled) gives each type a two-arity static `Create`:

```csharp
public static @this? Create(item.@this value)                  // pure core (relocated Convert body)
public static @this? Create(item.@this value, data.@this data) // ICreate courier: kind/strict + data.Fail
```

`Compare` coerces via the pure core (`b as @this ?? @this.Create(b)`), `CoerceOwn` inlines away, the
hub dies. I built `bool` as the proof-of-shape — it compiles and the type/compare suites are green
(NOT committed, held pending this).

## The three principles Ingi raised, in order

1. **"Compare operands should be `item`, not `object?`."**
2. **"Everything is an item and must be."**
3. **"Static methods are bad — most likely wrong unless I approve them."**

Individually minor; together they question whether construction/coercion should be **static factories**
at all, which is the load-bearing assumption of both the plan (model #4/#6: `T.Create`, `ICreate<T>`
`static virtual`) and your coercion answer.

## Entanglement 1 — `Compare`'s signature is a SHARED dispatch, not per-type

`Compare` is not called directly. It's invoked polymorphically:

```
data.CompareValues(other, a, b)
   → driver = other.Type.Rank(...)          // the winning type drives
   → driver.Compare(a, b)                    // every type's static Compare(object?, object?)
```

Every type (`bool/text/number/date/datetime/duration/guid/time/dict/list/choice/binary`) exposes
`static Comparison Compare(object? a, object? b)` matched by that one dispatch. So "Compare takes
`item`" isn't a bool edit — it's the **driver dispatch + all ~13 `Compare` signatures at once**, plus
confirming the operands arriving there are already items (born-native `Peek()` returns the item, so
they should be — needs verifying no raw literal slips through).

## Entanglement 2 — static vs instance for a FACTORY

`Create`/`Convert`/`Compare` are static today. The argument each way:

- **Static is correct here:** `Create` builds a *new* value from a source — there is no `this`
  instance to hang it on (it's a factory, the classic legitimate static). `ICreate<T>.Create` is a
  `static virtual` interface member *by design* — the typed ask `Value<T>()` dispatches to it at
  compile time with no instance. Making it instance would need an already-constructed target to
  "construct itself," which is circular.
- **Ingi's principle:** static = behavior off its owner, usually an obpv. He wants a hard look.

`Compare(a, b)` is the more defensible instance candidate — it's symmetric over two operands, and
could be an instance method on one (`a.Compare(b)`), which would also naturally make the operands
items. `Create` as a factory is the harder one to de-static without a redesign.

## The questions to investigate

1. **Is the static-factory `Create` (your Stage-2 shape + `ICreate<T>` static-virtual) the sanctioned
   exception to "static is bad"?** If yes, we proceed as specified and just don't add *other* statics.
   If no, de-staticing `Create` is a redesign larger than Stage 2 (it reshapes `ICreate<T>`,
   `Value<T>()` dispatch, model #6's `T.Create` delegate) — needs its own design, not a Stage-2 rider.
2. **Should `Compare` move to `item` operands (and possibly to an instance `a.Compare(b)`) as part of
   this, or as a separate item-purity pass?** It's a shared-dispatch change touching all ~13 types.
3. **If `Compare` goes instance but `Create` stays static factory**, is that split coherent (coercion
   asks the operand-instance to compare, construction stays a factory), or does the coercion-through-
   `Create` link (`Compare` calling `Create`) force them to the same shape?

## Coder lean (for you to confirm/redirect)

- `Create` as a **static factory** is genuinely correct — a factory has no `this`. I'd keep it static,
  treat `ICreate<T>` static-virtual as the sanctioned exception, and note it explicitly so it's not
  re-litigated per type.
- `Compare → item` operands: yes, worth doing — but as a **bounded shared-dispatch pass** (driver +
  all `Compare` signatures) either just before or just after the `Convert→Create` relocation, not
  interleaved per type.
- Instance `a.Compare(b)`: appealing for item-purity, but it's a comparison-pipeline redesign; I'd
  scope it separately from Stage 2's construction unification unless you see them as one move.

Holding all Stage-2 code until you weigh in. Nothing committed past the Stage-1 tip (`6c1d2e8ca`) +
the two clean deletions (STJ cheat).
