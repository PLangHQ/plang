# Piece 7: LLM Module (v2 — full C#)

> **Note for coder**: The pseudocode in this plan illustrates intent and flow, not exact API usage. Names like `PersistentCache`, `engine.RunAction(...)`, etc. are conceptual — they may not map 1:1 to actual Runtime2 classes or methods. You must understand the Runtime2 architecture (providers, Data, engine navigation, context, memory stack, etc.) before implementing. Read the OBP doc and existing modules (file, http) as reference. If unsure how something should be wired, ask Ingi.

## Decision Log

- **Full C# implementation.** Too early for PLang-implemented modules — the tool loop, message formatting, and provider wire formats need typed C# code. The PLang-goal approach (v1) can come later when PLang is more capable.
- **`ILlmProvider` interface.** Follows the existing provider pattern (like `IFileProvider`). Registered in Provider registry, switchable via `provider.set` action.
- **Tools are GoalCalls.** GoalCall gets `Description` and `Parallel` properties. The C# provider translates GoalCall descriptions into API-specific tool definitions.
- **Parallel tool execution.** GoalCall.Parallel (default false) controls whether tools can run concurrently. When the LLM requests multiple tools in one response and all matching GoalCalls have Parallel=true, the provider runs them with Task.WhenAll. Otherwise sequential.
- **OnToolCall callback.** Query has an OnToolCall GoalCall that the provider invokes around each tool execution, so the UI can show progress (which tool is running, results).
- **OnValidateResponse callback.** Query has an OnValidateResponse GoalCall that the provider invokes after getting the final response (after all tool calls are done). If validation fails, the error is fed back to the LLM for retry, up to MaxValidationRetries (default 3).
- **OnStream callback.** Query has an OnStream GoalCall for streaming responses. Provider sets `stream: true` on the API request and calls OnStream with each chunk. Uses the http module's streaming support.
- **ContinuePreviousConversation.** Follows Runtime1 pattern — stores full message list (including assistant response) in context after each call. When true, prepends stored history to the current messages. Also reuses previous schema if not specified.
- **LlmMessage carries tool metadata.** Role + Text + Image + ToolCallId + ToolCalls. Needed for multi-turn tool conversations — the provider builds these internally during the tool loop.
- **Tool execution loop is C#.** The provider calls engine.RunGoal for each tool call, appends results, re-queries. Clean typed code, no PLang string manipulation.
- **Tool errors go back to the LLM.** When engine.RunGoal fails for a tool call, the error message is sent back as the tool result. The LLM decides how to proceed.
- **Schema via system prompt, not response_format.** Follows Runtime1 approach — schema is appended to the system message as a text instruction (e.g., "You MUST respond in JSON, schema: {…}"). This is provider-agnostic and works with any LLM API.
- **Format is user-defined.** Any string — json, md, html, python, csharp, yaml, etc. Only `json` is special (schema handling + JSON parse validation). All other formats instruct the LLM to wrap response in a code block, and the provider extracts from the code block.
- **Response properties.** The returned Data carries properties (via `%!` syntax) with full LLM metadata: raw response, model, token usage, cost, messages, etc. Accessible as `%result!RawResponse%`, `%result!TotalTokens%`, `%result!Cost%`, etc.
- **Cache is persistent.** Not in-memory — stored to disk/database. Implementation detail left to the coder.
- **Cache defaults to false for tool calls.** Cache=true is only safe for deterministic queries (no tools). When Tools is non-null, caching is skipped regardless of the Cache flag.
- **MaxToolCalls is total individual calls**, not rounds. Matches Runtime1 behavior — prevents runaway regardless of how many calls per round.
- **HTTP via the http module.** The provider uses the runtime's http module for API calls, not raw HttpClient.

## Architecture

### C# Action: `llm.query`

