using App.Goals.Goal;
using App.Variables;
using App.modules.llm.providers;

namespace App.modules.llm;

/// <summary>
/// Sends a query to an LLM provider. Supports tools, streaming, validation,
/// conversation continuity, caching, and structured output (JSON/code block extraction).
/// </summary>
[Example("system: analyze sentiment\n  user: %comment%\n  schema: {sentiment: string}\n  write to %result%",
    "Messages=[{Role=system, Text=analyze sentiment}, {Role=user, Text=%comment%}], Schema={sentiment: string}")]
[Example("system: you are a helpful assistant\n  user: %question%\n  tools:\n    GetWeather, gets weather for a city, %city%(string), parallel\n  write to %answer%",
    "Messages=[{Role=system, Text=you are a helpful assistant}, {Role=user, Text=%question%}], Tools=[{Name=GetWeather, Description=gets weather for a city, Parameters=[{Name=city, Type=string}], Parallel=true}]")]
[Action("query")]
public partial class query : IContext
{
    /// <summary>Conversation messages (system, user, assistant).</summary>
    [IsNotNull]
    public partial List<LlmMessage> Messages { get; init; }

    /// <summary>Goals available as tools for the LLM to call.</summary>
    public partial List<GoalCall>? Tools { get; init; }

    /// <summary>Callback fired before/after each tool execution. Receives: name, arguments, status, result.</summary>
    [GoalCallback("toolCallInfo")]
    public partial GoalCall? OnToolCall { get; init; }

    /// <summary>Callback to validate the LLM's response. Return error to trigger retry.</summary>
    [GoalCallback("response")]
    public partial GoalCall? OnValidateResponse { get; init; }

    /// <summary>Callback fired for each streaming chunk. Receives: content, fullContent, isDone.</summary>
    [GoalCallback("streamChunk")]
    public partial GoalCall? OnStream { get; init; }

    /// <summary>JSON schema string the LLM must conform to. When set, format defaults to "json".</summary>
    public partial string? Schema { get; init; }

    /// <summary>Response format: "json", "python", "md", etc. Non-json formats extract from code blocks.</summary>
    public partial string? Format { get; init; }

    /// <summary>Model override (e.g., "gpt-4o"). Falls back to provider settings default.</summary>
    public partial string? Model { get; init; }

    /// <summary>When true, prepends stored conversation history from previous queries.</summary>
    [Default(false)]
    public partial bool ContinuePreviousConversation { get; init; }

    /// <summary>Sampling temperature. 0.0 = deterministic.</summary>
    [Default(0.0)]
    public partial double Temperature { get; init; }

    /// <summary>Maximum tokens in the response.</summary>
    [Default(4000)]
    public partial int MaxTokens { get; init; }

    /// <summary>Maximum total individual tool calls before stopping the loop.</summary>
    [Default(10)]
    public partial int MaxToolCalls { get; init; }

    /// <summary>Maximum validation retries before returning error.</summary>
    [Default(0)]
    public partial int MaxValidationRetries { get; init; }

    /// <summary>Whether to cache the response. Skipped when Tools is non-null.</summary>
    [Default(true)]
    public partial bool Cache { get; init; }

    [Provider]
    public partial ILlmProvider Llm { get; }

    public async Task<Data.@this> Run() => await Llm.Query(this);
}
