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
public class @this : global::app.type.@this
{
    private readonly System.Collections.Generic.IReadOnlyList<string> _mime = [];
    private readonly System.Collections.Generic.IReadOnlyList<string> _extension = [];
    private readonly bool _compressible;
    private readonly bool _text;
    // How the owning type writes this format — its class's IEncode, bound when the kind was made; null for a
    // type that writes none of its formats.
    private readonly Encoder? _encode;
    // How the owning type reads this format — its class's IDecode, bound when the kind was made; null for a type
    // whose content is born lazily.
    private readonly Decoder? _decode;
    // The view content of this format is written and read in when no one says: plang's own store for a format plang
    // keeps (a .pr), else what crosses to others.
    private readonly global::app.View _view = global::app.View.Out;

    /// <summary>How a type writes its formats: the value a Data holds onto a stream, in a view.</summary>
    public delegate global::System.Threading.Tasks.Task<global::app.data.@this> Encoder(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View view,
        System.Text.Encoding? encoding, System.Threading.CancellationToken ct);

    /// <summary>How a type reads its formats: content of one, made into a value of the type.</summary>
    public delegate global::System.Threading.Tasks.Task<global::app.data.@this> Decoder(byte[] raw,
        global::app.actor.context.@this context, string name, global::app.type.item.path.@this? origin);

    public @this(string name) : base(name ?? throw new System.ArgumentNullException(nameof(name))) { }

    /// <summary>A kind named <paramref name="name"/> of <paramref name="owner"/> — the app's entry for the type it is a
    /// kind of, handed in by the registry that enlists it. Its facts fall back to the owner's.</summary>
    protected @this(string name, global::app.type.@this owner) : base(name, owner) { }

    /// <summary>A kind whose own kind is <paramref name="ownKind"/> — a nested kind (<c>{list, kind: {list, kind:
    /// path}}</c>), where this kind is itself a kinded type. Null <paramref name="ownKind"/> leaves the born empty kind.</summary>
    private protected @this(string name, global::app.type.kind.@this? ownKind, global::app.type.@this owner) : base(name, ownKind, owner) { }

