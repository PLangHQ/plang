# Coder summary — compare-redesign

- **Version**: v7 — Stage 9 COMPLETE, slices 1–5 (born-typed core collapse,
  consumer tail kills, live templates, collection reference semantics,
  follow-ons), 2026-06-10/11
- **What this is**: The settled Data/Value model
  (`coder/data-value-model.md`) lands as code: Data holds ONE typed instance
  (the instance IS the value) beside name/properties/signature; everything
  the old shape kept beside the value moved onto the types that own it. The
  full work breakdown is `architect/stage-9-born-typed.md`; the verdicts in
  `architect/stage-9-demolition.md` are the contract — every slice-1 entry is
  ticked.

## What was done

Full detail in `v7/result.md` (read that first); headlines:

- `Data` collapsed: `_value`/`_type`/`_raw`/`_valueFactory`/`Materialize`/
  `ForceMaterialize`/`NarrowReference`/`As<T>`+cycle guard all gone.
  `Value()` forwards to the instance's own door (`item.Ready()`) and rebinds
  on a `Cacheable` answer — that one assignment IS the narrow. `Peek()` and
  `Type` are pure forwards (the instance mints its own entity via `Mint()`,
  chain included; `type.@this` is an item with value equality and owns
  `Judge` — the declared-stamp fold at the entry seam).
- `Value<T>()` replaces `As<T>` (generator retargeted, handler contract
  unchanged). New item types: `source` (born-with-bytes), `clr` (rung-2
  carrier + transitional courier label), `computed` (replaces DynamicData's
  factory), `absent` (typed absence). file/url load+parse in their own
  `Ready()`, single storage, chain accumulates.
- Five model-level bugs found by the suites and fixed (signed-hash drift from
  a context-sensitive mint; crypto.Hash opening the value door mid-sign;
  declared types vanishing through couriers; snapshot capture keying on the
  dead Data subtype; an over-broad kind→CLR mirror) — see result.md §bugs.

## Slice 2 (closed 2026-06-11) — consumer tail kills

Worklist + per-item status: `v7/slice2-worklist.md`. Headlines:

- Killed WITH their callers: `AsEnumerable`/`IsPlangIterable`/`IsPlangAssignable`,
  `ToBoolean` CLR arms (types' own `IsTruthy`), `SnapshotClone`,
  `GetValue<T>`/`GetValue(Type)` (test-only shim remains in
  `PLang.Tests/Shared/DataReadExtensions.cs`), `Data.Clr<T>` (sites use the
  door + `item.Lower<T>`), `UnwrapJsonElement` (json entry parse now lives on
  `app/type/item/serializer/json.cs:Parse`).
- Outbound implicit operators killed on `text`(→string), `bool`(→bool),
  `binary`(→byte[]) — inbound entry lifts stay. ~90 production sites + ~100
  test sites re-judged to read the value's face (`.Value`) at real .NET edges.
- `number : IConvertible` audited — KEPT (members already checked/loud;
  Fluid/Convert.* edges need the bridge for boxed numbers).
- Peek()/Open() tightening to `item?` DEFERRED to slice 3+ (carrier-out flip
  broke ~75 raw-shape consumers; note in `clr.cs`).
- Slice-2 gates: C# all green (2 deliberate slice-5 pin skips); plang 330
  pass / 4 skips / 0 real failures (halves + Builder+Simple combo for the two
  cross-half path artifacts).

## Slice 3 (closed 2026-06-11) — live templates

Plan + outcome detail: `v7/slice3-plan.md`. Headlines:

- `Template` is an init-only stamp on `item` ("plang"); the build-side
  detection is deterministic code at the authored seams (`goal.list.Add`,
  `GoalCall.LoadFromFile`, `Action.FromWire`, `Data.Authored()` for fixtures).
  Runtime input is never stamped — "%secret%" typed by a user prints
  literally.
- Stamped values resolve at every USE, never at set: `text.Render` fills
  holes via live variables (full-match answers through the variable value's
  own door; partial interpolates single-pass), `text.Cacheable` is false when
  stamped so the Value-door rebind never keeps a render.
- The `AsCanonical`/`AsT_Impl` %ref% sniffs are stamp-gated — TryFullVarMatch
  retired as a sniff, kept as the full-vs-partial classifier.
- Async `Write(IWriter)` split out (no behavioral win while the wire path is
  the documented-sync STJ exception that pre-resolves) — rides with slice 5.
- Gates: C# 0 failures (Data slice teardown-truncates before its summary —
  pre-existing; failures flush immediately and none appeared over 4 runs);
  plang 330 pass / 4 skips / 0 real failures (halves, identical to slice-2
  baseline).

## Slice 4 (closed 2026-06-11) — collection reference semantics

- `CopyStructure` REMOVED with both callers: `list.add`/`list.set` entries
  mint their OWN Data pointing at the value's current instance — O(1),
  nothing copied; an in-place mutation of a shared list shows through every
  name holding it (the [1,2,3] rule), while `set %x% = ...` rebinds and never
  touches entries.
