# Builder Consistency Framework

**Date:** 2026-03-06
**Status:** Planned — architect to design and implement
**Reference:** `.bot/runtime2-plang-test-gaps/coder/v1/builder-improvement-options.md` for full options analysis

## Problem

The builder LLM (currently OpenAI) is inconsistent when converting `.goal` → `.pr` JSON:
- Drops `onError` step properties silently
- Swaps parameter names (e.g., `Value`/`Container` in assert.contains)
- Same `.goal` input can produce different `.pr` output across builds

PLang is multilingual — validation can NEVER pattern-match step text. All validation must work on the structured `.pr` output.

## What We Already Have

- **Module registry** (source-generated records) — defines every module's actions, parameter names, types, required/optional
- **Working PLang test suites** (`Tests/Runtime2/`) — can serve as seed data for golden files

## The Two Layers

### 1. Structural Validation (every build, zero cost)

Runs automatically after the LLM generates a `.pr` file. Deterministic, no LLM calls.

**Validates:**
- Parameter names match the module registry for the given `module.action`
- Parameter types are compatible with registry type definitions
- Required parameters are present
- `path`/`prPath` follow correct naming conventions
- Step indices are unique and sequential
- No unknown `module.action` combinations
- If `onError` is present, its structure is valid

**Self-correcting feedback loop:** When a violation is found, feed the error + the correct module registry entry back to the builder LLM so it retries. Example: "You produced parameter `Valuee`, but `assert.equals` expects `Expected`, `Actual`, `Message`. Fix your output."

**Cannot validate:** Whether `onError` SHOULD be present (that's semantic — the LLM's job), whether the right module was chosen, whether parameter values are correct.

### 2. Golden Eval Suite (on-demand, for benchmarking)

A curated set of `.goal` files with human-verified `.pr.golden` output. Run when:
- Evaluating a new LLM
- After changing builder prompts
- Periodic confidence checks

**Metrics (from Cleanlab methodology):**
- **Field Accuracy** — proportion of individual fields correct across all golden files (e.g., "module correct 98%, onError correct 72%")
- **Output Accuracy** — proportion of golden files where EVERY field is correct (the hard metric)

**~50-100 golden files** covering: on error call/retry/ignore, foreach, if/else, variable set, goal call with return, etc.

### 3. Consistency Scoring (on-demand, for LLM comparison)

Build the same `.goal` file N times, measure output variation. From the STED framework (Dec 2025):
- Claude-3.7-Sonnet: 0.999 structural consistency
- Claude-3.5-Haiku: 46% degradation at higher temperatures

Use when comparing LLMs. Candidate must match or beat current LLM.

### 4. LLM-as-Judge (on-demand, for semantic validation)

Second LLM validates the first's output. Handles multilingual intent without hard-coded patterns. Most expensive — use selectively on patterns where structural checks aren't enough.

## Targets

- Baseline: measure current state (probably 70-80% Output Accuracy)
- v0.2: 90% (structural validation catches + self-corrects the worst failures)
- v0.3: 95% (consistency scoring identifies flaky patterns)
- v1.0: 99%+ (LLM-as-judge for remaining semantic issues)

## References

- [STED: Structured Output Consistency Scoring](https://arxiv.org/abs/2512.23712)
- [Cleanlab: Structured Output Benchmarks](https://cleanlab.ai/blog/structured-output-benchmark/)
- [JSONSchemaBench](https://arxiv.org/html/2501.10868v3)
- [DeepEval](https://deepeval.com/docs/evaluation-datasets)
- [Practical Guide for Evaluating LLMs](https://arxiv.org/html/2506.13023v1)
