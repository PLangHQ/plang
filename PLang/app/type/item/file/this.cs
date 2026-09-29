namespace app.type.item.file;

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
[global::app.Attributes.PlangType("file")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, global::app.type.item.IContent
{
    public static string Example => "/config/settings.json";
    public static string Description => "A file, by its path; its content is read when it is used.";
    public static string Shape => "string";
    /// <summary>A file is made from a path.</summary>
    public static bool Takes(global::app.type.@this other) => other.Is("path");

    /// <summary>The location facet — owns scheme, auth gate, stat.</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.item.path.@this Path { get; }

    // Raw content, loaded exactly once through the path's auth gate. Null until
    // first content access (the reference is born unread).
    private byte[]? _bytes;

    // Whether the loaded content is text (its mime is text/json/xml) — a fact about the content,
    // decided once at load with the reader's format registry.
    private bool _isText;

    // The canonical content kind ("json", "csv", …), worked out at creation through the
    // creator's format registry and kept as a fact — a format registered later does not
    // change a file already made.
    private readonly global::app.type.kind.@this? _kind;

    /// <summary>A file reference at <paramref name="path"/>; <paramref name="context"/> is the
    /// creator's, used once to name the kind and not kept. <paramref name="template"/> is a birth fact:
    /// the programmer asked for the content's variables to be filled (<c>read … resolve variables</c>) —
    /// its text content is born a template and renders itself at use.</summary>
    public @this(global::app.type.item.path.@this path, global::app.actor.context.@this context, string? template = null)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        _kind = path.Kind(context) is { IsNull: false, kind: { IsEmpty: false } k } ? k : null;
        Template = template;
        // Born from a path — inject its type into this value's history so `is path` answers from
        // the type chain (no CLR-inheritance lattice). The type owns its history of types.
        history.Add(path);
    }

    /// <summary>A file is made from its path, as <paramref name="declared"/> says: the reference to what is there,
    /// nothing read, born with the declaration's template. Anything else declines.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data) => raw switch
    {
        @this self => self,
        global::app.type.item.path.@this path => new @this(path, data.Context, (declared ?? data.Type).Template),
        _ => null,
    };

    /// <summary>True once the content is in memory (the reference was examined).</summary>
    public bool IsLoaded => _bytes != null;

    /// <summary>A file marked a template answers a render, which depends on the variables at each use —
    /// never kept (the file stays the reference; its bytes are read once).</summary>
    public override bool Cacheable => Template == null && base.Cacheable;

    /// <summary>A file's entity: name "file", kind = the canonical kind named at
    /// creation — location metadata, never reads content.</summary>
    protected internal override global::app.type.@this Type =>
        new global::app.type.@this("file", typeof(@this)) { kind = _kind, Template = Template };

    /// <summary>
    /// The value door — the <see cref="Content"/> sample decoded by the file's format (its mime stamps the
    /// content's {type, kind}), answering with the CONTENT's own instance (a json file answers as its json
    /// host, <c>clr(JsonElement)</c>, navigated by the json kind), this file stamped as its prior. Single
    /// storage: the parsed value is the one copy. FILE authors its own failures — an IO/parse failure lands
    /// on the data binding, answer absent. The sync <c>Bytes</c> getter serves the sampled bytes the leaf
    /// write emits.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        // The sample: one auth-gated read per value per program run — a later
        // narrow (another alias, a cached binding) serves from memory, never
        // from a re-read. The format decodes FROM the sample.
        var sample = await Content(data.Context);
        // The path's read error rides through WHOLE — its key, message and inner
        // exception — instead of being flattened into a bare-string IOException.
        if (!sample.Success) { data.Fail(sample.Error!); return Absent; }
        // The file's format decodes its content, with the asker's context.
        var read = await data.Context.App.type.list.Mime(Path.MimeType(data.Context)).Decode(Bytes, data.Context);
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        _ = await read.Value();
        if (!read.Success) { data.Fail(read.Error!); return Absent; }
        var answer = read.Item;
        if (answer == null || ReferenceEquals(answer, this)) return this;
        // a file born a template: its text content is born one, and the door answers it ready —
        // rendered at this use (a render is never kept: see Cacheable)
        if (Template != null && answer is global::app.type.item.text.@this content)
            return await new global::app.type.item.text.@this(content.ToString(), Template) { Kind = content.Kind }.Value(data);
        answer.history.Add(this);
        return answer;
    }

    /// <summary>
    /// Navigation is first-touch: a file is a reference to content, so it narrows itself
    /// (read + decode by mime — json→its json host) via the Data door, which caches the decoded value
    /// back onto <paramref name="parent"/> (the file Data BECOMES its content in place, read-once),
    /// then navigates the parsed value. A read/parse failure surfaces the parent's error.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
    {
        var materialized = await parent.Value();
        if (!parent.Success) return parent;
        return await materialized.Get(parent, key);
    }


    /// <summary>The raw bytes, read once through the path's gate; a later ask serves them from memory.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.binary.@this>> Content(global::app.actor.context.@this context)
    {
        if (_bytes != null) return context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(_bytes));
        var read = await Path.Bytes(context);
        if (!read.Success || read.Exits) return read;
        _bytes = (await read.Value())?.Value ?? System.Array.Empty<byte>();
        var mime = Path.MimeType(context);
        _isText = mime.StartsWith("text/", System.StringComparison.OrdinalIgnoreCase)
            || mime.Contains("json", System.StringComparison.OrdinalIgnoreCase)
            || mime.Contains("xml", System.StringComparison.OrdinalIgnoreCase);
        return read;
    }

    /// <summary>Truthiness of a reference is its location's: does it exist.</summary>
    public override bool IsTruthy() => Path is global::app.type.item.path.file.@this fp && fp.Exists;

    /// <summary>Raw content as text (renderers; UTF-8 — text files own this form).</summary>
    public string ContentText() => System.Text.Encoding.UTF8.GetString(_bytes ?? System.Array.Empty<byte>());

    /// <summary>In-memory raw bytes; empty until the content was loaded.</summary>
    public byte[] Bytes => _bytes ?? System.Array.Empty<byte>();

    /// <summary>True when the loaded content is text (text/json/xml mime).</summary>
    internal bool IsText => _isText;

    /// <summary>
    /// The file renders itself as its CONTENT (the bare-scalar contract:
    /// <c>write out %file%</c> emits what was read, never the location). The
    /// content was pre-materialised by the serialize chokepoint's <c>Load()</c>
    /// pass (file is <c>ILoadable</c>). Text content emits the UTF-8 text form;
    /// anything else emits the bytes.
    /// </summary>
    public override void Write(global::app.type.format.IWriter writer)
    {
        if (_isText) writer.String(ContentText());
        else writer.Bytes(Bytes);
    }

    /// <summary>Stat byte-size — the file's `!size` (<c>number</c>); never reads content.</summary>
    public global::app.type.item.number.@this Size =>
        Path is global::app.type.item.path.file.@this fp ? fp.Size : (global::app.type.item.number.@this)0;

    /// <summary>Drop the in-memory content — the narrow's single-storage step
    /// (the parsed value is the one copy; this becomes location-only again).</summary>
    internal void Release() => _bytes = null;

    /// <summary>Display is the location — content never leaks through ToString.</summary>
    public override string ToString() => Path.ToString();
}
