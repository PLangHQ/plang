# PLang Builder — Architecture & Hardening Plan

> Living document. Last updated: 2026-04-09

## Why This Matters

The builder is the compiler for PLang. It translates natural language `.goal` files into structured `.pr` files via LLM. Every PLang program passes through it. If the builder is unreliable, PLang is unreliable. This document captures the plan for making it rock-solid.

---

## Part 1: Pipeline Redesign

### Current State

The builder uses a two-pass LLM strategy:

```
Pass 1: BuildGoal (whole-goal)
  → LLM sees compact action summary + all steps in the goal
  → Returns module/action/parameters per step
  → Flags needsDetail: true on steps it couldn't fully construct

Pass 2: BuildStep (per-step detail)
  → For each needsDetail step, one-by-one
  → LLM gets full parameter schema for the identified module
  → Returns complete parameters
```

Validation happens in `ApplyStep`: `ValidateActions` checks if module.action exists in the registry. Failed validation triggers a BuildStep retry with error context.

### Problems

1. **`needsDetail` is binary** — The LLM either says "I got it" or "I didn't." There's no middle ground. A step where the LLM is 60% confident gets treated the same as one where it's 99% confident.

2. **No confidence signal** — We have no way to know which steps the LLM struggled with. Steps that pass but are subtly wrong look the same as steps that are perfectly correct.

3. **No batching** — Each `needsDetail` step gets its own LLM call. If 5 steps in the same module need detail, that's 5 separate calls, each loading the same module documentation.

4. **Validation failures handled one-at-a-time** — A step that fails validation goes back through BuildStep alone. If multiple steps failed for related reasons (e.g., all using the wrong parameter name for the same module), each gets a separate retry with no shared learning.

### New Design: Confidence + Grouped Batching

**Pass 1: Whole-Goal (same as today, with confidence)**

The LLM returns everything it returns today, plus a `confidence` score (0.0–1.0) per step. The `needsDetail` flag is removed.

```json
{
  "steps": [
    {
      "index": 0,
      "confidence": 0.95,
      "actions": [{ "module": "variable", "action": "set", ... }]
    },
    {
      "index": 1,
      "confidence": 0.6,
      "actions": [{ "module": "file", "action": "read", ... }]
    }
  ]
}
```

**Triage**

After pass 1 + validation, steps are categorized:

| Category | Condition | Action |
|----------|-----------|--------|
| **Done** | confidence ≥ threshold AND passes validation | Accept as-is |
| **Low confidence** | confidence < threshold | Send to pass 2 |
| **Validation failed** | ValidateActions returned error | Send to pass 2 with error context |

The threshold is configurable (start with 0.8, tune based on eval data).

**Grouping for Pass 2**

Steps that need pass 2 are grouped by module. Each group gets a single LLM call with:
- Full parameter schema/documentation for that module (and related modules if steps span multiple)
- All steps in the group
- For validation-failed steps: the specific error messages

```
Group 1: [step 1, step 4] → both use file module → one LLM call with file module docs
Group 2: [step 3]         → uses condition module → one LLM call with condition module docs  
Group 3: [step 2, step 5] → validation failures → one LLM call with error context + relevant module docs
```

**Why this is better:**
- Fewer LLM calls (batching by module)
- Better context per call (LLM sees related steps together)
- Confidence signal feeds into evals (we can track which modules/patterns produce low confidence)
- Validation errors are grouped — the LLM can learn from one error to fix related ones in the same batch

### Open Questions

- Should confidence be per-step or per-action? (A step can have multiple actions)
- Should groups that share modules be merged? (e.g., a low-confidence file step and a validation-failed file step)
- What's the right confidence threshold? (Needs eval data to tune)
- Should pass 2 failures go to a pass 3, or surface as build errors?

---

## Part 2: Eval Strategy

### The Four Layers

Building on prior analysis (`.bot/runtime2-plang-test-gaps/coder/v1/builder-improvement-options.md`):

### Layer 1: Structural Validation (Every Build)

**Runs:** Automatically on every build, deterministic, zero LLM cost.

