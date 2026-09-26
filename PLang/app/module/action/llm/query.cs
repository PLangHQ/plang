using app.Attributes;
using app.goal;
using app.variable;
using app.module.action.llm.code;

namespace app.module.action.llm;

/// <summary>
/// Sends a query to an LLM provider. Supports tools, streaming, validation,
/// conversation continuity, caching, and structured output (JSON/code block extraction).
/// </summary>
[Action("query")]
[RequiresCapability("llm")]
public partial class query : IContext
{
    /// <summary>Build-time judgement of my own Message, read as authored (Peek). A missing Message
    /// is the action's own required-parameter verdict, not mine.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate()
    {
        var value = Message.Peek();

        // The binding answers presence, the text instance its own emptiness via truthiness.
        if (!Message.HasValue
            || (value is global::app.type.item.text.@this st && !st.IsTruthy()))
            return new global::app.error.ProgramError("Parameter 'Message' is empty. Must be a list of {Role: string, Content: string} objects. Map system= to {\"Role\": \"system\", \"Content\": \"...\"} and user= to {\"Role\": \"user\", \"Content\": \"...\"}", key: "EmptyParameter");

        // a %variable% is unknown at build: its value is judged when the step runs
        if (Message.HasVariableReference) return null;

        if (value is not global::app.type.item.list.@this
            && value is not Clr { Value: System.Collections.IList }
            && value is not global::app.type.item.text.@this) // text already handled above
            return new global::app.error.ProgramError($"Parameter 'Message' must be a list of {{Role, Content}} objects, got {value!.Type.Name}", key: "WrongParameterType");

        return null;
    }

    /// <summary>Conversation messages (system, user, assistant).</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.list.@this<LlmMessage>> Message { get; init; }

    /// <summary>Goals available as tools for the LLM to call — each a <c>goal.call</c> action whose
    /// Parameter rows declare what the model must supply (a row with no value is required).</summary>
    public partial data.@this<global::app.type.item.list.@this>? Tool { get; init; }

    /// <summary>The call run before and after each tool execution, with %name%, %arguments%,
    /// %status% (starting/completed) and, once completed, %result%.</summary>
    [GoalCallback("status")]
    public partial data.@this<global::app.goal.step.action.@this>? OnToolCall { get; init; }

    /// <summary>The call that validates the LLM's response, with %response%. Returning an error
    /// triggers a retry.</summary>
    [GoalCallback("response")]
    public partial data.@this<global::app.goal.step.action.@this>? OnValidateResponse { get; init; }

    /// <summary>The call run for each streaming chunk — the response streams through http.request's
    /// OnStream, so the chunk arrives as %chunk%.</summary>
    [GoalCallback("chunk")]
    public partial data.@this<global::app.goal.step.action.@this>? OnStream { get; init; }

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
    public partial data.@this<global::app.type.item.text.@this>? Format { get; init; }

    /// <summary>Model override (e.g., "gpt-4o"). Falls back to provider settings default.</summary>
    public partial data.@this<global::app.type.item.text.@this>? Model { get; init; }

    /// <summary>When true, prepends stored conversation history from previous queries.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> ContinuePreviousConversation { get; init; }

    /// <summary>Sampling temperature. 0.0 = deterministic.</summary>
    [Default(0.0)]
    public partial data.@this<global::app.type.item.number.@this> Temperature { get; init; }

    /// <summary>Top-p (nucleus sampling). 0.0 = greedy, 1.0 = full distribution.</summary>
    public partial data.@this<global::app.type.item.number.@this>? TopP { get; init; }

    /// <summary>Maximum tokens in the response.</summary>
    [Default(16000)]
    public partial data.@this<global::app.type.item.number.@this> MaxTokens { get; init; }

    /// <summary>Maximum total individual tool calls before stopping the loop.</summary>
    [Default(10)]
    public partial data.@this<global::app.type.item.number.@this> MaxToolCalls { get; init; }

    /// <summary>Maximum validation retries before returning error.</summary>
    [Default(0)]
    public partial data.@this<global::app.type.item.number.@this> MaxValidationRetries { get; init; }

    /// <summary>Whether to cache the response. Skipped when Tool is non-null.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> Cache { get; init; }

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
        // A row marked a template holds a variable with no binding at build — the marker says so,
        // never the characters in it.
        var schemaRow = __action?["Schema"];
        var schema = schemaRow?.Value;
        if (schema is not (null or global::app.type.item.@null.@this)
            && schemaRow!.Type?.Template == null
            && !(schema is global::app.type.item.text.@this st && st.Clr<string>() is "" or null))
            return Task.FromResult(Context.Ok("json"));

        var formatRow = __action?["Format"];
        var format = formatRow?.Value?.ToString();
        if (!string.IsNullOrEmpty(format) && formatRow!.Type?.Template == null)
            return Task.FromResult(Context.Ok(format));

        return Task.FromResult(Context.Ok());
    }
}
