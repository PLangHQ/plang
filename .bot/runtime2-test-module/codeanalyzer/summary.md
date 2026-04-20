# Codeanalyzer — runtime2-test-module

## v1 (2026-04-20)

Five-pass review of coder v1 test-module implementation. Verdict: **NEEDS WORK**
(fail). Four must-fix findings: (1) direct `System.IO.Path` use in
`discover.cs:77` / `report.cs:259` (CLAUDE.md rule), (2) no-op copy-loop in
`run.cs:141-142` (iterating & adding to the same `HashSet`), (3) duplicated
declared-chain logic between `if.cs:160-165` and `BranchChain.ComputeFor` —
drift-risk pattern, (4) OBP rule 1/5 outside-iteration cluster — six new
instances across `discover.cs`, `BranchChain.cs`, `if.cs Orchestrate`
(handlers looping `goal.Steps` / `step.Actions` from outside the owner).
Several smaller simplifications and v2 items flagged. Recommended: send
back to coder. See `v1/summary.md` and `v1/result.md`.

## v2 (2026-04-20)

Re-review of coder commit `8a462217` that addresses v1 must-fix items.
Verdict: **NEEDS WORK** (fail). All four v1 must-fix items verifiably
resolved; BranchChain.cs deleted cleanly; OBP refactor is well-structured.
Bonus: previously-bare `catch (Exception)` sites in run.cs and discover.cs
were scoped. However, one new behavioural regression introduced:
`Action.IsFirstConditionInStep` uses `Step?.Actions.IsFirstCondition(this)
?? true`, and inner elseif actions have `Step == null` during Orchestrate
(because SplitAtConditions / IndexOf bypass the Actions indexer that sets
Step). The `?? true` fallback makes the coverage subscriber record phantom
branches at site `"?:?"` — the exact case the filter was meant to ignore.
V1 pre-fix code threw NRE (swallowed by `stopOnError: false`), which
accidentally prevented the record. Fix: `?? false` (one line). One v2
follow-up flagged: `ComputeBranchChain` can't emit "else" (latent bug for
future else-branch support). See `v2/summary.md` and `v2/result.md`.

## v3 (2026-04-20)

Re-review of coder commit `d05c138d` addressing v2 must-fix. Verdict:
**CLEAN** (pass). Coder fixed the root cause instead of the symptom:
`SplitAtConditions` now reads via `this[i]` (Actions indexer with
`a.Step ??= Step`) instead of `_items[i]`, so every action returned to
`Orchestrate` has Step propagated. The `?? true` → `?? false` change is
kept as belt-and-suspenders. 11 LOC, 2 files. The coder caught two
additional latent bugs I missed in v2 that shared the same root cause:
(a) `alreadyOrchestrating` guard-key mismatch (masked pre-fix by the
`actions == null` short-circuit) and (b) `DisableChildrenOf` silently
skipped on inner elseifs (indented sub-steps stayed disabled even when
an inner branch matched). Note: no existing test catches any of the
three bugs — recommended tester add a multi-action orchestrate coverage
test. Recommendation: ready for tester. See `v3/summary.md` and
`v3/result.md`.
