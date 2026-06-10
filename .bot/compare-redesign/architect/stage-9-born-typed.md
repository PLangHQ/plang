# Stage 9: Born-typed values — the instance IS the value

**The contract for this stage is [`coder/data-value-model.md`](../coder/data-value-model.md)** (Ingi + coder session, extended by the architect session, 2026-06-10). This file is the work breakdown against that contract; where any earlier stage doc (2, 2.1, 3) or the original born-typed proposal conflicts with the model doc, **the model doc wins**. This file was rebased on it 2026-06-10; the earlier `store(v)`-into-a-slot design is superseded.

**Why:** the `Data` value slot held **either shape** depending on origin — `set %x% = %a.port%` put a `number.@this` in the slot, `set %b% = "hello"` put a bare C# `string`, so every consumer carried an "is it the wrapper or the bare string?" branch. The design session went further than the original fix (lift raw CLR into the slot): the slot itself is the wrong shape. Data holds **one typed instance — the instance IS the value** — plus name, properties, signature. No `_value` next to a `_type` descriptor, no `_raw` on Data, no consumer branches.

**Goal:** every Data holds a typed `item` instance from birth. Raw CLR exists only at the boundaries (in: lifted once at entry; out: the item lowers itself at a real .NET edge). The consumer-side branches get deleted, not maintained.

**Scope:** the Data shape collapse, the entry lift, the three doors, live templates, the consumer tail, the immutability/collection semantics, and the follow-ons (`text.Value` private, `item.ToRaw()` removal). **Out of scope:** the schema layers (signature/encryption/compress — future branch; the `SetValueDirect` courier sites are marked transitional debt, do not extend them), CommandLineParser (stays outside Data, lifts when its own todo lands), dict/list copy-on-write (dead — collections are reference-semantics by design).

**Deliverables:**

