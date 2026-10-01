using Data = global::app.data.@this;

namespace app.module.llm.type.conversation;

/// <summary>
/// PLang <c>conversation</c> value — how a query stands to the conversation before it: <c>{continue: %answer%}</c>
/// continues the conversation an earlier query answered, its messages carried on that answer. Left out, a query
/// starts afresh. The llm module's own.
/// </summary>
[global::app.Attributes.PlangType("conversation")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.type.item.IDefault<@this>
{
    public static string Example => "{continue: %answer%}";
    public static string Description => "How a query stands to the conversation before it: the earlier llm response it continues.";
    public static string Shape => "object";

    /// <summary>The llm response whose conversation the query continues — its <c>Messages</c> and <c>Schema</c>
    /// properties; null for a fresh conversation.</summary>
    [Out, Store] public Data? Continue { get; }

    /// <summary>The default: a fresh conversation.</summary>
    public @this() : this(null) { }

    /// <summary>A query that says nothing of its conversation starts afresh.</summary>
    public static @this Default => new();

    internal @this(Data? @continue) => Continue = @continue;

    public override bool IsLeaf => false;
    /// <summary>A conversation is made from a dict of its members; a member that is no member of a conversation, or
    /// a continue that names no response, declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this conversation) return conversation;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error(
                $"a conversation is {{continue: %answer%}} — not a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name}", "ConversationInvalid", 400));
            return null;
        }
        Data? @continue = null;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "continue" when entry.Peek() is global::app.type.item.@bool.@this:
                    data.Fail(new global::app.error.Error(
                        "continue which conversation? Name the llm response it continues: {continue: %answer%}", "ConversationInvalid", 400));
                    return null;
                case "continue":
                    @continue = entry.Peek() is global::app.type.item.@null.@this ? null : entry;
                    break;
                default:
                    data.Fail(new global::app.error.Error(
                        $"a conversation's member is continue (the llm response it continues) — not {entry.Name}", "ConversationInvalid", 400));
                    return null;
            }
        return new @this(@continue);
    }

    /// <summary>Writes itself as its dict: the response it continues, as that response writes.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        if (Continue != null)
        {
            writer.Name("continue");
            await (await Continue.Value()).Output(writer, mode, context);
        }
        writer.EndObject();
    }
}
