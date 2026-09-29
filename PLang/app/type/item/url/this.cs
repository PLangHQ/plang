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
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.type.item.IContent
{
    public static string Example => "https://example.com/data.json";
    public static string Description => "A web address; its content is fetched when it is used.";
    public static string Shape => "string";
    public static IReadOnlyList<string> From => ["path"];


    /// <summary>The location facet (an <c>HttpPath</c> — owns consent + fetch).</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.item.path.@this Path { get; }

    private byte[]? _bytes;
    private string? _contentType;

    // The canonical content kind, worked out at creation through the creator's format
    // registry and kept as a fact.
    private readonly global::app.type.kind.@this? _kind;

    /// <summary>A url reference at <paramref name="path"/>; <paramref name="context"/> is the
    /// creator's, used once to name the kind and not kept. <paramref name="template"/> is a birth fact, as a
    /// file's: its text content is born a template and renders itself at use.</summary>
    public @this(global::app.type.item.path.@this path, global::app.actor.context.@this context, string? template = null)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        _kind = path.Kind(context) is { IsNull: false, kind: { IsEmpty: false } k } ? k : null;
        Template = template;
        // Born from a path — inject its type into this value's history (`is path` from the chain).
        history.Add(path);
    }

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

    public bool IsLoaded => _bytes != null;

    /// <summary>A url marked a template answers a render, which depends on the variables at each use —
    /// never kept (the url stays the reference; its bytes are fetched once).</summary>
    public override bool Cacheable => Template == null && base.Cacheable;

    /// <summary>A url's entity: name "url", kind = the canonical kind named at
    /// creation — location metadata, never fetches.</summary>
    protected internal override global::app.type.@this Type =>
        new global::app.type.@this("url", typeof(@this)) { kind = _kind, Template = Template };

    /// <summary>
    /// The value door — the <see cref="Content"/> sample decoded by its format (the response's
    /// Content-Type first) and answer with the CONTENT's own instance, this url stamped as its prior.
    /// The sync <c>Bytes</c> getter serves the sampled bytes.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        // The sample: one consent-gated fetch per value per program run — the format decodes FROM the
        // sample, never a second GET. URL authors its own failures (fetch stories) onto the data binding:
        // the fetch error rides through WHOLE — its key, message and inner exception.
        var sample = await Content(data.Context);
        if (!sample.Success) { data.Fail(sample.Error!); return Absent; }
        var bytes = Bytes;
        // The content's format by precedence: the response Content-Type rules; else the URL extension
        // is the hint (.json → json); else a typeless web response is text, not raw bytes. The format
        // decodes it, with the asker's context.
        var context = data.Context;
        var mime = !string.IsNullOrEmpty(_contentType) ? _contentType
            : !string.IsNullOrEmpty(Path.Extension) ? Path.MimeType(context)
            : "text/plain";
        // (plang's own content from another actor answers only signed — its format refuses it otherwise.)
        var read = await context.App.type.list.Mime(mime).Decode(bytes, context);
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        _ = await read.Value();
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        var answer = read.Item;
        if (answer == null || ReferenceEquals(answer, this)) return this;
        // a url born a template: its text content is born one, and the door answers it ready — rendered at
        // this use (a render is never kept: see Cacheable)
        if (Template != null && answer is global::app.type.item.text.@this content)
            return await new global::app.type.item.text.@this(content.ToString(), Template) { Kind = content.Kind }.Value(data);
        answer.history.Add(this);
        return answer;
    }


    /// <summary>The raw bytes, fetched once through the path's consent gate (the response's Content-Type
    /// kept with them); a later ask serves them from memory.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.binary.@this>> Content(global::app.actor.context.@this context)
    {
        if (_bytes != null) return context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(_bytes));
        var read = await Path.Bytes(context);
        if (!read.Success || read.Exits) return read;
        _contentType = await read.Properties.Get<string>("contentType");
        _bytes = (await read.Value())?.Value ?? System.Array.Empty<byte>();
        return read;
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
