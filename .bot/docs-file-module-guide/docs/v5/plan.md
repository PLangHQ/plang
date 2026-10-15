# v5 — retire stale docs + CLAUDE.md Merge() cleanup (as a proposal)

Ingi: "retire and yes and as claude proposal."

1. **Retire the stale docs.** `Documentation/v0.2/architecture.md` and
   `Documentation/v0.2/type-system.md` are superseded by the in-tree `start.md`
   tree. Retire them by replacing each with a short **redirect stub** that points
   to the current pages. Stub (not delete) because 6 legacy `Documentation/v0.2/`
   notes link to them — a stub keeps those links alive and sends readers to
   current truth.

2. **CLAUDE.md `Merge()` cleanup — "yes", "as claude proposal".** The repo
   CLAUDE.md line 22 lists `Merge()` among `Data`'s members, but no `Merge` is
   defined under `PLang/app/data/` (verified in v3). Route the fix through the
   documented CLAUDE.md-proposal workflow: append a proposal entry to
   `.bot/docs-file-module-guide/claude-md-proposals.md` authored as the change
   record, then — as docs, the proposal applier — apply it to CLAUDE.md and
   record the decision in `docs-report.json`.

## Decisions on the stale-doc content

- `architecture.md` (the old overview: `App.System/.Service/.Goals`, `Data` with
  `Type? Type`) is fully superseded → redirect to `PLang/app/start.md` + the
  concept pages.
- `type-system.md` ("Type System Notes") is largely superseded by
  `PLang/app/type/start.md` + `type/item/start.md`; any remaining cross-cutting
  runtime notes live in `good_to_know.md`. The stub says so, so nothing is
  silently lost.

## Not in scope

- Not rewriting the 6 legacy docs that link to the retired files (they are v0.2
  notes themselves; the stub keeps their links valid).
- No other CLAUDE.md edits.
