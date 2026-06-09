using app.Attributes;
using app.goal;
using app.variable;
using app.module.llm.code;

namespace app.module.llm;

/// <summary>
/// Sends a query to an LLM provider. Supports tools, streaming, validation,
/// conversation continuity, caching, and structured output (JSON/code block extraction).
/// </summary>
[Action("query")]
[RequiresCapability("llm")]
public partial class query : IContext, IBuildValidatable
{
    public static string? ValidateBuild(List<data.@this> parameters)
    {
        var messages = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Messages", StringComparison.OrdinalIgnoreCase));

        if (messages == null)
            return "Missing required parameter 'Messages'. Must be a list of {Role: string, Content: string} objects. Map system= to {Role: \"system\", Content: \"...\"} and user= to {Role: \"user\", Content: \"...\"}";

        var value = messages.Materialize();

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
    public partial data.@this<global::app.type.list.@this<LlmMessage>> Messages { get; init; }

    /// <summary>Goals available as tools for the LLM to call.</summary>
    public partial data.@this<global::app.type.list.@this<GoalCall>>? Tools { get; init; }

    /// <summary>Callback fired before/after each tool execution. Receives: name, arguments, status, result.</summary>
    [GoalCallback("toolCallInfo")]
    public partial data.@this<GoalCall>? OnToolCall { get; init; }

    /// <summary>Callback to validate the LLM's response. Return error to trigger retry.</summary>
    [GoalCallback("response")]
    public partial data.@this<GoalCall>? OnValidateResponse { get; init; }

    /// <summary>Callback fired for each streaming chunk. Receives: content, fullContent, isDone.</summary>
    [GoalCallback("streamChunk")]
    public partial data.@this<GoalCall>? OnStream { get; init; }

    /// <summary>JSON schema string the LLM must conform to. When set, format defaults to "json".</summary>
    /// <summary>
    /// Optional schema describing the expected response shape. Accepts any value the
    /// developer wrote in .goal source — the builder LLM normalizes it into a
    /// structured form (typically a JSON object Dictionary), but free-form strings,
    /// YAML/XML descriptions, etc. are also valid. The provider serializes to text
    /// before sending to the LLM (JSON via System.Text.Json for structured shapes,
    /// pass-through for strings).
    /// </summary>
    public partial data.@this? Schema { get; init; }

    /// <summary>Response format: "json", "python", "md", etc. Non-json formats extract from code blocks.</summary>
    public partial data.@this<global::app.type.text.@this>? Format { get; init; }

    /// <summary>Model override (e.g., "gpt-4o"). Falls back to provider settings default.</summary>
    public partial data.@this<global::app.type.text.@this>? Model { get; init; }

    /// <summary>When true, prepends stored conversation history from previous queries.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> ContinuePreviousConversation { get; init; }

    /// <summary>Sampling temperature. 0.0 = deterministic.</summary>
    [Default(0.0)]
    public partial data.@this<global::app.type.number.@this> Temperature { get; init; }

    /// <summary>Top-p (nucleus sampling). 0.0 = greedy, 1.0 = full distribution.</summary>
    public partial data.@this<global::app.type.number.@this>? TopP { get; init; }

    /// <summary>Maximum tokens in the response.</summary>
    [Default(16000)]
    public partial data.@this<global::app.type.number.@this> MaxTokens { get; init; }

    /// <summary>Maximum total individual tool calls before stopping the loop.</summary>
    [Default(10)]
    public partial data.@this<global::app.type.number.@this> MaxToolCalls { get; init; }

    /// <summary>Maximum validation retries before returning error.</summary>
    [Default(0)]
    public partial data.@this<global::app.type.number.@this> MaxValidationRetries { get; init; }

    /// <summary>Whether to cache the response. Skipped when Tools is non-null.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.@bool.@this> Cache { get; init; }

    [Code]
    public partial ILlm Llm { get; }

    // Polymorphic: response shape depends on Schema (raw string, structured
    // object, tool-call object). The provider declares Data<object>; the
    // action forwards cleanly.
    public async Task<data.@this> Run() => await Llm.Query(this);

    /// <summary>
    /// Compile-time hint: Schema set ⇒ "json" (the LLM is asked to fit a
    /// structured response). Format set without Schema ⇒ Format.Value. Neither
    /// ⇒ bare Ok() (defer to runtime / explicit user hint).
    /// </summary>
    public Task<data.@this> Build()
    {
        var schema = __action?.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, "Schema", System.StringComparison.OrdinalIgnoreCase))?.Materialize();
        if (schema != null && !(schema is string s && (string.IsNullOrEmpty(s) || s.Contains('%'))))
            return Task.FromResult(data.@this.Ok("json"));

        var format = __action?.Parameters?.FirstOrDefault(p =>
            string.Equals(p.Name, "Format", System.StringComparison.OrdinalIgnoreCase))?.GetValue<string>();
        if (!string.IsNullOrEmpty(format) && !format.Contains('%'))
            return Task.FromResult(data.@this.Ok(format));

        return Task.FromResult(data.@this.Ok());
    }
}
