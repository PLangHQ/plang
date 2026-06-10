# Stage 9: Born-typed values (the store seam)

**Why:** the `Data` value slot holds **either shape** depending on origin — `set %x% = %a.port%` puts a `number.@this` in the slot, `set %b% = "hello"` puts a bare C# `string`. For the bare case, `Data.Type` carries the `text` stamp but the behavior home (`text.@this` — Length, Contains, the Compare hook, Write) is never instantiated, so every consumer carries an "is it the wrapper or the bare string?" branch — the `is string` arms, the `(long)` casts, the leaf-collapse sites. The inconsistency is *which object* sits in the slot, never a duplicate copy (the slot is a reference; the typed value owns the backing). Stage 2 already promised "the value is always a typed PLang value"; this stage is that promise's deferred remainder.

**Goal:** every value is lifted to its typed wrapper **on the way in**, at one private store seam on `Data`. No consumer ever sees raw CLR in the slot again; the consumer-side branches get deleted, not maintained.

**Scope:** the store seam + the consumer tail + the slice-3 follow-ons (`text.Value` private, `item.ToRaw()` removal, `_raw` dissolution). **Out of scope:** dict/list copy-on-write (own todo), the CommandLineParser perimeter lift (own todo — ruling 3), the PLNG003 worklist walk (Stage 7's ongoing convergence; interleaves freely — the two don't block each other).

**Deliverables:** the seam + table below; slices 1–2 green on both suites; the six pinned `Stage2_ValueDoorTests` stubs filled and green; `WrapperImmutabilityTests` stays green throughout.

**Dependencies:** Stages 2–6 (the typed value model, the door, per-type Compare) and Stage 3 (references) — all landed. Stage 7's gate runs as warning alongside; no ordering constraint either way.

**Status:** direction Ingi-approved, rulings settled 2026-06-10 (this file is the stage contract; origin story + coder's framing in [`coder/stage-proposal-born-typed.md`](../coder/stage-proposal-born-typed.md)). Already landed ahead of the stage (d85c70d25): the `WrapperImmutabilityTests` gate (3 green) and the three seam-independent `Stage2_PlaneResolverTests` stubs (reserved-core protection, `@schema` dict-key block, `name` off the outbound wire). Remaining: the six `Stage2_ValueDoorTests` stubs, all pinned Skip-with-reason to this stage.

## Design

### The seam — one chokepoint

Lift on the way IN, at a single private `store(v)` on `Data` that every origin already flows through: the constructor, `SetValue`, and the parse path (the internal `Materialize` core). All origins converge; the setter change is ~20 lines. The stage's real weight is the **consumer tail** — everything that reads `await x.Value()` and pattern-matches raw CLR starts seeing wrappers; the suites enumerate the tail.

```
store(v):
  null                     → null            (present-null stays the null.@this story)
  already item.@this       → as-is           (wrappers, domain values, references)
  string                   → text.@this
  bool                     → bool.@this
  int/long/decimal/double… → number          (kind derives from the boxed CLR numeric —
                                              number.@this already works this way; never stored)
  byte[]                   → binary.@this
  DateTime/DateTimeOffset  → datetime.@this
  DateOnly                 → date.@this
  TimeOnly                 → time.@this
  TimeSpan                 → duration.@this
  data.@this (bare)        → THROW           (always the implicit-operator accident — ruling 2)
  anything else            → as-is, types as `item` — the "unknown" apex, exactly what
                             `object` is to C# (JsonElement, infra CLR objects,
                             take-over API results)
```

### The rulings (2026-06-10, settled with Ingi)

1. **Date arms are a 1:1 CLR map — no family judgement.** The backings already line up (`date.@this` holds `System.DateOnly`, `time.@this` holds `System.TimeOnly`, `datetime.@this` holds `System.DateTimeOffset`, `duration.@this` holds `System.TimeSpan`). The seam never inspects a value to pick the type (midnight `DateTime` ≠ `date`) — value-sniffing is non-deterministic magic. A `date`/`time` arrives only via explicit typed judgement (builder hint, `variable.set` force), never seam inference.

2. **Bare `data.@this` at the seam THROWS; nested Data always has an owning wrapper type.** Nested Data is a real shape — `- encrypt %data%, write to %encrypted%` yields `Data { type: encryption, value: encryption.@this { backing: the sealed inner Data } }` — and it follows list's pattern: the slot holds the wrapper, the wrapper's backing holds the Data boxes (`list.@this` holds `List<data>`). No approve-list; the one rule is "a Data inside a value always has an owning type." The throw only ever catches the accidental implicit-operator case (`return innerData;` → `Data<object>{ Value = Data<bool> }`, the CLAUDE.md footgun), which nothing legitimate produces — the footgun dies structurally at the chokepoint. Courier rule holds: `encryption` never opens the inner Data; only `decrypt` (the leaf) does, so the inner signature survives sealed.

3. **CommandLineParser stays outside Data — no carve-out.** The stage's payoff is consumers deleting their `is string` branches because the invariant is absolute; a carve-out makes every consumer keep the branch "just in case." CLI config never enters a Data slot (already true); it lifts at the perimeter when the CommandLineParser cleanup todo lands (todos.md 2026-06-05, updated 2026-06-10).

4. **Wrapper immutability is a precondition, locked by a gate.** Wrapper instances are shared today (`set %x% = %y%` aliases the wrapper through `Data.ShallowClone`); the instance cache widens that to program-wide singletons (every `true` is the same `bool.True` object). One writable field on a wrapper and a write through `%a%` appears in `%b%`. The rule: **wrappers are deeply immutable; everything per-occurrence (name, type, kind, properties, signature) lives on the Data box, which is never shared.** Concrete trap: storing kind on the `number` instance — one shared `5` can't be int-kind and long-kind at once; `number.@this` derives `Kind` from the boxed CLR type, keep it that way. `WrapperImmutabilityTests` (landed, 3 green: fields readonly incl. inherited, no setters, sealed) must stay green; no instance cache lands without it.

5. **Stub split — six wait, three didn't.** The three `Stage2_PlaneResolverTests` stubs were seam-independent and have landed. The six `Stage2_ValueDoorTests` stubs depend on this stage: `Value_AuthoredScalar_ReturnsTypedNumber` + `VarReference_RidesAsTypedText` are slice-1 outcomes; `DataType_Getter_NoCLRSniffing` lands when the seam stamps type at construction (the getter becomes `return _type;`, the CLR-sniffing block is deleted, not migrated — Stage 2's spec); `TextRawValue_IsPrivate`, `GenericToRaw_DoesNotExist`, `RawSlot_Dissolved` are slice-3 follow-ons.

