# Auditor v1 — runtime2-builder-bootstrap

## What this is

First and only auditor pass on `runtime2-builder-bootstrap`. The branch took a `runtime2` baseline through 26 commits across coder/codeanalyzer (4 rounds)/tester (4 rounds)/security (1 round). My job: cross-cutting integrity check — find what file-level reviewers, test-quality reviewers, and security reviewers each individually approved but might collectively miss in the seams.

## What was done

Read all prior reports (codeanalyzer v1-v4 result.md + summary.md, tester v4 result.md + plang_tests_results.json, security v1 summary.md + security-report.json, original coder/v1/report.md). Confirmed timestamps and commit chain. Then traced 5 cross-file paths the auditor cares about:

1. **Locale parse/format symmetry** (codeanalyzer v4 escalated). Verified commit `cc8e638d` closed it at all 3 sites.
2. **BuildingGuard removal rationale.** Found the commit's claim of a "file provider .pr write guard" doesn't match the code.
3. **F4 cluster categorization.** Triaged 23 reds into ~6 foundation-shaped + ~17 module-domain.
4. **Variables.GetAll() leak surface.** Found one path security missed (FluidProvider:94).
5. **Debug.Apply idempotency coverage.** Confirmed zero test bites, agree with codeanalyzer it's defensible.

Verdict: **PASS** with 3 minor + 2 nit findings. No critical/major.

Files written:
- `.bot/runtime2-builder-bootstrap/auditor-report.json` — structured findings
- `.bot/runtime2-builder-bootstrap/auditor/v1/verdict.json` — pass
- `.bot/runtime2-builder-bootstrap/auditor/v1/result.md` — full per-finding write-up
- `.bot/runtime2-builder-bootstrap/auditor/v1/plan.md` — plan
- `.bot/runtime2-builder-bootstrap/auditor/v1/summary.md` — this file

## Code example — the highest-impact finding

The pattern at the top of the auditor's value-add: detecting cross-file gaps between an architectural decision and its documented rationale.

Commit `4633674c` removed BuildingGuard with this rationale:

> Building.IsEnabled property is still used by other layers (Variables resolution short-circuit, **file provider .pr write guard**, Actor setup gating, App shutdown) — those uses stay.

A single grep verified the claim is wrong:

```bash
$ grep -rn "Build\.IsEnabled" PLang/ --include="*.cs"
PLang/Executor.cs:76                                    engine.Build.IsEnabled = true;
PLang/App/this.cs:397                                   if (Build.IsEnabled)
PLang/App/Variables/this.cs:480                         // BUT: when the app is in builder mode...
PLang/App/Actor/this.cs:120                             if (App.Build.IsEnabled && this != App.System)
PLang/App/modules/file/providers/DefaultFileProvider.cs:21    if (action.Context.App.Build.IsEnabled && path.Extension == ".pr")
PLang/App/modules/file/providers/DefaultFileProvider.cs:56    if (action.Context.App.Build.IsEnabled && path.Extension == ".pr")
```

Both `.pr`-extension gates in DefaultFileProvider are on the **Read** path (snapshot logic). The **Save** method has no such gate. So `builder.goalsSave` invoking `file.Save` at runtime works — the "file provider .pr write guard" the commit message references is fictional.

This is exactly what the auditor's role is for: codeanalyzer reviewed DefaultFileProvider, security reviewed the BuildingGuard removal, neither traced the rationale's claim against the code. The finding is doc/comm accuracy, not a new bug, and the architectural choice security accepted still stands. But the rationale as recorded in version-control history is misleading. **Severity: minor.**

## What's next

Per character file (pass verdict): suggest **docs** bot next. Two carryovers worth landing in branch docs: the actual BuildingGuard threat-model posture, and the Step.Clone deferral landmine. The 3 original coder gaps (variable.set AsDefault, file.read ResolveVariables, single→list auto-wrap) deserve dedicated developer-facing documentation as new public capabilities.

The F4 carryover should be split into Tier A (foundation: Foreach × 2, Condition Compound, ContextVars, SetupGoal, ErrorTypes — needs code investigation) and Tier B (module-domain, mostly missing-rebuild artifacts) when the next branch picks it up.
