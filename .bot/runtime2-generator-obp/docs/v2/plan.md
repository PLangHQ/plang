# docs v2 — runtime2-generator-obp

Plan for the docs pass following coder/v7 + coder/v8 (Variable + IRawNameResolvable migration; generator-side missing-name guard). Auditor/v3 PASS on coder/v8.

## Why a v2

docs/v1 closed the v4 source-generator OBP refactor through coder/v6. After that, architect/v5 → coder/v7 → coder/v8 landed a separate plan: retire `[VariableName]` entirely by introducing `App.Variables.Variable` + `IRawNameResolvable`, migrate 22 handler property declarations to `Data<Variable>`, delete the Legacy property emitter, and (v8) add a generator-side guard that restores the pre-v7 `MissingRequiredParameter` ServiceError contract for null variable-name slots. v1's docs treat `[VariableName]` as the canonical mechanism — those references are now stale.

## Documentation gaps

| Gap | File | Action |
|---|---|---|
| 1 | `CLAUDE.md` (root) — line 25 still describes the three-rule property kinds with `[VariableName] string` | Replace with the two-rule + Variable contract |
| 2 | `Documentation/v0.2/architecture.md` lines 221, 251, 265, 267 — `[VariableName]` in catalog attribute list, in the source-gen tree (`Legacy/this.cs`), in the property-kinds table, and in the PLNG001 diagnostic message | Drop Legacy from tree, refactor property-kinds table to two rows, fix PLNG001 message, update catalog attribute reference |
| 3 | `Documentation/v0.2/good_to_know.md` lines 614–620 — three-rule list, Currently exempt block referencing the deleted Legacy emitter | Convert to two-rule contract; add the Variable section per coder/v7 proposal #2 (also closes that gap) |
| 4 | `Documentation/v0.2/action-catalog.md` lines 66, 171–174 — attribute table row for `[VariableName]` and an example using it | Replace with `Data<Variable>` row + updated example |
| 5 | `Documentation/Runtime2/todos.md` — `[VariableName]` migration entry from 2026-04-30 still open | Mark resolved per coder/v7 proposal #3 |
| 6 | Generator missing-name guard (v8) — `__action.Parameters.FirstOrDefault(p => p.Name == "X")?.Value == null → MissingRequiredParameter ServiceError` is undocumented | Add a paragraph to `good_to_know.md` (folds with #3) |
| 7 | XML docs on Variable + IRawNameResolvable | Already excellent — no action |
| 8 | User-facing `docs/` website — does any module page reference `%var%` slot semantics for variable.set / list.*? | Spot-check; expected to be zero gaps (no public surface change for .goal authors) |

## CLAUDE.md proposals (3 from coder/v7)

| # | From | Target | Decision | Reason |
|---|---|---|---|---|
| 1 | coder/v7 | `/PLang/App/CLAUDE.md` | **apply** — fold into root CLAUDE.md | Same scoping reasoning as v1 (no per-folder file exists; rule is canonical for all action handlers). Replaces the v1-applied three-rule "Property kinds (PLNG001 build-time gate)" line at root CLAUDE.md:25. |
| 2 | coder/v7 | `/Documentation/v0.2/good_to_know.md` | **apply** | Variable + the implicit-conversion gotcha + WasPercentWrapped rationale are exactly the cross-cutting non-obvious shape that good_to_know is for. Closes gap #3/#6. |
| 3 | coder/v7 | `/Documentation/Runtime2/todos.md` | **apply** | Closing a delivered todo is plain hygiene. |

## Plan of edits

1. Apply proposal #1 → rewrite root `CLAUDE.md:25` "Property kinds" line.
2. Apply proposal #2 → append Variable section to `Documentation/v0.2/good_to_know.md`. While editing, fix the stale lines 614-620 (gap #3) and add the missing-name guard paragraph (gap #6) so the property-kinds entries in good_to_know.md become coherent end-to-end.
3. Apply proposal #3 → `Documentation/Runtime2/todos.md` (gap #5).
4. Edit `Documentation/v0.2/architecture.md` for gap #2 (4 lines).
5. Edit `Documentation/v0.2/action-catalog.md` for gap #4.
6. Verify build is still green (`plang p build` is a no-op for docs but keeps habit).
7. Spot-check `docs/` site for stale `[VariableName]` references — expected zero hits per `grep` already run.
8. Write `summary.md`, update bot-root `summary.md`, write `docs-report.json`, write `verdict.json`.
9. Commit.

## Out of scope

- The auditor/v3 nit (empty-string slot value bypassing the new guard) — coder declined as optional follow-up; not a documentation gap.
- Tester's missing PLang `.goal` example for `MissingRequiredParameter` — tester's job. v1 docs report flagged the equivalent for resolution-cycle keys; same pattern.

## Verdict expectation

PASS — all gaps fillable from documentation alone, no coder clarification needed.
