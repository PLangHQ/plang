# docs v1 — `plang-types`

## Scope

Final gate after security v2 PASS + tester v5 PASS + auditor not yet (going by branch state). Branch landed a large value-system spine: per-(type, kind, format) dispatch, three proving types (`number`, `image`, `code`), runtime DLL type-loading (`code.load` extended), math handler retype-through-`number.*`, primitive cleanups (`datetime` → DateTimeOffset, `duration` → TimeSpan).

XML doc quality across the new code is strong — `number/this.cs`, `NumberPolicy.cs`, `Loader.cs`, `image/this.cs`, `code/this.cs`, `this.Arithmetic.cs` all have what/why-shaped summaries. I'm not gap-filling those.

## Proposal decisions

### CLAUDE.md proposals (1)

| From | Target | Decision | Reason |
|---|---|---|---|
| architect v1 | `/CLAUDE.md` (OBP Smell Checklist) | **apply** | Rule #9 ("Only leaves touch `Data.Value`") landed in `object_pattern_formal.md` this branch. Project CLAUDE.md's smell checklist is the day-to-day gate; without the entry, a bot scanning C# won't catch mid-pipeline `data.Value as X` switches unless they know the canonical rule by heart. The proposal points to the formal rule; doesn't duplicate it. Material rule addition, applies to all future runtime2 work. |

### Character proposals (0)

No `character-proposals.md` on this branch.

## Documentation gaps

### User-facing (`docs/`)

1. **`docs/modules/math.md`** — needs:
   - `intdiv` action (new this branch, missing from the page).
   - One line under "Type Preservation" or near the top noting that `7 / 2 → 3.5` (divide leaves the integer track); intdiv is the explicit C# semantics.
   - Brief callout that overflow / precision-mix behaviour is configurable via `app.config` (Lenient default; Strict throws on overflow). Don't over-explain — a sentence with a pointer.
2. **`docs/modules/index.md`** — math row's action list missing `intdiv`.
3. **`docs/modules/code.md`** — `load` now also picks up `[PlangType]` classes and `ITypeRenderer` implementations from the DLL. One short paragraph + the sealed-names rule (identity / signature / signedoperation / callback / channel can't be shadowed).

### Action teaching (`os/system/modules/`)

4. **`os/system/modules/math/intdiv.description.md`** — missing. Without it, the builder doesn't surface `math.intdiv` cleanly.

### Architecture (`Documentation/v0.2/`)

5. **`Documentation/v0.2/good_to_know.md`** — add one entry: "Typed values — `app/types/<name>/`, per-(type, format) renderers, `type` + `kind` as separate `.pr` fields". Pointers to the architect plan + `object_pattern_formal.md` Rule #9. Keep concise — the canonical narrative is the architect plan (`.bot/plang-types/architect/plan.md`); good_to_know.md is the discoverability layer for future bots.

### CHANGELOG / `result.md`

6. `result.md` captures user-visible deltas: new `math.intdiv`, `7/2 → 3.5` behaviour, `code.load` extended to types and renderers, sealed-name guard.

## What I'm NOT writing

- New PLang `.goal` examples for `math.intdiv` / `image` / `code` value types — tester's job. Flag if absent in builder examples.
- A net-new `Documentation/v0.2/typed-values.md` deep dive — the architect plan + `object_pattern_formal.md` Rule #9 + a `good_to_know.md` discoverability entry cover this without duplicating the spine.
- XML docs on already-documented surfaces.

## Execution order

1. Apply CLAUDE.md proposal #7.
2. Write `os/system/modules/math/intdiv.description.md`.
3. Edit `docs/modules/math.md` (intdiv, divide promotion, policy callout).
4. Edit `docs/modules/index.md` (intdiv in math row).
5. Edit `docs/modules/code.md` (load extends to types + renderers + sealed names).
6. Edit `Documentation/v0.2/good_to_know.md` (typed-values entry).
7. Write `result.md`, `verdict.json`, `summary.md`, `docs-report.json`.
8. Commit + push.
