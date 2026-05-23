## docs — v1 — 2026-05-23

### What this is

Ingi asked for an inconsistency scan across all PLang docs, motivated by the principle that *inconsistency in docs degrades LLM output, consistency improves it*. Three trees scanned: `docs/` (user-facing, 33 files), `Documentation/v0.2/` (developer/architecture, 25 files), `Documentation/Runtime2/` (3 files).

After triage, Ingi reframed the criterion: PLang is intent-based, so *phrasing* variation (`into` vs `write to`, `equals` vs `is`) doesn't matter — the builder maps intent to action regardless. What matters is **factual** consistency: contradicting claims, stale renames, broken refs.

### What was done

Five factual fixes applied:

1. **`Documentation/v0.2/architecture.md:15`** — `.Providers — pluggable implementations` → `.Code — pluggable implementations registry`. The registry was renamed; this overview was the last stale mention.
2. **`Documentation/v0.2/architecture.md:398`** — `.pr files (PLang binary format)` → `.pr files (JSON format; see build_process.md)`. Confirmed by Ingi: `.pr` is JSON. Removes a head-on contradiction with `build_process.md`.
3. **`Documentation/v0.2/callbacks.md:41`** — `Providers registrations` → `Code registrations`. Same rename, swept here too.
4. **`Documentation/v0.2/variables.md:118,167,171`** — all three broken `goal-result.md` links repointed to `data-generic-design.md` and other real targets. Also rewrote the "Relationships" block to drop links to `contexts.md`, `app.md`, `call-stack.md`, `modules.md` (none of these files exist) — replaced with prose references to docs that do exist.
5. **`docs/modules/index.md:86`** — builder module row extended with the four actions documented in `builder.md` but missing from the index: `validateResponse`, `enrichResponse`, `promoteGroups`, `merge`.

### Findings raised but NOT acted on (intentional, per Ingi)

- `into` vs `write to` in `docs/modules/file.md` — both express the same intent; left alone.
- `equals` vs `is` in `hello-world.md` vs `condition.md` — same.
- `before each goal` vs `before goal` in `event.md` — same.
- Capitalization of "Goals" in `how-it-works.md` arch diagram — cosmetic.
- Actor privilege levels — confirmed agreement between `architecture.md` and `execution-flow.md`; the `0/1/100+` model in `Runtime2/todos.md` is explicitly a future proposal, not a contradiction.

### Side discovery (not yet fixed)

`Documentation/v0.2/variables.md` originally linked to **five** non-existent files (`goal-result.md`, `contexts.md`, `app.md`, `call-stack.md`, `modules.md`). All five repaired as part of fix #4 since they were in the same file and same kind of fix. Worth a wider broken-link sweep across `Documentation/v0.2/` in a follow-up — not done in this session.

### Code example of the pattern

### v1 follow-up — fresh-eyes pass (same session)

Ingi asked for a second read-through. Found and applied four more:

- `Documentation/v0.2/goals-steps.md` — Relationships block had 5 broken links (`app.md`, `modules.md`, `memory-stack.md`, `call-stack.md`, `events.md`). Same templated pattern as variables.md. Repointed to live docs.
- `Documentation/v0.2/io-channels.md` — Relationships block had 4 broken links (`serializers.md`, `goal-result.md`, `events.md`, `contexts.md`). Same pattern. Repointed.
- `docs/modules/error.md` — `%!error.Message/Key/StatusCode%` were shown in an example but never listed anywhere on the page; added a properties table right after the example so readers can discover them.
- `docs/modules/llm.md` — the `%var!Property%` metadata accessor was only obliquely described ("accessible via `%!` syntax"). Rewrote the intro line to explain the `!` accessor explicitly and contrast it with `.` field access.

**Findings raised but NOT acted on, pending Ingi:**

- `Data.FromError(...)` vs `Data.Fail(...)` — both names used widely; canonical name needs Ingi's call.
- `docs/modules/llm.md:35` default model `gpt-4.1-mini` — looks like a stale OpenAI name; needs confirmation.
- `Documentation/v0.2/builder-data-t-roadmap.md` — describes work already shipped; candidate for delete or archive.
- `Documentation/v0.2/path-polymorphism-plan.md` — handed off to a branch that was never opened; reframe-or-delete decision pending.
- `Documentation/v0.2/building-the-builder.md:86-92` "Known LLM regressions" — needs a date/status stamp.

### Code example of the pattern

The `.Providers → .Code` fix is the representative one:

```diff
- .Providers                    — pluggable implementations
+ .Code                         — pluggable implementations registry
```

Rename was completed in code months ago; `app-tree.md` documented it (`"was: Providers"`), but the overview block in `architecture.md` was missed. Pattern for future cleanups: when a rename lands, grep across **all** of `Documentation/v0.2/` for the old name, not just the canonical reference file.
