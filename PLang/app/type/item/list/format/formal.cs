namespace app.type.item.list.format;

/// <summary>
/// A list's formal form. A list whose every element is a NAMED Data is a list of arguments (goal.call's
/// and ui.render's <c>Parameter</c>): each is a row, written like a property —
/// <c>{kind: text = "x", path: item = %path%}</c>. Any other list is its elements: <c>[a, b]</c>. An
/// instance, held directly by <see cref="app.type.item.list.@this"/>'s own format map.
/// </summary>
public sealed class formal : global::app.channel.serializer.IOutput
{
    public async System.Threading.Tasks.ValueTask Output(
        global::app.type.item.@this value, global::app.channel.serializer.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        var list = (global::app.type.item.list.@this)value;
        var writerFormal = (global::app.channel.serializer.formal.Writer)writer;
        var items = list.Items(context!).ToList();
        if (items.Count > 0 && items.All(d => !string.IsNullOrEmpty(d.Name)))
        {
            writerFormal.BeginRows();
            foreach (var arg in items)
            {
                writerFormal.Row(arg.Name, arg.Type.Kind is { } kind ? $"{arg.Type.Name}<{kind.Name}>" : arg.Type.Name);
                await arg.Output(writer, mode, context);
            }
            writerFormal.EndRows();
            return;
        }
        writer.BeginArray(items.Count);
        foreach (var element in items) await element.Output(writer, mode, context);
        writer.EndArray();
    }
}
