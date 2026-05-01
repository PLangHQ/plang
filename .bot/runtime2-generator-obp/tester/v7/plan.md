# Tester v7 — catch up from v4 baseline through v5/v6/v7 (cumulative)

## What this is

Last tester pass was v4 (against coder/v4 — Pattern B regex + IsOrphanMethod
helper). The branch has since landed:

- v5: security findings #1 ([Sensitive] masking) + #3 (cycle/depth → ServiceError)
- v6: closure of v5 finding #1 (auditor)
- v7: **the big one** — Variable + IRawNameResolvable migration; 22 handlers; PLNG001 collapse
- v7-cleanup commit ac028f0f: trivial codeanalyzer findings
- v7 commit 4 (53780c2d): fix variable.set Properties loss + implement ListAdd identity stubs

Reviews already on file: codeanalyzer/v4 PASS (3 MINOR + 7 NIT, no MAJOR);
auditor v1 PASS on v5 (1 MINOR + 3 NIT). Security/v1 from coder/v4. No
tester pass since v4.

I'll do a single cumulative review focused on test quality post-Variable
migration, avoiding duplication of the codeanalyzer's findings (those are about
production-code shape; mine are about whether tests would catch regressions).

## Approach

1. **Run both suites** to confirm clean state (the coder summary claims
   2554/4-fail and 160/16 plang, but commit 4 may have moved the count).
2. **Deletion-test the load-bearing contracts** — IRawNameResolvable carve-out,
   Variable's three operators (Resolve, implicit string, ToString), the
   v7/commit-4 CopyProperties fix in variable.set.
3. **Builder-false-green check on plang tests** — read `.pr` files for the
   migrated `list/*` and `loop.foreach` handlers, verify step text semantically
   matches the action emitted (parameters carry the variable-name string
   that Data<Variable> will resolve through the carve-out).
4. **Coverage check** — what's not tested? Especially around the latent
   contract trap codeanalyzer flagged: a future T : IRawNameResolvable
   without static Resolve falls silently through.

## Pre-stated risks I'm watching for

- **Carve-out asymmetry between Path and Variable**: Path uses
  `Data/this.cs:632-644` (post-substitution); Variable uses
  `Data/this.cs:549-562` (pre-substitution). If a test at the line-612
  TryFullVarMatch path were inadvertently hit by Variable, it would mask the
  carve-out's value. Unlikely (the marker check IS the discriminator) but
  worth deletion-testing.
- **Variable.ToString and the implicit operator** are easy to break and
  hard to detect — they're called in interpolation and method-call sites
  but those don't error out, they just produce wrong strings.
- **WasPercentWrapped has no consumer** — codeanalyzer flagged this. Tests
  likely pin the value but not its purpose. Worth verifying.
- **The PLNG001 reactivated tests** — coder said 5 stubs were activated. If
  the test source passes both pre- and post-migration generators (e.g. the
  source doesn't actually use [VariableName]), the test name is misleading.
- **Plang integration vs C# unit gap** — commit 4 fixed 10 plang
  TestReport tests via CopyProperties. If only the integration tier pins
  the Properties-survival contract, that's a coverage observation worth
  filing.

## What I won't redo

- codeanalyzer's DRY/style findings on production code.
- auditor's contract assessment between Data<T> and legacy emission paths
  (legacy emission is GONE in v7 — this is moot).
- security analysis of the new attack surface (Variable doesn't change the
  trust boundary; the IRawNameResolvable bypass operates on the same input
  string the existing %var% substitution would have seen).

## Outputs

- `v7/plan.md` (this file)
- `v7/summary.md` — findings + verdict
- `v7/coverage.json` — minimal; full C# coverage harness was flaky under v3
  per prior tester reports
- `v7/verdict.json`
- `.bot/runtime2-generator-obp/test-report.json` — branch-shared report
- `.bot/runtime2-generator-obp/tester/summary.md` — bot-root summary append
- `.bot/runtime2-generator-obp/report.json` — session entry
