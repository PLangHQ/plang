using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query;

/// <summary>
/// PLang <c>query</c> value — what to take from a list, said as one sentence: <c>where</c> (a field compared to a
/// value, joined by <c>and</c>/<c>or</c>), <c>group</c>, <c>distinct</c>, <c>order</c>. Written as a dict:
/// <c>{where: {field: "age", op: "&gt;", value: 20}, order: "age"}</c>.
///
/// <para>A where alone is a query, and so is each clause: the clauses are the query's kinds
/// (<c>{query, kind: where}</c>), and a query made from a dict is the list of them (<c>query.list</c>), run in SQL's
/// order or as written. Each clause applies itself — the list and its items do the work (<c>list.Where</c>,
/// <c>list.Group</c>, <c>list.Unique</c>, <c>list.Sort</c>). A query never changes the list it runs on: it answers a
/// new one. The clauses are the query's own: written only inside its dict, never navigated as values.</para>
///
/// <para>The list module's own: it lives under the module that runs it (<c>list.query</c>), as crypto's
/// <c>hash</c> does.</para>
/// </summary>
[global::app.Attributes.PlangType("query"), global::app.Attributes.Kinds]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "{where: {field: \"age\", op: \">\", value: 20}, order: \"age\"}";
    public static string Description => "What to take from a list: where (fields compared, joined by and/or), group, distinct, order.";
    public static string Shape => "object";

    /// <summary>A structure, never a single-token leaf.</summary>
    public override bool IsLeaf => false;

    /// <summary>The clause's name, as the query writes it — where it lives: <c>where</c>, <c>group</c>,
    /// <c>distinct</c>, <c>order</c>.</summary>
    public string Name => NameOf(GetType());

    // a clause class's name: the last segment of its namespace (query/where/ → where)
    private static new string NameOf(System.Type clause) => clause.Namespace![(clause.Namespace!.LastIndexOf('.') + 1)..];

    /// <summary>A query reads as <c>query</c>, a clause as its kind (<c>{query, kind: where}</c>).</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this), Name);

    /// <summary>How a clause is made from what the query's dict holds under its name: the clause, or null with why
    /// on <c>data</c>.</summary>
    internal delegate @this? Maker(Data clause, Data data, global::app.actor.context.@this context);

    /// <summary>The clauses a query has, each under its name — the classes under the query with their own static
    /// <c>Create(clause, data, context)</c>, found once.</summary>
    internal static IReadOnlyDictionary<string, Maker> Known => _known.Value;

    private static readonly System.Lazy<IReadOnlyDictionary<string, Maker>> _known = new(() =>
        typeof(@this).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(@this).IsAssignableFrom(t))
            .Select(t => (t, create: t.GetMethod("Create", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                [typeof(Data), typeof(Data), typeof(global::app.actor.context.@this)])))
            .Where(c => c.create != null)
            .ToDictionary(c => NameOf(c.t), c => (Maker)System.Delegate.CreateDelegate(typeof(Maker), c.create!),
                StringComparer.OrdinalIgnoreCase));

    /// <summary>The clauses' names, for a refusal that says what a query takes: <c>distinct, group, order, where</c>.</summary>
    internal static string Names => string.Join(", ", Known.Keys.Order(StringComparer.Ordinal));

    /// <summary>A query is made from a dict: the list of its clauses, in the order written — one clause is a list of
    /// one. A key that is no clause, or a clause that doesn't read, declines with why, naming the clause.</summary>
    public static @this? Create(object? query, global::app.type.@this? declared, Data data)
    {
        if (query is @this already) return already;
        if (query is global::app.type.item.dict.@this dict) return list.@this.Create(dict, data);
        data.Fail(new global::app.error.Error(
            $"a query is a dict of its parts — {{{Names}}} — not {(query as global::app.type.item.@this)?.Type.Name ?? query?.GetType().Name ?? "nothing"}",
            "QueryInvalid", 400));
        return null;
    }

    /// <summary>What this query answers for <paramref name="list"/> — a new list; <paramref name="list"/> is
    /// unchanged. A clause applies itself; the list of them orders them first (<see cref="list.@this"/>).</summary>
    public virtual System.Threading.Tasks.Task<Data> Run(List list, execution order, global::app.actor.context.@this context)
        => Apply(list, [], context);

    /// <summary>A clause's place in SQL's order: where, group, distinct, order. The list of clauses orders by it.</summary>
    internal abstract int Rank { get; }

    /// <summary>What this answers for <paramref name="rows"/>, with <paramref name="rest"/> applied to it after. A new
    /// list; <paramref name="rows"/> is unchanged.</summary>
    internal abstract System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<@this> rest,
        global::app.actor.context.@this context);

    /// <summary>The clauses after this one, applied to what it <paramref name="answered"/>; an error is the answer,
    /// named by this clause.</summary>
    protected async System.Threading.Tasks.Task<Data> Next(Data answered, IReadOnlyList<@this> rest,
        global::app.actor.context.@this context)
    {
        if (!answered.Success) return Named(answered, context);
        if (rest.Count == 0) return answered;
        return await answered.Use<List>(rows => rest[0].Apply(rows, rest.Skip(1).ToList(), context));
    }

    /// <summary>A failure, said as this clause's: <c>where: No item has a field 'agee' …</c>.</summary>
    protected Data Named(Data failed, global::app.actor.context.@this context)
    {
        if (failed.Success || failed.Error is not { } error) return failed;
        return context.Error(new global::app.error.Error($"{Name}: {error.Message}", error.Key, error.Status) { list = [error] });
    }
}
