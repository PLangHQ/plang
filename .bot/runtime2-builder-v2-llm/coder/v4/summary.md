# Coder v4 Summary — Fix Auditor Findings

## What this is

Fixes 2 major bugs and 4 minor issues found by the auditor's cross-cutting integrity audit.

## What was done

### Major Bug Fixes
1. **MaxToolCalls batch-overshoot** — Tool calls are now sliced to remaining budget before execution. `toolCalls.Take(MaxToolCalls - toolCallCount)` prevents executing more tools than the limit allows.
2. **Empty loop exit** — When MaxToolCalls is exhausted, the result now carries `lastContent`, metadata (Model, ToolCallCount, tokens), and `Truncated=true` property so callers can detect truncation.

### Minor Fixes
3. **Numeric boxing** — `RestoreFromCache` now uses `TryGetInt64` consistently with `ParseToolArguments`.
4. **MaxToolCalls tests** — Both tests now assert exact `CallCount` with round-by-round documentation and verify `Truncated` property.
5. **Redundant ternary** — `action.OnStream != null ? action.OnStream : null` → `action.OnStream`.
6. **ParseToolArguments error surfacing** — `JsonException` now adds a `__parse_error__` Data entry with the error message, which flows as tool result text back to the LLM.

### Files Modified
- `PLang/App/modules/llm/providers/OpenAiProvider.cs` — all 6 code fixes
- `PLang.Tests/App/Modules/llm/QueryToolTests.cs` — finding 4
- `PLang.Tests/App/Modules/llm/QueryEdgeCaseTests.cs` — finding 4

## Code Example

Before (all tools execute past limit):
```csharp
var toolCalls = ParseToolCalls(toolCallsProp);
// ... execute ALL toolCalls ...
for (int i = 0; i < toolCalls.Count; i++)
{
    if (toolCallCount >= action.MaxToolCalls) break; // too late — tools already ran
```

After (slice before execution):
```csharp
var toolCalls = ParseToolCalls(toolCallsProp);
int remaining = action.MaxToolCalls - toolCallCount;
if (toolCalls.Count > remaining)
    toolCalls = toolCalls.Take(remaining).ToList();
// ... execute only budgeted toolCalls ...
```

## Test Results
- 1962/1962 total tests passing (0 failures, 4 skipped)
