namespace app.type.url;

/// <summary>
/// PLang <c>url</c> value — the remote-scheme REFERENCE: a location plus lazily
/// fetched content and metadata. Same shape as <c>file</c> — the content
/// materialises (and the holding <c>Data</c> narrows) on first examination;
/// the location surface (<c>!url!path</c>, <c>!url!host</c>) never fetches.
/// The scheme know-how (consent gate, redirects, signing) stays on the
/// composed <c>HttpPath</c>.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.data.ILoadable, module.IContext
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

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context
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
        var t = Context?.App.Format.TypeFromExtension(Path.Extension);
        return new global::app.type.@this("url", typeof(@this)) { Kind = t is { IsNull: false } ? t.Kind : null };
    }

    /// <summary>
    /// The fetched content bytes — one GET through the path's consent gate on
    /// first access, cached after. A fetch failure surfaces here.
    /// </summary>
    public async System.Threading.Tasks.Task<byte[]> BytesAsync()
    {
        if (_bytes != null) return _bytes;
        var read = await Path.ReadBytes();
        if (!read.Success)
            throw new System.Net.Http.HttpRequestException(read.Error!.Message);
        var bin = await read.Value();
        return _bytes = bin?.Value ?? System.Array.Empty<byte>();
    }

    public System.Threading.Tasks.Task LoadAsync() => BytesAsync();

    /// <summary>
    /// The value door — fetch + parse through the file channel (mime stamps the
    /// content's {type, kind}; the consent gate rides on <c>Path.ReadBytes</c>)
    /// and answer with the CONTENT's own instance, this url stamped as its
    /// prior. Single storage: the fetched bytes are released after the parse.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Ready()
    {
        var channel = new global::app.channel.type.file.@this(Path);
        var read = await channel.Read();
        if (!read.Success)
            throw new System.Net.Http.HttpRequestException(read.Error!.Message);
        _ = await read.Value();
        if (!read.Success)
            throw new System.Net.Http.HttpRequestException(read.Error!.Message);
        var answer = read.Instance;
        if (answer == null || ReferenceEquals(answer, this)) return this;
        Release();
        answer.Accumulate(this);
        return answer;
    }


    public string ContentText() => System.Text.Encoding.UTF8.GetString(_bytes ?? System.Array.Empty<byte>());
    public byte[] Bytes => _bytes ?? System.Array.Empty<byte>();

    /// <summary>Drop the in-memory content — the narrow's single-storage step.</summary>
    internal void Release() => _bytes = null;

    public override string ToString() => Path.ToString();
}
