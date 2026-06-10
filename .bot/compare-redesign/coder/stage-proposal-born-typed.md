# Stage proposal: Born-typed values (the store seam)

**Status:** PROPOSED (coder, Ingi-approved direction, 2026-06-10). Not started —
documented for the architect to fold into the stage plan as the next stage after
Stage 7's gate worklist. Nine existing spec stubs are pinned to this scope (list
at the bottom) and stay red-by-design until it lands.

## The problem

The `Data` value slot holds **either shape** depending on origin:

```plang
- read file 'a.json', write to %a%
- set %x% = %a.port%     / slot holds number.@this — typed, behavior attached
- set %b% = "hello"      / slot holds a bare C# string — no text.@this anywhere
```

For `%b%`, `Data.Type` carries the `text` *stamp* (metadata: name, kind) but the
behavior home (`text.@this` — Length, Contains, the Compare hook, Write) is never
instantiated. Every consumer therefore carries an "is it the wrapper or the bare
string?" branch — the `is string` arms, the `(long)` casts, the leaf-collapse
sites. There is never a duplicate copy (the slot is a reference; the typed value
owns the backing) — the inconsistency is *which object* sits in the slot.

## The fix — one chokepoint

Lift on the way IN, at a single private store seam on `Data` that every origin
already flows through (the constructor, `SetValue`, the parse cache in
`Materialize`). All origins converge; no consumer ever sees raw CLR in the slot
again.

```
store(v):
  null                     → null            (present-null stays the null.@this story)
  already item.@this       → as-is           (wrappers, domain values, references)
  string                   → text.@this
  bool                     → bool.@this
  int/long/decimal/double… → number (kind = precision)
  byte[]                   → binary.@this
  DateTime/DateTimeOffset/TimeSpan → datetime/date/time/duration (family judgement)
  anything else            → as-is, types as `item` — the "unknown" apex,
                             exactly what `object` is to C# (JsonElement, infra
                             CLR objects, take-over API results)
```

The setter change is ~20 lines. The stage is the **consumer tail**: everything
that reads `await x.Value()` and pattern-matches raw CLR starts seeing wrappers.
Much of the convergence already exists (Compare hooks coerce wrappers, the
serializer renders them, GetValue/leaf-collapse unwrap) — but the tail is real
and only the suites will enumerate it.

## Slices (each ends green on both suites)

1. **Scalars** — string/bool/numeric/byte[]/date-family through the store seam.
   Run both suites; fix the consumer tail (expect: `is string`/`is long` arms in
   handlers, assert formatting, `As<T>` conversion paths, STJ value-slot
   serialization, test fixtures constructing `new Data("x", "raw")`).
2. **Containers** — raw `Dictionary`/`List` → dict/list at the seam. Staged
   separately: the CLI-config perimeter (`CommandLineParser`) is deliberately
   raw today and must either stay outside Data or get an explicit carve-out.
3. **The follow-ons the stubs pin** (only safe once 1–2 are green):
   - `text.@this.Value` (the backing string) goes private — emitted only via
     `text.Write(IWriter)`; consumers use the typed ops.
   - `item.ToRaw()` removed — remaining leaf-collapse sites (config, assert raw
     fallbacks, tester, CommandLineParser) route through types/serializers.
   - `_raw` slot dissolution — bare bytes off a channel refine in place through
     the same instance (no parallel raw field).
   - Reserved-core protection — a type may not shadow `@schema`/`type`/`error`/
     `success`; `name` leaves the envelope (free as a data key).

## Pinned spec stubs (red-by-design until this stage)

`Stage2_ValueDoorTests`: Value_AuthoredScalar_ReturnsTypedNumberNotRawInt,
VarReference_RidesAsTypedText_NeverBareCSharpString, TextRawValue_IsPrivate,
GenericToRaw_DoesNotExist_OnItemBase, RawSlot_Dissolved,
DataType_Getter_ReturnsBackingField_NoCLRSniffing.
`Stage2_PlaneResolverTests`: BangReservedCore_Protected_TypeMayNotShadow,
AtSchemaBlocked_AsDictKey_WireMarkerOnly, NameField_RemovedFromEnvelope.

## Risks / open points

- **Perf:** scalar wrapping allocates on every store — number/bool should reuse
  cached instances (bool.True/False exist; small-int cache worth measuring).
- **Equality/keying:** code keying dictionaries on slot values gets wrapper
  semantics (text is ordinal-ignore-case by design — verify each keying site
  wants that).
- **Infra Data:** `!data`, Properties values, and courier Data carry engine
  objects — the as-is/`item` arm covers them, but watch for infra consumers
  casting `.Value()` results.
- Interplay with the PLNG003 worklist decision (todos.md 2026-06-10): if domain
  entities stay exempt from the gate, they still pass through the seam as-is —
  the two decisions don't block each other.
