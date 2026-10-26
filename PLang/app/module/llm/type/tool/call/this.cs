using app.Attributes;

namespace app.module.llm.type.tool.call;

/// <summary>
/// One tool call an llm answered with: the provider parses its response into these, and the tool loop finds the goal
/// each names and runs it. A plang value, so a conversation saved mid-tool-call keeps the calls its assistant message
/// made; a dict of its members (what a stored conversation reads back as) is read as the record it is. The llm
/// module's own; the provider's alone — no step names it.
/// </summary>
[PlangType("tool.call")]
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Plang keeps it for itself: no step names it.</summary>
    public static bool Internal => true;

    /// <summary>A structure, not a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>Provider-assigned call ID.</summary>
    [Store]
    public string Id { get; init; } = "";

    /// <summary>Goal name to call.</summary>
    [Store]
    public string Name { get; init; } = "";

    /// <summary>JSON string of arguments from the LLM.</summary>
    [Store]
    public string Arguments { get; init; } = "";

    /// <summary>What an OnToolCall callback is handed about this call: its name and arguments, where it is
    /// (<paramref name="status"/>), and — once it ran — what it <paramref name="answered"/>: its value, or its error.</summary>
    public IEnumerable<global::app.data.@this> State(status status, global::app.data.@this? answered, global::app.actor.context.@this context)
    {
        var types = context.App.type.list;
        yield return new global::app.data.@this("name", Name, context: context);
        yield return new global::app.data.@this("arguments", Arguments, context: context);
        yield return new global::app.data.@this("status",
            new global::app.type.item.choice.@this<status>(status, types[typeof(global::app.type.item.choice.@this<status>)]), context: context);
        if (answered != null)
            yield return new global::app.data.@this("result", answered.Success ? answered.Peek() : answered.Error, context: context);
    }
}
