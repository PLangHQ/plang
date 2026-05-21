# tester — filesystem-permission

## Version
v3 (matches coder v3 — the version under review)

## What this is
The `filesystem-permission` branch adds PLang's consent-gated filesystem
access: a `Permission` record, a `Path.Authorize(verb)` gate that prompts the
actor on out-of-root access, per-actor in-memory + sqlite grant storage, a v2
Path-in/Data-out FS surface, and a snapshot/resume engine for stateless
suspend. coder v3 was a small closing version — it fixed codeanalyzer v2 #2
(a one-line OS-aware case-comparison fix in `PLangFileSystem.ValidatePath`)
and deferred v2 #1 (handler copy-paste) to a new branch.

This is the **tester** pass — test-quality review, run after codeanalyzer v3
PASSed the code.

## What was done
- Clean rebuild (per the stale-binary rule). C# suite **2846/2846 pass**;
  PLang suite **213 pass, 6 fail** — all 6 fails are intentional fail-fixtures
  (`_fixtures_fail`, `_fixtures_sensitive`), not coder regressions.
- Coverage run → `v3/coverage.json`. Changed FS files are well line-covered
  (Path.Authorize.cs 98.4%) — which turned out to be the trap, not the
  reassurance.
- Read every permission test file (C# + PLang `.test.goal` + `.pr`) with the
  deletion test and parameter-swap test.
- Verdict: **NEEDS-FIXES** — 3 major false-greens, 5 minor, 1 process violation.
  Output: `v3/result.md`, `v3/plan.md`, `v3/verdict.json`, `v3/coverage.json`,
  shared `test-report.json`.

## Key findings
1. **(major) v3's fix has no test.** The only behavioral change in v3 —
   `ValidatePath:227` `OrdinalIgnoreCase`→`RootComparison` — can be reverted
   without failing a single test. It is a permission gate (case-variant path
   bypass on Linux). 100% line-covered, 0% behaviorally verified.
2. **(major) Move can't be told from Copy.** No test checks that `MoveTo`
   removes the source. Flip `isMove` in `PerformTransfer` → suite stays green.
3. **(major) 6 of 8 PLang permission goals are false greens.** Named after
   Stage 5 scenarios (`RestartStillNoPrompt`, `NoGrantSuspends`, ...) but
   bodies do trivial in-root round-trips that never reach the gate.
4–8. (minor) empty Scenario4 body reports *passed*; weak storage assertions;
   tautological `LegacyFsGoalTests`; untested `OsDirectory` clause; Move/Copy
   "n" + stateless branches untested.
9. (minor/process) no `baseline-tests.md` in any coder version.

All 9 are test-quality gaps — no code bug found. The code is correct; the
suite just doesn't prove it.

## Code example — the false green that defines the verdict
```diff
// v3's entire behavioral change (PLangFileSystem.cs:227):
- if (!path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase))
+ if (!path.StartsWith(RootDirectory, App.FileSystem.Path.RootComparison))
```
Every in-root test uses exact casing (matches under both); every out-of-root
test uses a wholly distinct path (matches under neither). The one input that
separates `Ordinal` from `OrdinalIgnoreCase` — a path whose root prefix
differs only in case — is never supplied. Fix: a Linux-gated `ValidatePath`
test with an upper-cased root segment, asserting out-of-root treatment.

## Next
Back to coder to add the missing tests (findings 1–3 are the blockers; cheap
fixes — ~1 ValidatePath test, 1 Move assertion block, rename/delete 6 goals).