```csharp
[Example("system: analyze sentiment\n  user: %comment%\n  schema: {sentiment: string}\n  write to %result%",
    "Messages=[{Role=system, Text=analyze sentiment}, {Role=user, Text=%comment%}], Schema={sentiment: string}")]
[Action("query")]
public partial class Query : IContext
{
    [IsNotNull]
    public partial List<LlmMessage> Messages { get; init; }

    public partial List<GoalCall>? Tools { get; init; }

    public partial GoalCall? OnToolCall { get; init; }

    public partial GoalCall? OnValidateResponse { get; init; }

    public partial GoalCall? OnStream { get; init; }

    public partial string? Schema { get; init; }

    public partial string? Format { get; init; }

    public partial string? Model { get; init; }

    [Default(false)]
    public partial bool ContinuePreviousConversation { get; init; }

    [Default(0.0)]
    public partial double Temperature { get; init; }

    [Default(4000)]
    public partial int MaxTokens { get; init; }

    [Default(10)]
    public partial int MaxToolCalls { get; init; }

    [Default(3)]
    public partial int MaxValidationRetries { get; init; }

    [Default(true)]
    public partial bool Cache { get; init; }

    [Provider]
    public partial ILlmProvider Llm { get; }

    public async Task<Data> Run()
    {
        return await Llm.Query(this);
    }
}
```

`Run()` delegates to the provider — same pattern as `file.read` delegates to `IFileProvider`. The source generator wires `[Provider]` to the provider registry.

### Types

#### LlmMessage

```csharp
public class LlmMessage
{
    [Store, LlmBuilder]
    public string Role { get; set; }          // system, user, assistant, tool

    [Store, LlmBuilder]
    public string? Text { get; set; }

    [Store, LlmBuilder]
    public string? Image { get; set; }

    // --- Tool conversation fields (not set by builder, used internally) ---

    public string? ToolCallId { get; set; }   // For role=tool: which call this responds to

    public List<ToolCall>? ToolCalls { get; set; }  // For role=assistant: tools the LLM wants to call
}
```

`ToolCallId` and `ToolCalls` are internal — the builder never sets them. They exist so the provider can build multi-turn tool conversations without provider-specific message types leaking out.

#### ToolCall

```csharp
public class ToolCall
{
    public string Id { get; set; } = "";       // Provider-assigned call ID
    public string Name { get; set; } = "";     // Goal name to call
    public string Arguments { get; set; } = ""; // JSON string of arguments
}
```

Minimal carrier for what the LLM responded with. The provider parses its API response into these; the tool loop uses them to find matching GoalCalls and execute.

#### GoalCall (updated)

Add `Description` and `Parallel` to existing GoalCall:

```csharp
public sealed class GoalCall
{
    [Store, LlmBuilder]
    public string Name { get; init; } = "";

    [Store, LlmBuilder]
    public string? Description { get; init; }

    [Store, LlmBuilder]
    public bool Parallel { get; init; }

    [Store, LlmBuilder]
    public List<Data>? Parameters { get; init; }

    [Store]
    public string? PrPath { get; set; }
}
```

- `Description` tells the LLM what the goal does and what parameters it accepts, in natural language (e.g., `"gets actions belonging to %modules%(list<string>)"`).
- `Parallel` (default false) marks this tool as safe for concurrent execution. When the LLM requests multiple tools in one response and all matching GoalCalls have `Parallel = true`, the provider runs them with `Task.WhenAll`. If any requested tool has `Parallel = false`, all tools in that batch run sequentially.

### Provider Interface

```csharp
public interface ILlmProvider : IProvider
{
    Task<Data> Query(Query action);
}
```

One method. The provider owns the full lifecycle: format messages, call API, handle tool loop, return result.

### OpenAI Provider

```csharp
public class OpenAiProvider : ILlmProvider
{
    public string Name => "OpenAi";
    public bool IsDefault { get; set; }

    public async Task<Data> Query(Query action)
    {
        // See pseudocode below
    }
}
```

### Provider Configuration

Settings read by the OpenAI provider:

| Setting | Default | Description |
|---------|---------|-------------|
| `llm.endpoint` | `"https://api.openai.com/v1/chat/completions"` | API endpoint |
| `llm.apiKey` | env `OPENAI_API_KEY` | API authentication |
| `llm.model` | `"gpt-4.1-mini"` | Default model (overridden by Query.Model) |

