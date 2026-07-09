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

    // Index-descend is only defined on an IList (a bare sequence has no positional access).
    public override (bool, object?) Descend(object obj, string key, global::app.actor.context.@this ctx)
        => obj is System.Collections.IList l && int.TryParse(key, out var i) && i >= 0 && i < l.Count
            ? (true, l[i]) : (false, null);

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
