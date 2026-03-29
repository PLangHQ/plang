# Coder v3 Summary — Fix Tester Findings

## What this is

Addresses all 8 tester findings (4 major, 4 minor) — all are test quality improvements: stronger assertions, honest test names, and new coverage for untested branches.

## What was done

### Major Fixes
1. **ProviderNotRegistered → ProviderRegistered_ByDefault** — Renamed to match actual behavior. Added `IsTypeOf<OpenAiProvider>()` assertion.
2. **MaxToolCalls loop** — Added `_handler.CallCount == 3` and `callIndex == 3` assertions. Documented the exact round-by-round behavior.
3. **API error tests** — Added `result.Error.Key == "HttpError"` and `result.Error.Message.Contains("400"/"500")`.
4. **OnToolCall callback** — Renamed to `ToolLoopCompletesWithCallback`. Added `CallCount == 2` and tool result verification. Documented why callback invocation can't be verified in unit tests.

### Minor Fixes
5. **ParseToolArguments mixed types** — New test with `true`, `false`, `42`, `null`, `{"nested":"val"}` to exercise all switch branches.
6. **ResolveImage file/base64 paths** — Strengthened existing test with `expectedBase64` assertion. Added `JpgExtension_CorrectMimeType` and `ImageBase64_PassedDirectly` tests.
7. **RestoreFromCache** — Added `result2.Value.ToString() == "preserved"` and token metadata assertions to verify deserialization.
8. **Parallel tool results** — New test verifying both `call_1` and `call_2` appear in re-query request body.

### Files Modified
- `QueryEdgeCaseTests.cs` — findings 1, 2
- `QueryBasicTests.cs` — finding 3
- `QueryCallbackTests.cs` — finding 4
- `QueryToolTests.cs` — findings 5, 8
- `QueryImageTests.cs` — finding 6
- `QueryCacheTests.cs` — finding 7

## Code Example

Before (weak assertion):
```csharp
var result = await action.Run();
await Assert.That(result).IsNotNull(); // MaxToolCalls could be ignored
```

After (bounded assertion):
```csharp
var result = await action.Run();
await Assert.That(result).IsNotNull();
await Assert.That(_handler.CallCount).IsEqualTo(3);
await Assert.That(callIndex).IsEqualTo(3);
```

## Test Results
- 1962/1962 total tests passing (0 failures, 4 skipped)
- 4 new tests added (mixed types, parallel results, jpg mime, base64 passthrough)
