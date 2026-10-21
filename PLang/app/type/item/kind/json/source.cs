namespace app.type.item.kind.json;

/// <summary>
/// json content, unread — a json file's characters. Its characters already are json, so a json document takes them as
/// they are (pasted, never quoted); in any other format it is a source's own characters. Made by the json kind
/// (<see cref="@this.Load"/>), which is its format.
/// </summary>
public class source : global::app.type.item.source
{
    /// <summary>json content holding <paramref name="raw"/> (characters, or their UTF-8 bytes), of
    /// <paramref name="type"/>, read by <paramref name="json"/>, off <paramref name="origin"/> when it came from a file.</summary>
    public source(object raw, global::app.type.@this type, @this json, global::app.type.item.path.@this? origin = null)
        : base(raw, type, origin: origin, format: json) { }

    // the characters as they are: bytes are their UTF-8
    private string Characters => Raw as string ?? System.Text.Encoding.UTF8.GetString((byte[])Raw);

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