Provider switching uses the standard `provider.set` action — same as all other modules.

## Pseudocode

> **Coder note**: This pseudocode shows the logical flow. Names like `PersistentCache`, `engine.RunAction(...)`, `context.Set(...)` are conceptual placeholders. Map them to the actual Runtime2 APIs. If you're unsure how something maps, ask Ingi.

```
OpenAiProvider.Query(action):
    engine = action.Context.Engine
    settings = engine.Property
    context = action.Context

    // --- Config ---
    endpoint = settings.Get("llm.endpoint") ?? "https://api.openai.com/v1/chat/completions"
    apiKey   = settings.Get("llm.apiKey") ?? env("OPENAI_API_KEY")
    model    = action.Model ?? settings.Get("llm.model") ?? "gpt-4.1-mini"

    // --- Build messages ---
    messages = clone(action.Messages)

    // Continue previous conversation — prepend stored history
    if action.ContinuePreviousConversation:
        prevMessages = context.Get<List<LlmMessage>>("__llm_conversation__")
        if prevMessages != null:
            messages.InsertRange(0, prevMessages)
        // Reuse previous schema if not specified
        if action.Schema == null:
            schema = context.Get<string>("__llm_schema__")
        else:
            schema = action.Schema
    else:
        schema = action.Schema
        // Clear stored conversation when not continuing
        context.Remove("__llm_conversation__")
        context.Remove("__llm_schema__")

    // Append format/schema instruction to system message
    formatInstruction = BuildFormatInstruction(action.Format, schema)
    if formatInstruction != null:
        systemMsg = messages.Find(m => m.Role == "system")
        if systemMsg != null:
            systemMsg.Text += "\n" + formatInstruction
        else:
            messages.Insert(0, { Role="system", Text=formatInstruction })

    // --- Cache check (persistent storage) ---
    cacheKey = null
    if action.Cache and action.Tools == null:
        cacheKey = Hash(messages, model, action.Temperature, schema, action.Format)
        cached = PersistentCache.Get(cacheKey)
        if cached != null:
            return cached

    // --- Build tools for API ---
    apiTools = null
    if action.Tools != null:
        apiTools = action.Tools.Select(t => {
            type: "function",
            function: { name: t.Name, description: t.Description,
                        parameters: BuildParamSchema(t.Parameters) }
        })

    // --- Track totals across the loop ---
    toolCallCount = 0
    validationRetries = 0
    totalPromptTokens = 0
    totalCompletionTokens = 0
    totalCost = 0.0

    loop:
        // --- HTTP request (via http module) ---
        body = {
            model: model,
            messages: ToApiMessages(messages),
            temperature: action.Temperature,
            max_tokens: action.MaxTokens,
            tools: apiTools,
            stream: action.OnStream != null
        }

        // --- Streaming path ---
        if action.OnStream != null:
            fullContent = ""
            toolCalls = []
            usage = null

            for each chunk in http.Stream(endpoint, body, apiKey):
                delta = chunk.choices[0].delta

                if delta.content != null:
                    fullContent += delta.content
                    engine.RunGoal(action.OnStream, {
                        content: delta.content,
                        fullContent: fullContent,
                        isDone: false
                    })

                if delta.tool_calls != null:
                    MergeToolCallDeltas(toolCalls, delta.tool_calls)

                if chunk.usage != null:
                    usage = chunk.usage

            // Signal stream complete
            engine.RunGoal(action.OnStream, {
                content: null,
                fullContent: fullContent,
                isDone: true
            })

            choice = { content: fullContent, tool_calls: toolCalls or null }
            responseUsage = usage

        // --- Non-streaming path ---
        else:
            response = http.Post(endpoint, body, apiKey)

            if response.Error:
                return Data.FromError(response.Error)

            choice = response.choices[0].message
            responseUsage = response.usage

        // --- Accumulate token usage ---
        if responseUsage != null:
            totalPromptTokens += responseUsage.prompt_tokens
            totalCompletionTokens += responseUsage.completion_tokens
            totalCost += CalculateCost(model, responseUsage)

        // --- Tool calls? ---
        if choice.tool_calls != null and choice.tool_calls.Count > 0:
            if toolCallCount >= action.MaxToolCalls:
                break  // hit limit, return what we have

            // Append assistant message with tool_calls to conversation
            messages.Add({
                Role = "assistant",
                ToolCalls = choice.tool_calls.Select(tc => {
                    Id: tc.id, Name: tc.function.name,
                    Arguments: tc.function.arguments
                })
            })

            // Determine if all requested tools are parallel-safe
            allParallel = choice.tool_calls.All(tc =>
                action.Tools.Find(t => t.Name == tc.function.name)?.Parallel == true)

            if allParallel:
                results = await Task.WhenAll(
                    choice.tool_calls.Select(tc => ExecuteTool(engine, action, tc)))
            else:
                results = []
                for each tc in choice.tool_calls:
                    results.Add(await ExecuteTool(engine, action, tc))

            // Append tool results to conversation
            for i, tc in choice.tool_calls:
                messages.Add({
                    Role = "tool",
                    ToolCallId = tc.id,
                    Text = results[i]
                })
                toolCallCount++

            continue loop  // re-query with tool results

        // --- No tool calls — we have a content response ---
        content = choice.content
        rawResponse = content

        // --- Format extraction ---
        effectiveFormat = action.Format ?? (schema != null ? "json" : null)
        content = ExtractResponse(content, effectiveFormat)

        // --- JSON validation (only for json format) ---
        if effectiveFormat == "json":
            parsed = TryParseJson(content)
            if parsed.Error:
                parsed = TryExtractJsonFromCodeBlock(content)
            if parsed.Error:
                return Data.FromError("Response is not valid JSON")
            content = parsed.Value

        // --- Custom validation via OnValidateResponse ---
        if action.OnValidateResponse != null:
            if validationRetries >= action.MaxValidationRetries:
                return Data.FromError("Validation failed after {max} retries")

            validationResult = engine.RunGoal(action.OnValidateResponse,
                                              { response: content })

            if validationResult.Error:
                validationRetries++
                messages.Add({ Role="user",
                    Text="Your response failed validation: "
                         + validationResult.Error.Message
                         + "\nPlease fix and try again." })
                continue loop  // re-query

        // --- Store conversation for continuity ---
        messages.Add({ Role="assistant", Text=rawResponse })
        context.Set("__llm_conversation__", messages)
        context.Set("__llm_schema__", schema)

        // --- Cache store (persistent) ---
        if cacheKey != null:
            PersistentCache.Set(cacheKey, content)

        // --- Build result with properties ---
        // Value = the parsed content (JSON object, extracted text, etc.)
        // Properties = LLM metadata, accessible via %result!PropertyName% in PLang
        result = Data.Ok(content)
        result.Properties = {
            RawResponse:        rawResponse,
            Model:              model,
            Messages:           messages,
            Temperature:        action.Temperature,
            MaxTokens:          action.MaxTokens,
            Cached:             false,
            PromptTokens:       totalPromptTokens,
            CompletionTokens:   totalCompletionTokens,
            TotalTokens:        totalPromptTokens + totalCompletionTokens,
            Cost:               totalCost,
            ToolCallCount:      toolCallCount,
            ValidationRetries:  validationRetries,
            Format:             effectiveFormat,
            Schema:             schema
        }
        return result


ExecuteTool(engine, action, toolCall):
    // OnToolCall — starting
    if action.OnToolCall != null:
        engine.RunGoal(action.OnToolCall, {
            name: toolCall.function.name,
            arguments: toolCall.function.arguments,
            status: "starting"
        })

    // Find matching GoalCall
    goalCall = action.Tools.Find(t => t.Name == toolCall.function.name)
    if goalCall == null:
        result = "Error: unknown tool '{toolCall.function.name}'"
    else:
        args = ParseArguments(toolCall.function.arguments, goalCall.Parameters)
        goalResult = engine.RunGoal(goalCall.Name, args)

        if goalResult.Success:
            result = Serialize(goalResult.Value)
        else:
            result = "Error: " + goalResult.Error.Message

    // OnToolCall — completed
    if action.OnToolCall != null:
        engine.RunGoal(action.OnToolCall, {
            name: toolCall.function.name,
            result: result,
            status: "completed"
        })

    return result


BuildFormatInstruction(format, schema):
    effectiveFormat = format ?? (schema != null ? "json" : null)

    if effectiveFormat == null:
        return null

    if effectiveFormat == "json" and schema != null:
        return "You MUST respond in JSON, schema: " + schema
    if effectiveFormat == "json":
        return "You MUST respond in JSON"

    // General format — instruct LLM to wrap in code block
    return "You MUST respond in ```" + effectiveFormat + "``` code block"


ExtractResponse(content, format):
    if format == null or format == "json":
        return content  // json validation handled separately

    // Extract from code block: ```{format}\n...\n```
    match = Regex("```" + format + "\n(.*?)\n```", content, singleline)
    if match:
        return match.Group(1)

    // Fallback: try any code block
    match = Regex("```\n?(.*?)\n?```", content, singleline)
    if match:
        return match.Group(1)

    // No code block found — return raw
    return content
```