    /// <summary>A kind is a full type already — its facts are its own (a setting kind's options, a
    /// format's MIMEs), its parent's the fallback. It navigates its OWN members directly, never
    /// re-resolved through the registry the way a bare <c>{name}</c> identity is (a kind is no top-level
    /// type, so that would mint a bare type by the kind's name and lose its facts). A key the kind has
    /// no member for falls to the type's own navigation.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        var member = await new global::app.type.clr.@this(this, parent.Context).Get(parent, key);
        return member.IsInitialized ? member : await base.Get(parent, key);
    }

    /// <summary>A format of <paramref name="owner"/> — a kind its class declares with <c>[Format]</c>: the
    /// MIMEs and extensions it answers to, whether its content compresses, and how its type writes and reads it.</summary>
    public @this(global::app.Attributes.FormatAttribute format, global::app.type.@this owner, Encoder? encode = null, Decoder? decode = null)
        : this(format.Name, owner)
    {
        _mime = format.Mime is { } mime ? [mime] : [];
        _extension = format.Extension;
        _compressible = format.Compressible;
        _text = format.Text;
        _view = format.Store ? global::app.View.Store : global::app.View.Out;
        _encode = encode;
        _decode = decode;
    }

    /// <summary>The view content of this format is written and read in when its asker names none: Store for a format
    /// plang keeps (a <c>.pr</c>), Out for every other.</summary>
    public virtual global::app.View View => _view;


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
    internal override bool Names(string key)
    {
        // A kind answers to its own name and aliases — never its owner's, which the type's names fall back to — AND
        // to its MIMEs and extensions: a format kind answers to `application/json` and `.json` too.
        if (string.IsNullOrEmpty(key)) return false;
        if (string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase)
            || Own.Contains(key, System.StringComparer.OrdinalIgnoreCase)) return true;
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

    // A kind's own options / example / description are its answers to the type's Own* hooks (a kind IS a
    // type): a choice set overrides OwnValues, a list's element kind OwnDescription/OwnExample. The base
    // answers the hook's default (null), so there is nothing to declare here — and no member to hide type's.

    /// <summary>What a value of this kind can be in <paramref name="step"/>, as the decider is offered it: one of the
    /// step's own variables (<c>%field%</c>), each once. A closed set offers its options.</summary>
    public override System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => new(new global::app.type.item.variable.parser.@this(step.Text, step.Type.Types!).Variable
            .DistinctBy(v => v.Text).Cast<global::app.type.item.@this>().ToList());

    /// <summary>Whether what this kind offers is all a value of it can be (a choice's options, a bool's true and
    /// false); false for an offer that is a convenience over an open set (the step's variables, a goal's name).</summary>
    public override bool IsClosed => false;

    /// <summary>Whether a value of C# class <paramref name="clr"/> rides as this kind — its
    /// <see cref="ClrForm"/> takes it.</summary>
    public virtual bool Carries(System.Type clr) => ClrForm is { } form && form.IsAssignableFrom(clr);

    /// <summary>Content of this kind, unread: <paramref name="raw"/> (characters or bytes) as a value of
    /// <paramref name="type"/>, read off <paramref name="origin"/> when it came from a file — a template holding
    /// <paramref name="variable"/> (the build's own when <paramref name="built"/>), its raw in <paramref name="format"/>
    /// when it was read in another. By default a source; a kind whose content looks different in a format makes its own.</summary>
    public virtual global::app.type.item.source Make(object raw, global::app.type.@this type, global::app.type.item.path.@this? origin,
        System.Collections.Generic.IReadOnlyList<global::app.type.item.variable.@this>? variable = null, bool built = false,
        @this? format = null)
        => new(raw, type, variable, origin, built, format);

    // A kind's own aliases (int → "integer") — what it answers to by name besides its name; never its owner's, which a
    // kinded type ({text, md}) reads as its family's.
    private System.Collections.Generic.IReadOnlyList<string> Own => OwnAlias ?? [];

    /// <summary>The keys this kind answers to as its type holds it: its name, its own aliases, its MIMEs.</summary>
    internal System.Collections.Generic.IEnumerable<string> Keys => Own.Prepend(Name).Concat(Mime);

    /// <summary>The type this kind is a kind of — the app's entry it was born holding: the type whose class
    /// declares it as a format (png is image's, md text's), or whose kind class it is (number's precisions are
    /// number's, json/list/dict/<c>*</c> are item's). Null for a kind of a name no type holds.</summary>
    protected internal virtual global::app.type.@this? Owner => _family;

    /// <summary>
    /// The type this kind is a kind of — what its content is (md → text, json → item, int →
    /// number): its owner, else the type whose reader reads this kind, else
    /// <c>binary</c>. <c>{binary, md}</c> is bytes whose content is text. Asked with the caller's context.
    /// </summary>
    public global::app.type.@this type(actor.context.@this context)
        => Owner ?? context.App.type.list[new global::app.type.@this(
            context.App.type.list.Reader.TypeOf(Name) ?? "binary"), context];

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
    // `new`: this host verb — "is a HOST of this kind positional" — shares a name with item's own
    // IsSequence (a property: "is this value a sequence"), a coincidental collision once kind : item.
    // It is a host kind's concern (json/dict/list/*) and moves off the base in stage 3 with the rest.
    public new virtual bool IsSequence(object host) => false;

    /// <summary>Write a child <paramref name="key"/> onto a host of this kind — the kind owns HOW
    /// its content takes a new child (<paramref name="isIndex"/> tells positional from named).
    /// Returns the value carried as an item; a kind with no writable content refuses, its error carried.</summary>
    public virtual global::System.Threading.Tasks.ValueTask<global::app.type.item.@this> Set(
        object host, string key, bool isIndex, object? value, global::app.actor.context.@this ctx)
        => throw new global::app.error.AppException(new global::app.error.@this(
            $"kind '{Name}' cannot set a child", "NoAddedMember", 404));

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
    public global::System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, string name = "",
        System.Threading.CancellationToken ct = default, global::app.type.item.template.kind.@this? template = null, global::app.type.item.path.@this? origin = null)
        => Decode(raw, context, View, name, ct, template, origin);

    /// <summary>The decode door, read as written for <paramref name="view"/>.</summary>
    public virtual async global::System.Threading.Tasks.Task<global::app.data.@this> Decode(byte[] raw,
        global::app.actor.context.@this context, global::app.View view, string name = "",
        System.Threading.CancellationToken ct = default, global::app.type.item.template.kind.@this? template = null, global::app.type.item.path.@this? origin = null)
    {
        // a type that reads this format itself makes the value now (a .pr into a goal)
        if (_decode != null && template == null) return await _decode(raw, context, name, origin);
        // else content decoded into a new value is a birth: it comes through its type's on.create, unread until used
        return await context.App.type.list.Of(this, context, template).Create(raw, context, name, origin);
    }

    /// <summary>
    /// A Data written onto a stream in this format — the one encode door, <see cref="Decode"/>'s pair. The
    /// type that declares the format writes it (bound once, when the kind was made); a kind whose content is
    /// written its own way overrides it (json, plang's transport). A kind with no writer of its own (one coined
    /// for an extension: <c>.xyz</c>) writes as its family writes; a family with no format answers an error.
    /// With no view named, the format writes in its own (<see cref="View"/>: a .pr in Store).
    /// </summary>
    public global::System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => Encode(stream, data, context, View, encoding, ct);

    /// <summary>The encode door, written in <paramref name="view"/>.</summary>
    public virtual global::System.Threading.Tasks.Task<global::app.data.@this> Encode(System.IO.Stream stream,
        global::app.data.@this data, global::app.actor.context.@this context, global::app.View view,
        System.Text.Encoding? encoding = null, System.Threading.CancellationToken ct = default)
        => _encode != null ? _encode(stream, data, context, view, encoding, ct)
            : !IsEmpty ? (Owner ?? context.App.type.list["binary"]).kind.Encode(stream, data, context, view, encoding, ct)
            : global::System.Threading.Tasks.Task.FromResult(context.Error(new global::app.error.@this(
                $"nothing writes {Owner?.Name ?? "binary"} content", "NoEncoder", 400)));

    /// <summary>What characters of this kind stand for, opened when they are needed — a text of kind json walked by
    /// <c>%x.a%</c> is the json its characters are — or, as a failed Data, why they don't read as this kind. Null when
    /// the characters stand for nothing but themselves (plain text, md): the default. Distinct from
    /// <see cref="Parse"/>, which a read asks: a kind can keep a text a text when it is read, and open it only when it
    /// is navigated.</summary>
    public virtual global::app.data.@this? Open(string characters, global::app.actor.context.@this context) => null;

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

    /// <summary>A kind held AS A VALUE writes its own name — <c>%x!type.kind%</c> is "json", "decimal" — in every
    /// format. The kind is a type, but its face is the subtype word, never a type's identity or word; navigation reaches
    /// the rest (.name).</summary>
    protected internal override global::System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.String(Name);
        return default;
    }

    /// <summary>The word a kind goes by is its name ("json", "decimal"), never a type's word: text writes it.</summary>
    internal override string Face => Name;

    /// <summary>A kind held AS A VALUE lowers to its name — its value IS the subtype word ("md",
    /// "decimal"), so a text coercion and <c>%x!type.kind% equals "md"</c> read it as that name.
    /// Navigation reaches the rest (.name, .list). Other targets fall to the base.</summary>
    internal override object? Clr(System.Type target)
        => target == typeof(string) || target == typeof(object) ? Name : base.Clr(target);

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

    public override string ToString() => Name;

    public override bool Equals(object? obj) => obj switch
    {
        @this k => string.Equals(Name, k.Name, System.StringComparison.OrdinalIgnoreCase),
        string s => string.Equals(Name, s, System.StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    public override int GetHashCode() => System.StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
