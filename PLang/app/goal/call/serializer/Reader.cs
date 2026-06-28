namespace app.goal.call.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) reader for <c>goal.call</c> — a call
/// descriptor (<see cref="global::app.goal.GoalCall"/>, itself an <c>item.@this</c>). Its
/// dotted type name can't be derived from a <c>serializer</c> namespace, so it NAMES itself
/// (<see cref="app.type.reader.INamedTypeReader"/>). goal.call still rides STJ — its nested
/// Data params deserialize through the Wire — so the reader builds the read options from its
/// own <c>ReadContext</c> (no passed options). Removing this STJ ride is the goal.call follow-on.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader, global::app.type.reader.INamedTypeReader
{
    public string TypeName => "goal.call";
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var options = global::app.channel.serializer.json.Options.Read(ctx.Context);
        options.Converters.Add(new global::app.data.Wire(ctx.View, context: ctx.Context, template: ctx.Template));
        return (global::app.type.item.@this?)System.Text.Json.JsonSerializer
                   .Deserialize<global::app.goal.GoalCall>(reader.RawValue(), options)
               ?? new global::app.type.@null.@this("goal.call", kind);
    }
}
