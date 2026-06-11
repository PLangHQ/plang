# Coder — compare-redesign — summary (latest state)

**Version:** v8 (continuing slice 2b consumer tail)

## What this is

Stage 9 "born-typed values" + slice 2b/2c consumer tail. Every value is its own
typed instance; `Data` is a thin binding (one typed instance + name/properties/
signature); consumers never branch on raw CLR shape. The typed ask is `T.Create`
(the TARGET constructs itself).

## Latest change — Value<T>() / Create return the INSTANCE, not a Data<T>

Full detail: `v8/value-t-instance-refactor.md`. In short:

- `ICreate<T>.Create(item, asking)` → `TSelf?` (the instance). Pass-through returns
  the *same* instance (zero alloc); decline lands on `asking.Fail` and returns null.
- `Data.Value<T>()` → `T?` = `T.Create(await Value(), this)`. `Data<T>.Value()` is
  just that — the old allocate-a-Data<T>-then-unwrap round-trip is gone.
- `Variable.Create` is **pure pass-through-or-decline** — no `Resolve`, no
  `ToString()` reparse. A variable is born a Variable at the wire boundary
  (`type.Judge` for a `type:variable` param); reparsing-at-ask magic deleted, and
  the `IRawNameResolvable` reflection carve-out in `Value<T>()` deleted.
- The slot stays `Data<T>`, formed once at the dispatch boundary:
  `__d.ShallowClone<T>(await __d.Value<T>())` (the CLR can't re-view base→`Data<T>`
  free). `CloneError<T>` deleted; `ShallowClone<T>(null)` carries the decline error.

## Deep-resolve root — raw-container Lift bridge (temporary)

Nested `%var%` inside a container only deep-resolves when the container is **native**
(`list.@this`/`dict.@this`, door recurses). A raw C# `List<object?>`/`Dictionary`
parked as a `clr` carrier (no-op door) never rendered its holes. Real params are always
native off the wire; raw containers only enter via C# composition (tests) + a few
result/stored-value seams. Per Ingi: no silent global conversion — added a guard at
`Lift` (the throw is kept **commented**, restored when the seams go born-native) and a
**temporary** Lift bridge that converts a raw container to native via
`json.Parse(SerializeToElement(v))`. Marked `TODO(remove)` + `todos.md`.

## Born-Variable test fidelity + UnknownType '' root fix

Making `Variable.Create` pure regressed tests whose hand-built `variable.set` steps
lacked `type:variable` on `Name` (the builder always stamps it). Fixed the seams to
mirror the builder: shared `PrParam.List`/`IsVarNameSlot`, `TestAction.Create`,
`MakeStep` helpers, and the `.pr` fixtures. Then fixed a real pre-existing bug those
tests unmasked: `variable.set` gated its optional `as` type on `typeValue != null`,
but an omitted clause is the non-null `absent` citizen → minted an empty-name type
(`UnknownType ''`). Now gated on `Type.IsEmpty()`.

## Result

C# suite, baseline c026ff245 → now: Modules 230→145, Data 144→105, Wire 60→40,
Types 47→29, Runtime 76→70, Generator 12→7. **Net −173** (−171 net of the 2
documented action-template regressions). Total failing 569 → 396.

Further root fixes after the −104 mark:
- **variable.set no-type path collapsed to one line** (`Context.Variable.Set(name.Name, Value)`)
  — caught a widespread empty-value bug: `ShallowClone(name)` bound to the GENERIC
  `ShallowClone<Variable>` (name is a Variable), cloning the name as the value (~26 tests).
- **text.Value method-group leaks** (`Func→text`) — `text.Value` was deleted; three prod
  Convert sites read it and got the door method group. Fixed to `Clr<string>()`.
- **wire Normalize** — a nested Data now rides AS the dict entry (`Entry` helper), not
  re-boxed (killed the "bare Data stored as value" throws). NOTE: architect is reworking
  Normalize for laziness (drop the eager copy for native containers) — leave it.
- **born-Variable test fidelity, completed** — `PrParam` now covers `variable.set` name +
  `loop.foreach` ItemName/KeyName; modifier/condition inline PrActions + `.pr` fixtures stamped.
- AsT identity tests migrated to the instance contract.

Two action-template tests regressed from the bridge (`DataWrappedActionList_*`) — the
bridge flattens action templates and the stamp walker over-resolves deferred sub-action
`%var%`. Documented in `todos.md`; fixed for free by the template-ownership design
(`v8/template-ownership-proposal.md`, for architect) which removes the walker.

## What's next (remaining tail roots — diverse, each needs fresh investigation)

- **"Expected failure but Data succeeded with value: null"** (~17 in Modules) — diverse
  actions (identity Archive/Create-duplicate, crypto Hash-null, llm Query API-errors)
  whose error paths return Success. Likely several distinct pre-existing roots, not one.
- **lazy/signed wire round-trip** — bare-Data throws gone, but these chain to the next
  wire/signing step (key-BLOB SigningError, signature survival). Architect reworking Normalize.
