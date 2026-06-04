# tester v13 — re-test merged type-kind-strict + lazy-deserialize

## Context
My v8 verdict was **FAIL** — not a logic defect, but PLang suite not reproducibly
green from a clean binary (688/703 committed `.pr` stale vs stage-4 `variable.set.Type`
entity shape; flapped 0–4 fails across identical runs; "262/262" was a warm-cache artifact).

coder v9–v13 + the `lazy-deserialize` merge (`d4fdd030c`) claim this is resolved:
PLang now **273/273** deterministic across two runs, `git status` clean after each.
codeanalyzer v3 PASSed (273/273 + 4025/0). coder v13 itself is **test-only** (two new
files closing carry-forward gaps).

## What I must verify (in priority order)
1. **Reproducibility — the heart of my v8 FAIL.** Clean binary, run `plang --test`
   ≥2 consecutive times. After each run `git status` MUST be clean (zero `.pr`
   rewritten by the run). If `.pr` files get rewritten, the warm-cache artifact is
   back → FAIL. This is the single most important check.
2. **C# suite** — clean rebuild, `dotnet run --project PLang.Tests`. Confirm ~4025/0.
3. **PLang suite** — confirm 273/273, no stale `.bot` fixtures bleeding in (cd Tests).
4. **coder v13 test quality** — the two new C# test files. Already code-read:
   - `MaterialiseErrorPathTests.cs` — asserts `Error.Key == "MaterializeFailed"`
     (distinct from NotFound), message names source. Honest. Mutation-verified by coder.
   - `SignedDataSurvivesVariableSetListTests.cs` — signs, binds via real `variable.set`,
     reads element back, asserts Signature non-null AND verify==true. Honest.
5. **Strict×lazy seam** — the integration seam codeanalyzer traced. Spot-check the
   strict-kind enforcement tests still pin both paths (read-lift + lazy path-backed).

## Method
- Reproducibility gate first (it's the gate that failed before).
- Then suite counts, then quality read.
- strict-red-is-red: any failing test OR confirmed false-green = FAIL.
