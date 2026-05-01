# docs — runtime2-data-share-state

## v1 — Identity-preservation contract + cross-cutting entries

First docs pass on this branch. Documented the data identity-preservation contract that landed in phases 1–4 — the four `As<T>` wrap rules, `AsCanonical` for plain Data slots, `Variables.Set`'s events-follow-name + Properties-stay-with-Data semantic, `variable.set` as the sole binding-mint site, the string-not-iterable rule, and the `JsonNode`/`JsonArray` `TypeConverter` dispatch.

Filled gaps in 6 docs:
- `Documentation/v0.2/data-generic-design.md` (new section)
- `Documentation/v0.2/variables.md` (API surface fix + behaviour rules rewrite + stale `*__Generated` ref fix)
- `Documentation/v0.2/architecture.md` (Variables snippet fix)
- `Documentation/v0.2/debug.md` (events-aliased correction)
- `Documentation/v0.2/good_to_know.md` (6 cross-cutting entries)
- `docs/modules/loop.md` + `docs/modules/variable.md` (user-visible behaviour notes)

Applied 1 CLAUDE.md proposal (coder/v2 — running plang tests).

Build clean post-edits (0 errors, 364 pre-existing warnings).

See [v1/summary.md](v1/summary.md) for full details.