**What it checks** (all from structured .pr JSON, never from step text):
- Parameter names match the module registry
- Parameter types are compatible with registry type definitions
- Required parameters are present
- No unknown module/action combinations
- `onError` structure is valid when present
- Step indices are unique and sequential
- `path` and `prPath` follow naming conventions

**What it cannot check:**
- Whether the *right* module was chosen (semantic)
- Whether parameter *values* are correct (semantic)
- Whether `onError` *should* be present (intent)

**Feedback loop:** Validation errors feed back into pass 2 (see pipeline redesign above). The LLM self-corrects with concrete error messages.

**Status:** Partially exists (`ValidateActions` checks module.action existence). Needs expansion to cover parameter validation.

### Layer 2: Golden Eval Suite (On-Demand)

**Runs:** After changing builder prompts, before switching LLMs, periodic confidence checks.

**What it is:** A curated set of `.goal` files paired with human-verified `.pr.golden` files. The known-correct answer for each PLang pattern.

**Structure:**
```
Tests/Builder/Evals/
  variable-set.goal          → .build/variable-set.pr.golden
  on-error-call.goal         → .build/on-error-call.pr.golden
  foreach-call.goal          → .build/foreach-call.pr.golden
  if-else.goal               → .build/if-else.pr.golden
  goal-call-return.goal      → .build/goal-call-return.pr.golden
  file-read-write.goal       → .build/file-read-write.pr.golden
  ... (~50-100 files covering all known patterns)
```

**Metrics:**
- **Output Accuracy**: proportion of golden files where ALL fields correct
- **Field Accuracy**: proportion of individual fields correct across all golden files (shows where the LLM struggles)

**Targets:**
- Baseline: measure where we are today
- v0.2: 90% Output Accuracy
- v0.3: 95%
- v1.0: 99%+

### Layer 3: Consistency Scoring (When Evaluating LLMs)

**Runs:** When evaluating a new LLM candidate.

**What it does:** Builds the same `.goal` file N times (5-10), compares outputs against each other. Produces a consistency score (1.0 = identical every time, 0.5 = different half the time).

**Why it matters:** A pattern might match the golden output 80% of the time. Golden tests catch this only if you're unlucky. Consistency scoring catches it by design.

**Cost:** N × LLM calls per pattern. Periodic use only.

### Layer 4: LLM-as-Judge (Semantic Validation)

**Runs:** On-demand for patterns where structural checks aren't sufficient.

**What it does:** A second LLM evaluates whether the builder's output correctly represents the user's intent. Handles multilingual validation (can judge Icelandic, Swedish, etc. steps).

**Cost:** Doubles LLM cost per step. Use selectively.

### Learning from Failures

Every eval run produces data. That data should feed back into the system:

1. **Failure patterns** → New golden test cases (the suite grows from real failures)
2. **Low-confidence patterns** → Builder prompt improvements
3. **Consistency drops** → Module documentation improvements (give the LLM better information)
4. **Field-level accuracy** → Targeted structural validation (if parameters are the weak point, validate parameters harder)

The eval suite is not just a test — it's a learning system.

---

## Part 3: Additional Ideas

*This section is for ongoing ideas as we develop the plan.*

<!-- Ingi: add your thoughts here -->

---

## Implementation Order

1. **This document** — Get alignment on the vision (now)
2. **Golden eval suite** — Start measuring before we change anything
3. **Structural validation expansion** — Expand ValidateActions to cover parameters
4. **Pipeline redesign** — Replace needsDetail with confidence + grouping
5. **Consistency scoring** — Build the tooling for LLM evaluation
6. **LLM-as-judge** — Add semantic validation where needed

---

## References

- Builder-improvement-options analysis: `.bot/runtime2-plang-test-gaps/coder/v1/builder-improvement-options.md`
- Builder architecture memory: `/home/claude/.claude/projects/-workspace-plang/memory/architecture_builder.md`
- Builder validation design: `.bot/runtime2-builder-validation/architect/v1/plan.md`
- STED framework (structured output consistency): arxiv.org/abs/2512.23712
- Cleanlab structured output benchmarks: cleanlab.ai/blog/structured-output-benchmark/
