# Piece 7: LLM Module

## Decision Log

- **C# defines the contract, PLang implements the behavior.** The `query` action handler is a C# type definition (parameters, types, examples for builder). The actual implementation is a PLang goal.
- **No `ILlmProvider` interface.** The "provider" is a setting pointing to a goal name. Default: `OpenAi`. User sets `llm.provider` to `Anthropic` or whatever, and a different goal handles the HTTP call.
- **Tools are GoalCalls.** GoalCall gets a `Description` property. Tools are passed as `List<GoalCall>` — the provider goal formats them for the API (text-based descriptions, not strict JSON Schema).
- **LlmMessage is a simple type.** Role + Text + Image. Provider-agnostic — the provider goal translates to whatever wire format the backend needs.
- **Tool execution loop is PLang.** When the LLM responds with tool calls, PLang code executes them as GoalCalls and re-queries.

## Architecture

### C# Action: `llm.query`

Type definition only. The `Run()` method reads the provider setting and calls the corresponding PLang goal, passing `this` as data.

```csharp
[Example("system: analyze sentiment\n  user: %comment%\n  scheme: {sentiment: string}\n  write to %result%",
    "Messages=[{Role=system, Text=analyze sentiment}, {Role=user, Text=%comment%}], Schema={sentiment: string}")]
[Action("query")]
public partial class Query : IContext
{
    [IsNotNull]
    public partial List<LlmMessage> Messages { get; init; }

    public partial List<GoalCall>? Tools { get; init; }

    public partial string? Schema { get; init; }

    public partial string? Model { get; init; }

    [Default(0.0)]
    public partial double Temperature { get; init; }

    [Default(4000)]
    public partial int MaxTokens { get; init; }

    [Default(10)]
    public partial int MaxToolCalls { get; init; }

    [Default(true)]
    public partial bool Cache { get; init; }

    public async Task<Data> Run()
    {
        // Read llm.provider setting, default "OpenAi"
        // Call /system/modules/llm/providers/{provider} goal, passing this as data
        // Return result
    }
}
```

### Types

#### LlmMessage

```csharp
public class LlmMessage
{
    [Store, LlmBuilder]
    public string Role { get; set; }      // system, user, assistant

    [Store, LlmBuilder]
    public string? Text { get; set; }     // text content

    [Store, LlmBuilder]
    public string? Image { get; set; }    // image URL or base64
}
```

Minimal. No tool_call metadata, no content arrays, no provider-specific fields. The provider goal translates this to whatever format the API needs.

#### GoalCall (updated)

Add `Description` to existing GoalCall:

```csharp
public sealed class GoalCall
{
    [Store, LlmBuilder]
    public string Name { get; init; } = "";

    [Store, LlmBuilder]
    public string? Description { get; set; }

    [Store, LlmBuilder]
    public List<Data>? Parameters { get; init; }

    [Store]
    public string? PrPath { get; set; }
}
```

Description is used when GoalCall is passed as a tool — tells the LLM what the goal does and what parameters it accepts, in natural language (e.g., `"gets actions belonging to %modules%(list<string>)"`).

### Provider Pattern

The provider is a setting, not a C# interface:

```
llm.provider = "OpenAi"  (default)
```

Maps to goal: `/system/modules/llm/providers/OpenAi.goal`

User changes provider via PLang:
```plang
- set settings 'llm.provider' to 'Anthropic'
```

Now `llm.query` calls `/system/modules/llm/providers/Anthropic.goal` instead.

### File Structure

```
PLang/App/modules/llm/
├── query.cs                            — C# type definition, routes to provider goal
├── LlmMessage.cs                       — message type (Role, Text, Image)

system/modules/llm/
├── providers/
│   ├── OpenAi.goal                     — OpenAI-compatible (default)
│   └── (future: Anthropic.goal, etc.)
├── HandleToolCalls.goal                — tool execution loop
├── FormatToolsOpenAi.goal              — formats GoalCalls for OpenAI function calling
```

### Provider Goal Responsibilities

Each provider goal receives the Query action as data and must:

1. **Format messages** — translate `List<LlmMessage>` to API-specific format
2. **Format tools** — translate `List<GoalCall>` descriptions to API-specific tool definitions (text in system prompt, or native function calling)
3. **Format schema** — set up structured output (response_format for OpenAI, etc.)
4. **HTTP POST** — send request to endpoint with auth headers
5. **Parse response** — extract content from API-specific response format
6. **Handle tool calls** — if response contains tool calls, delegate to HandleToolCalls.goal
7. **Return result** — return content as Data

### Provider Configuration

Provider goals read their config from settings:

| Setting | Default | Description |
|---------|---------|-------------|
| `llm.provider` | `"OpenAi"` | Which provider goal to call |
| `llm.endpoint` | `"https://api.openai.com/v1/chat/completions"` | API endpoint |
| `llm.apiKey` | (from env `OPENAI_API_KEY`) | API authentication |
| `llm.model` | `"gpt-4.1-mini"` | Default model |

### Tool Execution Loop

HandleToolCalls.goal receives:
- The tool calls from the LLM response
- The original messages
- The available tools (GoalCalls)
- MaxToolCalls limit

For each tool call:
1. Find the matching GoalCall by name
2. Execute via `call goal %toolCall.name%` with the arguments
3. Collect result
4. Append assistant message (with tool calls) and tool result messages

Re-query the LLM with updated messages. Repeat until:
- LLM returns a final response (no tool calls), or
- MaxToolCalls reached (return error)

### Caching

The provider goal handles caching:
- Hash the request (messages + model + temperature + schema)
- Check settings/datasource for cached response
- On cache hit, return cached result
- On cache miss, make HTTP call and store result
- `Cache = false` on the action skips caching

### What the Builder Sees

The builder uses `llm.query` like any other action. From BuildGoal.goal:

```plang
- system: %buildGoalPrompt%
  user: %goalForLlm%
  scheme: {steps: [{index: int, ...}]}
  write to %stepResults%
```

The builder maps this to `llm.query` with Messages, Schema. No tools needed for the builder.

### Bootstrapping

The LLM module's PLang goals need pre-built .pr files (same as the builder itself). These live in `system/modules/llm/.build/` and are committed to the repo. When the builder improves, they get rebuilt.

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/App/modules/llm/query.cs` | C# type definition, routes to provider goal |
| `PLang/App/modules/llm/LlmMessage.cs` | Message type |
| `system/modules/llm/providers/OpenAi.goal` | OpenAI-compatible provider |
| `system/modules/llm/HandleToolCalls.goal` | Tool execution loop |

## Files to Modify

| File | Change |
|------|--------|
| `PLang/App/Engine/Goals/Goal/GoalCall.cs` | Add `Description` property |
| `PLang/App/Engine/Modules/this.cs` | Register llm module |

## Definition of Done

- `llm.query` action resolves parameters via source generator
- Provider setting routes to correct PLang goal (default: OpenAi)
- OpenAi provider goal sends messages, handles response, returns content
- Tool execution loop works (call GoalCalls, re-query until done)
- Schema enforcement via response_format
- Caching works (hash-based, opt-out via Cache=false)
- GoalCall.Description available for tool definitions
- Builder's existing `[llm]` calls work through the new module
- Pre-built .pr files committed for bootstrapping
