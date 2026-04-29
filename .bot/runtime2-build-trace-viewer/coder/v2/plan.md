# Coder v2 — Plan: Add `formal` intermediate representation to builder response

## Goal

Let the LLM emit a formal, pipe-separated shorthand of each step's action chain alongside the JSON. Makes the natural → formal → JSON translation visible to Ingi in the viewer, and forces the LLM to commit to a compact shape before serializing. Same notation as the catalog in `summary.md` (`Name([type?=default] value)`), but with actual values filled in.

Example:

```
step     : - read file.txt, ascii, write to %content%
formal   : file.read Path([path] file.txt), Encoding([string] ascii)
           | variable.set Name([string] %content%), Value([object] %__data__%)
actions  : [{"module":"file","action":"read",...},
            {"module":"variable","action":"set",...}]
```

## Confirmed syntax rules (from Ingi this session)

- Params comma-separated within an action.
- Actions pipe-separated within a step (`|`).
- Values inline after `[type]`: `Name([type] value)`. Strings with spaces or commas are quoted (matches how the natural step would write them). Numbers, bools, and `%var%` refs unquoted.
- Structured values stay JSON (e.g. `goal.call` payload): `GoalName([goal.call] {"name":"Greet","parameters":[...]})`.
- Modifiers (`error.handle`, `cache.wrap`, `timeout.after`) included as extra pipe segments right after the action they wrap.

## Scope (v2)

Three edits. All in the builder pipeline + viewer — no runtime change.

### 1. `system/builder/llm/BuildGoal.llm`

**Add `formal` to the per-step response specification** — before `actions`, so the LLM writes the formal first and then maps to JSON:

- Update the "Response Format" list (currently lines 27–31) to:
  ```
  1. guidance — your reasoning
  2. formal — the action chain in shorthand: module.action Name([type] value), ... | next-action ...
  3. actions — the mapped actions as JSON
  4. level + confidence
  5. group (optional)
  ```

- Add a short "Formal syntax" section explaining the notation: pipe between actions, comma between params, `[type]` tag before value, JSON for structured values, modifiers are extra pipe segments.

- Update the existing example response (lines 64–117) so every step carries a `formal` field matching its `actions`. One example per pattern: simple call, multi-action pipe (`write to %var%`), conditional with sub-step, foreach, error modifier, cache modifier.

- Mention in a single line at the top of the Rules section that `formal` and `actions` must agree — same module/action names, same params. If they don't, the validator should flag it (separate concern, not in scope for v2 — just stated as an invariant).

### 2. `system/builder/BuildGoal.goal`

Extend the scheme in the `llm.query` call (line 23) to add `formal: string` to each step:

```
scheme={description: string, errors?: [...], warnings?: [...],
        steps: [{index: int, guidance: string, formal: string,
                 actions?: [...], errors?: [...], warnings?: [...],
                 level: string, confidence: int}]}
```

The existing `LlmFixer` path (line 50) uses the same scheme — update that copy too.

**Rebuild risk:** rebuilding `system/builder/BuildGoal.goal` itself still carries the LLM-drift hazard I hit in v1. Mitigation plan: try a clean rebuild first; if drift corrupts unrelated steps, revert `buildgoal.pr` and hand-patch only the scheme change in the `llm.query` step. Per the v1 precedent Ingi approved, that hand-edit is acceptable for the builder's own `.pr`.

### 3. `system/builder/web/index.html`

Show the `formal` line in the step card, under the natural step text. CSS class `.step-formal` — monospace, muted color, broken on `|` for readability:

```
┌─────────────────────────────────────────────────────
│ [0] read file.txt, ascii, write to %content%
│     formal:
│       file.read Path([path] file.txt), Encoding([string] ascii)
│       | variable.set Name([string] %content%), Value([object] %__data__%)
│     [flow pipeline row]
│     [actions summary]
└─────────────────────────────────────────────────────
```

If a step has no `formal` (older traces), the line is simply omitted — no error.

## Not in scope (v2)

- No `formal`-vs-`actions` validator. If they disagree, it's a builder bug — we'll see it in the viewer first and decide later whether to enforce. A validator is a v3 concern.
- No change to the catalog notation in `summary.md`. It already uses `Name([type?=default])`; the formal reuses that notation with actual values. Catalog and formal are now consistent by design.
- No migration of historical traces. Old traces just won't show a `formal` row.
- No test. Same reasoning as v1 — viewer is a dev tool, eyeball-validated.

## Risks

- **R1 — LLM drift on builder self-rebuild.** Known from v1. Mitigation: hand-patch the two scheme strings in `buildgoal.pr` if a clean rebuild drifts, as in v1.
- **R2 — Schema field ordering.** The current scheme puts `actions` before `level`/`confidence`. Adding `formal` between `guidance` and `actions` means the LLM is told to emit `formal` → `actions` → `level`. If the LLM ignores the order, we still get the fields — just not the "think formal first" benefit. Low risk.
- **R3 — Formal string grows unbounded.** For a 20-action step with big JSON payloads, `formal` could be large. Not a correctness risk; just means the viewer needs reasonable line wrapping. Already in the plan.

## Order of work

1. Edit `BuildGoal.llm` — new section, update response format, update example. Keep example concise (1-2 new lines per step to add formal, not a full rewrite).
2. Edit `BuildGoal.goal` scheme (two places: main `llm.query`, and `LlmFixer`).
3. Rebuild `system/builder/BuildGoal.goal`. If drift: revert `buildgoal.pr`, hand-patch the two scheme strings only.
4. Rebuild `Start.goal`. Read a fresh trace. Confirm `pass1.response.steps[].formal` is present and reasonable.
5. Edit `index.html` — add `.step-formal` rendering.
6. Smoke in browser. Commit, push, generate patch.

## Deliverables

- `system/builder/llm/BuildGoal.llm` — formal syntax + response-format change + example updates
- `system/builder/BuildGoal.goal` — scheme change in both `llm.query` calls
- `system/builder/web/index.html` — step-formal rendering
- `.bot/runtime2-build-trace-viewer/coder/v2/summary.md`
- `.bot/runtime2-build-trace-viewer/coder/v2/changes.patch`

## Open question

None — syntax rules confirmed. Ready to proceed after approval.
