namespace app.type.item.kind.json;

/// <summary>
/// json content, unread — a json file's characters. Its characters already are json, so a json document takes them as
/// they are (pasted, never quoted); in any other format it is a source's own characters. Made by the json kind
/// (<see cref="@this.Make"/>), which is its format. Asked whether it is empty, it is what it stands for: <c>{}</c> and
/// <c>[]</c> are empty.
/// </summary>
public class source : global::app.type.item.source
{
    /// <summary>json content holding <paramref name="raw"/> (characters, or their UTF-8 bytes), of
    /// <paramref name="type"/>, read by <paramref name="json"/>, off <paramref name="origin"/> when it came from a file.</summary>
    public source(object raw, global::app.type.@this type, @this json, global::app.type.item.path.@this? origin = null)
        : base(raw, type, origin: origin, format: json) { }

    // What the characters stand for, once opened — kept, so a truth asked first and a navigation after parse once. It
    // holds no context: every asker uses it with their own.
    private global::app.type.item.@this? _opened;

    // the characters as they are: bytes are their UTF-8
    private string Characters => Raw as string ?? System.Text.Encoding.UTF8.GetString((byte[])Raw);

    internal override global::app.type.item.@this? Parse(global::app.actor.context.@this ctx)
        => _opened ??= base.Parse(ctx);

    /// <summary>Whether it holds anything is what it stands for's answer — opening it is a use (<c>{}</c>, <c>[]</c>,
    /// <c>""</c>, <c>0</c>, <c>false</c> and <c>null</c> are empty). Characters that are no json answer why.</summary>
    public override async System.Threading.Tasks.Task<bool> AsBooleanAsync(global::app.actor.context.@this context)
    {
        global::app.type.item.@this? opened;
        try { opened = Parse(context); }
        catch (System.Text.Json.JsonException shape)
        {
            throw new global::app.error.AppException(Unread(shape, new global::app.data.@this("", this, context: context)));
        }
        return opened is null ? IsTruthy() : await opened.AsBooleanAsync(context);
    }

    protected internal override System.Threading.Tasks.ValueTask Write(Writer writer)
    {
        writer.Raw(Characters);
        return default;
    }

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.item.wire.kind.plang.Writer writer)
    {
        writer.Raw(Characters);
        return default;
    }
}
