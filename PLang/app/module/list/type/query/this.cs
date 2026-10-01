using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query;

/// <summary>
/// PLang <c>query</c> value — what to take from a list, said as one sentence: <c>where</c> (a field compared to a
/// value, joined by <c>and</c>/<c>or</c>), <c>group</c>, <c>distinct</c>, <c>order</c>. Written as a dict:
/// <c>{where: {field: "age", op: "&gt;", value: 20}, group: "name", distinct: true, order: "age"}</c>.
///
/// <para>The list module's own: it lives under the module that runs it (<c>list.query</c>), as crypto's
/// <c>hash</c> does. Each part applies itself — the list and its items do the work (<c>list.Where</c>,
/// <c>list.Group</c>, <c>list.Unique</c>, <c>list.Sort</c>); the query only puts the parts in order. A query never
/// changes the list it runs on: it answers a new one.</para>
/// </summary>
[global::app.Attributes.PlangType("query")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{where: {field: \"age\", op: \">\", value: 20}, order: \"age\"}";
    public static string Description => "What to take from a list: where (fields compared, joined by and/or), group, distinct, order.";
    public static string Shape => "object";

    // the parts, in the order the query writes them
    private readonly IReadOnlyList<part.@this> _part;

    private @this(IReadOnlyList<part.@this> part) => _part = part;

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>A query is made from a dict: each key a part, in the order written. A key that is no part, or a
    /// part that doesn't read, declines with why, naming the part.</summary>
    public static @this? Create(object? query, global::app.type.@this? declared, Data data)
    {
        if (query is @this already) return already;
        if (query is not global::app.type.item.dict.@this dict)
        {
            data.Fail(new global::app.error.Error(
                $"a query is a dict of its parts — {{{part.@this.Names}}} — not {(query as global::app.type.item.@this)?.Type.Name ?? query?.GetType().Name ?? "nothing"}",
                "QueryInvalid", 400));
            return null;
        }
        var context = data.Context!;
        var parts = new List<part.@this>();
        // each key names a part the query has, made from what the key holds; one that doesn't read says why on data
        foreach (var entry in dict.Entries(context))
        {
            if (!part.@this.Known.TryGetValue(entry.Name, out var make))
            {
                data.Fail(new global::app.error.Error(
                    $"'{entry.Name}' is no part of a query — its parts are {part.@this.Names}", "QueryInvalid", 400));
                return null;
            }
            if (make(entry, data, context) is not { } made)
            {
                // the part's why, said as the part's: "where: …"
                var why = data.Error!;
                data.Fail(new global::app.error.Error($"{entry.Name}: {why.Message}", why.Key, why.Status) { list = [why] });
                return null;
            }
            parts.Add(made);
        }
        if (parts.Count == 0)
        {
            data.Fail(new global::app.error.Error($"a query names at least one part: {part.@this.Names}", "QueryInvalid", 400));
            return null;
        }
        return new @this(parts);
    }

    /// <summary>What this query answers for <paramref name="list"/>: its parts in SQL's order (where, group,
    /// distinct, order), or as written, each applying itself to what the one before answered. A new list;
    /// <paramref name="list"/> is unchanged.</summary>
    public System.Threading.Tasks.Task<Data> Run(List list, execution order, global::app.actor.context.@this context)
    {
        var parts = order == execution.sql ? _part.OrderBy(p => p.Rank).ToList() : _part;
        return parts[0].Apply(list, parts.Skip(1).ToList(), context);
    }

    /// <summary>Writes itself as its dict: each part under its name, in the order written.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        foreach (var part in _part)
        {
            writer.Name(part.Name);
            await part.Output(writer, mode, context!);
        }
        writer.EndObject();
    }
}
