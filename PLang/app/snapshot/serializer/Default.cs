using System.Reflection;
using app.channel.serializer;
using app.data;

namespace app.snapshot.serializer;

/// <summary>
/// The snapshot's leaf-serializer (Rule #9) — the snapshot owns its own wire
/// shape. It walks its sections + entries and emits through the format-agnostic
/// <see cref="IWriter"/>; the channel decides JSON / protobuf / … The snapshot
/// never names a format. A value it can't render structurally (an error) IS a
/// plang item and rides as itself — the writer dispatches its own Write —
/// composition, not reaching in.
/// </summary>
public static class Default
{
    public static void Write(global::app.snapshot.@this snap, IWriter writer)
        => writer.Value(Render(snap));

    /// <summary>A snapshot node renders as an object: its entries then its sub-sections.
    /// Entries born self-describing Data in the snapshot's own context (born-with-context).</summary>
    private static object? Render(global::app.snapshot.@this node)
    {
        var ctx = node.Context;
        var obj = new global::app.type.item.dict.@this(ctx);
        foreach (var (key, value) in node.Entries)
            obj.Set(new data.@this(key, Render(value, ctx), context: ctx));
        foreach (var (name, section) in node.Sections)
            obj.Set(new data.@this(name, Render(section), context: ctx));
        return obj; // native dict → `{}` on the wire
    }

    /// <summary>One entry value → a wire-tree node. Context borns nested Data/dicts.</summary>
    private static object? Render(object? value, global::app.actor.context.@this? ctx)
    {
        switch (value)
        {
            case null:
                return null;
            case global::app.snapshot.@this section:
                return Render(section);
            case data.@this:
                return value; // a Data rides as its own record
            case string or bool or int or long or double or float or decimal
                or System.DateTime or System.DateTimeOffset or System.Guid:
                return value;
            case global::app.error.IError err:
                return err; // error is an item — it renders itself
            case System.Collections.IDictionary dict:
            {
                var obj = new global::app.type.item.dict.@this(ctx);
                foreach (System.Collections.DictionaryEntry e in dict)
                    obj.Set(new data.@this(e.Key?.ToString() ?? "", Render(e.Value, ctx), context: ctx));
                return obj;
            }
            case List<data.@this> records:
                return new List<object?>(records); // array of full records (keeps type)
            case System.Collections.IEnumerable seq:
            {
                var arr = new List<object?>();
                foreach (var item in seq) arr.Add(Render(item, ctx));
                return arr;
            }
            default:
                // A plain domain record (Provider Registration / DefaultOverride):
                // reflect its public properties into an object. camelCase the keys
                // so the Io read (case-insensitive) rebinds the record's ctor.
                var node = new global::app.type.item.dict.@this(ctx);
                foreach (var p in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.GetIndexParameters().Length > 0) continue;
                    var name = char.ToLowerInvariant(p.Name[0]) + p.Name[1..];
                    node.Set(new data.@this(name, Render(p.GetValue(value), ctx), context: ctx));
                }
                return node;
        }
    }
}
