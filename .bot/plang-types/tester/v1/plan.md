# tester v1 — plang-types test-quality plan

Reviewing coder v1 (commits `205063c5..b28282ee4`) implementing the 7-stage
`type + kind` model. codeanalyzer PASS (2 minor dead-code findings). My job:
confirm the green suite is honest, not false.

## Process notes (flag, don't block)
- **No coder `summary.md`, `plan.md`, or `baseline-tests.md`** under
  `.bot/plang-types/coder/` — only `report.md`. Baseline missing is a process
  violation per my workflow. I cannot mechanically separate regressions from
  pre-existing failures; I rely on codeanalyzer's "3609/3609" + my own clean run.

## Steps
1. Clean rebuild (stale-binary trap) — in progress.
2. Run full C# suite (`dotnet run --project PLang.Tests`). Record totals.
3. Validate builder works (cache=false on a throwaway) before trusting plang --test.
4. Run plang suite (`cd Tests && plang --test`). Record totals.
5. Coverage on changed files.
6. **Test-quality deep dives — the false-green hunt:**
   - **Finding #2 (DoDivide dead branch):** does any test pin that divide
     ignores policy, or do they all cement always-Decimal blindly? Mutation:
     flip the int-track promotion — does a test catch it?
   - **Finding #1 (IndexAssembly collapsed if/else):** is the runtime-registered
     `object`-param renderer path actually exercised by a test, or only the
     concrete-typed shipped serializers?
   - **Runtime DLL (Stage 7 / Cut 4):** is it a real DLL roundtrip or does it
     assert against the test assembly itself? Does it pin runtime-wins precedence
     for BOTH name resolution AND renderer dispatch?
   - **number arithmetic promotion table + overflow/÷0:** assertions check
     Error.Key/StatusCode, not just `!Success`? Mutation on overflow path.
   - **path serializer migration:** byte-for-byte parity test real, or tautology?
   - **kinds.Of swallows hook exceptions silently** (analyzer note) — is the
     silent-no-stamp path tested?
   - **plang .pr files:** read each new `.test.pr`, confirm step `text`
     semantically matches `actions[0].module.action` (builder false greens).
   - **image Resolve/ResolveAsync split** — sync overload returns null for
     path inputs; tested?
7. Write `test-report.json`, `coverage.json`, `verdict.json`, `result.md`.

Strict-red rule: any failing test OR confirmed false-green ⇒ FAIL.
