namespace app.type.file;

/// <summary>
/// PLang <c>file</c> value — a REFERENCE: a location plus lazy content and
/// metadata. <c>read X</c> yields one with <b>nothing read</b>; the content
/// materialises (and the holding <c>Data</c> narrows to the content's type) on
/// first examination through the value door. The location/stat surface
/// (<c>!file!path</c>, <c>!file!size</c>) never triggers a content read.
///
/// <para>Substitutability: a file is-a path (the location facet), declared via
/// the static-<c>Type</c> lattice convention — same shape as <c>image</c>.
/// The scheme know-how stays on the composed <see cref="Path"/>
/// (<c>FilePath</c>/<c>HttpPath</c>); this type owns content laziness.</para>
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.data.ILoadable, module.IContext
{
    public static string Example => "/some/config.json";
    public static string Shape => "string";

    /// <summary>The is-a lattice — a file is-a path (read by <c>type.@this.Is</c>).</summary>
    public static new System.Collections.Generic.IReadOnlyList<System.Type> Type { get; }
        = new[] { typeof(@this), typeof(global::app.type.path.@this) };

    /// <summary>The location facet — owns scheme, auth gate, stat.</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.path.@this Path { get; }

    // Raw content, loaded exactly once through the path's auth gate. Null until
    // first content access (the reference is born unread).
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

    /// <summary>True once the content is in memory (the reference was examined).</summary>
    public bool IsLoaded => _bytes != null;

    /// <summary>A file's entity: name "file", kind = the extension's canonical
    /// form through the format registry ("json", "csv", …) — location metadata,
    /// never reads content.</summary>
    protected internal override global::app.type.@this Mint()
    {
        var t = Context?.App.Format.TypeFromExtension(Path.Extension);
        return new global::app.type.@this("file", typeof(@this)) { Kind = t is { IsNull: false } ? t.Kind : null };
    }

    /// <summary>
    /// The raw content bytes, read through the path's auth gate on first access
    /// and cached. Idempotent — a loaded file returns its cache with no I/O.
    /// A read failure (missing, denied) surfaces here, at first content access.
    /// </summary>
    public async System.Threading.Tasks.Task<byte[]> BytesAsync()
    {
        if (_bytes != null) return _bytes;
        var read = await Path.ReadBytes();
        if (!read.Success)
            throw new System.IO.IOException(read.Error!.Message);
        var bin = await read.Value();
        return _bytes = bin?.Value ?? System.Array.Empty<byte>();
    }

    /// <summary>Write-out pre-materialisation (the serialize chokepoint) — pulls
    /// the raw content into memory so the sync renderer can emit it.</summary>
    public System.Threading.Tasks.Task LoadAsync() => BytesAsync();

    /// <summary>
    /// The value door — read + parse through the file channel (mime stamps the
    /// content's {type, kind}; the auth gate rides on <c>Path.ReadBytes</c>) and
    /// answer with the CONTENT's own instance (a json file answers as
    /// <c>dict</c>), this file stamped as its prior. Single storage: the parsed
    /// value is the one copy. FILE authors its own failures — an IO/parse
    /// failure lands on the data binding, answer absent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        // The sample: one auth-gated read per value per program run — a later
        // narrow (another alias, a cached binding) serves from memory, never
        // from a re-read. The channel stamps + parses FROM the sample.
        byte[] bytes;
        try
        {
            bytes = await BytesAsync();
        }
        catch (System.IO.IOException ex)
        {
            data.Fail(new global::app.error.Error(
                $"could not read '{Path}': {ex.Message}", "FileReadFailed", 400) { Exception = ex });
            return Absent;
        }
        var channel = new global::app.channel.type.file.@this(Path);
        var read = await channel.Read(bytes);
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        _ = await read.Value();
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        var answer = read.Instance;
        if (answer == null || ReferenceEquals(answer, this)) return this;
        answer.Accumulate(this);
        return answer;
    }

    /// <summary>
    /// Navigation is first-touch: a file is a reference to content, so it narrows itself
    /// (read + parse by mime — json→dict) via the Data door, which caches the parsed value
    /// back onto <paramref name="parent"/> (the file Data BECOMES a dict in place, read-once),
    /// then navigates the parsed value. A read/parse failure surfaces the parent's error.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
    {
        var materialized = await parent.Value();
        if (!parent.Success) return parent;
        return await materialized.Navigate(parent, key);
    }


    /// <summary>Truthiness of a reference is its location's: does it exist.</summary>
    public override bool IsTruthy() => Path is global::app.type.path.file.@this fp && fp.Exists;

    /// <summary>Raw content as text (renderers; UTF-8 — text files own this form).</summary>
    public string ContentText() => System.Text.Encoding.UTF8.GetString(_bytes ?? System.Array.Empty<byte>());

    /// <summary>In-memory raw bytes; empty until <see cref="BytesAsync"/> ran.</summary>
    public byte[] Bytes => _bytes ?? System.Array.Empty<byte>();

    /// <summary>
    /// The file renders itself as its CONTENT (the bare-scalar contract:
    /// <c>write out %file%</c> emits what was read, never the location). The
    /// content was pre-materialised by the serialize chokepoint's <c>Load()</c>
    /// pass (file is <c>ILoadable</c>). Text-ish mime emits the UTF-8 text form;
    /// anything else emits the bytes.
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        var mime = Path.MimeType;
        if (mime.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase)
            || mime.Contains("json", System.StringComparison.OrdinalIgnoreCase)
            || mime.Contains("xml", System.StringComparison.OrdinalIgnoreCase))
            writer.String(ContentText());
        else
            writer.Bytes(Bytes);
    }

    /// <summary>Stat byte-size — the file's `!size` (<c>number</c>); never reads content.</summary>
    public global::app.type.number.@this Size =>
        Path is global::app.type.path.file.@this fp ? fp.Size : global::app.type.number.@this.From(0);

    /// <summary>Drop the in-memory content — the narrow's single-storage step
    /// (the parsed value is the one copy; this becomes location-only again).</summary>
    internal void Release() => _bytes = null;

    /// <summary>Display is the location — content never leaks through ToString.</summary>
    public override string ToString() => Path.ToString();
}
