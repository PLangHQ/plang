# Stage proposal: Born-typed values (the store seam)

**Status:** ADOPTED as **[Stage 9](../architect/stage-9-born-typed.md)** (architect, 2026-06-10) — that file is the stage contract; this one keeps the origin framing. Six `Stage2_ValueDoorTests` stubs remain pinned to the stage (the three `PlaneResolver` stubs landed seam-independently, d85c70d25).

**Architect rulings (2026-06-10, settled with Ingi) are folded in below** — the store table, slice 2, and the perf risk were amended in place; the reasoning is in the "Architect rulings" section.

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
  int/long/decimal/double… → number (kind derives from the boxed CLR numeric —
                             number.@this already works this way; never stored)
  byte[]                   → binary.@this
  DateTime/DateTimeOffset  → datetime.@this
  DateOnly                 → date.@this
  TimeOnly                 → time.@this
  TimeSpan                 → duration.@this
  data.@this (bare)        → THROW — a bare Data in the slot is always the
                             implicit-operator accident (the CLAUDE.md double-wrap
                             footgun); legitimate nesting goes through a wrapper
                             type that owns the inner Data (ruling #2)
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
   raw today and **stays outside Data — no carve-out** (ruling #3; the seam's
   invariant has no exceptions). It lifts at the perimeter when the existing
   CommandLineParser cleanup lands (todos.md 2026-06-05, updated 2026-06-10).
3. **The follow-ons the stubs pin** (the first three only safe once 1–2 are
   green; reserved-core is seam-independent — ruling #5):
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

Split (ruling #5): the six `Stage2_ValueDoorTests` stubs wait for slices 1–2; the three `Stage2_PlaneResolverTests` stubs are seam-independent and can land any time, even before slice 1.

## Architect rulings (2026-06-10, settled with Ingi)

**1. The date arms are a 1:1 CLR map — no family judgement.** `DateTime`/`DateTimeOffset` → `datetime`, `DateOnly` → `date`, `TimeOnly` → `time`, `TimeSpan` → `duration`. The backings already line up (`date.@this` holds `System.DateOnly`, `time.@this` holds `System.TimeOnly`, `datetime.@this` holds `System.DateTimeOffset`, `duration.@this` holds `System.TimeSpan`). The seam never inspects a value to pick the type (midnight `DateTime` ≠ `date`) — that's value-sniffing, non-deterministic, and against no-magic. A `date`/`time` arrives only via explicit typed judgement (builder hint, `variable.set` force), never seam inference.

**2. Bare `data.@this` at the seam THROWS; nested Data always has an owning wrapper type.** Nested Data is a real, wanted shape — `- encrypt %data%, write to %encrypted%` yields `Data { type: encryption, value: encryption.@this { backing: the sealed inner Data } }` — but it follows list's pattern: the slot holds the wrapper (`list.@this`), the wrapper's backing holds the Data boxes (`List<data>`). No approve-list of nesting-allowed types; the one rule is "a Data inside a value always has an owning type," and any future nesting case (`signed`, `cached`, `queued`, …) follows the same pattern. The throw therefore only ever catches the accidental implicit-operator case (`return innerData;` → `Data<object>{ Value = Data<bool> }`, the CLAUDE.md footgun) — nothing legitimate produces a bare Data in a slot, so the footgun dies structurally at the chokepoint. This also matches the courier rule: `encryption` never opens the inner Data; only `decrypt` (the leaf) does, so the inner value's signature survives sealed.

**3. CommandLineParser stays outside Data — no carve-out.** The stage's payoff is consumers deleting their `is string` branches because the invariant is absolute; a carve-out makes every consumer keep the branch "just in case" — cost without payoff. CLI config never enters a Data slot (already true today); it lifts at the perimeter (typed option records) when the CommandLineParser cleanup todo lands (todos.md 2026-06-05 entry, updated 2026-06-10 with this ruling).

**4. Wrapper immutability is a stated precondition, locked by a gate test.** Wrapper instances are already shared today — `set %x% = %y%` aliases the wrapper through `Data.ShallowClone` — and the instance cache widens that to program-wide singletons (every `true` in the program is the same `bool.True` object). Sharing is invisible only while the wrapper can never change; one writable field on a wrapper and a write through `%a%` appears in `%b%` — value corruption at a distance. The rule: **wrappers are deeply immutable; everything per-occurrence (name, type, kind, properties, signature) lives on the Data box, which is never shared.** Concrete trap this forbids: implementing the table's number arm by *storing* kind on the `number` instance — one shared `5` can't be int-kind in one variable and long-kind in another; `number.@this` already derives `Kind` from the boxed CLR type, keep it that way. Verified green today: `bool`/`text` are `Value { get; }`, `number` is `private readonly object _value`, all scalar wrappers are `sealed`, the `item.@this` base carries no instance fields.

**Gate test (coder to add — lands green now, fails the future bad edit):** `PLang.Tests/App/CompareRedesign/WrapperImmutabilityTests.cs`, reflection gate in the `Stage0_PlangTypeRemovalTests` offenders style, over `{text, bool, number, binary, date, datetime, time, duration, null}` (`dict`/`list` deliberately absent — mutable containers with their own copy-on-write story). Three asserts: (a) every instance field, including inherited, is readonly (`IsInitOnly`, walk `BaseType` chain below `object`, `DeclaredOnly` per level); (b) no instance property has a setter (init-only would be safe but none exist — gate on "no setter," relax deliberately if one appears); (c) every wrapper is sealed (a subclass could add mutable state and flow through the same shared slots). Note in the test header that binary's `byte[]` *contents* are out of scope — interior mutation is the collections copy-on-write concern, not wrapper shape. **This sketch is a suggestion — you own the final shape.**

**5. Stub split — six wait, three don't.** The six `Stage2_ValueDoorTests` stubs depend on slices 1–2 (born-typed slot, consumer tail converted — `ToRaw` can only be deleted after its remaining callers have a typed path). The three `Stage2_PlaneResolverTests` stubs (reserved-core protection, `@schema` dict-key block, name-leaves-envelope) never touch the store seam and can go green any time, even before slice 1 — don't queue independent work behind the seam. (Stage 2 already grep-verified nothing reads the envelope `name` on the wire read-path.)

## Risks / open points

- **Perf:** scalar wrapping allocates on every store — number/bool should reuse
  cached instances (bool.True/False exist; small-int cache worth measuring).
  Caching = sharing one instance program-wide, safe only under the
  wrapper-immutability precondition (ruling #4) — the `WrapperImmutabilityTests`
  gate must be in place before any cache lands.
- **Equality/keying:** code keying dictionaries on slot values gets wrapper
  semantics (text is ordinal-ignore-case by design — verify each keying site
  wants that).
- **Infra Data:** `!data`, Properties values, and courier Data carry engine
  objects — the as-is/`item` arm covers them, but watch for infra consumers
  casting `.Value()` results.
- Interplay with the PLNG003 worklist decision (todos.md 2026-06-10): if domain
  entities stay exempt from the gate, they still pass through the seam as-is —
  the two decisions don't block each other.

## Slice-1 design refinement — templates (settled with Ingi, 2026-06-10)

The consumer-tail's biggest member — "which strings does the resolver substitute
%refs% into?" — got a real design instead of per-consumer unwraps:

- **Template-ness is determined by the BUILDER, recorded as a stamp.** Two facts
  only the builder knows: the string was AUTHORED in a step, and the literal
  contains a %ref%. Runtime-born strings are never templates (closes today's
  scan-every-string injection surface).
- **The stamp lives on the type entity, beside `strict`** (the established home
  for builder-stamped treat-this-value flags): `type: {name:"text",
  template:"plang"}`. The value slot stays a plain string — no polymorphic
  json, no custom parsing beyond the existing type-entity converter.
- **`text.Template { get; init; }`** names the template LANGUAGE ("plang" =
  %var% substitution; future "fluid"). Init-only — the WrapperImmutabilityTests
  gate takes its planned deliberate relaxation (init-only allowed, setters
  still banned).
- **Declared types compose over text** — `read file '%folder%/x.json'` keeps
  its `path` slot; the templated text renders first, the path constructs from
  the rendered text (path(text)).
- **Rendering is the value's own behavior** at the door (the IBooleanResolvable
  pattern): template text renders against the context; finished text passes
  through untouched. Equality questions dissolve — a template renders on
  .Value() before any comparison sees it.
- **Wire impact: Store only.** Templates exist only in .pr (authored); the
  outbound wire always carries rendered text — public wire shape unchanged.
- **Transition:** until the builder stamps templates, the resolution door keeps
  the legacy scan for unstamped values so existing .prs run; the scan dies when
  the tree is rebuilt with stamps.
