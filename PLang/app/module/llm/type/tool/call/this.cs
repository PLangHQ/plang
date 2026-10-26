using app.Attributes;

namespace app.module.llm.type.tool.call;

/// <summary>
/// One tool call an llm answered with: the provider parses its response into these, and the tool loop finds the goal
/// each names and runs it. A plang value, so a conversation saved mid-tool-call keeps the calls its assistant message
/// made. The llm module's own; the provider's alone — no step names it.
/// </summary>
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Plang keeps it for itself: no step names it.</summary>
    public static bool Internal => true;

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>Provider-assigned call ID.</summary>
    [Store]
    public string Id { get; set; } = "";

    /// <summary>Goal name to call.</summary>
    [Store]
    public string Name { get; set; } = "";

    /// <summary>JSON string of arguments from the LLM.</summary>
    [Store]
    public string Arguments { get; set; } = "";

    /// <summary>What an OnToolCall callback is handed about this call: its name and arguments, where it is
    /// (<paramref name="status"/>), and — once it ran — its <paramref name="result"/>.</summary>
    public IEnumerable<global::app.data.@this> State(string status, string? result, global::app.actor.context.@this context)
    {
        yield return new global::app.data.@this("name", Name, context: context);
        yield return new global::app.data.@this("arguments", Arguments, context: context);
        yield return new global::app.data.@this("status", status, context: context);
        if (result != null) yield return new global::app.data.@this("result", result, context: context);
    }

    /// <summary>The pure core — a tool call passes through; nothing else converts into one without its asker.</summary>
    public static @this? Create(object? raw) => raw as @this;

    /// <summary>A tool call passes through; a dict of its members (what a stored conversation reads back as) is read as
    /// the call it is; anything else declines.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data) => raw switch
    {
        @this call => call,
        global::app.type.item.dict.@this members => new()
        {
            Id = members.Get("id", data.Context)?.Peek().ToString() ?? "",
            Name = members.Get("name", data.Context)?.Peek().ToString() ?? "",
            Arguments = members.Get("arguments", data.Context)?.Peek().ToString() ?? "",
        },
        _ => null,
    };
}
