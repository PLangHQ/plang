using Data = global::app.data.@this;

namespace app.module.llm.type.limit;

/// <summary>
/// PLang <c>limit</c> value — how far a query may go: <c>{token: 16000, tool: 10, retry: 0}</c>. Each member left out
/// keeps its default, which lives here once. The llm module's own.
/// </summary>
[global::app.Attributes.PlangType("limit")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.type.item.IDefault<@this>
{
    public static string Example => "{token: 16000, tool: 10, retry: 0}";
    public static string Description => "How far a query may go: the tokens in its answer, the tool calls it makes, the retries of an answer that fails validation.";
    public static string Shape => "object";

    /// <summary>The most tokens in the answer — <c>%!llm.query.setting.limit.token%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Token { get; }

    /// <summary>The most tool calls before the query stops — <c>%!llm.query.setting.limit.tool%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Tool { get; }

    /// <summary>How many times an answer that fails validation is asked again — <c>%!llm.query.setting.limit.retry%</c>.</summary>
    [Out, Store] public global::app.type.item.number.@this Retry { get; }

    /// <summary>The defaults: 16000 tokens, ten tool calls, no retry.</summary>
    public @this() : this(16000, 10, 0) { }

    /// <summary>A query that says nothing of its limit takes the defaults.</summary>
    public static @this Default => new();

    internal @this(global::app.type.item.number.@this token, global::app.type.item.number.@this tool, global::app.type.item.number.@this retry)
    {
        Token = token;
        Tool = tool;
        Retry = retry;
    }

    public override bool IsLeaf => false;
    /// <summary>A limit is made from a dict of its members — any left out keeps its default; a member that is no
    /// member of a limit, or a value it can't take, declines with why.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, Data data)
    {
        if (raw is @this limit) return limit;
        if (raw is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error("a limit is {token, tool, retry}", "LimitInvalid", 400));
            return null;
        }
        var fresh = new @this();
        global::app.type.item.number.@this token = fresh.Token;
        global::app.type.item.number.@this tool = fresh.Tool;
        global::app.type.item.number.@this retry = fresh.Retry;
        foreach (var entry in dict.Entries(data.Context!))
            switch (entry.Name.ToLowerInvariant())
            {
                case "token" when entry.Peek() is global::app.type.item.number.@this t: token = t; break;
                case "tool" when entry.Peek() is global::app.type.item.number.@this t: tool = t; break;
                case "retry" when entry.Peek() is global::app.type.item.number.@this r: retry = r; break;
                default:
                    data.Fail(new global::app.error.Error(
                        $"a limit's members are token, tool and retry (each a number) — not {entry.Name}", "LimitInvalid", 400));
                    return null;
            }
        return new @this(token, tool, retry);
    }

    /// <summary>Writes itself as its dict.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("token"); Token.Write(writer);
        writer.Name("tool"); Tool.Write(writer);
        writer.Name("retry"); Retry.Write(writer);
        writer.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
