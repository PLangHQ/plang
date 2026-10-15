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

    /// <summary>The message's content as a plang VALUE, unrendered — a text template (<c>rank these %orders%</c>)
    /// kept as it was authored, never a string rendered before a writer was chosen. The llm provider opens it at
    /// its own boundary, through the llm writer, so a value inside the content (a list, a long text) is cut the way
    /// the provider decides. A message built in C# (a tool result, an assistant reply) assigns a plain text.</summary>
    [Store, LlmBuilder]
    public global::app.type.item.text.@this? Content { get; set; }

    [Store, LlmBuilder]
    public List<string>? Images { get; set; }

    // --- Tool conversation fields (internal, not exposed to builder) ---

    /// <summary>For role=tool: which call this responds to.</summary>
    public string? ToolCallId { get; set; }

    /// <summary>For role=assistant: tools the LLM wants to call.</summary>
    public List<ToolCall>? ToolCalls { get; set; }

    /// <summary>A message holds what a step quoted by its content's own characters, never only by the escaped form a
    /// writer gives it.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
        => Content?.ToString().Contains(quoted) == true || await base.Holds(quoted, context);

    /// <summary>A message is born from a text as the user's message with that content — a prompt written as text
    /// (<c>ask llm "say hi"</c>) is what the user says. The text is kept as the value it is (a template
    /// unrendered), opened only when the provider sends it.</summary>
    public static @this? Create(object? raw) => raw switch
    {
        @this message => message,
        global::app.type.item.text.@this text => new() { Role = "user", Content = text },
        string text => new() { Role = "user", Content = (global::app.type.item.text.@this)text },
        _ => null,
    };

    /// <summary>A message is born from a text (the user's prompt) or from a dict of its members — <c>Role</c>,
    /// <c>Content</c>, <c>Images</c>. The message builds itself so its <c>Content</c> stays the plang value it is
    /// (a text template, unrendered) — the generic record deserialize would lower it to a C# string, which is the
    /// very thing this type exists to avoid. A member the message does not name is ignored.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this message) return message;
        if (Create(raw) is { } fromText) return fromText;
        if (raw is not global::app.type.item.dict.@this dict) return null;

        var made = new @this();
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "role": made.Role = entry.Peek().ToString() ?? ""; break;
                case "content":
                    made.Content = entry.Peek() as global::app.type.item.text.@this
                                   ?? (global::app.type.item.text.@this)(entry.Peek().ToString() ?? "");
                    break;
                case "images":
                    if (entry.Peek() is global::app.type.item.list.@this images)
                        made.Images = images.Clr<List<string>>();
                    break;
            }
        return made;
    }
}
