# v3 Summary — Fresh-Eyes Review

## What this is
Fresh-eyes re-analysis of the builder module, requested by Ingi after the v2 PASS. The goal was to find what the v1/v2 analysis missed.

## What was found

Two real findings the v1/v2 analysis missed:

1. **`Describe()` leaks `[Provider]` properties** (medium) — Every handler's `[Provider]` property (e.g., `Builder`, `Assert`, `Http`) shows up in the `Describe()` output that feeds the LLM builder prompt. The LLM sees these as parameters to map, which is wrong. This affects ALL modules, not just builder. Fix: add `ProviderAttribute` to the skip list in the reflection loop.

2. **`Step.Clone()` drops Action fields** (minor) — The deep copy in Clone creates new Action objects but only copies Module/ActionName/Parameters/Return. Misses Defaults, Errors, Warnings. Classic clone family pattern. No current callers but public method on core entity.

Plus one nit: dead `'\t'` check in `Parse()` continuation line detection (line 314) after tabs were already replaced with spaces (line 203).

## Verdict: NEEDS WORK
Send finding #1 (Describe) and #2 (Clone) to coder for fixes.
