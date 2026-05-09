using App.Attributes;
using App.Goals.Goal;
using App.Variables;
using App.modules.llm.code;

namespace App.modules.llm;

/// <summary>
/// Sends a query to an LLM provider. Supports tools, streaming, validation,
/// conversation continuity, caching, and structured output (JSON/code block extraction).
/// </summary>
[ModuleDescription("Send prompts to an LLM and receive structured or streamed responses, with optional tool use")]
[System.ComponentModel.Description("Send a conversation (system + user messages) to an LLM and return the response, with optional schema or tool use")]
[Example("system: analyze sentiment, user: %comment%, schema: {sentiment: string}, write to %result%",
    "llm.query Messages([list<LlmMessage>] [{\"Role\":\"system\",\"Content\":\"analyze sentiment\"},{\"Role\":\"user\",\"Content\":\"%comment%\"}]), Schema([string] {sentiment: string}) | variable.set Name([string] %result%), Value([object] %__data__%)")]
[Action("query")]
[RequiresCapability("llm")]
public partial class query : IContext, IBuildValidatable
{
    public static string? ValidateBuild(List<Data.@this> parameters)
    {
        var messages = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Messages", StringComparison.OrdinalIgnoreCase));

        if (messages == null)
            return "Missing required parameter 'Messages'. Must be a list of {Role: string, Content: string} objects. Map system= to {Role: \"system\", Content: \"...\"} and user= to {Role: \"user\", Content: \"...\"}";

        var value = messages.Value;

        if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
            return "Parameter 'Messages' is empty. Must be a list of {Role: string, Content: string} objects. Map system= to {\"Role\": \"system\", \"Content\": \"...\"} and user= to {\"Role\": \"user\", \"Content\": \"...\"}";

        if (value is not System.Collections.IList list || list.Count == 0)
        {
            if (value is not string) // already handled above
                return $"Parameter 'Messages' must be a list of {{Role, Content}} objects, got {value.GetType().Name}";
        }

        return null;
    }

    /// <summary>Conversation messages (system, user, assistant).</summary>
    [IsNotNull]
    public partial Data.@this<List<LlmMessage>> Messages { get; init; }

    /// <summary>Goals available as tools for the LLM to call.</summary>
    public partial Data.@this<List<GoalCall>>? Tools { get; init; }

    /// <summary>Callback fired before/after each tool execution. Receives: name, arguments, status, result.</summary>
    [GoalCallback("toolCallInfo")]
    public partial Data.@this<GoalCall>? OnToolCall { get; init; }

    /// <summary>Callback to validate the LLM's response. Return error to trigger retry.</summary>
    [GoalCallback("response")]
    public partial Data.@this<GoalCall>? OnValidateResponse { get; init; }

    /// <summary>Callback fired for each streaming chunk. Receives: content, fullContent, isDone.</summary>
    [GoalCallback("streamChunk")]
    public partial Data.@this<GoalCall>? OnStream { get; init; }

    /// <summary>JSON schema string the LLM must conform to. When set, format defaults to "json".</summary>
    /// <summary>
    /// Optional schema describing the expected response shape. Accepts any value the
    /// developer wrote in .goal source — the builder LLM normalizes it into a
    /// structured form (typically a JSON object Dictionary), but free-form strings,
    /// YAML/XML descriptions, etc. are also valid. The provider serializes to text
    /// before sending to the LLM (JSON via System.Text.Json for structured shapes,
    /// pass-through for strings).
    /// </summary>
    public partial Data.@this<object>? Schema { get; init; }

    /// <summary>Response format: "json", "python", "md", etc. Non-json formats extract from code blocks.</summary>
    public partial Data.@this<string>? Format { get; init; }

    /// <summary>Model override (e.g., "gpt-4o"). Falls back to provider settings default.</summary>
    public partial Data.@this<string>? Model { get; init; }

    /// <summary>When true, prepends stored conversation history from previous queries.</summary>
    [Default(false)]
    public partial Data.@this<bool> ContinuePreviousConversation { get; init; }

    /// <summary>Sampling temperature. 0.0 = deterministic.</summary>
    [Default(0.0)]
    public partial Data.@this<double> Temperature { get; init; }

    /// <summary>Top-p (nucleus sampling). 0.0 = greedy, 1.0 = full distribution.</summary>
    public partial Data.@this<double>? TopP { get; init; }

    /// <summary>Maximum tokens in the response.</summary>
    [Default(16000)]
    public partial Data.@this<int> MaxTokens { get; init; }

    /// <summary>Maximum total individual tool calls before stopping the loop.</summary>
    [Default(10)]
    public partial Data.@this<int> MaxToolCalls { get; init; }

    /// <summary>Maximum validation retries before returning error.</summary>
    [Default(0)]
    public partial Data.@this<int> MaxValidationRetries { get; init; }

    /// <summary>Whether to cache the response. Skipped when Tools is non-null.</summary>
    [Default(true)]
    public partial Data.@this<bool> Cache { get; init; }

    [Provider]
    public partial ILlm Llm { get; }

    public async Task<Data.@this> Run() => await Llm.Query(this);
}