## Schema and Format Handling

Follows the Runtime1 approach — schema and format are instructions to the LLM via the system prompt, not API-level structured output.

### How it works

1. **Format** is any user-defined string. Only `"json"` has special behavior (schema support + JSON validation). All other formats instruct the LLM to respond in a code block and the provider extracts from it.
   - `"json"` → `"You MUST respond in JSON"`
   - `"python"` → `"You MUST respond in` `` ```python``` `` `code block"`
   - `"md"` → `"You MUST respond in` `` ```md``` `` `code block"`
   - Any string works — `"yaml"`, `"sql"`, `"toml"`, etc.
   - `null` with no schema → no format instruction
2. **Schema** (only meaningful when Format is json, which is the default when Schema is set):
   - Appended to the format instruction: `"You MUST respond in JSON, schema: {sentiment: string, score: int}"`
   - The builder produces this schema string — it's what the PLang developer wrote
3. **Response extraction** for non-json formats:
   - Extract content from `` ```{format}...``` `` code block in the response
   - Fallback to any code block if format-specific block not found
   - Return raw content if no code block found
4. **JSON validation**: parse response as JSON. If it fails, attempt extraction from markdown code blocks. If still fails, return error.
5. **Provider-agnostic** — this works with any LLM API. No dependency on OpenAI's `response_format` field.

