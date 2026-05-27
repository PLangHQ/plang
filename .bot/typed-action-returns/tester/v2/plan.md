# Tester v2 — typed-action-returns

## Context
Coder commit `6965c89e4` claims to address tester v1 findings:
- **#1 (critical false-green)** FileRead BuildWarning test now drains a memory channel and asserts the missing path appears in the message.
- **#6 (critical false-green)** New `RuntimeDoubleWrapTests.cs` invokes 4 representative `Data<object>` handlers and asserts `result.Value is Data` is false; a 5th test pins the full Data<object> handler set as a tripwire.
- **#2 (minor)** 4 malformed-input tests in `JsonStreamSerializerTests` + 2 IOException tests in `TextStreamSerializerTests`.
- **#3 (minor)** `BodyDispatch_BrokenJsonContentType_FallsBackToString` in Stage3 http tests.

## Approach
1. Re-run full suites after clean rebuild (stale-binary trap).
2. **Mutation test** the two critical fixes — the whole point of tester v1's FAIL was that the original tests passed regardless of behavior. The new tests must FAIL when the production code is deliberately broken.
3. Audit the minor-coverage tests: do they assert behavior, or just `Success == false`?
4. Re-check that no NEW regressions landed.

## Mutation plan
- **FileRead fix**: comment out the `BuildWarning` write inside `file/read.cs` Build(); re-run the test; expect failure with "missing path not in channel text". Revert.
- **RuntimeDoubleWrap**: inside `list/first.cs` (or similar `Data<object>` handler), force the Data<object> implicit-operator double-wrap path — return a Data wrapped in `Data<object>.Ok(innerData)`. Confirm test catches it. Revert.

## Open questions
None blocking. Will commit/push at end as usual under `.bot/` only.
