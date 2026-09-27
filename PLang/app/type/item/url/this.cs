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
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "https://example.com/data.json";
    public static string Description => "A web address; its content is fetched when it is used.";
    public static string Shape => "string";


    /// <summary>The location facet (an <c>HttpPath</c> — owns consent + fetch).</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.item.path.@this Path { get; }

    private byte[]? _bytes;
    private string? _contentType;

    // The canonical content kind, worked out at creation through the creator's format
    // registry and kept as a fact.
    private readonly global::app.type.kind.@this? _kind;

    /// <summary>A url reference at <paramref name="path"/>; <paramref name="context"/> is the
    /// creator's, used once to name the kind and not kept.</summary>
    public @this(global::app.type.item.path.@this path, global::app.actor.context.@this context)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        _kind = path.Kind(context) is { IsNull: false, kind: { IsEmpty: false } k } ? k : null;
        // Born from a path — inject its type into this value's history (`is path` from the chain).
        history.Add(path);
    }

    /// <summary>The remote host — location surface, never fetches.</summary>
    public string Host =>
        System.Uri.TryCreate(Path.Absolute, System.UriKind.Absolute, out var u) ? u.Host : "";

    public bool IsLoaded => _bytes != null;

    /// <summary>A url's entity: name "url", kind = the canonical kind named at
    /// creation — location metadata, never fetches.</summary>
    protected internal override global::app.type.@this Type =>
        new global::app.type.@this("url", typeof(@this)) { kind = _kind };

    /// <summary>
    /// The value door — fetch + parse through the file channel (mime stamps the
    /// content's {type, kind}; the consent gate rides on <c>Path.ReadBytes</c>)
    /// and answer with the CONTENT's own instance, this url stamped as its
    /// prior. Single storage: the fetched bytes are released after the parse.
    /// <para>Owns the one consent-gated GET (idempotent via <c>_bytes</c>) —
    /// <c>.Value()</c> is the one materialize door; the sync <c>Bytes</c> getter
    /// serves the cached bytes.</para>
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        // The sample: one consent-gated fetch per value per program run — the
        // channel stamps + parses FROM the sample, never a second GET.
        // URL authors its own failures (fetch stories) onto the data binding.
        byte[] bytes;
        if (_bytes != null) bytes = _bytes;
        else
        {
            var readBytes = await Path.ReadBytes(data.Context);
            // The fetch error rides through WHOLE — its key, message and inner exception —
            // instead of being flattened into a bare-string HttpRequestException.
            if (!readBytes.Success) { data.Fail(readBytes.Error!); return Absent; }
            _contentType = await readBytes.Properties.Get<string>("contentType");
            var bin = await readBytes.Value();
            bytes = _bytes = bin?.Value ?? System.Array.Empty<byte>();
        }
        // The content's format by precedence: the response Content-Type rules; else the URL extension
        // is the hint (.json → json); else a typeless web response is text, not raw bytes. The format
        // decodes it, with the asker's context.
        var context = data.Context;
        var mime = !string.IsNullOrEmpty(_contentType) ? _contentType
            : !string.IsNullOrEmpty(Path.Extension) ? Path.MimeType(context)
            : "text/plain";
        // (plang's own content from another actor answers only signed — its format refuses it otherwise.)
        var read = await context.App.type.list.Mime(mime, context).kind.Decode(bytes, context);
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        _ = await read.Value();
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        var answer = read.Item;
        if (answer == null || ReferenceEquals(answer, this)) return this;
        answer.history.Add(this);
        return answer;
    }


    public string ContentText() => System.Text.Encoding.UTF8.GetString(_bytes ?? System.Array.Empty<byte>());
    public byte[] Bytes => _bytes ?? System.Array.Empty<byte>();

    /// <summary>Drop the in-memory content — the narrow's single-storage step.</summary>
    internal void Release() => _bytes = null;

    public override string ToString() => Path.ToString();

    /// <summary>
    /// The url renders itself as its fetched CONTENT (same bare-scalar contract
    /// as file), pre-materialised by the serialize chokepoint's <c>Load()</c>
    /// pass. An unfetched url renders its location — write-out alone is not
    /// consent to fetch.
    /// </summary>
    public override void Write(global::app.type.format.IWriter writer)
    {
        if (!IsLoaded) { writer.String(ToString()); return; }
        writer.String(ContentText());
    }
}
