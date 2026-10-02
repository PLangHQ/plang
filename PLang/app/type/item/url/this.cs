namespace app.type.item.url;

/// <summary>
/// PLang <c>url</c> value — the remote-scheme REFERENCE: a location plus lazily
/// fetched content and metadata. Same shape as <c>file</c> — the content
/// materialises (and the holding <c>Data</c> narrows) on first examination;
/// the location surface (<c>!url!path</c>, <c>!url!host</c>) never fetches.
/// The scheme know-how (consent gate, redirects, signing) stays on the
/// composed <c>HttpPath</c>.
/// </summary>
[global::app.Attributes.PlangType("url")]
public sealed class @this : global::app.type.item.content.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "string";
    /// <summary>A url is made from a path.</summary>
    public static bool Takes(global::app.type.@this other) => other.Is("path");

    /// <summary>A url reference at <paramref name="path"/>, born with <paramref name="template"/>.</summary>
    public @this(global::app.type.item.path.@this path, global::app.actor.context.@this context, global::app.type.item.template.kind.@this? template = null)
        : base(path, context, template) { }

    /// <summary>A url is made from its path, as <paramref name="declared"/> says: the reference to what is there,
    /// nothing fetched, born with the declaration's template. Anything else declines.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data) => raw switch
    {
        @this self => self,
        global::app.type.item.path.@this path => new @this(path, data.Context, (declared ?? data.Type).Template),
        _ => null,
    };

    /// <summary>The remote host — location surface, never fetches.</summary>
    public string Host =>
        System.Uri.TryCreate(Path.Absolute, System.UriKind.Absolute, out var u) ? u.Host : "";

    /// <summary>The fetched content's format: the response's Content-Type rules; else the url's extension is
    /// the hint (.json → json); else a typeless web response is text, not raw bytes.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.type.kind.@this> Format(
        global::app.data.@this read, global::app.actor.context.@this context)
    {
        var contentType = await read.Properties.Get<string>("contentType");
        var mime = !string.IsNullOrEmpty(contentType) ? contentType
            : Path.Extension.IsTruthy() ? Path.MimeType(context)
            : "text/plain";
        return context.App.type.list.Mime(mime);
    }
}
