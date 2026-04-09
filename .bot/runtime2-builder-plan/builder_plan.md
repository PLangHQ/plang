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

### New Design: Formalization → Actions → Self-Assessment

The new pipeline has three distinct phases per step, produced in a single LLM pass:

#### Phase 1: Formalization (before actions)

The LLM first translates the natural language step into a **structured pseudo-syntax** that makes modules, data flow, and variable assignments explicit.

```
Step text:  "read file.txt and write it into %content%"
Formalized: ["file.read file.txt", "variable.set %content%"]

Step text:  "if %count% is greater than 5, call HandleOverflow"
Formalized: ["condition.if %count% > 5", "goal.call HandleOverflow"]

Step text:  "get http://api.example.com/users and set %users%"
Formalized: ["http.get http://api.example.com/users", "variable.set %users%"]
```

`formalized` is an array where each entry is one module operation. The array order is the pipeline — each entry’s output (available as `%__data__%`) flows into the next. The 1:1 mapping between `formalized` entries and `actions` makes validation straightforward. This is **structured chain-of-thought**: the LLM reasons about what the step means before committing to action parameters.

**Why formalization matters:**
- Forces the LLM to decompose multi-module steps explicitly
- Creates an evaluatable intermediate representation (we can check formalization independently of actions)
- Makes data flow visible — you can see exactly where each output goes
- Serves as human-readable documentation if stored in the `.pr` file

#### Phase 2: Actions (no more `return`)

Actions are built from the formalization. The key change: **`return` is removed from the action schema.** Setting a variable is just `variable.set` — there's no special "return" mechanism.

Today:
```json
{
  "actions": [{ "module": "file", "action": "read", "parameters": [...] }],
  "return": [{ "name": "%content%" }]
}
```

New:
```json
{
  "actions": [
    { "module": "file", "action": "read", "parameters": [{ "name": "path", "value": "file.txt" }] },
    { "module": "variable", "action": "set", "parameters": [{ "name": "name", "value": "%content%" }, { "name": "value", "value": "%__data__%" }] }
  ]
}
```

The formalization already showed this: `["file.read file.txt", "variable.set %content%"]`. Two entries, two actions, 1:1 mapping. No magic `return` property needed. No magic `return` property needed.

#### Phase 3: Self-Assessment (after actions)

After producing actions, the LLM assesses its own work with **both** a categorical level and a numeric confidence:

```json
{
  "steps": [
    {
      "index": 0,
      "formalized": ["file.read file.txt", "variable.set %content%"],
      "actions": [...],
      "level": "high",
      "confidence": 0.95
    },
    {
      "index": 1,
      "formalized": ["condition.if %count% > 5", "goal.call HandleOverflow"],
      "actions": [...],
      "level": "medium",
      "confidence": 0.7
    }
  ]
}
```

**Why both level and confidence?** LLMs are better at verbalizing assessment than producing calibrated numbers. A `"level": "low"` is a more honest signal than `"confidence": 0.3` — the LLM knows it's uncertain, but can't reliably quantify *how* uncertain. The level is the LLM's real judgment; the confidence number is the machine-readable approximation for thresholding and automation.

| Level | Meaning | Typical action |
|-------|---------|----------------|
| **high** | LLM is confident in module, action, and all parameters | Accept (if validation passes) |
| **medium** | Module/action likely right, but parameters may be incomplete or uncertain | Candidate for pass 2 |
| **low** | LLM is guessing — unclear which module, ambiguous step | Definitely needs pass 2 |

**Triage**

After pass 1 + validation, steps are categorized:

| Category | Condition | Action |
|----------|-----------|--------|
| **Done** | level=high AND passes validation | Accept as-is |
| **Needs refinement** | level=medium OR low | Send to pass 2 |
| **Validation failed** | ValidateActions returned error (any level) | Send to pass 2 with error context |

**Grouping for Pass 2**

Steps that need pass 2 are grouped by module. Each group gets a single LLM call with:
- Full parameter schema/documentation for that module (and related modules if steps span multiple)
- All steps in the group (including their formalization — the LLM already knows what it's trying to build)
- For validation-failed steps: the specific error messages

```
Group 1: [step 1, step 4] → both use file module → one LLM call with file module docs
Group 2: [step 3]         → uses condition module → one LLM call with condition module docs
Group 3: [step 2, step 5] → validation failures → one LLM call with error context + relevant module docs
```

**Why this is better:**
- **Formalization as CoT** — the LLM thinks before it acts, producing better actions
- **No `return`** — one less special case, variable assignment is always explicit
- **Level + confidence** — honest self-assessment the LLM can actually produce reliably
- **Fewer LLM calls** — batching by module
- **Better context per call** — LLM sees related steps together
- **Confidence feeds evals** — track which modules/patterns produce low confidence
- **Validation errors grouped** — the LLM can learn from one error to fix related ones

### Decisions

- **`formalized` is stored in the .pr file.** The `.pr` file is runtime-independent bytecode — like IL or JVM bytecode. The underlying runtime engine can be in any language (currently C#). The formalization is part of this portable contract: it tells any runtime what the step *means*, independent of implementation.

- **`%__data__%` is implicit.** The runtime always sets `%__data__%` after each module execution. The array order in `formalized` represents this flow — there is no explicit wiring needed. Every action can read `%__data__%` to get the previous action's result.

- **Grouping starts simple.** No merging of groups initially (low-confidence and validation-failed stay separate). Once evals are in place, we can experiment with merging and measure the impact.

- **Pass 2 failures get 2 retries, then build error.** On validation failure, the error feeds back to the LLM (onValidation). Two retry attempts max. If it still fails, surface as a build error — don't spiral.

- **Formalization syntax should be standardized.** Since .pr files are portable bytecode, the formalization syntax needs a defined grammar: `module.action [params] | module.action [params]`. This enables any runtime to parse and understand the formalization, and enables structural validation of the formalization itself.

### Remaining Open Questions

- **Formalization grammar**: Will emerge from real LLM output. Run the builder, collect examples, then formalize the grammar bottom-up from what the LLM actually produces. Don't define the spec before we have data.

### Additional Decisions

- **`level` and `confidence` are stored in the `.pr` file.** They are part of the build artifact, not transient metadata. This enables evals to analyze builder confidence patterns from existing `.pr` files without re-running the builder.

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
4. **Formalization + return removal** — Add formalization phase to builder prompt, remove return from schema
5. **Pipeline redesign** — Replace needsDetail with level/confidence + grouping
6. **Consistency scoring** — Build the tooling for LLM evaluation
7. **LLM-as-judge** — Add semantic validation where needed

---

## References

- Builder-improvement-options analysis: `.bot/runtime2-plang-test-gaps/coder/v1/builder-improvement-options.md`
- Builder architecture memory: `/home/claude/.claude/projects/-workspace-plang/memory/architecture_builder.md`
- Builder validation design: `.bot/runtime2-builder-validation/architect/v1/plan.md`
- STED framework (structured output consistency): arxiv.org/abs/2512.23712
- Cleanlab structured output benchmarks: cleanlab.ai/blog/structured-output-benchmark/
