# v7 result — Slice 1: the core collapse (born-typed values)

Contract: `coder/data-value-model.md` + `architect/stage-9-born-typed.md` +
`architect/stage-9-demolition.md`. This slice redoes the reverted in-flight
retype properly: Data holds ONE typed instance; every slice-1 demolition entry
is gone.

## What Data is now

`PLang/app/data/this.cs`: one `private protected item? _instance` beside
name/properties/signature/result-state. Killed together (slice-1 demolition
list, all ticked): `_value`, `_type`, `_raw`, `_valueFactory`,
`SetValue(Func)`, `Materialize()`, `ForceMaterialize()`, `_materializeCount`,
`NarrowReference()`, `ScalarContent`, `As<T>`/`AsT cycle guard
(`_resolvingValues`, `ResolveDepthLimit`).

- `Value()` — forwards to the instance's own door and rebinds when the
  instance allows: `answer = await _instance.Ready(); if (Cacheable) _instance
  = answer;`. Load/parse failure → `Data.Error` (`MaterializeFailed` key kept).
- `Peek()` — pure forward to `instance.Peek()`.
- `Type` — pure forward: the instance mints its own entity, chain included;
  Data only stamps Context. No setter (instance owns identity); `Kind` is
  read-only the same way.
- `Value<T>()` — THE typed ask (replaces `As<T>`; generator retargeted, same
  emitted contract: resolve at `await`, error on the returned Data, guard
  after). Mechanics: resolution preamble (full/partial %var%, the
  IRawNameResolvable and action-destination carve-outs, container walk — all
  preserved), satisfied-expectation fast paths (binding is not use: a file
  bound to `Data<file>`/`Data<item>` stays unread), then OPEN THE DOOR
  (`await Value()`, the type parses itself, the Data rebinds) and the evolved
  answer satisfies T, answers from its chain facet, or converts. Conversion
  never rebinds. The cycle guard died with single-pass resolution.

## The item side

`app/type/item/this.cs` base gains:
- `Ready()` — the type-side value door (named `Ready` because text/bool/binary
  still expose public `Value` properties until the follow-ons slice; C# can't
  override a method into a class that declares a same-named property).
- `Peek()` / `Open()` — the sync look vs. what the door hands over (`Open` is
  the transitional consumer-facing form; tightens to the instance in slice 2).
- `Cacheable` — may the Data keep the answer (false for computed/template).
- `Prior` + `Accumulate(prior)` + `Facet(name)` — the narrow chain, appended
  at the END (a parse answer may already carry its source form).
- `Type` (internal) + `protected internal Mint()` — each type mints its own
  entity ITS way: number stamps the exact boxed CLR + precision kind, text and
  binary their `Kind` init-stamp, file/url the extension-derived kind, image
  the mime kind, hash its algorithm, goal.call/ask their non-namespace names.
  The base derives the name from the namespace tail.

New item types (`app/type/item/`):
- `source` — the born-with-bytes value: undecoded raw + declared {type, kind,
  strict}; `Ready()` parses via the reader registry (the old `ParseRaw`
  semantics, owned by the type); single storage — parse rebinds and the source
  rides the prior chain. `Peek` shows utf-8 for text/bytes declarations only.
- `clr` — rung 2: a strongly-typed C# object plang holds; transparent
  (`Peek`/`ToRaw` answer the real object), identity from the registry; also
  serves as the TRANSITIONAL courier label (declared {name, kind, strict}
  riding over a payload — compress Wrap, wire reads). Dies with the schema
  layers.
- `computed` — the always-fresh cell (replaces `DynamicData`'s factory;
  `DynamicData` is now a thin Data subclass holding one). Never kept.
- `absent` — a typed absence (declared type, no value yet): tool-definition
  parameter slots, typed nulls.

`type.@this` is an item now (settled in the model doc) with value equality
({Name, Kind, Strict}) since entities are minted per ask. It also owns
`Judge(value)` — the entry seam's fold of a declared stamp onto a lifted
instance (match → pass/kind-stamp; raw text → `source`; bytes → carrier-
labeled binary; structured under a different name → transparent carrier with
the declared label so build validation and the signed type slot survive).

`file`/`url` own their load+parse in `Ready()` (through the file channel —
mime stamps the content type, auth/consent gate rides on `Path.ReadBytes`),
release bytes after parse (single storage), and accumulate themselves on the
answer — `%x!file!path%` answers from the chain, `is file` stays true.

## Bugs the suites caught (model-level, not test churn)

1. **Signed-hash drift** — the carrier's minted name was Context-sensitive;
   sign-time and verify-time type slots differed → `DataHashMismatch`
   everywhere grants/forwarding were verified. Fix: `SetValueDirect`
   propagates Context immediately (stable identity from first mint).
2. **`crypto.Hash` opened the value door** to sniff for bytes — narrowing the
   value mid-sign (file→text rebind during serialize; canonical divergence).
   Hashing is a courier read: `Peek`.
3. **Declared types vanishing through couriers** — wire reads and build
   params reconstructed property bags as plain dicts, losing the declared
   (and signed) type slot. The labeled carrier keeps the declaration without
   touching the value.
4. **Snapshot capture** keyed always-fresh cells on the `DynamicData` subtype;
   the lazy moved into the instance — capture now checks the instance.
5. **`{file, json}` minted with ClrType=JsonNode** (over-broad kind→CLR
   mirror) made `read file` un-storable; the mirror is numbers-only.

## Test state at slice boundary

- C#: 0 failures (from 136 when the collapse first compiled; baseline had 2
  pre-existing fails which this slice fixed/subsumed). 2 deliberate skips
  remain: `GenericToRaw_DoesNotExist`, `TextRawValue_IsPrivate` — both pinned
  to the follow-ons slice. 4 of the 6 born-typed stubs filled and green.
- plang: full-suite numbers in summary.md (run at commit time). The
  intermittent native segfault AFTER results print is the pre-existing watch
  item — with block-buffered output it can eat the tail of the log, which
  masqueraded as a mid-run crash during this session; run with `stdbuf -oL`
  when piping to a file.
- Old-shape test pins were re-judged to the model (single storage, computed
  never kept, raw slot dissolved, present-null is the singleton, string-face
  asserts); courier-construction tests use the labeled carrier explicitly.

## Open points / flags for Ingi + next slices

- **`Ready()` naming** — the model doc calls the type-side door `Value()`;
  C# blocks that until slice 5 privatizes `text.Value`/`bool.Value`/
  `binary.Value`. Rename then if wanted.
- **First-use load race** — the model says the load path guards its own race
  with a lock in the owning class. `file.BytesAsync`/`source.Ready` currently
  converge last-write-wins (both answers valid, same as the old narrow's
  documented idempotence) but carry no lock yet. Slice 2 item.
- **Peek()/Open() tightening to `item?`** is logged on the slice-2 task (the
  carrier stops unwrapping; raw-shape consumer arms die with their callers).
- **Raw `Dictionary`/`List` still lift to the carrier**, not to native
  dict/list — that's the collections cascade (generic list<T> memory), not
  this slice.
- **Declared-label duplication** — `source`/`clr`/`computed`/`absent` each
  hold a declared {name, kind} pair. Smell-adjacent; `clr`'s copy dies with
  the schema layers, and if a fifth carrier ever appears the label wants its
  own type.
