# kind-is-type — tester v2 (re-check after v1's FAIL)

HEAD: 58348d1cc (the coder's F1/F2 fix d03051109; the codeanalyzer's v3 PASS). Forced build. Baseline:
`.bot/kind-is-type/coder/baseline-failures.txt` (as in v1).

**Verdict: PASS.** Both v1 false greens are closed. Each fix goes red under the mutation that exposed it.
No new red names. One small coverage gap noted (N1), not blocking.

## The fixes, mutated (each reverted, git clean after)

| # | mutation | v1 | v2 |
|---|---|---|---|
| M5a | file.read's Type branch at run off (`read.cs:32`) | nothing in C# (F1) | `Read_AsText_LandsTheContentAsText_NotAFileReference` red (now asserts `result.Type.Name` and `Peek()` before materializing); plang `AsTextLandsTextUncaptured` red |
| M8 | `Refine` a no-op (`pick/list/this.cs:646`) | nothing (F2) | `KindFollowUpTests.AKindFollowUp_RefinesTheTypeOptionToTheKindedType` red |
| M9 | `KindsOffered` drops `!k.Internal` (`pick/list/this.cs:640`) | — (new) | **nothing** (N1) |

## Suites

| suite | result | against the baseline |
|---|---|---|
| C# | Modules 1287/0, Runtime 1067/0 (fresh; +1 KindFollowUpTests); Types 852/0, Wire 533/6, Data 842/0, Generator 188/0 (dev.sh carried them from v1's sweep at 2e83d21ff; `git diff 2e83d21ff HEAD -- PLang PlangConsole PLang.Generators os` is empty, so the same code) | the 6 Wire snapshot/resume only |
| plang (`cd test && plang --test`) | 294 / 264 pass / 15 fail / 12 stale / 3 skipped | all 15 in the baseline; none new |

## Notes
- **N1 (minor, not blocking)** The kind follow-up's filter that leaves `item`'s host kinds (dict, list,
  reflection) out of the offer (814) has no test: with it removed, KindFollowUpTests, KindListTests,
  OptionQuestionTests and the decider fixtures stay green. KindListTests pins the `Internal` fact, not the offer.
  It's a coverage gap, not a false green (no test claims it). A second case in KindFollowUpTests would pin it:
  pick `item`, assert the KindOf question offers `json` and not `dict`/`list`/`*`.
- KindFollowUpTests covers the `decimal` refine; `none` and a list's element aren't pinned yet (same test file,
  if wanted).

## OPEN (carried, not findings)
- The plan's stage-3 proof `SetAsImageGifStrictMismatch` (called from `start.goal`) is still red (baseline).
- The codeanalyzer's F3.v3 (`Fluid.cs:165`, `format is { Name: "item" }`) is carried by its v3, not blocking.
- `AsTextLandsTextUncaptured` (v1) isn't in `start.goal`'s index yet.
