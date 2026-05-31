# tester — v8 — plan

Validating coder v8 (codeanalyzer v2 PASS). Matching coder's version: v8.

## What I'm checking
1. Clean rebuild + C# suite (`dotnet run --project PLang.Tests`).
2. PLang suite (`cd Tests && plang --test`) from a clean binary, **multiple runs**
   to test reproducibility — the v1 failure mode was false greens.
3. F1 (strict-kind enforcement) is the review-driven, highest-risk area:
   - Read `IStrictKindEnforcer`, `image.@this`, `variable/set.cs`.
   - Confirm the two enforcement paths (read-lift/already-loaded → fail at set;
     lazy path-backed → throw at `BytesAsync`) and the raw-`byte[]` probe path
     are each genuinely covered, not vacuous.
   - Builder false-green check: read the strict `.pr` files, confirm step text
     matches `variable.set` with a `{name,kind,strict}` Type entity.
4. F2–F5 spot-confirm (Data.Kind JsonIgnore, Scheme null-guard, text Build-hook
   deletion, dead fast-path removal).
5. Builder sanity (cache=false on a throwaway) before trusting `plang --test`.

## Status: COMPLETE — verdict FAIL
Headline: the C# suite is solid (3815/3815, F1 honestly covered), but the
**PLang suite is not reproducibly green from a clean binary** — committed `.pr`
fixtures are stale w.r.t. the branch's own stage-4 `variable.set.Type` change,
so the runtime runs them wrong or LLM-rebuilds them (non-deterministic). See
`result.md`.
