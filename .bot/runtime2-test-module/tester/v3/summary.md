# v3 Summary — Tester: test-quality review of shipped test module

## What this is

First actual testing pass on the PLang test module. Tester v1 and v2 were plan refinement only. The coder has shipped v1 (11 phases, commits `1178300a..ca844212`), and codeanalyzer v1/v2/v3 have reviewed. Codeanalyzer v3 is CLEAN (pass) and explicitly flagged one test-coverage gap for the tester to confirm and propose a test for.

## What was done

- Built the solution (PLang + PLang.Tests; PlangWindowForms skipped — Windows-only project on Linux host).
- Ran full C# test suite: **2244 tests, 2243 pass, 1 pre-existing `Query_ToolCall_LlmRequestsToolAndHandlesError` LLM flake**. Same baseline as coder's reported status.
- Ran coverage via TUnit's built-in collector, saved to `v3/coverage.cobertura.xml`.
- Read every test file in `PLang.Tests/App/Testing/` and cross-referenced against the changed production files (39 total under `PLang/`).
- Checked `Tests/TestModule/` for PLang-side tests — all 19 present are stubs.
- Read the SplitAtConditions fix (`d05c138d`) and its callers to understand what codeanalyzer v3 meant by "no existing test catches any of the three bugs."

### Verdict: `needs-fixes`

Suite is green, but the green is not solid:

- **4 critical** findings: PLang-side tests are all stubs with the wrong extension, the three-bug cluster fixed by the coder has 0% test coverage, Coverage.RecordBranchLabel/Chain/Merge are 0%, and Executor.Run (the CLI entry) is 0% covered.
- **7 major** findings: three tautology tests in RunActionTests (the author-admitted "we can't easily probe" comments are the giveaway), JUnit XML failure/timeout/skipped cases all at 0%, weak branchIndex conditional assertion, missing AssertionError.Variables integration, missing PLang test for condition.if branchIndex, missing count/order assertions for modifier AfterAction firing.
- **6 minor** findings: weak `.Contains("0")` / `.Contains("1")` style assertions, untested forward-compat paths, a tautology around Include/Exclude.Clear semantics.

See [test-report.json](../../test-report.json) for structured findings; [result.md](result.md) for detailed analysis including the proposed test for the three-bug cluster that codeanalyzer v3 asked for.

## Code example — the false-green pattern

The clearest illustration of what makes a test a false green. From `RunActionTests.Run_SystemDirectory_InheritedFromParentApp`:

```csharp
// Test asks: "does test.run propagate SystemDirectory from parent App to each child App?"
_app.SystemDirectory = "/some/system/dir";
// ... run a fixture ...
await Assert.That(_app.SystemDirectory).IsEqualTo("/some/system/dir");
```

This is `x = y; run(); assert x == y` — always true regardless of what `run()` does. The test comment admits this: *"We can't easily probe inside the child App. Instead verify indirectly: the parent's value is unchanged."* The parent's own value was never in doubt. The child's value — the thing the test name claims to verify — is never observed.

The fix is to introduce a probe: register a BeforeAction binding on the child (from the parent test, before running) that snapshots `ctx.App.SystemDirectory` into a shared reference. Then assert the shared reference equals the parent's value. That actually verifies inheritance.

## What I recommend next

- **Send back to coder** to address the critical/major findings. In priority order:
  1. Propose-and-write the three-bug-cluster test from finding #2 (lock in the d05c138d fix).
  2. Replace the 19 stub `.goal` files under `Tests/TestModule/` with real tests (rename to `.test.goal`, implement bodies, build).
  3. Fix the three tautology tests in `RunActionTests.cs` — add proper child-App probes.
  4. Add JUnit XML failure/timeout/skipped/stale test cases.
  5. Add `AssertionError.Variables` integration assertion in the failure-propagation test.
  6. Add `RecordBranchLabel`/`RecordBranchChain`/Merge-union tests.
  7. Add `Executor.Run` CLI argv parsing tests.
- After coder addresses these, tester re-review.
- Not yet time for the **security** pass — testing isn't ready to sign off.

## Files written this session

- `.bot/runtime2-test-module/tester/v3/plan.md` — plan (written first, read for approval)
- `.bot/runtime2-test-module/tester/v3/coverage.cobertura.xml` — raw coverage (163k lines, gitignored, local-only; re-run via `cd PLang.Tests && dotnet run --no-build --configuration Debug -- --coverage --coverage-output-format cobertura --coverage-output <path>`)
- `.bot/runtime2-test-module/tester/v3/result.md` — detailed findings
- `.bot/runtime2-test-module/tester/v3/verdict.json` — `{ "status": "fail", ... }`
- `.bot/runtime2-test-module/tester/v3/summary.md` — this file
- `.bot/runtime2-test-module/test-report.json` (branch-shared) — 17 structured findings
- `.bot/runtime2-test-module/report.json` — session record updated
