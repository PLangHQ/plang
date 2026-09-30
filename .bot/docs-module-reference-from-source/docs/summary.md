# docs — summary

**Version:** v1
**Branch:** `docs/module-reference-from-source` (based on `app-systems`)

## What this is
Learner-facing module reference pages (`docs/modules/*.md`) are hand-written and
drift from the handlers. Direction (Ingi): the property data lives in the
`os/system/modules/<module>/<action>.{description,notes,examples}.md` source (some
already does on `app-systems`), and the learner pages are **generated** from that
source plus the handler C# attributes, so nothing can drift.

## What was done (v1 = spec + golden sample; generator is a coder feature)
- **Format decided (Ingi):** one `notes.md` per action, one line per property,
  tagged — `· say:` (how you type it) and `· builder:` (compile-only directive).
  One file feeds both audiences; each render strips the other's tag.
- **Spec written:** `Documentation/v0.2/module-reference-generation.md` — fact
  ownership (one home per fact), data sources (the descriptor
  `app.goal.step.action.@this` + attribute metadata), type-display mapping,
  generation rules, and a **golden output sample** for the whole `file` module
  (the exact target the generator must emit).
- **File-module prose enriched** in the spec (ready-to-apply `notes.md` for all 7
  file actions), grounded in the real `app-systems` handlers.

## Not done / next (coder)
- The render pass: a second consumer of `app.goal.step.action.@this` (parallel to
  the compile-teaching render) that writes `docs/modules/<module>.md`.
- Teach the compile-teaching loader to strip `· say:` / `· builder:` tags so they
  never reach build prompts. **The tagged `notes.md` files must land in the same
  change as the stripping** — do not commit tagged `notes.md` before it, or tag
  text leaks into build prompts.

## Interim work on the other branch
`doc-tree-app-obp` got a hand-written "How you say it" column across all module
pages + accuracy fixes (file.list returns `path` values not "file objects";
`file.exists` returns a path whose truthiness answers existence; Path params are
type `path`). That prose is the seed the generator's `notes.md` enrichment reuses —
not throwaway.

## Code example (the source → page contract)
Source line (`file/list.notes.md`):
```
Pattern — which files come back, as a glob. · say: matching '<glob>' · builder: only when the step names one
```
Generated learner row (`docs/modules/file.md#list`):
```
| Pattern | matching '<glob>' | string | no | * | which files come back, as a glob |
```
Compile-teaching line (same source, `say:` stripped):
```
Pattern — which files come back, as a glob. only when the step names one
```
