# tester v1 — fix-stepvartypes-incremental

## Scope

Branch carries 8 C# files specific to this branch (rest merged in from `purge-systemio-from-actions`, tested elsewhere):

- `PLang/app/modules/builder/BuildResponse.cs` — XML comment only (no test surface)
- `PLang/app/modules/llm/code/OpenAi.cs` — new `Pricing` table + `PriceFor()` + `CachedTokens` accumulation + per-call cost math
- `PLang/app/modules/test/report.cs` — System.IO purge (merge content, not branch-specific)
- `PLang/app/modules/test/run.cs` — adds **output capture via BeforeWrite event binding** + **per-step timings via Stopwatch**
- `PLang/app/modules/this.cs` — `Describe()` async + `ResolveMarkdownTeachingRoot` returns `path.@this` + `%var%` slot description (dropped trailing `string`)
- `PLang/app/tester/Run.cs` — `CapturedOutput` → `Output` rename + `Timings` field
- `PLang/app/tester/Timing.cs` — NEW record
- `PLang/app/tester/Timings.cs` — NEW collection

## Process violations to flag

- **No coder folder** at `.bot/fix-stepvartypes-incremental/coder/` → no `baseline-tests.md`. I can't separate regressions from pre-existing PLang failures.
- Codeanalyzer v1/v2 PASSed but did not re-run tests; the rename `CapturedOutput`→`Output` was reviewed (line-level) without checking who else depends on the old name.

## Plan

1. Run C# build → **already done, 2 errors**.
2. Run plang --test → **already done, 195 pass / 22 fail; no baseline to diff against**.
3. Quality pass over the new code areas:
   - **OpenAi cost math**: only `Cost == null` is tested; no positive arithmetic check, no `CachedTokens`, no unknown-model debug, no longest-prefix-wins.
   - **Output capture (BeforeWrite event)**: no test exercises this; the property rename broke two existing setter-only fakes.
   - **Per-step Timings**: no test populates or asserts Timings; the entry-goal-path filter logic (TrimStart('/')) is untested.
   - **%var% slot description**: existing `GetActions_VariableNameParams_Marked` is coarse — `.Contains("%var%")` matches both old and new strings, comment lies.
   - **modules/this.cs Describe() now async**: covered shape-wise by DescribeTests, but the markdown-teaching async load path is untested for the specific scan-then-merge sequence.
4. Write `test-report.json`, `verdict.json`, `summary.md`.
5. Verdict: **FAIL** — the build itself is red, blocking the whole C# suite from running.
