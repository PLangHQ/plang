namespace app.goal.serializer;

/// <summary>
/// A goal's key read back (<c>{goal, key}</c>): the record the snapshot wrote for a goal it found, <c>{name, pr,
/// hash}</c>, read as a dict whose every member the key names is made by its own type (<c>pr</c> a file, unread). The
/// goal's <c>Select</c> finds the live goal from it, the first time it is used.
/// </summary>
public sealed class Key : global::app.type.reader.ITypeReader
{
    public string Kind => "key";

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        var record = (global::app.type.item.dict.@this)new global::app.type.item.dict.serializer.Reader().Read(ref reader, null, ctx);
        foreach (var entry in global::app.type.item.kind.reflection.Tagged.PropertiesFor(typeof(global::app.goal.@this), global::app.View.Snapshot))
        {
            var type = entry.Property.PropertyType;
            if (!typeof(global::app.type.item.@this).IsAssignableFrom(type) || record.Get(entry.WireName, context) is not { } member) continue;
            record.Set(entry.WireName, context.App.type.list[type].Make(member.Peek(), context));
        }
        return record;
    }
}
