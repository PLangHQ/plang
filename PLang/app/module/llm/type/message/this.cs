using app;
using app.Attributes;

namespace app.module.llm.type.message;

/// <summary>
/// PLang <c>message</c> value — one message in an LLM conversation: Role + Content + optional Images for multimodal.
/// ToolCallId and ToolCalls are internal — used by the provider during tool conversations, never set by the builder.
/// The llm module's own.
/// </summary>
[PlangType("message")]
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    [Store, LlmBuilder]
    public string Role { get; set; } = "";

    [Store, LlmBuilder]
    public string? Content { get; set; }

    [Store, LlmBuilder]
    public List<string>? Images { get; set; }

    // --- Tool conversation fields (internal, not exposed to builder) ---

    /// <summary>For role=tool: which call this responds to.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>For role=assistant: tools the LLM wants to call.</summary>
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>A message is born from a text as the user's message with that content — a prompt written as text
    /// (<c>ask llm "say hi"</c>) is what the user says; a dict of its members is read as the record it is.</summary>
    public static @this? Create(object? raw) => raw switch
    {
        @this message => message,
        global::app.type.item.text.@this text => new() { Role = "user", Content = text.ToString() },
        string text => new() { Role = "user", Content = text },
        _ => null,
    };
}
