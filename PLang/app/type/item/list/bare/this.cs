namespace app.type.item.list.bare;

/// <summary>
/// A list written bare — each element in its own shape, one after the other, the array its reader reads (a goal's
/// steps, a step's actions, a goal's tags and sub-goals, a frame's errors, a variable's hops, a node's warnings). A
/// program's own typed lists (<c>list&lt;path&gt;</c>) are not bare: each element is written with what it is.
/// </summary>
public class @this<T> : global::app.type.item.list.@this<T>
    where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
{
    /// <summary>An empty list, born holding <paramref name="type"/>.</summary>
    protected @this(global::app.type.@this type) : base(type) { }

    /// <summary>A program node's list over <paramref name="backing"/> — shared across runs, so it stores no context.</summary>
    protected @this(System.Collections.Generic.List<object?> backing, global::app.type.@this type) : base(backing, type) { }

    /// <summary>A list of <paramref name="values"/>, in order.</summary>
    protected @this(System.Collections.Generic.IEnumerable<global::app.type.item.@this> values, global::app.type.@this type) : base(values, type) { }

    /// <summary>Adopts another list's rows.</summary>
    protected @this(global::app.type.item.list.@this source, global::app.type.@this type) : base(source, type) { }

    /// <summary>Written as its elements, each in its own shape — in any format, bare json too.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var items = Items().ToList();
        writer.BeginArray(items.Count);
        foreach (var item in items) await item.Output(writer);
        writer.EndArray();
    }
}