### Format defaulting

```
if Schema is set and Format is null → Format = "json"
if Schema is null and Format is null → no format instruction
```

### Caching

- Hash: messages + model + temperature + schema + format (deterministic inputs only)
- Storage: **persistent** (disk/database, not in-memory). Implementation detail for the coder.
- Skip when: `Cache = false` OR `Tools != null` (tool results are non-deterministic)
- Provider checks cache before HTTP call, stores on miss

### Response Properties

The returned Data carries metadata as properties, accessible via `%!` syntax in PLang:

| Property | Type | Description |
|----------|------|-------------|
| `RawResponse` | string | Raw text response from the LLM |
| `Model` | string | Model that was used |
| `Messages` | List\<LlmMessage\> | Full conversation history (including assistant response) |
| `Temperature` | double | Temperature that was used |
| `MaxTokens` | int | MaxTokens that was used |
| `Cached` | bool | Whether this response came from cache |
| `PromptTokens` | int | Total prompt tokens across all API calls |
| `CompletionTokens` | int | Total completion tokens across all API calls |
| `TotalTokens` | int | PromptTokens + CompletionTokens |
| `Cost` | double | Estimated cost across all API calls |
| `ToolCallCount` | int | Total tool calls executed |
| `ValidationRetries` | int | Number of validation retries that occurred |
| `Format` | string? | Effective format used |
| `Schema` | string? | Schema used |

Example PLang usage:
```plang
- system: analyze this
  user: %text%
  schema: {sentiment: string}
  write to %result%
- write out 'Cost: %result!Cost%, Tokens: %result!TotalTokens%'
- write out 'Raw: %result!RawResponse%'
- write out 'Sentiment: %result.sentiment%'
```

