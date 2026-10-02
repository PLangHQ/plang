using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.list;

/// <summary>
/// A query made from its dict: its clauses, in the order written — one clause is a list of one. It runs them in
/// SQL's order (where, group, distinct, order) unless told to run them as written, and writes itself as the dict.
/// A query value is always one of these.
/// </summary>
public sealed class @this : query.@this
{
    // the clauses, in the order the query writes them
    private readonly IReadOnlyList<query.@this> _clause;

    private @this(IReadOnlyList<query.@this> clause) => _clause = clause;

    /// <summary>The query a dict is: each key a clause, made from what the key holds. A key that is no clause, or a
    /// clause that doesn't read, declines with why on <paramref name="data"/>, naming the clause.</summary>
    internal static @this? Create(global::app.type.item.dict.@this dict, Data data)
    {
        var context = data.Context!;
        var clauses = new System.Collections.Generic.List<query.@this>();
        foreach (var entry in dict.Entries(context))
        {
            if (!Known.TryGetValue(entry.Name, out var make))
            {
                data.Fail(new global::app.error.Error(
                    $"'{entry.Name}' is no part of a query — its parts are {Names}", "QueryInvalid", 400));
                return null;
            }
            if (make(entry, data, context) is not { } made)
            {
                // the clause's why, said as the clause's: "where: …"
                var why = data.Error!;
                data.Fail(new global::app.error.Error($"{entry.Name}: {why.Message}", why.Key, why.Status) { list = [why] });
                return null;
            }
            clauses.Add(made);
        }
        if (clauses.Count == 0)
        {
            data.Fail(new global::app.error.Error($"a query names at least one part: {Names}", "QueryInvalid", 400));
            return null;
        }
        return new @this(clauses);
    }

    /// <summary>A query value reads as <c>query</c>, the type itself.</summary>
    protected internal override global::app.type.@this Type => new(typeof(query.@this));

    /// <summary>Its clauses in SQL's order (where, group, distinct, order), or as written, each applying itself to
    /// what the one before answered. A new list; <paramref name="list"/> is unchanged.</summary>
    public override System.Threading.Tasks.Task<Data> Run(List list, execution order, global::app.actor.context.@this context)
    {
        var clauses = order == execution.sql ? _clause.OrderBy(c => c.Rank).ToList() : _clause;
        return clauses[0].Apply(list, clauses.Skip(1).ToList(), context);
    }

    /// <summary>A list of clauses has no place of its own in SQL's order — <c>Rank</c> is a clause's, and the list
    /// only orders by it.</summary>
    internal override int Rank => 0;

    /// <summary>Its clauses as written, then <paramref name="rest"/>.</summary>
    internal override System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<query.@this> rest,
        global::app.actor.context.@this context)
        => _clause[0].Apply(rows, _clause.Skip(1).Concat(rest).ToList(), context);

    /// <summary>Writes itself as its dict: each clause under its name, in the order written.</summary>
    public override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        foreach (var clause in _clause)
        {
            writer.Name(clause.Name);
            await clause.Output(writer, mode, context);
        }
        writer.EndObject();
    }
}
