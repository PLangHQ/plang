# v1 review summary — what I flagged, what changed

## v1 verdict: FAIL — 9 findings

| ID | Severity | Type | What I flagged | Coder addressed? |
|----|----------|------|----------------|------------------|
| F1 | critical | build-break | `Run.CapturedOutput → Output` rename not propagated to `ReportActionTests.cs:56`, `EdgeCaseTests.cs:129` | Yes — commit 81c9dabfa |
| F2 | critical | missing-coverage | BeforeWrite output-capture path in `test/run.cs:130-148` had zero tests | Yes — `Run_OutputCapture_OutputChannelOnly_ErrorChannelExcluded` |
| F3 | critical | missing-coverage | Per-step Stopwatch Timings + `IsEntryGoalStep` filter had zero tests | Yes — `Run_Timings_OnlyEntryGoalTopLevelSteps_NestedRollUp` |
| F4 | major | missing-coverage | Only `Cost == null` was tested; no positive cost math, no longest-prefix, no multi-call accumulation | Yes — three new tests in `QueryBasicTests` |
| F5 | major | missing-coverage | `CachedTokens` reaches Properties on three exit paths, only `Cost == null` was tested | Yes — assertions on success-exit and tool-call-exit paths |
| F6 | major | weak-assertion | `GetActions_VariableNameParams_Marked` used `.Contains("%var%")` (passes both old and new strings); comment lied | Yes — `.IsEqualTo("%var%")` + updated comment |
| F7 | major | missing-coverage | `ResolveImage` happy path not tested (only denial covered) | Already covered by pre-existing `QueryImageTests` — coder noted in commit message; verified |
| F8 | minor | missing-coverage | `unknownModelLogged` once-per-Run debug guard not tested | Intentionally skipped per coder triage |
| F9 | minor | missing-coverage | `childApp.Parent = parentApp` has no consumer to assert against | Intentionally skipped per coder triage |

## Process gap

- No `coder/` folder still exists on this branch — `baseline-tests.md` is absent. Cannot triage PLang failures as regressions vs pre-existing. Going to flag again, but not gate v2 on it (coder fixed the actual code issues).

## Test mutations to satisfy myself

- F2 `Run_OutputCapture_*`: asserts both `Contains("hello-output")` AND `DoesNotContain("hello-error")` — bidirectional. Inverting the channel-name filter in production would flip both assertions.
- F3 `Run_Timings_*`: asserts `Count == 3` (not 5). Deleting the `IsEntryGoalStep` filter would push count to 5 (entry + helper); inverting it would push count to 2.
- F4 `Query_Cost_PositiveArithmetic_*`: exact decimal equality on (60·input + 40·cached + 50·output)/1M. Any rate-swap or cached/non-cached arithmetic flip changes the expected value.
- F4 `Query_Cost_LongestPrefixWins_*`: 1M·1M of each rate kind on `gpt-5.4-mini-2026-03-17`. If the longest-prefix logic regressed to first-match-wins, it would pick `gpt-5.4` base (2.50+15.00=17.50) instead of the asserted 5.25.
- F6 `GetActions_VariableNameParams_Marked`: exact equality — a revert to `"%var% string"` would fail.

I did not run a literal source mutation; the test shapes are explicit enough that a static read confirms they catch the relevant regressions.
