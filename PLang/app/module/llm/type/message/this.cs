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

    /// <summary>For role=tool: which call this responds to. Stored, so a conversation saved mid-tool-call keeps it.</summary>
    [Store]
    public string? ToolCallId { get; set; }

    /// <summary>For role=assistant: tools the LLM wants to call. Stored, so a conversation saved mid-tool-call keeps
    /// them.</summary>
    [Store]
    public List<tool.call.@this>? ToolCalls { get; set; }

    /// <summary>A message holds what a step quoted by its content's own characters, never only by the escaped form a
    /// writer gives it.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
        => Content?.Contains(quoted) == true || await base.Holds(quoted, context);

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
