# Tester v2 Summary — LLM Module

## What this is

Test quality analysis of the LLM module (coder v2): `query.cs`, `LlmMessage.cs`, `ToolCall.cs`, `ILlmProvider.cs`, `OpenAiProvider.cs`. Validates that the 63 C# tests are honest — not false greens.

## Test Results

- **1958 passed, 0 failed, 4 skipped** (skipped are sub-engine tests, unrelated)
- No PLang integration tests (stubs only)

## Coverage

| File | Line | Branch |
|------|------|--------|
| OpenAiProvider.cs | 82.8% | 61.5% |
| query.cs | 100% | 100% |
| LlmMessage.cs | 100% | 100% |
| ToolCall.cs | 100% | 100% |

Key gaps in OpenAiProvider: `RestoreFromCache` (0%), `ResolveImage` file/base64 paths (0%), `ParseToolArguments` non-string branches (0%), `ResolveConfigAsync` env fallback (0%).

## Findings (4 major, 4 minor)

### Major

1. **False-green: ProviderNotRegistered** — Test name says "not registered" but code verifies it IS registered. Never tests the error path.
2. **Weak: MaxToolCalls loop** — Only asserts `result.IsNotNull()`. MaxToolCalls=5 could be completely ignored.
3. **Weak: API error tests** — Only check `Success==false`. No `Error.Key` or message verification. Wrong error type passes silently.
4. **False-green: OnToolCall callback** — No proof the callback fires. Only checks final result value.

### Minor

5. ParseToolArguments: True/False/Null branches untested (only strings flow through)
6. ResolveImage: file path + base64 fallback at 0% coverage
7. RestoreFromCache: both deserialization paths at 0% (cache tests may not reach this code)
8. Parallel tool execution: no verification of actual concurrency vs sequential

## Verdict

**FAIL** — 4 major findings need fixes before approval. Recommend sending back to coder.

## Code Example — Finding #2 (Weak MaxToolCalls assertion)

Current (passes even if MaxToolCalls is broken):
```csharp
var result = await action.Run();
await Assert.That(result).IsNotNull(); // That's it
```

Should be:
```csharp
var result = await action.Run();
await Assert.That(result).IsNotNull();
await Assert.That(callIndex).IsLessThanOrEqualTo(3); // 2 tool rounds + 1 content
await Assert.That(result.Success).IsTrue();
```
