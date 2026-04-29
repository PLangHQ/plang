# Coder v5 — Prior-build hints: `@known` / `@hint` + `keep: true`

## Problem

1. Every rebuild, the `goalFormatForLlm.template` appends `<= null` to fresh steps. LLMs hallucinate operator semantics ("<= null" = null comparison) and emit warnings. (Fixed in progress: drop null case.)
2. When a step HAS been built before, the prompt reproduces its full mapping back from the LLM on every rebuild. Wasted tokens; reproduction itself is a hallucination surface.
3. The builder has no language for "this step already has a good mapping — don't rebuild it." The LLM decides semantically per call.

## Goal

Three-state prompt hint + a `keep: true` response shortcut so the builder deterministically tells the LLM which steps are authoritative, which need refinement, and which are new — and the LLM never re-emits a mapping that's already correct.

## Contract

### Builder → LLM: three-state hint per step

- **`- <text>   @known: <actions>`** — prior `.pr` step text exactly equals current text. Mapping is authoritative.
- **`- <text>   @hint: <actions>`** — prior `.pr` has actions for this step, but the step text drifted. Prior mapping is a hint.
- **`- <text>`** (no marker) — new step, no prior actions.

Diff is strict: any character difference between `currentText` and `priorStep.Text` counts as drift.

### LLM → builder: `keep: true` shortcut

- **Saw `@known`** → respond `{index, keep: true}`. Omit `guidance`, `formal`, `actions`, `level`, `confidence`.
- **Saw `@hint`** → evaluate fit; emit full corrected response (refine if the prior mapping no longer fits the new text).
- **No marker** → build from scratch as today.

Guard rails:
- `keep: true` with no prior actions on the step → validation error.
- `keep: true` combined with `actions` → validation error (ambiguous).
- When in doubt, LLM should emit full mapping; omission is the confident-no-op signal.

### Trace backfill

After the LLM call, for any step the LLM marked `keep: true`, the builder copies the prior `.pr` `actions` and builds a `formal` into the trace response before saving. The trace is always self-contained; the viewer never needs to fetch the .pr. A per-step `source` field (`"known"|"hint"|"new"`) records which hint the builder offered.

### Viewer

Per step, add a small chip: `new` / `known` / `hint`, derived from trace `source`. When `keep: true`, overlay a `kept` chip. Legacy traces without `source` render as today.

## Files

### C# — track prior text on Step

`PLang/App/Goals/Goal/Steps/Step/this.cs`
- Add `PriorText` (string?) property. `[JsonIgnore]` — not serialized to `.pr`; transient per-build.
- `Step.Merge(from)` does NOT copy `PriorText` — prior text is set only by the `Goal.MergeFrom` path.

