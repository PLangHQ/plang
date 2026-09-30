namespace app.type.kind;

/// <summary>
/// A kind — the subtype token ("json", "md", "int", "*") that names HOW a value of a type is
/// specialised, AND the behavior for that specialisation. The kind IS the behavior: a value
/// asks its own kind to navigate / enumerate / load / convert / lower it
/// (<c>value.Kind.Get(…)</c>) — a direct virtual call, no registry hop.
///
/// <para>This base owns the verb DEFAULTS (a plang-path walk that re-derives the node's kind
/// each hop; the rest throw "not X"). Each real kind subclasses it under the type it
/// specialises (<c>type/item/kind/json</c>, <c>type/number/kind/int</c>) and overrides what it
/// does differently. An UNKNOWN kind ("md", "csv", a host's class name) is just a base instance
/// carrying the name — the defaults are its behavior.</para>
///
/// <para>A kind lives on the type it is a kind of (that type's empty kind holds it); the type
/// list answers kind lookups by name and by C# class by walking its types' kinds — never a static
/// factory. A kind holds no context: its verbs take the caller's. Equality is by
/// <see cref="Name"/> (case-insensitive); the wire form is the name string.</para>
/// </summary>
public class @this
{
    public string Name { get; }

    private readonly string? _owner;
    private readonly System.Collections.Generic.IReadOnlyList<string> _mime = [];
    private readonly System.Collections.Generic.IReadOnlyList<string> _extension = [];
    private readonly bool _compressible;
    private readonly bool _text;
    // How the owning type writes this format — its class's IEncode, bound when the kind was made; null for a
    // type that writes none of its formats.
    private readonly Encoder? _encode;

    /// <summary>How a type writes its formats: the value a Data holds onto a stream, in a view.</summary>
    public delegate global::System.Threading.Tasks.Task<global::app.data.@this> Encoder(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view,
        System.Text.Encoding? encoding, System.Threading.CancellationToken ct);

    public @this(string name)
    {
        Name = name ?? throw new System.ArgumentNullException(nameof(name));
    }

    /// <summary>A format of <paramref name="owner"/> — a kind its class declares with <c>[Format]</c>: the
    /// MIMEs and extensions it answers to, whether its content compresses, and how its type writes it.</summary>
    public @this(global::app.Attributes.FormatAttribute format, string owner, Encoder? encode = null) : this(format.Name)
    {
        _owner = owner;
        _mime = format.Mime is { } mime ? [mime] : [];
        _extension = format.Extension;
        _compressible = format.Compressible;
        _text = format.Text;
        _encode = encode;
    }

    /// <summary>The MIMEs content of this kind arrives as — each a bare media type.</summary>
    public virtual System.Collections.Generic.IReadOnlyList<string> Mime => _mime;

    /// <summary>The file extensions of this kind, each with its dot.</summary>
    public virtual System.Collections.Generic.IReadOnlyList<string> Extension => _extension;

    /// <summary>Whether compressing content of this kind pays; false for a kind that declares no format.</summary>
    public virtual bool Compressible => _compressible;

