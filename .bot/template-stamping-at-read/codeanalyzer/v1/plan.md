# codeanalyzer v1 — plan — `template-stamping-at-read`

## Scope reality
First codeanalyzer review on this branch. The branch carries the **entire
`scalars-as-native` refactor** (527 commits off `runtime2`, 361 production C#
files, +10359/-5157) **plus** the recent template-stamping work the coder
handed off as B1–B5. Exhaustive line-by-line over 361 files in one pass is not
honest; this review is **risk-prioritized** with explicit coverage notes.

## Coverage plan (highest risk first)
1. **Handoff B1–B5** (coder explicitly flagged): datetime navigable members,
   `Data.Clr<T>(fallback)` lift, system-var typed reads, `Variables.GetValue`
   removal, bracket-resolution latent-bug fix. **Security-critical:** B3 signing/
   identity reads (Ed25519).
2. **scalars-as-native core architecture:** `item.@this` apex, the
   `ScalarComparer`/`Compare` → `Comparison` + `app.type.compare` mediator
   collapse, the condition `Operator`, `Data.cs` core.
3. **Mandated mechanical passes** over the changed production set: System.IO ban,
   Console.* ban, OBP Rule 9 courier `.Value` smell, provenance/history comments.
4. **Root-cause pass** on the `fix(...)` commits (cache JsonElement, Fluid blank
   render).
5. **Deletion test** on fix-introduced and refactor-introduced code.

## Deferred (documented, not oversight)
- Exhaustive per-file review of the full scalars-as-native body (the ~120 new/
  changed `app/type/**` files). Carried by the prior architect → test-designer →
  coder review rounds visible in the branch history; sampled here, not re-derived.
- Part A test-migration files — mechanical per handoff; spot-checked only.

Verdict + next bot at the end of `report.md` and in `verdict.json`.
