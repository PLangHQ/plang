namespace app.type.item.kind.list;

/// <summary>
/// The list kind — a raw CLR <see cref="System.Collections.IList"/> host (a POCO's
/// <c>List&lt;Step&gt;</c>, <c>StepActions</c>, …). Owns index-descend (<c>goal.Steps[0]</c>),
/// element-enumeration (foreach), and array-Output. Claims <see cref="System.Collections.IList"/>
/// by assignable match, so any concrete list resolves here (exact ClrForm wins first, so
/// <c>JsonElement</c> stays json).
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    public @this(global::app.actor.context.@this? context = null) : base("list", context) { }

    // Claims IEnumerable (not just IList) so ANY sequence — HashSet, a LINQ enumerable — resolves
    // here and enumerates, instead of falling to the * kind and reflecting its C# properties. The
    // door prefers more-derived claims, so IDictionary → dict wins over IEnumerable → list.
    public override System.Type? ClrForm => typeof(System.Collections.IEnumerable);

    // Index (`[0]`) → the element at that position. A member (`.Count`, `.Length`) → a real
    // property on the host's class, which the * kind reflects — the host declares it, the grammar
    // said "named, not positional". Positional access spans a non-generic IList (arrays, List<T>)
    // AND a generic-only IList<T> (a domain collection like goal.Steps) — Positioned reaches both.
    public override (bool, object?) Descend(object obj, string key, bool isIndex, global::app.actor.context.@this ctx)
        => isIndex
            ? int.TryParse(key, out var i) && i >= 0 && i < Length(obj)
                ? (true, At(obj, i)) : (false, null)
            : ctx.App.Type.Kind["*"].Descend(obj, key, isIndex, ctx);

    // Index write (`[0] = step`) → replace the element in place on the sequence host (its identity
    // holds — it mutates, no rebind). A named member → the * kind (a settable property on the
    // sequence class), mirror of Descend. The value arrives concrete (clr.Set opened any Data
    // door before reaching a host), so it rides straight into the slot.
    public override global::System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(
        object host, string key, bool isIndex, object? value, global::app.actor.context.@this ctx)
    {
        if (!isIndex) return ctx.App.Type.Kind["*"].Set(host, key, isIndex, value, ctx);
        var n = Length(host);
        if (!int.TryParse(key, out var i) || i < 0 || i >= n)
            throw new System.NotSupportedException(
                $"cannot set [{key}] on a sequence of {n} — index out of range or not numeric");
        if (host is System.Collections.IList il) il[i] = value;
        else Indexer(host).SetValue(host, value, new object[] { i });
        return new(new global::app.type.clr.@this(host, ctx));
    }

    // Positional access over any sequence host — a non-generic IList (array, List<T>) is direct;
    // a generic-only IList<T> (goal.Steps) answers through its reflected Count + Item indexer.
    private int Length(object host)
        => host is System.Collections.IList l ? l.Count
           : host.GetType().GetProperty("Count")?.GetValue(host) is int c ? c : -1;

    private object? At(object host, int i)
        => host is System.Collections.IList l ? l[i] : Indexer(host).GetValue(host, new object[] { i });

    private System.Reflection.PropertyInfo Indexer(object host)
        => host.GetType().GetProperty("Item")
           ?? throw new System.NotSupportedException($"'{host.GetType().Name}' has no indexer");

    public override System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
    {
        foreach (var el in (System.Collections.IEnumerable)obj)
            yield return Data("", el, null, ctx);
    }

    // A collection writes as an array of self-writes — each element through its own kind.
    public override async global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
    {
        writer.BeginArray(-1);
        foreach (var el in (System.Collections.IEnumerable)obj) if (el != null) await WriteReflected(writer, el, mode, ctx!);
        writer.EndArray();
    }
}