    /// <summary>
    /// Whether this kind answers to <paramref name="key"/>: its name, an alias, one of its MIMEs (the media
    /// type — <c>; charset=…</c> and any other parameter dropped, here and nowhere else) or one of its
    /// extensions (with or without the dot). Case aside.
    /// </summary>
    public bool Names(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var alias in Alias)
            if (string.Equals(alias, key, System.StringComparison.OrdinalIgnoreCase)) return true;
        if (Mime.Count > 0)
        {
            var semicolon = key.IndexOf(';');
            var media = (semicolon >= 0 ? key.AsSpan(0, semicolon) : key.AsSpan()).Trim();
            foreach (var mime in Mime)
                if (media.Equals(mime, System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        if (Extension.Count > 0)
        {
            var bare = key.AsSpan().TrimStart('.');
            foreach (var extension in Extension)
                if (bare.Equals(extension.AsSpan().TrimStart('.'), System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>True for a type's empty kind — the type has no kind.</summary>
    public bool IsEmpty => Name.Length == 0;

    /// <summary>Whether content of this kind is characters, as its format declares (a text media type, or said
    /// outright: xml, ini); a kind that is its own class says so itself (json).</summary>
    public virtual bool IsText => _text;

    /// <summary>The file a test run's report is written to in this kind (<c>results.json</c>), its content
    /// this kind's <see cref="Encode"/> of the run; null when this kind writes no report.</summary>
    public virtual string? Report => null;

    /// <summary>One of this kind's type's kinds, by its name or an alias — a type holds its kinds on its
    /// empty kind; null when it holds none by that name (the type list's <c>Kind(name)</c> then mints the
    /// unknown kind, after asking every type).</summary>
    public virtual @this? this[string name] => null;

    /// <summary>A kind of this kind's type coined for <paramref name="name"/> — a kind the type holds none of ahead,
    /// because it can be any other type (a list's element: <c>{list, path}</c>); null when it coins none. Only
    /// the type door asks, for its own entry: coining is not holding, so no walk over every type finds it.</summary>
    public virtual @this? Coin(string name) => null;

    /// <summary>One of this kind's type's kinds, by the C# form its values ride as (exact wins, then the
    /// most derived assignable: <c>JsonElement</c>→json, <c>IList</c>→list); null when none claims it.</summary>
    public virtual @this? this[System.Type clr] => null;

    /// <summary>The CLR form values of this kind ride as (json → <c>JsonElement</c>), or null
    /// when the kind claims no single CLR carrier. The collection's <c>[clrType]</c> door reads
    /// this to bridge a raw host to its kind (exact wins, then assignable — <c>IList</c>→list).</summary>
    public virtual System.Type? ClrForm => null;

    /// <summary>The class values of this kind are, given the class of its type's values — the same class,
    /// unless the kind closes it (a choice set: <c>choice&lt;T&gt;</c> → <c>choice&lt;Format&gt;</c>).</summary>
    public virtual System.Type? Of(System.Type? type) => type;

    /// <summary>The options a value of this kind is one of — a set of options answers them (a choice's
    /// closed set); any other kind answers none, and its type's own apply.</summary>
    public virtual System.Collections.Generic.IReadOnlyList<string>? Values => null;

    /// <summary>Whether a value of C# class <paramref name="clr"/> rides as this kind — its
    /// <see cref="ClrForm"/> takes it.</summary>
    public virtual bool Carries(System.Type clr) => ClrForm is { } form && form.IsAssignableFrom(clr);

    /// <summary>Whether <paramref name="writer"/> writes this kind's own format — content of this kind is then
    /// already a token of it, relayed verbatim. By default no writer is.</summary>
    public virtual bool Owns(global::app.type.format.IWriter writer) => false;

    /// <summary>The other names this kind answers to (<c>integer</c> for int).</summary>
    public virtual System.Collections.Generic.IReadOnlyList<string> Alias => [];

    /// <summary>The name of the type this kind is a kind of: the type whose class declares it as a format
    /// (png is image's, md text's), or whose kind class it is (number's precisions are number's,
    /// json/list/dict/<c>*</c> are item's). Null for a name no type declares.</summary>
    protected internal virtual string? Owner => _owner;

    /// <summary>
    /// The type this kind is a kind of — what its content is (md → text, json → item, int →
    /// number): the type that declares it, else the type whose reader reads this kind, else
    /// <c>binary</c>. <c>{binary, md}</c> is bytes whose content is text. Asked with the caller's context.
    /// </summary>
    public global::app.type.@this type(actor.context.@this context)
    {
        string name = Owner
                      ?? context.App.type.list.Reader.TypeOf(Name)
                      ?? "binary";
        return context.App.type.list[new global::app.type.@this(name), context];
    }

    /// <summary>
    /// The kinds of this kind's type, each as the full type it makes: <c>{number, int}</c>,
    /// <c>{number, long}</c>, … for a number kind; the formats that are text for a text kind.
    /// </summary>
    public virtual list.@this list(actor.context.@this context) => type(context).kind.list(context);

    // --- Verbs: the kind owns what you can do with its values. Defaults here; kinds override. ---

    /// <summary>Descend one level: the value at <paramref name="key"/> on <paramref name="obj"/>,
    /// or <c>(false, null)</c> when absent. <paramref name="isIndex"/> is true when the key came
    /// from an index bracket (`[0]`) rather than a member dot (`.Count`) — a sequence host answers
    /// positional vs named differently. The list kind indexes, the dict kind keys, the <c>*</c>
    /// kind reflects a property — each owns its own descend.</summary>
    public virtual (bool found, object? node) Descend(object obj, string key, bool isIndex, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Name}' is not navigable");

    /// <summary>Build the child <c>Data</c> for a landed node. Default: a node that already IS
    /// a Data rides through as itself; otherwise the raw node becomes a child Data (the ctor
    /// lifts it to its plang type / re-derives its kind). json overrides (scalar vs clr(json)).</summary>
    public virtual global::app.data.@this Data(string name, object? node,
        global::app.data.@this? parent, global::app.actor.context.@this ctx)
        => node is global::app.data.@this d ? d
           : new global::app.data.@this(name, node, parent: parent, context: ctx);

    /// <summary>Each child of a container, for <c>foreach</c> — array elements or object members.</summary>
    public virtual System.Collections.Generic.IEnumerable<global::app.data.@this> Enumerate(
        object obj, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Name}' is not enumerable");

    /// <summary>Whether a host of this kind holds its children by position (an array) rather than by name (an
    /// object's members).</summary>
    public virtual bool IsSequence(object host) => false;

    /// <summary>Write a child <paramref name="key"/> onto a host of this kind — the kind owns HOW
    /// its content takes a new child (<paramref name="isIndex"/> tells positional from named).
    /// Returns the value carried as an item; a kind with no writable content throws.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(
        object host, string key, bool isIndex, object? value, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException($"kind '{Name}' cannot set a child");

    /// <summary>The SYNC decode: raw (string / bytes) → a value item OF this kind, or DECLINE
    /// (null). This is the SINGLE decode body — <see cref="Load"/> is its async face, and the sync
    /// entrances (a wire graduating to lower / write) call it directly. Decline (null) is the
    /// discriminator (same answers-or-declines convention as <c>ICreate.Create</c>): a kind whose
    /// content is just text (md, unknown) declines here and reads as text through its family. Only
    /// a kind that owns a real decode overrides (json → clr).</summary>
    public virtual global::app.type.item.@this? Parse(object raw, global::app.actor.context.@this ctx) => null;

    /// <summary>
    /// Content of this kind, read off I/O into a Data — the one decode door (a channel read, a file's or a
    /// url's content, an http body). By default the bytes are a value of this kind's type, left unread until
    /// touched (<c>{image, png}</c>, <c>{text}</c>); a kind whose content is a whole Data (plang's own
    /// format) overrides it. Born with the caller's context; with a <paramref name="template"/> the content is
    /// born a template (a file read with its variables resolved). Content read off a file is born knowing it
    /// (<paramref name="origin"/>); null for content with no location.
    /// </summary>
    public virtual async global::System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name = "", global::app.View view = global::app.View.Out,
        System.Threading.CancellationToken ct = default, string? template = null, global::app.type.item.path.@this? origin = null)
    {
        // content decoded into a new value is a birth: it comes through its type's on.create
        var type = context.App.type.list[new global::app.type.@this(Owner ?? "binary", IsEmpty ? null : Name, template: template), context];
        return await type.Create(raw, context, name, origin);
    }

    /// <summary>
    /// A Data written onto a stream in this format — the one encode door, <see cref="Decode"/>'s pair. The
    /// type that declares the format writes it (bound once, when the kind was made); a kind whose content is
    /// written its own way overrides it (json, plang's transport). A kind with no writer of its own (one coined
    /// for an extension: <c>.xyz</c>) writes as its family writes; a family with no format answers an error.
    /// With no <paramref name="view"/> the format writes its own face (a .pr its Store face).
    /// </summary>
    public virtual global::System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View? view = null,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => _encode != null ? _encode(stream, data, context, view, encoding, ct)
            : !IsEmpty ? context.App.type.list[Owner ?? "binary"].kind.Encode(stream, data, context, view, encoding, ct)
            : global::System.Threading.Tasks.Task.FromResult(context.Error(new global::app.error.Error(
                $"nothing writes {Owner ?? "binary"} content", "NoEncoder", 400)));

    /// <summary>The async face over <see cref="Parse"/> — the materialization rung (<c>source.Value</c>)
    /// asks the kind first; a decline (null) falls to the family's type reader. No second decode
    /// lives here: Load wraps the one <see cref="Parse"/> body.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this?> Load(
        object raw, global::app.actor.context.@this ctx)
        => Parse(raw, ctx) is { } item ? new(ctx.Ok(item)) : new((global::app.data.@this?)null);

    /// <summary>Convert a source value INTO a value of this kind — the outbound owns it (dict
    /// from json, audio from text). An error <c>Data</c> when the source can't become this kind.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.data.@this> Convert(
        global::app.data.@this source, global::app.actor.context.@this ctx)
        => throw new System.NotSupportedException(
            $"cannot convert {source.Type?.Name} into kind '{Name}'");

    /// <summary>Write a host value OF this kind to the wire — json emits raw json, <c>*</c>
    /// reflects a POCO's tagged fields. The carrier delegates its <c>Output</c> here.</summary>
    public virtual global::System.Threading.Tasks.ValueTask Output(
        object obj, global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? ctx)
        => throw new System.NotSupportedException($"kind '{Name}' cannot write itself");

    /// <summary>Lower a value OF this kind INTO the CLR shape <paramref name="target"/> asks for
    /// — the clr carrier delegates its lower here. json bridges its content to a reader and drives
    /// the <c>*</c> kind's host <c>Read</c>. The default is terminal: a host that isn't already the
    /// target (identity is handled by the carrier) and can't be built genuinely can't lower.</summary>
    public virtual object? Clr(object host, System.Type target, global::app.actor.context.@this ctx)
        => throw new System.InvalidCastException(
            $"a '{Name}' value cannot lower to {target.Name} — the kind cannot build that shape.");

    /// <summary>Bridge this kind's raw <paramref name="obj"/> content into a stream and drive
    /// <paramref name="reader"/> (the declared type's own reader), handing it the element
    /// <paramref name="kind"/> (list&lt;action&gt; → action) so a list reader loops the element's
    /// reader. This is the ONLY place a format is named — a format kind (json/…) overrides; a kind
    /// that is not a format has nothing to bridge and declines (null → the caller's Clr path).</summary>
    public virtual object? Read(object obj, global::app.type.reader.ITypeReader reader, string? kind,
                                global::app.actor.context.@this context) => null;

    // OBPV — carry marked, collapse after the restructure compiles: this is a verb+noun name
    // AND a type-switch fork standing in for a value's own self-write. Fix: a reflected value
    // writes itself via `new Data(name, value, ctx).Output(...)` — Data.Output already emits bare
    // vs {name,type,value} envelope by the writer's format (EmitsSchema), not by value type, so
    // the switch below should dissolve. (todos.md 2026-07-09)
    protected async global::System.Threading.Tasks.ValueTask WriteReflected(
        global::app.type.format.IWriter writer, object value, global::app.View mode,
        global::app.actor.context.@this ctx)
    {
        switch (value)
        {
            case global::app.type.item.@this item: await item.Output(writer, mode, ctx); break;
            case global::app.data.@this d: await d.Output(writer, mode, ctx); break;
            case string s: writer.String(s); break;
            // A raw C# scalar the writer renders (number/bool/date/enum/…). Everything else —
            // a collection OR an object — writes through ITS kind (IDictionary → object, IList /
            // any sequence → array, an object → the * kind's declared-face Output). One rule, no
            // categories: the kind decides; an undeclared plang type throws there, loud.
            default:
                if (value.GetType().IsClass) await ctx.App.type.list.Kind(value.GetType()).Output(value, writer, mode, ctx);
                else writer.Value(value);
                break;
        }
    }

    public override string ToString() => Name;

    public override bool Equals(object? obj) => obj switch
    {
        @this k => string.Equals(Name, k.Name, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Name, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
