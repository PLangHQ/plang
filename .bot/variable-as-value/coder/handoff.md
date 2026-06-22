# Handoff — variable-as-value (conversion / lazy-container arc)

Read `lazy-container-clr-plan.md` next to this — it has the full design + every probe
finding. This file is "where we are right now / what to do next".

## Working-tree state at handoff
- **One uncommitted, UNTESTED change:** `PLang/app/type/list/this.Generic.cs` — a
  `list<T>.Create(value, data)` override that RE-TAGS (wraps the value's rows as
  `list<T>` with NO per-element conversion). Edited but the test run was interrupted.
  **First action next session: build + run the suite on it.**
  - If green → commit ("list<T>.Create is a re-tag, not an eager convert").
  - If red → breakers are consumers that read `list<T>` elements expecting them already
    materialized (`.At`/`.Peek`) instead of enumerating + `.Value()`/`Clr<List<T>>`.
    Move those to per-element reads, OR revert and rethink.
- Everything else committed + pushed (`origin/variable-as-value`). `git status` should
  show only `Tests/Scratch/Repro.goal` (untracked) + the Generic.cs edit.

## Build / test (memory: build_speed_workflow)
```
dotnet msbuild PLang.Tests/All.proj -t:Build -p:Configuration=Debug -p:RunAnalyzers=false -v:q -nologo
PLang.Tests/<Area>/bin/Debug/net10.0/PLang.Tests.<Area> --timeout 40s   # Runtime Types Modules Data Generator Wire
```
Green baseline = Modules **4 failed** (the R1 set: `Integration_FileExists/NotExists`,
`Set_NameTypedAsText`, `ActionRunAsync` — Ingi's active work, LEAVE them), else 0.
`Runtime: ReadLocalFile_ReturnsFileType_ChainIsFilePathItem` is a known ORDER-FLAKE
(passes isolated) — not a regression.

## Landed this session (green, pushed)
Bug sweep (~32 tests): registry collision (`app.variable.path/.list` shadowing
`app.type.path/.list` → only item-derived `@this` claim a value-type name); `list`
render preserves the element-type tag; Diff OOM test → behavioral; generator shape
asserts `As<T>`; deep-resolution read via the value door; `[IsNotNull]` guard uses
`IsNull`; conversion-error timing; `Data.As<T>` → internal.

Conversion architecture:
- `type.Create(raw)` = the one CLR→plang LIFT, in the type system. `Data.Lift` +
  `json.BornFromRaw` DELETED (JSON out of object construction).
- `list`/`dict` LOWER themselves inline (`row.Peek().Clr(elem)`), no static helpers.
- `list : IEnumerable<Data>`, `Data.Clr<T>()`.
- family-aware LOWER in `TryConvert`: value lowers itself (`value.Clr`) only when target
  is its OWN family (`OwnerOf(target)` matches the value); cross-family → CONVERT.
- `choice<T>` arm MIGRATED: `choice<T>.Convert` hook + arm deleted — `OwnerOf`'s
  `Discover(target)` routes it. This is the per-arm migration PATTERN.

## Model (decided with Ingi) — 3 directions, no central hub
- LIFT raw→plang = `type.Create`. LOWER plang→CLR = `value.Clr` (terminal). CONVERT
  →plang family = `family.Convert` (terminal).
- `TryConvert` collapses to: identity / `target is plang → family.Convert` / `else →
  value.Clr`. Migrate arms ONE AT A TIME (give the target type a `Convert`, delete its
  arm; `Discover` picks it up). NOT a big-bang — `choice<T>` proved it.
- Convert hooks should THROW, not return null — but only once the dispatch gates the
  owner (today `null` = decline). Lands with the collapse.
- LOWER/CONVERT boundary is FAMILY-based, not CLR-vs-plang (probe-proven).

## The big remaining piece — typed-list lazy materialization (OURS)
`list<LlmMessage>` = `type=list, kind=LlmMessage`; each element = `type=dict,
kind=LlmMessage` — a dict that RETURNS AS LlmMessage on `.Value()`. NOT an eager walk
— O(1) re-tag; `dict→LlmMessage` materializes per element, lazily, on touch.
- **Ingi's constraint:** `kind` is a `string` and can't faithfully name a domain type
  — store `typeof(T)` (a real `System.Type`). Slot exists: `type` entity `_clrType`,
  via `@this(string name, System.Type)` ctor (`type/this.cs:771`).
- **Storage finding:** a `Data`'s `Type` is minted by its VALUE (`_type.Type`/`Mint()`)
  — no declared-type slot. So the VALUE carries the materialize-as `typeof(T)` (drives
  `Mint()` + `.Value()`/`Build`). `type.Build` passes a non-leaf native straight
  through today — extend it to materialize when `ClrType` is a domain type the native
  isn't (`dict.Clr(typeof(T))`, the record build).
- Steps: (1) `list.@this` `protected virtual type? ElementType => null`; `list<T>`
  overrides carrying `typeof(T)`. (2) rows stamp it. (3) `Build`/`.Value()` materialize
  `dict→T` lazily via it. (4) delete the eager `list<T>` arm in `Conversion.cs` (the
  `GetGenericTypeDefinition() == typeof(list.@this<>)` block). The uncommitted
  `list<T>.Create` re-tag is toward (4).
- Then the same migration for: `string→record` (`dict→Goal` deserialize — record owns
  `From` vs deserialize), `FromWire`-string, ctor-string arms.

## Conventions / notes (Ingi this session)
- `ICreate.Create(@this value, @this asking)` — the param should be **`data`**, not
  `asking` ("data is just data"). Uncommitted `list<T>.Create` already uses `data`; the
  interface + other impls still say `asking` — rename as a convention pass.
- Inline complexity AT the operation; extract a method ONLY for genuinely SHARED logic
  (no single-caller `.Of`/helpers). The value owns its conversion — no static
  `ClrList`/`Record.Of`. 5+/10+ line methods = OBP smell to re-examine. `kind` carries
  strings; domain identity needs a `Type`.
- `Modules 4` (R1) + the `ReadLocalFile` flake are NOT yours to fix.
- Always `git push` after the final commit; rebase on `origin/variable-as-value` first
  (other bots push there).
