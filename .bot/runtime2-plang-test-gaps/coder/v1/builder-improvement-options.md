# Builder Consistency Framework — Options

## The Problem

The builder uses an LLM to convert PLang `.goal` syntax → `.pr` JSON. The LLM is inconsistent:
- Drops `onError` step properties silently
- Swaps parameter names (`Value`/`Container` in assert.contains)
- Same .goal input can produce different .pr output across builds

We need a framework that:
1. Gives confidence the builder is consistent today
2. Proves a new LLM meets the standard before switching
3. Tracks consistency over time as the builder evolves

**Constraint:** PLang is multilingual. Users write steps in any language. We can NEVER validate by pattern-matching step text (e.g., checking for "on error" in the text). All validation must work on the **structured .pr output**, not on the natural-language input.

**What we already have:** The module registry (source-generated records defining each module's actions and parameter specs) already exists and is used during building. The options below build on top of that.

---

## Option 1: Golden Eval Suite

**What:** A curated set of `.goal` files paired with human-verified `.pr` output. This is the same concept the LLM evaluation industry calls a "golden dataset" — a benchmark suite where the correct answer is known, and you measure how well the LLM reproduces it.

**Concretely:**
1. Create `Tests/Builder/` with one `.goal` file per PLang pattern:
   - `on-error-call.goal` → step that calls a goal with `on error call Handler`
   - `on-error-retry.goal` → step with retry error handling
   - `foreach-call.goal` → foreach with goal call
   - `if-else.goal` → conditional branching
   - `variable-set.goal` → simple variable assignment
   - `goal-call-return.goal` → goal call that writes result to a variable
   - etc. — one file per distinct pattern, ~50-100 files total
2. For each, store a **verified `.pr.golden`** file — the known-correct output, reviewed and approved by you
3. After a build, compare each generated `.pr` against its `.pr.golden` using **field-level comparison** (not string diff):
   - Does `module` match?
   - Does `action` match?
   - Do parameter names and types match?
   - Is `onError` present when the golden says it should be?
   - Are `return` mappings correct?
4. Report a **score**: `85/100 patterns correct`, with details on which fields diverged

**How comparison works — two levels (from Cleanlab's approach):**
- **Field Accuracy**: proportion of individual fields correct across all golden files. e.g., "module correct 98%, action correct 96%, onError correct 72%, parameters correct 88%"
- **Output Accuracy**: proportion of golden files where EVERY field is correct. This is the hard metric — a file with one wrong field counts as a failure.

**LLM switching:** Build the golden suite on the candidate LLM. Compare its Output Accuracy against the current LLM. If equal or higher, it's a candidate. If lower, you see exactly which patterns it fails on and can decide if those matter.

**Effort:** Low. The golden files are just verified .pr files — you already have working ones from existing tests. The comparison tool is straightforward JSON comparison.

**Limitation:** Only covers patterns you've written. Unknown patterns can still fail silently. But the suite grows over time as you discover new failure modes.

---

## Option 2: Structural Schema Validation on .pr Output (every build)

**What:** A post-build validator that checks every generated `.pr` file against the **module registry** (which already exists). This is not text matching — it validates the structured JSON output against the source of truth for what each module/action expects.

**Runs on every build.** This is the one piece that's always on — it's deterministic, no LLM calls, and fast. When it catches a violation, it feeds the error back to the builder LLM so it can retry and fix the output. This is how you get reliability without running expensive eval suites on every build.

**What it validates (all from structured .pr JSON, never from step text):**
- Parameter names in the .pr must match the module registry's expected parameter names for that `module.action`
- Parameter types must be compatible with the registry's type definitions
- Required parameters must be present
- `path` and `prPath` must follow correct naming conventions (derivable from file location)
- Step indices must be unique and sequential
- No unknown module/action combinations
- If `onError` is present, its structure must be valid (has `goal.name`, valid `retry` shape, etc.)

**What it does NOT validate (because it can't):**
- Whether `onError` SHOULD be present — that requires understanding the user's natural-language intent, which is the LLM's job
- Whether the right module was chosen — that's semantic, not structural
- Whether parameter VALUES are correct — that's also semantic

**The feedback loop:** When the validator catches a violation (e.g., parameter `Valuee` doesn't exist on `assert.equals`), it sends the error + the module registry entry back to the LLM: "You produced parameter `Valuee`, but `assert.equals` expects `Expected`, `Actual`, `Message`. Fix your output." The LLM retries with this concrete information and gets it right. This turns a silent failure into a self-correcting build.

**LLM switching:** Same validator runs regardless of LLM. Structural validity is a minimum bar — any LLM that produces structurally invalid output is immediately disqualified (or self-corrects via retry).

**Effort:** Medium. The module registry already exists (source generator). The validator walks each .pr file and checks parameters against the registry. One-time investment, deterministic forever.

---

## Option 3: Consistency Scoring via Repeated Generation

**What:** Measure how consistent the LLM is by building the same `.goal` file multiple times and comparing outputs. This is what the STED framework (Dec 2025) does — it quantifies how much an LLM's structured output varies across repeated generations of the same input.

**How it works:**
1. Take a set of `.goal` files (can overlap with the golden suite)
2. Build each one N times (e.g., 5-10 times) with the same LLM
3. Compare the N outputs against each other:
   - Do they always produce the same `module.action`?
   - Do parameter names stay the same across runs?
   - Does `onError` appear consistently or intermittently?
4. Produce a **consistency score** per pattern: 1.0 = identical every time, 0.5 = different half the time

**Key insight from STED research:** Claude models show significantly different consistency profiles. Claude-3.7-Sonnet maintained near-perfect structural consistency (0.999) even at high temperatures, while Claude-3.5-Haiku degraded 46% across the same range. This means consistency scoring directly predicts production reliability.

**What it catches that golden tests don't:** A pattern might match the golden output 80% of the time and produce a different (but also valid) output 20% of the time. Golden tests catch this only if you're unlucky enough to get the wrong output during testing. Consistency scoring catches it by design — if the output varies, the score drops.

**LLM switching:** Run consistency scoring on both the current and candidate LLM. The candidate must have equal or higher consistency scores across all patterns. Low consistency on critical patterns (error handling, return mappings) is a hard disqualifier.

**Effort:** Medium. Requires multiple build runs per pattern (cost = N × LLM calls). But gives uniquely valuable signal that single-run tests can't provide.

**Limitation:** Expensive (multiple LLM calls per evaluation). Best used periodically or when evaluating a new LLM, not on every build.

---

## Option 4: LLM-as-Judge Validation

**What:** Use a second LLM call to validate the first LLM's output. The judge LLM receives the `.goal` input, the generated `.pr` output, and the module registry, then evaluates whether the output correctly represents the user's intent.

**How it works:**
1. After the builder LLM generates a `.pr` file, send a validation prompt to a judge LLM:
   - "Given this PLang step text: `[step text]`"
   - "And this module registry entry: `[registry for the matched module.action]`"
   - "And this generated .pr output: `[the output]`"
   - "Does the output correctly represent the user's intent? Check: correct module, correct action, correct parameter mapping, error handling preserved, return values mapped."
2. The judge returns pass/fail with reasoning
3. On failure, optionally retry the builder with the judge's feedback

**Why this handles multilingual:** The judge LLM understands natural language just like the builder LLM. It can evaluate whether `on error` in English, `vid fel` in Swedish, or `þegar villa` in Icelandic was correctly mapped to `onError` — without hard-coded string patterns.

**LLM switching:** The judge can be a different (more capable) model than the builder. When switching builder LLMs, keep the same judge for consistent evaluation.

**Effort:** Medium. Doubles LLM cost per build step. But provides semantic validation that structural checks can't — "did the LLM understand the intent correctly?"

**Limitation:** Another LLM can also be wrong. Not deterministic. Best used as a safety net alongside structural validation, not as the sole validator.

---

## Recommended Path

### What runs when

| Tool | When it runs | Cost |
|------|-------------|------|
| **Structural Validation** (Option 2) | Every build, automatically | Zero — deterministic, no LLM calls |
| **Golden Eval Suite** (Option 1) | On-demand: after changing builder prompts, before switching LLMs, periodic confidence checks | Low — one build of the golden suite |
| **Consistency Scoring** (Option 3) | On-demand: when evaluating a new LLM | Medium — N builds per pattern |
| **LLM-as-Judge** (Option 4) | On-demand: for patterns where structural checks aren't enough | High — extra LLM call per step |

### Build phases

**Phase 1 (now): Option 2 — Structural Validation on every build**
- This is the foundation — catches LLM mistakes and feeds errors back so the LLM self-corrects
- Uses the existing module registry, no LLM cost
- A step that passes structural validation has correct parameter names, types, and structure

**Phase 2 (next): Option 1 — Golden Eval Suite (on-demand)**
- Create 50-100 `.goal` files covering all known PLang patterns
- Store verified `.pr.golden` files
- Run on-demand to get Field Accuracy and Output Accuracy scores
- **This is your benchmark**: "today, with OpenAI, we get 85% Output Accuracy"
- Structural validation catches invalid output; the golden suite catches wrong-but-valid output

**Phase 3 (when evaluating LLMs): Option 1 + Option 3**
- Run the golden suite on the candidate LLM → Output Accuracy score
- Run consistency scoring on critical patterns → Consistency score
- Decision matrix: candidate must match or beat current LLM on both scores

**Phase 4 (longer term): Option 4 — LLM-as-Judge**
- Add semantic validation for the patterns where structural checks aren't enough
- This is where you handle the "right module but wrong parameter semantics" cases
- On-demand, use selectively on high-value patterns

---

## Measuring Progress

Track two numbers:

```
output_accuracy = (golden tests where ALL fields correct) / (total golden tests) × 100
field_accuracy  = (correct fields across all golden tests) / (total fields) × 100
```

Output Accuracy is the hard metric. Field Accuracy shows where the LLM struggles.

**Targets:**
- Baseline: measure where you are today (probably 70-80% Output Accuracy)
- v0.2: 90% (structural validation catches the worst failures)
- v0.3: 95% (consistency scoring identifies and addresses flaky patterns)
- v1.0: 99%+ (LLM-as-judge catches remaining semantic issues)

**When evaluating a new LLM:**
1. Run golden suite → Output Accuracy (must be ≥ current)
2. Run consistency scoring on critical patterns (must be ≥ current)
3. Run structural validation (must pass 100%)
4. If all three pass, the new LLM is qualified

---

## References

- [STED: Structured Output Consistency Scoring](https://arxiv.org/abs/2512.23712) — Framework for measuring LLM consistency on structured JSON output
- [Cleanlab: Structured Output Benchmarks](https://cleanlab.ai/blog/structured-output-benchmark/) — Field Accuracy / Output Accuracy metrics, golden dataset methodology
- [JSONSchemaBench](https://arxiv.org/html/2501.10868v3) — 10K real-world JSON schema benchmark
- [DeepEval](https://deepeval.com/docs/evaluation-datasets) — Golden dataset framework for LLM evaluation
- [Deepchecks](https://www.deepchecks.com/llm-evaluation/framework/) — Continuous LLM evaluation with regression detection
- [Practical Guide for Evaluating LLMs](https://arxiv.org/html/2506.13023v1) — Datasets + Metrics + Methodology framework
