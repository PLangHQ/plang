# Tester v7 — plan

Reviewing coder v6 + v7 (and the typed-returns sweep that landed between v5 and v6).

## Scope

- **Coder v6** — `GoalCall` slash resolution; `builder.actions` Actions filter; inverted `File.Exists` in `builder/this.cs`; builder Validate added two structural rules (Class 2: `write to %x%` → trailing `variable.set`; Class 3: `variable.set Type=json` with `%var%` value rejected).
- **Coder v7** — pure docstring edits to `Data<T>.From` and orphan summary on `DescribeReturnTypeName`.
- **Intervening typed-returns sweep** — ~25 commits flipping 69 action handlers `Task<Data>` → `Task<Data<T>>` and typing provider interfaces (`IPath`, `IIdentity`, `IStore`, `ISigning`, `ICrypto`, `ITemplate`, `IEvaluator`, `IAssert`, `ILlm`). codeanalyzer v4 already gave this CLEAN-modulo-docs.
- **Carry-forward** — tester v5 left 6 findings. Pull shows new `PathEqualityTests.cs` and `+69 lines` on `AssertTests.cs`. Verify those address F2/F3; check F1, F4, F5, F6.

## Process flags (raise in result)

- No `baseline-tests.md` in coder/v6/ or coder/v7/. v5 had the same pattern (raised as a note then). Worth flagging again.

## Test passes

1. Clean rebuild (stale-binary trap) — required.
2. C# `dotnet run --project PLang.Tests` — expect ≈2889/2889 per codeanalyzer v4.
3. plang `cd Tests && plang --test` — expect ≈203/203 (LLM 503 was external).
4. Diff against the v5 result: no regressions where we were 2882/203.

## Quality lens (the actual job)

- **Verify F2 (path.Equals/GetHashCode)** — `PathEqualityTests.cs` exists. Read it; mutation-test by reverting `RootComparison` → `OrdinalIgnoreCase` and confirm a test goes red on Linux.
- **Verify F3 (assert path truthiness)** — `AssertTests.cs` +69 lines including `IsTrue_PathToMissingFile_Fails` etc. Read; mutation-test by deleting the `IBooleanResolvable` branch and confirm a test goes red.
- **Verify F1 (vacuous `Assert.That(true).IsTrue()`)** — `HandlerShapeTests` no longer contains that name. Confirm by grep across suite for `Assert.That(true).IsTrue()`.
- **Verify F5 (`File.test.goal` weak `is not null`)** — now `assert %info% is true`. Confirm.
- **Verify F6 (`PLangFileSystem_Absent...` stale comment)** — confirm it is gone or fixed.
- **Verify F4 (negative-branch plang test for `if X exists`)** — `ConditionFileExistsSubSteps.test.goal` was the gap; still only tests the positive branch. **Likely unaddressed — confirm and re-raise as carry-forward.**

## New surfaces to test-audit

- **Builder validators (`Validate`)** — coder v6 added two structural rules. C# test coverage for them?
- **`GoalCall.GetGoalAsync` slash resolution** — has unit test coverage? Or only the "self-rebuild succeeds" smoke?
- **Typed-returns sweep** — providers now return `Data<T>`. Spot-check: do consumer tests still assert `Error.Key` (not just `Success==false`)?

## Output

- `result.md` — findings with severity, code/test paths, mutation evidence
- `verdict.json` — pass/fail
- `summary.md` (overwrite at `.bot/<branch>/tester/summary.md`)
- `.bot/<branch>/test-report.json`
- Commit + push.