- `clr`-unwrap (`Lower`/ArrayList/Hashtable, Decompress source.Clr-on-Func),
  `As<T>` CLR reconstruction (RecordWithPositionalCtor, NoParameterlessCtor),
  snapshot-`clr`-wire (Wire), generator `IsNotNull`/sensitive-snapshot.

## Slice-2b structural — progress

- ✅ **`item.ToRaw` DELETED** at every visibility. The CLR edge is `Clr(Type)` alone:
  base defaults to `ClrConvert(Peek(), target)` (every type answers); `dict`/`list`
  own their decompose inline; recurrence pin `GenericToRaw_DoesNotExist_OnItemBase`.
  Exit gate `\.ToRaw()` → 0. Zero regressions.
- ✅ **`IsRef`→`item` virtual** — `Data` no longer does `_type is text t && t.IsRef()`;
  `Data.IsVariable`/`AsCanonical` ask `_type.IsRef(out name)`.
- ⬜ **`Peek()` → `item?`** — stage-deferred (entangled with the `clr` rung-2 carrier).
- ⬜ **`set.cs` `as`-block collapse** — blocked on the "type-entity lift at entry" seam.
- 🔶 **`is/as` exit-gate sweep** — the cleanly-doable items are DONE; the remainder is
  stage-7. Done: `IsRef`→item virtual; `Properties` dead-`is dict` delete; all 17
  raw-`.Value` bool reads → `Data.ToBooleanAsync()` (and fixed its latent template
  bug); redundant `as T` casts on already-`Data<T>` params dropped; new
  `Data<T>.Clr<TClr>(fallback)` edge accessor (applied at `timer.start`).
  **Left = stage-7 / rework / legit:** plain-`Data` param reads (`(await p.Value())
  as text`) need the param typed `Data<T>`; sync `Peek() as T` needs a typed sync
  accessor on `Data<T>` (a `data` change = stage-7); `LoadValue`/`Normalize` die with
  the async-`Write`/lazy-streaming rework; navigators + base `is item` checks are
  legit ("proven leaves"). So the gate can't reach green without stage-7 surface-typing.

## slice-2c — started; residue is design-blocked for overnight-autonomous

First flip done & pushed: **callstack `tag` store holds typed `Data`**
(`tag/this.cs` now `IDictionary<string,Data>`; surface `Set(text key, Data value)`
lowers the key at the leaf; `call.Tag(text, data)`; `debug/tag.cs` passes the
binding `target.Tag(entry.Name, entry)` — no point-in-time resolve). Rule nailed:
flip signature → plang types · store typed (no half-flips) · lower at the leaf only ·
pass the binding (resolve only for snapshots) · update readers/tests · parity-gate.

The 2b `Value<T>` work already **dissolved most slice-2c sites** — the finder grep
(`(await X.Value())?.ToString()` etc.) is down from 62 → **23**; the courier group
(data/variable) is **0**. The remaining 23 split into:
- **Legit .NET edges (leave):** Scriban template (`goal/Methods`), reflection
  diagnostics (`debug`), `list.join`/`group` stringification, third-party `Fluid`.
- **Stage-10 interior-property flips:** `Ask.Answer` is an `[Out]` wire property —
  typing it cascades to wire/navigation; that's stage-10 (value-bearing members at rest).
- **Big design-cascades (BLOCKER):** `ServiceError(string msg, string key)` carrying
  `text`/`data` is the **error model** — thousands of construction sites; `Goal.Parse(string)`
  → `text` is parse everywhere (~10+ load-bearing callers).

## BLOCKER — why I stopped here (overnight-autonomous safety)

The remaining slice-2c residue + **Stage 7** (full public-surface typing + the build
gate, ~291 props) + **Stage 10** (typed interior, ~291 props) are large, **design-heavy**
refactors that I should not grind blind while you sleep:
1. **Design decisions are yours/architect's** — how errors carry `text`/`data`
   (the `ServiceError` model), and the Stage-7 build-gate shape. Inventing these
   overnight would lock in choices you may want different.
2. **Unverifiable overnight** — the broken-test clusters (llm ~45, crypto/signing,
   snapshot) mask regressions; "no half-flips" cascades each flip to storage+readers+
   tests; blast radius is thousands of sites. I can't course-correct a subtle break
   I can't see, so the risk is net-negative churn.

So I took slice-2c to its **safe limit** (residue = legit-edges + design-cascades) and
stopped per "unless you have a blocker." Suite is at committed-green parity
(Data 105 / Modules 145 / Runtime 70 / Wire 40 / Types 29 / Generator 7), tree clean,
all pushed. Next session (with you awake): pick the `ServiceError` error-model design,
then Stage 7 surface-typing behind the build gate, then Stage 10 interior.

## Design hand-offs to architect
- template-ownership (`v8/template-ownership-proposal.md`)
- lazy-streaming wire writer / drop the Normalize eager copy (in discussion)
- `ServiceError` / error-model: how does an error carry `text`/`data` instead of `string`? (slice-2c blocker)
- Stage-7 build-gate shape (what the gate forbids; migration order for ~291 props)

Design hand-offs to architect: template-ownership (`v8/template-ownership-proposal.md`).
Deferred (todos.md): typed-null slot citizen `@null.@this<T>`; remove the Lift bridge.
