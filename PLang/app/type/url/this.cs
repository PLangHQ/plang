namespace app.type.url;

/// <summary>
/// PLang <c>url</c> value — the remote-scheme REFERENCE: a location plus lazily
/// fetched content and metadata. Same shape as <c>file</c> — the content
/// materialises (and the holding <c>Data</c> narrows) on first examination;
/// the location surface (<c>!url!path</c>, <c>!url!host</c>) never fetches.
/// The scheme know-how (consent gate, redirects, signing) stays on the
/// composed <c>HttpPath</c>.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, module.IContext
{
    public static string Example => "https://example.com/data.json";
    public static string Shape => "string";

    /// <summary>The is-a lattice — a url is-a path.</summary>
    public static new System.Collections.Generic.IReadOnlyList<System.Type> Type { get; }
        = new[] { typeof(@this), typeof(global::app.type.path.@this) };

    /// <summary>The location facet (an <c>HttpPath</c> — owns consent + fetch).</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.path.@this Path { get; }

    private byte[]? _bytes;
    private string? _contentType;

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this Context
    {
        get => Path.Context;
        set => Path.Context = value;
    }

    public @this(global::app.type.path.@this path)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
    }

    /// <summary>The remote host — location surface, never fetches.</summary>
    public string Host =>
        System.Uri.TryCreate(Path.Absolute, System.UriKind.Absolute, out var u) ? u.Host : "";

    public bool IsLoaded => _bytes != null;

    /// <summary>A url's entity: name "url", kind = the extension's canonical
    /// form through the format registry — location metadata, never fetches.</summary>
    protected internal override global::app.type.@this Mint()
    {
        var t = Context.App.Format.TypeFromExtension(Path.Extension);
        return new global::app.type.@this("url", typeof(@this)) { Kind = t is { IsNull: false } ? t.Kind : null };
    }

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
            var readBytes = await Path.ReadBytes();
            // The fetch error rides through WHOLE — its key, message and inner exception —
            // instead of being flattened into a bare-string HttpRequestException.
            if (!readBytes.Success) { data.Fail(readBytes.Error!); return Absent; }
            _contentType = await readBytes.Properties.Get<string>("contentType");
            var bin = await readBytes.Value();
            bytes = _bytes = bin?.Value ?? System.Array.Empty<byte>();
        }
        // Stamp the content's type by precedence: the response Content-Type rules;
        // else the URL extension is the hint (.json → dict); else a typeless web
        // response is text, not raw bytes.
        global::app.data.@this read;
        if (!string.IsNullOrEmpty(_contentType))
            read = await new global::app.channel.type.http.@this(_contentType, bytes, Context).Read();
        else if (Context.App.Format.TypeFromExtension(Path.Extension) is { IsNull: false })
            read = await new global::app.channel.type.file.@this(Path).Read(bytes);
        else
            read = await new global::app.channel.type.http.@this("text/plain", bytes, Context).Read();
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        _ = await read.Value();
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        var answer = read.Item;
        if (answer == null || ReferenceEquals(answer, this)) return this;
        answer.Accumulate(this);
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
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        if (!IsLoaded) { writer.String(ToString()); return; }
        writer.String(ContentText());
    }
}