`PLang/App/Goals/Goal/this.cs` — `MergeFrom`
- Pass 1: exact-text match (today's behavior). On match, `step.Merge(prior)` + `step.PriorText = prior.Text`.
- Pass 2: for unmatched current steps, positional match against unmatched prior steps. On positional match, `step.Merge(prior)` + `step.PriorText = prior.Text` (so it differs from current text → drift).

This keeps exact-match robustness for reorders/additions and surfaces drift when a step was edited.

### Template — emit marker

`system/builder/templates/v2/goalFormatForLlm.template` (line 4)
- Current: `- {{ step.Text }}  <= {% if step.Actions.size > 0 %}<actions>{% else %}null{% endif %}`
- New:
  - `step.Actions.size > 0` AND `step.PriorText == step.Text` → `- {{ step.Text }}   @known: <actions>`
  - `step.Actions.size > 0` AND `step.PriorText != step.Text` → `- {{ step.Text }}   @hint: <actions>`
  - `step.Actions.size == 0` → `- {{ step.Text }}`

### Prompt — teach the markers

`system/builder/llm/BuildGoal.llm`
Replace the current `## Prior Build Hints — @known:` section with:

> Step inputs may carry one of two trailing annotations, emitted by the builder — not by the developer. They are metadata, not code, not operators.
>
> - `@known: <actions>` — the builder confirmed the step text is unchanged since this mapping was produced. Treat the mapping as authoritative. **Respond with `{index, keep: true}` only — omit guidance, formal, actions, level, confidence.**
> - `@hint: <actions>` — the builder detected the step text drifted since this mapping was produced. The prior mapping is a hint, not a promise. Evaluate whether it still fits; if it does, reproduce it; if the drift introduces new intent (e.g., an `on error` clause the prior mapping lacks), refine it. Emit the full response as for any other step.
>
> No annotation → new step, build from scratch.

### Scheme — allow `keep: true`

`system/builder/BuildGoal.goal` (line 23 and 50 — LlmFixer)
- Make `guidance`, `formal`, `level`, `confidence` optional.
- Add `keep?: bool`.
- Result scheme per step: `{index: int, guidance?: string, formal?: string, actions?: [...], errors?: [...], warnings?: [...], level?: string, confidence?: int, keep?: bool}`.

### Apply — skip merge when keep

`system/builder/ApplyStep.goal`
- Current:
  ```
  - builder.validate actions=%stepResult.actions%, on error call HandleValidationError
  - builder.merge step=%goal.Steps[stepResult.index]%, stepFromLlm=%stepResult%, write to %goal.Steps[stepResult.index]%
  - if %stepResult.level% != "high", call BuildStep
  ```
- New: wrap those in `if %stepResult.keep% != true`, else no-op (prior actions are already on the step from the initial `MergeFrom`).

### Validator — guard rails

`system/builder/ValidateBuildResponse.goal`
- When `stepResult.keep == true`:
  - Require `goal.Steps[stepResult.index].Actions.size > 0` (prior exists) — else validation error.
  - Reject if `stepResult.actions` is also present.
- Relax "actions required" check to skip when keep:true.

### Trace backfill + source tag

`system/builder/BuildGoal.goal` → `BuildGoalCore`
- Between the LLM response and the trace save (line 26), add a processing step:
  - For each `stepResult` with `keep: true`, copy `goal.Steps[stepResult.index].Actions` into `stepResult.actions` and generate `stepResult.formal` from the actions.
  - For each `stepResult`, compute `source`:
    - `keep == true` → `"known"` (only way to get keep)
    - else if prior actions existed and prior text differs → `"hint"`
    - else if prior actions existed and prior text matches → `"known"` (unchanged, but LLM chose to re-emit)
    - else → `"new"`
  - Write `source` onto `stepResult` before the trace JSON is assembled.

This likely needs a small C# helper since PLang step logic for formal-from-actions is non-trivial. Simplest: a builder action `builder.backfillKept` that mutates `%stepResults%` in place. To keep scope manageable for this pass, we can inline the backfill logic into the existing `builder.validate` or add a lightweight `builder.enrichResponse`.

**Open design question (flag for Ingi):** is inline PLang-only logic OK here, or add a C# helper? PLang would be cleaner but the formal-from-actions rendering is awkward in Liquid. Recommendation: new C# action `builder.enrichResponse` on the default provider — mirror of `builder.validate`, single responsibility.

### Viewer — chips

`system/builder/web/index.html`
- In `traceToEntry`, pass through `source` and `keep` on each step.
- In `renderEntry`'s step card, after the step-index chip, add:
  - `<span class="badge badge-new">new</span>` or `known` or `hint`, color-coded.
  - If `keep: true`, add a second chip `kept` (distinct color).
- Legacy traces without `source` render unchanged (no chip).

## Order of operations

The builder's own `.pr` files would be invalid mid-migration because we're changing the `llm.query`'s `scheme` parameter. Approved: hand-edit `system/builder/BuildGoal.pr` and friends after the .goal edits, then run a build to verify self-healing.

1. C# `Step.PriorText` + `Goal.MergeFrom` two-pass — build runtime first.
2. Template `@known` / `@hint` rendering — visible in next build's prompts.
3. Prompt rules — teach LLM the three states.
4. Scheme loosening + `keep?: bool` + `ApplyStep` branch + validator — LLM can now skip reproduction.
5. C# `builder.enrichResponse` (or inline) — trace backfill + `source` tag.
6. Hand-edit builder `.pr` for scheme and ApplyStep changes. Rebuild builder from `Build.goal` down, verify .pr files self-heal.
7. Viewer chips.

## What I'll verify before shipping

- `Risky` trace (which used to emit the `<= null` warning) no longer warns.
- Unchanged-step rebuild: LLM response has `keep: true` for all steps; no guidance/actions reproduced; trace backfill restores the full picture for the viewer.
- Drift test: edit one step (e.g., add `on error ignore`), rebuild; that step's response is full-refined while others stay `keep: true`.
- Fresh rebuild (no prior .pr) — template emits bare steps; LLM builds from scratch; trace `source` = `"new"` everywhere.
- Viewer shows `new`/`known`/`hint` chips correctly on a mixed build.
