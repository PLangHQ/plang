using Data = global::app.data.@this;

namespace app.module.llm.type.conversation;

/// <summary>
/// PLang <c>conversation</c> value — how a query stands to the conversation before it: <c>{continue: true}</c>
/// carries the earlier messages forward. Each member left out keeps its default, which lives here once: a query
/// that says nothing starts afresh. The llm module's own.
/// </summary>
[global::app.Attributes.PlangType("conversation")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.type.item.IDefault<@this>
{
    public static string Example => "{continue: true}";
    public static string Description => "How a query stands to the conversation before it: whether it continues it.";
    public static string Shape => "object";

    /// <summary>Whether the query continues the conversation before it — <c>%!llm.query.setting.conversation.continue%</c>.</summary>
    [Out, Store] public global::app.type.item.@bool.@this Continue { get; }

    /// <summary>The default: a fresh conversation.</summary>
    public @this() : this(false) { }

    /// <summary>A query that says nothing of its conversation starts afresh.</summary>
    public static @this Default => new();

    internal @this(global::app.type.item.@bool.@this @continue) => Continue = @continue;

    public override bool IsLeaf => false;

    /// <summary>As a step writes it — the catalog shows the default this way.</summary>
    public override string ToString() => $"{{continue: {(Continue.Value ? "true" : "false")}}}";

    /// <summary>A conversation is made from a dict of its members — any left out keeps its default; a member that
    /// is no member of a conversation declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this conversation) return conversation;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a conversation is {continue}", "ConversationInvalid", 400));
            return null;
        }
        global::app.type.item.@bool.@this @continue = new @this().Continue;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "continue" when entry.Peek() is global::app.type.item.@bool.@this c: @continue = c; break;
                default:
                    data.Fail(new global::app.error.Error(
                        $"a conversation's member is continue (true or false) — not {entry.Name}", "ConversationInvalid", 400));
                    return null;
            }
        return new @this(@continue);
    }

    /// <summary>Writes itself as its dict.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("continue"); writer.Bool(Continue.Value);
        writer.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
