namespace app.type.item.content;

/// <summary>
/// A reference to content somewhere else — a <c>file</c>, a <c>url</c>: a location plus content read lazily.
/// The reference is born unread; its bytes are sampled once, through the location's gate, with their format;
/// its value is that sample decoded by the format, and the holding <c>Data</c> narrows to the content on first
/// examination. The location surface never reads. A program never names it: the types hold it, internal, by
/// its word <c>content</c>.
/// </summary>
[global::app.Attributes.PlangType("content")]
public abstract class @this : global::app.type.item.@this
{
    /// <summary>plang's own base for file and url, never offered as a type.</summary>
    public static bool Internal => true;

    /// <summary>A reference's facts are its own public members, read without reading its content (<c>!path</c>,
    /// <c>!size</c>, <c>!host</c>); one that needs the asker's context (<c>!kind</c>, <c>!mimetype</c>) is asked with
    /// the binding's own.</summary>
    internal override System.Threading.Tasks.ValueTask<global::app.data.@this?> Fact(string key, global::app.data.@this parent)
    {
        const System.Reflection.BindingFlags Public = System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase;
        if (GetType().GetProperty(key, Public) is { } property)
            return new(new global::app.data.@this(key, property.GetValue(this), parent: parent));
        if (GetType().GetMethod(key, Public, binder: null, types: [typeof(global::app.actor.context.@this)], modifiers: null) is { } asks)
            return new(new global::app.data.@this(key, asks.Invoke(this, [parent.Context]), parent: parent));
        return default;
    }

    /// <summary>The location — owns scheme, auth gate, stat and fetch.</summary>
    [global::app.LlmBuilder, global::app.Out, global::app.Store]
    public global::app.type.item.path.@this Path { get; }

    // The sample: the raw bytes and the format they are in, taken together once. Null until the content is
    // first asked for.
    private byte[]? _bytes;
    private global::app.type.kind.@this? _format;

    /// <summary>A reference at <paramref name="path"/>; <paramref name="context"/> is the creator's, used once to
    /// name the kind and not kept. <paramref name="template"/> is a birth fact: the programmer asked for the
    /// content's variables to be filled (<c>read … resolve variables</c>) — its text content is born a template
    /// and renders itself at use. <paramref name="clr"/> is the concrete reference type (file, url). Born holding the
    /// app's entry for <paramref name="clr"/> paired with the app's kind its path's format is (a <c>.json</c> file
    /// holds item's json kind) — both the app's objects; only the pairing is per value, as an element kind's is.
    /// The list keeps no pairing: it keys its types by entry, not by (entry, kind object).</summary>
    protected @this(System.Type clr, global::app.type.item.path.@this path, global::app.actor.context.@this context, global::app.type.item.template.kind.@this? template)
        : base(context.App.type.list[clr] is var entry
               && (path.Kind(context) is { IsNull: false, kind: { IsEmpty: false } k } ? k : null) is var kind
               && (kind != null || template != null)
            ? new global::app.type.@this(entry, kind, false, template)
            : context.App.type.list[clr])
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        // Born from a path — its type joins this value's history, so `is path` answers from the chain.
        history.Add(path);
    }

    /// <summary>Never final — its door loads the content it references.</summary>
    internal override bool IsFinal => false;

    /// <summary>True once the content is in memory (the reference was examined).</summary>
    public bool IsLoaded => _bytes != null;

    // Whether its content, decoded, holds no variable — a fact of the sample, known once it is decoded.
    private bool _plain;

    /// <summary>A reference marked a template whose content holds variables answers a render, which depends on
    /// what they hold at each use — never kept (the reference stays; its bytes are sampled once). Content holding
    /// none (a template whose text names only what it may not fill) renders the same every time: kept once
    /// opened, as unmarked content is.</summary>
    public override bool Cacheable => (Type.Template == null || _plain) && base.Cacheable;

    /// <summary>The format the sampled bytes are in, as the <paramref name="read"/> that fetched them tells — by
    /// default the path's.</summary>
    protected virtual System.Threading.Tasks.ValueTask<global::app.type.kind.@this> Format(
        global::app.data.@this read, global::app.actor.context.@this context)
        => new(context.App.type.list.Mime(Path.Mime(context).ToString()));

    /// <summary>
    /// The value door — the sample decoded by its format, handed this reference's template, answering with the
    /// CONTENT's own instance (a json file answers as its json host), this reference stamped as its prior.
    /// Single storage: the decoded value is the one copy. A read or decode failure lands on the
    /// <paramref name="data"/> binding whole — its key, message and inner exception — and the answer is absent.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(global::app.data.@this data)
    {
        var sample = await Content(data.Context);
        if (!sample.Success) { data.Fail(sample.Error!); return data.Empty; }
        var read = await _format!.Decode(Bytes, data.Context, template: Type.Template, origin: Path);
        if (!read.Success) { data.Fail(read.Error!); return data.Empty; }
        // the decoded value says whether it holds variables (born holding only what its origin allows)
        _plain = !read.Peek().HasVariable;
        // what the decode answers — a template's render at this use, which its Data never keeps
        var answer = await read.Value();
        if (!read.Success) { data.Fail(read.Error!); return data.Empty; }
        // the binding takes what came with the read (a whole Data's format brings its signature and properties)
        data.Carry(read);
        if (answer == null || ReferenceEquals(answer, this)) return this;
        answer.history.Add(this);
        return answer;
    }

    /// <summary>The raw bytes, sampled once through the path's gate with their format; a later ask serves them
    /// from memory.</summary>
    public async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.binary.@this>> Content(global::app.actor.context.@this context)
    {
        if (_bytes != null) return context.Ok<global::app.type.item.binary.@this>(new global::app.type.item.binary.@this(_bytes, context.App.type.list[typeof(global::app.type.item.binary.@this)]));
        var read = await Path.Bytes(context);
        if (!read.Success || read.Exits) return read;
        _format = await Format(read, context);
        _bytes = (await read.Value())?.Value ?? System.Array.Empty<byte>();
        return read;
    }

    /// <summary>Raw content as text (renderers; UTF-8).</summary>
    public string ContentText() => System.Text.Encoding.UTF8.GetString(_bytes ?? System.Array.Empty<byte>());

    /// <summary>In-memory raw bytes; empty until the content was sampled.</summary>
    public byte[] Bytes => _bytes ?? System.Array.Empty<byte>();

    /// <summary>Drop the in-memory content — the narrow's single-storage step (the decoded value is the one
    /// copy; this becomes location-only again).</summary>
    internal void Release() => _bytes = null;

    /// <summary>Display is the location — content never leaks through ToString.</summary>
    public override string ToString() => Path.ToString();

    /// <summary>
    /// The reference renders itself as its CONTENT once sampled (its value door was opened): text content as its
    /// text, any other as its bytes — the format says which. An unsampled reference renders its location:
    /// writing it out is not consent to read it.
    /// </summary>

    /// <summary>Opened to be written out: the content is sampled through the location's gate, so its write is
    /// the content, not the location.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.error.@this?> Open(global::app.actor.context.@this context)
    {
        var sample = await Content(context);
        return sample.Success ? null : sample.Error;
    }

    /// <summary>A reference writes its content once sampled, as what its format makes of its bytes, unread (json
    /// content pastes into json; any other is bytes of its format, the writer deciding how they look); unsampled, its
    /// location.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        if (!IsLoaded) { writer.String(ToString()); return; }
        var format = _format ?? Type.kind;
        await format.Make(Bytes, writer.Context.App.type.list.Of(format, writer.Context), Path).Output(writer);
    }
}
