# Coder summary — compare-redesign

- **Version**: v7 — Stage 9 slice 1 (the born-typed core collapse), 2026-06-10
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

## Test state

- **C#: 0 failures** (136 → 0 over the session; the 2 pre-existing baseline
  fails were on the disposable path and are subsumed). 2 deliberate skips
  remain (`GenericToRaw_DoesNotExist`, `TextRawValue_IsPrivate` — follow-ons
  slice pins). 4 of 6 born-typed stubs filled and green.
- **plang: 4 known fails remain** (next session's first item):
  1. `Modules/Modifiers/MultipleModifiersCompose` + `Modules/Cache/Basic/Cache`
     — the cache modifier path (first-read assert shape + second-call cache
     miss). Start at `app/module/cache/wrap.cs` + `Memory.cs`.
  2. `ScalarsAsNative/Stage3/BareLiteralJudgedByForm` — `"2026-01-01"` literal
     should store a `date`; the stored instance minted "dict". Probe via the
     PrPipeline pattern (engine.Goal.LoadFromFileAsync + RunGoalAsync — this
     session's probes showed the C#-composed path is fine; only the .pr
     pipeline diverges).
  3. `LazyDeserialize/ReadConfigJson_UntouchedIsJsonString` + `ReadCsv_…` —
     blocked on a LANGUAGE-SEMANTICS DECISION (below), not a bug.
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

## Next

1. Ingi's call on scalar-equality (above), then the 4 plang fails.
2. Slice 2 (consumer tail kills — see task list / stage doc), with the new
   working rule: filtered `./dev.sh test <Class>` per fix, full gates only at
   slice boundaries, transitional-behavior test pins get skip-with-reason
   instead of rewrites.
