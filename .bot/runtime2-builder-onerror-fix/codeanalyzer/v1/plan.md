# Code Analyzer v1 Plan — runtime2-builder-onerror-fix

## Scope

Review all code changes on this branch vs `runtime2`. The branch fixes the builder prompt to preserve `onError` and literal values in `.pr` output, renames `RetryOverSeconds` to `RetryOverMs`, and adds multilingual + new onError test suites.

## Analysis passes

1. **OBP compliance** — Check C# changes (ErrorHandler.cs, Methods.cs, GoalMapper.cs, StepRetryTests.cs, GoalDataTests.cs) for OBP violations.
2. **Simplification** — Look for unnecessary complexity in the changes.
3. **Readability** — Naming, consistency, clarity.
4. **Behavioral reasoning** — Trace the rename end-to-end: does `retryOverSeconds` still appear anywhere that would cause a runtime mismatch? Check .pr files for stale schema.
5. **Deletion test** — Do the new PLang tests actually verify the behavior they claim to test? Would they pass even if the feature was broken?

## Key files to review

- `system/builder/BuildGoal.goal` — builder prompt change
- `system/builder/llm/BuildGoal.llm` — LLM prompt with onError rules
- `system/builder/templates/goalFormatForLlm.template` — schema template
- `PLang/App/Engine/Goals/Goal/Steps/Step/ErrorHandler.cs` — rename
- `PLang/App/Engine/Goals/Goal/Steps/Step/Methods.cs` — rename
- `PLang/App/Engine/Utility/GoalMapper.cs` — rename mapping
- `PLang.Tests/App/Core/StepRetryTests.cs` — C# test update
- `PLang.Tests/App/Utility/GoalDataTests.cs` — C# test update
- `system/builder/.build/BuildGoal/07.*.pr` — stale .pr check
- All new test `.goal` and `.build/*.pr` files
