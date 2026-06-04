# docs — lazy-deserialize — v1

**Verdict: PASS.** Four new sections added to the v0.2 architecture notes; index updated.

**HEAD documented:** `175584e03` (auditor v2 PASS).
**Upstream verdicts read:** coder v3 + v4, security v1, auditor v1 + v2, architect plan.

## What landed

| File | Section added | Anchors |
|---|---|---|
| `Documentation/v0.2/type-system.md` | **Reader registry — `app.type.reader.@this`, the read-side mirror** | dispatch shape, file convention, precedence, mid-graph json converter, distributed `OwnerOf`, error-stays-in-Data |
| `Documentation/v0.2/data-internals.md` | **Lazy materialization — `_raw`, `Materialize`, `ForceMaterialize`** | `FromRaw`, `_raw` is `string \| byte[]`, mutation invalidates, access-driven resolution; error-surface at four navigation seams (`GetChildValue` ×2, `SetValueOnObjectByPath` ×2); read-time→touch-time |
| `Documentation/v0.2/wire-serialization.md` | **Wire passthrough — `RawUntouched` / `EmitRawVerbatim`** | byte-identical relay, deferred raw on `Wire.Read`, `LiftDataIfShaped` rationale, signing recanonicalizes (unchanged), no-auto-verify on read |
| `Documentation/v0.2/io-channels.md` | **`channel.read` — the single read boundary** | `StampReadAsync` container vs value branch, file/http as channel kinds, `http.response` dissolves, access-driven resolution |
| `Documentation/v0.2/good_to_know.md` | Index — 4 new pointers | — |

Cross-links between the four sections wired in both directions (reader registry ↔ lazy materialization ↔ wire passthrough ↔ channel.read).

## What I deliberately did NOT do

- **No CLAUDE.md proposal.** The lazy/Materialize/reader-registry rules are documented in the v0.2 notes, not the global instruction file — they're load-bearing details, not cross-cutting do/don't gotchas that would justify a global-rules entry. CLAUDE.md already has the OBP courier rule (#7) which is the rule that *uses* `RawUntouched`. If a future incident shows a developer reaching into `.Value` mid-flight despite the rule, that's the time to file.
- **No update to `cli_reference.md` / `modules.md`.** No new CLI flags, no new action surface — purely runtime mechanism.
- **No tests added.** The contract is pinned by `MaterialiseErrorPathTests` (auditor v2-cited) plus the `LazyMaterialisationTests` / `LazyDeserialize` goal suite. Auditor v2 standing carry-forward (symmetric set-path test for `SetValueOnObjectByPath`) is the tester's call, not mine.
- **No `Documentation/Runtime2/good_to_know.md` edit.** Per CLAUDE.md the architectural insights go to `Documentation/Runtime2/good_to_know.md` — but the v0.2 tree is now the active notes set and the `good_to_know.md` *index* lives there (the Runtime2 file appears not to be in active use; v0.2 notes are what's loaded). If user wants the v0.1 file updated separately, that's a one-line ask.

## Verification

- `grep -n` for `Materialize\|FromRaw\|RawUntouched\|EmitRawVerbatim\|reader registry` across the four touched docs: every claim cited to a real symbol in `PLang/app/`. File:line references match HEAD (`175584e03`).
- No grep for stale type names needed — this branch added concepts, didn't rename existing ones.
- No build/test run — documentation-only, no source changes.

## Carry-forward (not actioned here)

- **Symmetric set-path test** (auditor v2). Mirror `Navigation_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound` for `SetValueOnObjectByPath`. Tester's call.
- **F3 (tester)** — `variable.set` List-arm goal regression. Symmetry insurance.
- **Fully type-driven nested Data** — out of scope per architect plan; lives in `Documentation/Runtime2/todos.md`.
- **`(table, xlsx)` reader + table renderer** — out of scope; same.

## Next bot

**none — clear to merge.** Auditor v2 already gave the merge-clear; docs are now in.