### Incumbent trace — where raw enters, who branches on it

Entry points the seam takes over (all already funnel through `Data`): the constructor, `SetValue`, the internal parse core. No other path writes the slot.

Known consumer-tail shapes (slice 1 flushes the rest via the suites): `is string`/`is long`/`is bool` arms in handlers; numeric casts; `As<T>` conversion paths; assert formatting; STJ value-slot serialization; test fixtures constructing `new Data("x", "raw")`. Much of the convergence already exists — Compare hooks coerce wrappers, the serializer renders them, GetValue/leaf-collapse unwrap — but the tail is real and only the suites will enumerate it. Equality/keying sites get wrapper semantics (`text` is ordinal-ignore-case by design — verify each keying site wants that).

### Slices (each ends green on both suites)

1. **Scalars** — string/bool/numeric/byte[]/date-family through the seam; fix the consumer tail the suites surface.
2. **Containers** — raw `Dictionary`/`List` → `dict`/`list` at the seam. CLI config stays outside Data (ruling 3).
3. **Follow-ons** (need 1–2 green): `text.@this.Value` private (emitted only via `text.Write(IWriter)`); `item.ToRaw()` removed (remaining leaf-collapse sites route through types/serializers); `_raw` dissolution (bare bytes off a channel refine in place through the same instance).

### Risks / open points

- **Perf:** scalar wrapping allocates on every store — `bool.True`/`False` reuse is free; small-int cache worth measuring *after* slice 1 is green, never before the immutability gate is in place (ruling 4).
- **Infra Data:** `!data`, Properties values, and courier Data carry engine objects — the as-is/`item` arm covers them; watch for infra consumers casting `.Value()` results.
- **PLNG003 interplay:** if domain entities stay exempt from the gate, they still pass through the seam as-is — the two decisions don't block each other.

### You own this

Code shapes in this file and in the coder's proposal (the `store(v)` sketch, the gate-test layout) are suggestions — the coder owns the final shape. The contracts that are fixed: the table's arms (incl. the THROW), the five rulings, slices end green on both suites, and the immutability gate stays green throughout.
