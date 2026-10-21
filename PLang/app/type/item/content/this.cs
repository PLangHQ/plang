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
        if (GetType().GetProperty(key, Public) is { } own)
            return new(new global::app.data.@this(key, own.GetValue(this), parent: parent));
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
    /// Kept per pairing would need the list to key its cache by (entry, kind object): a list surface change, Ingi's.</summary>
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

    /// <summary>The format the content is in, as the <paramref name="media"/> type that came with it tells (a
    /// response's Content-Type; empty when none came) — by default the path's.</summary>
    private protected virtual global::app.type.kind.@this Format(string media, global::app.actor.context.@this context)
        => context.App.type.list.Mime(Path.Mime(context).ToString());

    /// <summary>The content as it streams, with the format it is in, handed to <paramref name="use"/> — through the
    /// location's gate. By default it is sampled and handed from memory; a location that streams overrides it.</summary>
    private protected virtual async System.Threading.Tasks.Task<global::app.data.@this> Stream(
        System.Func<System.IO.Stream, global::app.type.kind.@this, System.Threading.Tasks.Task<global::app.data.@this>> use,
        global::app.actor.context.@this context)
    {
        var sample = await Content(context);
        if (!sample.Success || sample.Exits) return sample;
        using var held = new System.IO.MemoryStream(Bytes, writable: false);
        return await use(held, _format!);
    }

    /// <summary>
    /// Saved to a file before it was read, content that would be born a plain value of the file's own family — its
    /// format reads nothing its own way, the family's value is its bytes (<see cref="IVerbatim"/>), nothing is bound
    /// to that birth — is what reading and writing it would give: its bytes, poured as they stream, never held. Any
    /// other is sampled from what is open (a url fetched once) and opened and encoded as any value is.
    /// </summary>
    internal override async System.Threading.Tasks.Task<global::app.data.@this> Save(global::app.data.@this self,
        System.IO.Stream into, global::app.type.kind.@this format, global::app.actor.context.@this context)
    {
        if (IsLoaded || Type.Template != null || Type.Strict) return await base.Save(self, into, format, context);
        return await Stream(async (body, kind) =>
        {
            if (Verbatim(kind, context) is { } family && ReferenceEquals(family, format.Owner))
            {
                await body.CopyToAsync(into, context.CancellationToken);
                return context.Ok();
            }
            await Sample(body, kind, context);
            return await base.Save(self, into, format, context);
        }, context);
    }

    /// <summary>Before it is read, content that would be born a plain value whose truth is having bytes (binary, image,
    /// text) answers from its first byte, never read whole. Any other is sampled from what is open (a url fetched
    /// once) and answers when it is opened.</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Truth(global::app.actor.context.@this context)
    {
        if (IsLoaded || Type.Template != null || Type.Strict) return null;
        bool? known = null;
        var opened = await Stream(async (body, kind) =>
        {
            if (Verbatim(kind, context) != null) known = await body.ReadAsync(new byte[1], context.CancellationToken) > 0;
            else await Sample(body, kind, context);
            return context.Ok();
        }, context);
        return !opened.Success || opened.Exits ? opened : known is { } truth ? context.Ok(truth) : null;
    }

    // The sample, taken from content already open (once), with the format it is in.
    private async System.Threading.Tasks.Task Sample(System.IO.Stream body, global::app.type.kind.@this kind, global::app.actor.context.@this context)
    {
        if (IsLoaded) return;
        using var whole = new System.IO.MemoryStream();
        await body.CopyToAsync(whole, context.CancellationToken);
        _bytes = whole.ToArray();
        _format = kind;
    }

    // The family content of kind is born a plain value of — when that value is its bytes (IVerbatim) and nothing is
    // bound to the birth; null when it is anything else (its kind reads it its own way, a family that writes its own
    // way, a birth something listens to).
    private static global::app.type.@this? Verbatim(global::app.type.kind.@this kind, global::app.actor.context.@this context)
        => kind.IsBorn && kind.Owner is { } family
           && family.ClrType is { } clr && typeof(IVerbatim).IsAssignableFrom(clr)
           && context.App.type.list[new global::app.type.@this(family.Name, kind.IsEmpty ? null : kind.Name), context] is var born
           && !born.on.create.IsBound(born, context)
            ? family : null;

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
        _format = Format(await read.Properties.Get<string>("contentType") ?? "", context);
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

    /// <summary>A reference writes its content once sampled, as the bytes of its format — the writer decides how
    /// they look; unsampled, its location.</summary>
    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer,
        global::app.View mode, global::app.actor.context.@this? context)
    {
        if (!IsLoaded) { writer.String(ToString()); return default; }
        writer.Bytes(Bytes, _format ?? Type.kind);
        return default;
    }
}