Note: `%result.sentiment%` accesses the parsed value. `%result!Cost%` accesses a property on the Data envelope.

### What the Builder Sees

The builder uses `llm.query` like any other action:

```plang
- system: %buildGoalPrompt%
  user: %goalForLlm%
  schema: {steps: [{index: int, ...}]}
  write to %stepResults%
```

Maps to: `Messages=[{system, %buildGoalPrompt%}, {user, %goalForLlm%}], Schema={steps: [...]}, Cache=true`

With tools, validation, and streaming:

```plang
- system: you are a helpful assistant
  user: %question%
  tools:
    GetWeather, gets weather for a city, %city%(string), parallel
    SearchWeb, searches the web, %query%(string), parallel
  onToolCall call DisplayToolStatus
  onValidateResponse call ValidateAnswer
  onStream call DisplayChunk
  write to %answer%
```

Maps to: `Messages=[...], Tools=[{Name=GetWeather, Description=..., Parallel=true, ...}, ...], OnToolCall={Name=DisplayToolStatus}, OnValidateResponse={Name=ValidateAnswer}, OnStream={Name=DisplayChunk}`

With conversation continuity:

```plang
- system: you are a helpful assistant
  user: %firstQuestion%
  write to %answer1%

- system: you are a helpful assistant
  user: %followUp%
  continuePreviousConversation
  write to %answer2%
```

The second step gets the full conversation history (system + user + assistant response from step 1) prepended automatically.

With format:

```plang
- system: explain this code
  user: %code%
  format: md
  write to %explanation%
```

```plang
- system: convert this to python
  user: %csharpCode%
  format: python
  write to %pythonCode%
```

### File Structure

```
PLang/Runtime2/modules/llm/
├── query.cs                     — action record, delegates to ILlmProvider
├── LlmMessage.cs                — message type (Role, Text, Image, ToolCallId, ToolCalls)
├── ToolCall.cs                  — tool call carrier (Id, Name, Arguments)
├── providers/
│   ├── ILlmProvider.cs          — provider interface
│   └── OpenAiProvider.cs        — OpenAI-compatible implementation
```

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/llm/query.cs` | Action record, delegates to provider |
| `PLang/Runtime2/modules/llm/LlmMessage.cs` | Message type with tool metadata |
| `PLang/Runtime2/modules/llm/ToolCall.cs` | Tool call carrier type |
| `PLang/Runtime2/modules/llm/providers/ILlmProvider.cs` | Provider interface |
| `PLang/Runtime2/modules/llm/providers/OpenAiProvider.cs` | OpenAI implementation |

## Files to Modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Goals/Goal/GoalCall.cs` | Add `Description` and `Parallel` properties |
| `PLang/Runtime2/Engine/Providers/this.cs` | Register ILlmProvider in ResolveType + RegisterDefaults |

## Definition of Done

- `llm.query` action resolves parameters via source generator
- `[Provider]` on Query wires to ILlmProvider from registry
- OpenAiProvider sends messages, parses response, returns content as Data
- HTTP calls go through the runtime's http module
- Tool execution loop works (execute GoalCalls, re-query until done or MaxToolCalls)
- Parallel tool execution works when all tools in a batch have Parallel=true
- OnToolCall callback fires before/after each tool execution (with status)
- OnValidateResponse callback fires on final response; error feeds back to LLM for retry
- MaxValidationRetries (default 3) limits validation retry loops independently from tool calls
- OnStream callback fires for each chunk during streaming; http module handles SSE
- ContinuePreviousConversation stores/restores message history in context
- Tool errors are sent back to the LLM as tool results
- Schema appended to system prompt as text instruction, JSON response validated
- Format is user-defined (any string); non-json formats extracted from code blocks
- Response properties populated on returned Data (tokens, cost, raw response, model, etc.)
- Properties accessible via `%result!PropertyName%` syntax in PLang
- Caching is persistent (not in-memory), hash-based, skipped for tool queries
- GoalCall.Description and GoalCall.Parallel available for tool definitions
- Builder's `[llm]` syntax works through the new module
- Provider switchable via standard `provider.set` action
