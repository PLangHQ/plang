# Auditor v1 Summary — Final Audit

## What this is
Final code integrity review of the `runtime2-builder-onerror-fix` branch before merge. This branch fixes two builder reliability issues: (1) the builder LLM silently dropping `onError` from .pr files, and (2) the LLM rewriting literal assertion values. It also renames `RetryOverSeconds` to `RetryOverMs` to fix a truncation bug in GoalMapper where millisecond values were divided by 1000.

## What was done

### Review scope
- **C# changes**: ErrorHandler.cs (property rename), Methods.cs (delay calculation update), GoalMapper.cs (remove /1000 division)
- **Builder prompt**: BuildGoal.llm (2 CRITICAL rules added, retryOverMs in examples/schema), BuildGoal.goal (schema field rename), goalFormatForLlm.template (RetryOverMs)
- **C# tests**: StepRetryTests.cs, GoalDataTests.cs (field rename)
- **PLang tests**: 4 new suites (ErrorRetryOnly, ErrorGoalFirst, ErrorMixed, OnErrorMultilingual)
- **Docs**: pr-file-format.md, todos.md updated
- **.pr files**: All rebuilt by plang builder in v0.2 format

### Findings
4 findings, all minor or nit:
1. **Minor**: Legacy v0.1 per-step .pr folders still have `RetryOverSeconds` in schema metadata — cosmetic, not loaded
2. **Nit**: ErrorGoalFirst test has dead counter variable after retry assertion removal
3. **Nit**: CacheDynamicKey .pr rebuild changed some parameter types — cosmetic, test passes
4. **Minor**: Bare catch in RetryAsync — by design for retry loop, but could be narrowed

### Verdict: PASS
No critical or major findings. All pipeline stages passed. Branch is ready for merge.

## Code example
The core fix — GoalMapper no longer truncates milliseconds:

```csharp
// Before (truncation bug — 500ms became 0 seconds)
RetryOverSeconds = oldHandler.RetryHandler?.RetryDelayInMilliseconds != null
    ? (int)(oldHandler.RetryHandler.RetryDelayInMilliseconds.Value / 1000)
    : null,

// After (direct passthrough)
RetryOverMs = oldHandler.RetryHandler?.RetryDelayInMilliseconds != null
    ? (int)oldHandler.RetryHandler.RetryDelayInMilliseconds.Value
    : null,
```
