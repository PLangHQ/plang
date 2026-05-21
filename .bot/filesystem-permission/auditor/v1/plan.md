# Auditor v1 — plan

## Task
Cross-cutting integrity audit of `filesystem-permission`. Three reviewers
already passed: codeanalyzer v3, tester v4, security v1. My job is the gaps
between them, not re-running their passes.

## What the prior reviewers concluded
- **codeanalyzer v3 — PASS** with one open follow-up: `RootComparison` should
  reach `Path.cs:125,127` (Relative getter) and `PLangFileSystem.cs:254`
  (system/ fallback). Both still use `OrdinalIgnoreCase`. Punted to "next pass".
- **tester v4 — PASS**. All 9 v3 findings mutation-verified closed. Calls
  Scenario4 "a real cross-App persistence gate". Three minor notes N1–N3.
- **security v1 — PASS** with 4 findings (F1 signer identity not pinned,
  F2 unsigned persisted rows auto-trusted, F3 persisted grants expire after
  5 min, F4 regex ReDoS). All rated non-blocking.

## Audit focus (the seams)
1. **Branch/merge integrity.** Merge-base is `79d76aa0`, before the
   app-lowercase merge into runtime2. runtime2 now has lowercase `app/`;
   this branch adds ~40 new files under PascalCase `App/`. Assess merge
   blast radius — no reviewer checked the branch against *current* runtime2.
2. **tester vs security contradiction on Scenario4.** tester: "real
   persistence gate". security F3: "false-greens — grants expire after
   5 min, tests only pass because they run in ms". Adjudicate: does the
   test verify the *code path* but not the *documented intent*?
3. **codeanalyzer's punted follow-up.** Is `Path.cs:125,127` truly only
   observability (security's claim) or does `Relative` feed a gate? Verify
   `PLangFileSystem.cs:254` independently.
4. **Snapshot/resume engine.** The largest change (App.Run deleted, action
   owns execution, recursive cross-goal resume). codeanalyzer reviewed
   files, tester reviewed tests — did anyone review the *mechanism* as a
   whole for correctness?
5. **v1/v2 filesystem surface coexistence.** v1 surface kept for ~50 legacy
   callers. Are there file-action code paths that still bypass `Authorize`?
6. **Cross-file contracts** around the deleted callback classes
   (`ICallback`, `AskCallback`, `ErrorCallback`) — all consumers adapted?

## Deliverables
- `.bot/filesystem-permission/auditor-report.json`
- `.bot/filesystem-permission/auditor/v1/verdict.json`
- `.bot/filesystem-permission/auditor/v1/result.md`
- Update `summary.md`, `report.json`