- **Data collapses** to: name, the typed instance, properties, signature (signature on Data this branch only). Killed: the `object? _value` slot, `_type` as a separate descriptor entity, `_raw` on Data (moves onto the types that have an unparsed form), public/internal `Materialize()` (parse lives inside the type's own `Value()` path).
- **The entry lift** — CLR → item, once, at the boundary. The mapping survives from the original design:
  ```
  string → text          bool → bool            byte[] → binary
  numerics → number      (kind derives from the boxed CLR numeric — never stored)
  DateTime/DateTimeOffset → datetime    DateOnly → date
  TimeOnly → time                       TimeSpan → duration   (1:1 — no value-sniffing)
  bare data.@this → THROW               (nothing legitimate produces it)
  POCO / third-party CLR → item | kind  (rung 2 — kind names the class; generic
                                         navigation/render/compare work day one)
  engine plumbing (Assembly, …) → never enters Data
  ```
- **The three doors, one job each:** `Peek()` sync, in-memory, no I/O/parse/resolve (ToString/Equals/GetHashCode read it, never load); `await Value()` parse + resolve, may answer as a different type — Data rebinds its instance, that rebind IS the narrow; `await Write(IWriter)` the type streams itself — loads if needed, resolves its refs inline, passthrough stays byte-for-byte with zero parse. `Write` goes async; the `await GetValue()` pre-crawl dies; STJ `[JsonConverter]` is the one documented sync perimeter (pre-resolves; channel paths use our writers).
- **Templates live** (design settled with Ingi): build validator stamps `template` deterministically; stamped values (text or collection literals with refs) resolve at every use, never at set, result never stored; cache `Value()`'s answer iff `template == null`; render is single-pass (fills builder-recorded holes only — never re-scans output; unstamped input prints "%secret%" literally).
- **`As<T>` removed — `Value<T>()` is the one typed ask** (settled 2026-06-10): T is a plang type only; mechanics = `await Value()`, then answer-is-T/chain-facet → hand over, else the answer's own Convert hook, else `Data.Error`. **Conversion never rebinds** — only `Value()`'s own answer does. Old `As<int>`-style sites become compile errors; each re-judges to `Value<dict>()` or the type's own lowering (`ToInt64()`, `Clr<T>()`) at a .NET edge.
- **Chain + sampling** (settled 2026-06-10, in the model doc): the chain lives on the answering instance — the narrowing type stamps itself as the prior at mint (init-only); item base holds the nullable priors slot and owns the `Is`/`Facet` walk; **the chain grows only on rebind** (parse rebinds, render never). A value samples its source ONCE, at first use through any door — bytes land in the instance's private `_raw`, nulled at parse (single storage); aliases share the instance so they share the sample (one truth per value; divergence only across separate `read file` steps); **rebind is the only keep**. The load path guards its own first-use race.
- **Immutability + collections:** values immutable everywhere (`WrapperImmutabilityTests` stays green — one named exemption: a private `_raw` field, the load-once slot; extend its set as types convert); working on a value = new instance + Data rebind (file content change included); **list/dict are mutable containers of immutable values — reference semantics** (`set %y% = %x%` shares the list; `add` mints a new Data per entry pointing at the value). `CopyStructure` copy-on-add is REMOVED — it fights reference semantics. Property bag copied per Data at set (bag only; values shared).
- **The six pinned `Stage2_ValueDoorTests` stubs filled and green** (they pin outcomes, not mechanics, so they survive the rebase): Value_AuthoredScalar_ReturnsTypedNumber, VarReference_RidesAsTypedText, DataType_Getter_NoCLRSniffing (the instance knows its own name/kind — the getter's CLR-sniffing block is deleted), TextRawValue_IsPrivate, GenericToRaw_DoesNotExist, RawSlot_Dissolved (`_raw` off Data, onto the types).
- Both suites green at every slice boundary.

**Dependencies:** Stages 2–6 + Stage 3 references landed. Stage 7's PLNG003 walk interleaves freely. The coder's pre-model tail work (C# 306→23) was made against the in-flight retype the model doc calls disposable — **coder: mark which of those fixes survive the model** before redoing the core, so the 280 already-fixed sites don't get re-litigated.

**Demolition worklist:** [`stage-9-demolition.md`](stage-9-demolition.md) — the member-by-member audit of `app/data/` + the value types (what dies in slice 1, what dies with the tail, what is transitional, what stays). The verdicts there are part of this stage's contract.

## Slices (each ends green on both suites)

1. **The core** — Data shape collapse + entry lift + the three doors (the slice-1 section of the demolition list). This redoes the in-flight retype properly; it is the bulk and the blocker for everything else.
2. **Consumer tail** — the suites enumerate it: `is string`/`is long` arms, casts, `As<T>` paths, assert formatting, fixtures constructing `new Data("x", "raw")`. Fix by asking the item or lowering via the type's own `Clr<T>`/`ToInt64()` at a real .NET edge — never `.ToString()`, never a raw getter. The slice-2 demolition entries die here, each with its callers.
3. **Templates** — stamp + live resolution + async Write (design settled; see the model doc and the template section of the proposal).
4. **Collections semantics** — remove `CopyStructure` copy-on-add, pin reference semantics with tests (the `[1,2,3]` case), property-bag copy at set.
5. **Follow-ons** — `text.Value` private (emitted only via `text.Write`), `item.ToRaw()` removed (callers route through types/serializers).

## Rulings that survive the rebase (2026-06-10, settled with Ingi)

1. **1:1 date map, no family judgement** — the lift never inspects a value to pick a type; `date`/`time` arrive only via explicit typed judgement.
2. **Bare `data.@this` THROWS at entry.** Note the nesting answer changed: not a wrapper-owns-Data pattern — nesting is solved by the **schema layers** (model doc, "Nested Data does not exist"), on a future branch. The throw survives unchanged; `SetValueDirect` courier sites are the marked transitional debt.
3. **No CLI carve-out** — CommandLineParser stays outside Data.
4. **Immutability locked by the gate** — now the general rule (values immutable; collections the deliberate, documented exception), not just a caching precondition. Instance caches (bool.True/False, small ints) remain restricted to stamp-free instances.
5. **Stub split** — the three `PlaneResolverTests` stubs landed seam-independently (d85c70d25); the six `ValueDoorTests` stubs land with slices 1–2.

## You own this

Code shapes here and in the model doc (`Peek`/`Value` sketches, `WithContent`/`Rebind`, the lift table's spelling) are suggestions — the coder owns the final shape. Fixed contracts: the model doc itself, the lift table's arms (incl. the THROW), values-immutable/collections-reference-semantics, the three doors' jobs, single-pass render, and green suites at every slice boundary.