- Property bag is per-binding: `ShallowClone` (the `set %y% = %x%` path) now
  copies the bag (values inside shared by pointer) — `set %y!NewProp% = 1`
  lands on %y% only.
- Pins: `Set_ListAlias_InPlaceAddVisibleThroughBothNames`,
  `Set_Alias_PropertyWrite_LandsOnAliasOnly`, re-judged
  `Add_List_SharesSourceInstance_ReferenceSemantics` +
  `AddList_ReferenceSemantics_SharedInstanceBothWays` (old structure-copy
  pins flipped to the model position).
- Gates: C# 0 failures (Data slice printed its full summary this round:
  992/0); plang 330 pass / 4 skips / 0 real failures.

## Slice 5 (closed 2026-06-11) — follow-ons

- `item.ToRaw()` is INTERNAL (base + every override) — no public raw escape;
  the pin `GenericToRaw_DoesNotExist_OnItemBase` is filled and green. The
  in-assembly conversion/comparison/wire seams keep it as gated interop.
- `text.Value` is INTERNAL — no public string face; the pin
  `TextRawValue_IsPrivate_NotPublicProperty` is filled and green. In-assembly
  leaf handlers (the slice-2 `.Value` edge reads) keep working; outside the
  engine the string leaves only via `text.Write(IWriter)`.
  JUDGEMENT CALL for Ingi: the stubs said "private"; I used `internal` —
  the reflection pins check "no PUBLIC member", and private would have
  re-broken the ~200 sanctioned leaf edge reads slice 2 just created.
  Flip to private + a different edge mechanism if you want it stricter.
- Async `Write(IWriter)` is BLOCKED ON A PREREQUISITE, not on coder effort:
  the only real wire consumer today is the sync STJ converter — the model
  doc's documented exception, which pre-resolves. Converting now means
  sync-over-async or dead code. It lands when the channel path gets its own
  async writer pipeline off STJ (its own work item).
- Gates: C# all six slices 0 failures, 0 skips remaining (both slice-5 pins
  now enforce); plang 330 pass / 4 skips / 0 real failures.

## Test state

- **C#: 0 failures** (136 → 0 over the session; the 2 pre-existing baseline
  fails were on the disposable path and are subsumed). 2 deliberate skips
  remain (`GenericToRaw_DoesNotExist`, `TextRawValue_IsPrivate` — follow-ons
  slice pins). 4 of 6 born-typed stubs filled and green.
- **plang: 0 real failures** as of slice-2 close (the earlier 4 were fixed
  during slice 1 close-out: cache-modifier pair via sampling-keeps-bytes +
  channel `Read(bytes)`; BareLiteral .pr rebuilt; the two LazyDeserialize
  goals re-pinned to the PROVISIONAL scalar-equality position below).
- **Watch items**: (a) pre-existing native segfault at process teardown can
  EAT the buffered tail of a full `plang --test` log — a completed run can
  look like a mid-run crash; the summary did print in subset runs. (b) the
  Data C# slice sometimes segfaults after printing results (pre-existing).
- **Stale-binary trap bit twice this session**: `./dev.sh test` does NOT
  rebuild PlangConsole — always `dotnet build PlangConsole` (or dev.sh
  build) before any `plang --test`.

## DECISION NEEDED (Ingi) — scalar equality on an unread reference

`assert %cfg% equals "{\"port\":8080}"` after `read file 'config.json'`:
the old ScalarContent contract made scalar uses (compare, interpolation) see
the RAW content string without narrowing. Under the model, compare is a USE →
`Value()` → the file parses → dict vs text → Incomparable. The doc settles
write-out (Write door, no parse) and navigation (parse) but not `==`.
Options: (a) the model position — comparing a json file to its raw text is
dishonest; re-pin those tests (navigate, or `as text`); (b) keep a raw-face
compare for unparsed sources (compare through Peek when untouched). The two
LazyDeserialize tests stay red until called.

## Code example (the landed shape)

```csharp
// Data.Value() — the whole mechanism
if (_instance == null) return null;
var answer = await _instance.Ready();       // the type loads/parses ITSELF
if (!ReferenceEquals(answer, _instance) && _instance.Cacheable)
    _instance = answer;                      // the rebind IS the narrow
return answer.Open();                        // transitional face; slice 2 tightens
```

## Next / open points for Ingi

1. Scalar-equality on an unread reference (above) — PROVISIONAL model
   position taken, two LazyDeserialize goals re-pinned, easy to flip.
2. Slice-5 judgement call: `internal` vs `private` for `text.Value` /
   `item.ToRaw` (see slice 5 section).
3. Async `Write(IWriter)` — needs the channel pipeline off STJ first (see
   slice 5 section); then `var _`-style pre-crawls die and templates render
   during the walk.
4. Slice-2 leftover: Peek()/Open() tighten toward `item?` (carrier stops
   unwrapping) — ~75 raw-shape consumers remain; revisit after templates
   reduce the raw-shape surface.
5. `Ready()` naming — rename to `Value()` is possible now on types whose
   public `Value` property went internal (text); blocked on the others
   (bool/binary/number still expose `Value`).
