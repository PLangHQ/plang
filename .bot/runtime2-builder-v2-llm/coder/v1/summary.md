# LLM Module Implementation — Coder v1 Summary

## What this is

The LLM module for PLang App — enables PLang developers to query LLMs (OpenAI-compatible APIs) with support for tools, streaming, validation, conversation continuity, caching, structured output (JSON/code blocks), and multimodal images.

## What was done

### Files Created
- `PLang/App/modules/llm/query.cs` — Action record with all parameters (Messages, Tools, Schema, Format, Model, etc.), delegates to ILlmProvider via `[Provider]`
- `PLang/App/modules/llm/LlmMessage.cs` — Message type (Role, Text, Images, internal ToolCallId/ToolCalls)
- `PLang/App/modules/llm/ToolCall.cs` — Tool call carrier (Id, Name, Arguments)
- `PLang/App/modules/llm/providers/ILlmProvider.cs` — Provider interface extending IProvider
- `PLang/App/modules/llm/providers/OpenAiProvider.cs` — Full OpenAI-compatible implementation (~550 lines)
- `PLang.Tests/App/Modules/llm/LlmTestHelper.cs` — Shared test infrastructure (MockHttpMessageHandler, response builders)

### Files Modified
- `PLang/App/Engine/Goals/Goal/GoalCall.cs` — Added `Description` (string?) and `Parallel` (bool) properties
- `PLang/App/Engine/Providers/this.cs` — Added `"llm"` to ResolveType(), registered OpenAiProvider in RegisterDefaults()
- All 9 test files — Implemented all 61 test stubs (removed 1 API key test per Ingi's direction)

### Key Design Decisions
1. **HTTP via engine.RunAction** — LLM provider creates `http.request` actions and runs them through the full engine pipeline (signing, etc.)
2. **Cache via SettingsStore (SQLite)** — Table "LlmCache", key=SHA256 hash. Stored as dictionary since Data.Properties is [JsonIgnore]. Restored with metadata on cache hit.
3. **Streaming + tool calls** — OnStream skipped for tool-call rounds per Ingi's direction
4. **Tool execution** — LLM arguments parsed into GoalCall.Parameters (List<Data>). Tool errors sent back to LLM as text results.
5. **Conversation continuity** — Pre-mutation originals stored in PLangContext via Set<T>/Get<T>. Format instructions re-applied fresh on continuation.

## Code example

The action record pattern (query.cs):
```csharp
[Action("query")]
public partial class query : IContext
{
    [IsNotNull]
    public partial List<LlmMessage> Messages { get; init; }
    public partial List<GoalCall>? Tools { get; init; }
    [Provider]
    public partial ILlmProvider Llm { get; }
    public async Task<Data> Run() => await Llm.Query(this);
}
```

Tool execution in OpenAiProvider:
```csharp
var execCall = new GoalCall
{
    Name = goalCall.Name,
    PrPath = goalCall.PrPath,
    Parameters = ParseToolArguments(toolCall.Arguments, goalCall.Parameters)
};
var goalResult = await engine.RunGoalAsync(execCall, context);
```

## Test Results
- 61/61 C# tests passing
- 1951/1951 total project tests passing (0 failures)
- PLang integration tests (8 .goal files) not yet implemented — stubs remain

## What's next
- PLang integration tests need real OpenAI API calls
- Streaming implementation is basic (v1 passes through HTTP module's SSE directly)
- Cost calculation not implemented (returns null — needs pricing data per model)
