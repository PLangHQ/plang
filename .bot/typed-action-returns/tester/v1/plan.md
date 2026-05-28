# Tester v1 — typed-action-returns

## Scope

Validate the test quality of the v1 work shipped on `typed-action-returns`: Stages 0-4 typed Run() returns, Serializers Data refactor, http Content-Type body dispatch, Ask shape change.

## State going in

- C# suite: **3124/3124 PASS** (no regressions)
- PLang suite: **221/221 PASS** (0 fail, 0 stale)
- codeanalyzer v3 verdict: PASS (all v2 findings closed)
- Coder did NOT write `baseline-tests.md` → process violation, noted as finding (info severity).

## What to investigate (in priority)

1. **Stage 0-4 PLang TypedReturns goals (13 tests)** — read each `.test.goal` and the built `.pr.json`. Verify step text matches `actions[0].module.action`. These are the highest-risk false-green pool: they were authored as stubs by test-designer, implemented later by coder once the API was in place. Builder-shift drift very likely.

2. **Stage 2 typed `Run()` signatures** — open each handler and verify Tests/PLang.Tests/App/TypedReturnsTests/Stage2_MechanicalTypings_*Tests.cs covers more than signature shape. The "if the implementation were subtly wrong" question: does the test catch a `Data<object>` regression? Does it catch double-wrap (`Data<object>{ Value = Data<X>{...} }`)?

3. **Ask shape change** — `output.ask` now returns `Data<Ask>`. `Ask.ToString() => Answer`. The handoff lists call sites (`path/this.Authorize.cs`, `path/file/this.Operations.cs`) using `.Answer`. Do any tests verify ToString() rendering vs. `.Answer` access? Coarse-mutation trap: a single `Ask` record change can break many things.

4. **Serializers Data refactor** — every `ISerializer` method now returns `Data`. Are call sites tested for the Data.Fail path? It would be very easy to deliver "happy-path passes / error swallowed" green tests here.

5. **http Content-Type dispatch** — `ParseResponseAsync` now goes through `Serializers.GetByContentType` + `TextFallback`. Tests should cover: known CT json/csv/xml, unknown CT, broken JSON in CT=application/json (fallback to text), binary (byte[]).

6. **Build() seam** — `SetAction` is generator-emitted on every handler. Is there a test that catches a handler whose Build() throws/returns Fail? Build pass aborts on Fail; is that exercised?

7. **`tester.File` → `tester.Test` rename** — Stage1_TesterFileRenameTests covers it. Verify the test isn't just structural ("type exists") but actually exercises the rename surface.

8. **(type) hint LLM rule** — `Compile.llm` kernel rule for `write to %x%(json)`. Do tests cover precedence of user (type) hint over Build() inference? (Stage 4 says yes, verify depth.)

## Process notes

- No baseline-tests.md from coder → cannot diff against pre-coder baseline. Treat current C# 3124/3124 + PLang 221/221 as the working baseline.
- 12 "stale placeholders" reported in coder handoff have all been implemented (commit `2553dd7f2`). Confirmed: 13 TypedReturns goals run and pass.

## Workflow

- Run full suites (done).
- Audit each TypedReturns .pr.json for module/action vs text mismatch.
- Spot-check 5-6 critical C# stage tests for false-green / weak-assertion patterns.
- Spot-check Serializers and http call-site error paths.
- Write `test-report.json`, `verdict.json`, `summary.md`.
