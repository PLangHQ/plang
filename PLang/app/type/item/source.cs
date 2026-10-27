namespace app.type.item;

/// <summary>
/// A born-with-bytes value — the undecoded source form (<c>string</c> for a
/// text source, <c>byte[]</c> for a binary one) carrying its declared
/// <c>{type, kind}</c> judgement (stamped by the channel boundary from mime, or
/// by the wire reader). It IS the declared type, unparsed: <see cref="Mint"/>
/// answers the declaration, <see cref="Peek"/> answers the raw form, and
/// <see cref="Ready"/> parses through the reader registry for (type, kind) —
/// the holding <c>Data</c> rebinds to the parsed answer, this source riding its
/// prior chain. Verbatim passthrough (serialize-without-parse) reads
/// <see cref="Raw"/> as long as no parse happened.
/// </summary>
public class source : @this
{
    // Held for the wire subclass's verbatim write + re-birth; a plain source reads it
    // through value.Reader, never a format lookup.
    private protected readonly object _value;
    // The declaration, WHOLE — the source IS this type, unparsed. Born with it at Create,
    // never shredded into Name/Kind/Strict/Template scalars and reassembled (Mint answers it
    // directly). Name/Kind/Strict/Template all read off this one object.

    // The format the raw bytes are in — the raw's own fact, not the declaration's. When a value is re-declared
    // (a dict slot given an unread json: {item, json} → {dict}, kind dropped), the format rides here so the
    // declared type can still make itself from what the format parses. Null when the raw
    // carries no format of its own.
    private readonly global::app.type.kind.@this? _format;

    // The variables a template source holds — decided once, at birth, by whose bytes these are (see the
    // constructor). Empty for a plain source.
    private readonly IReadOnlyList<global::app.type.item.variable.@this> _variable = [];

    /// <inheritdoc/>
    public override IReadOnlyList<global::app.type.item.variable.@this> Variable => _variable;

    // Where the raw came from — the file it was read off; null for raw with no location. Its read hands it down.
    private readonly global::app.type.item.path.@this? _origin;

    // A full-match %ref% (`%!data%`, `%messages%`) is a REFERENCE, not content — decided ONCE
    // at birth: the raw form and the authored-template flag are immutable, so reading it back is
    // a bool check (references re-resolve every read — Cacheable=false).
    public override bool IsVariable { get; }

    /// <inheritdoc/>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => IsVariable ? await _variable[0].Start(ctx) : null;

    /// <summary>Whether its bytes are the build's own — a goal's <c>.pr</c>, or the build reading its own
    /// answer: only those reads grant it (<see cref="global::app.type.reader.ReadContext.IsBuilt"/>). A birth
    /// fact; a re-declared source keeps it.</summary>
    internal bool IsBuilt { get; }

    /// <summary>Born from a declared type entity + a raw form — the source-maker the entity's
    /// <c>Create</c> door shares. The source holds the declaration WHOLE (Name/Kind/Strict/Template
    /// all live on it) and reads its own raw through the (type, kind) reader — no format name. It
    /// stores no context: the load uses the asking Data's. A template source holds the variables
    /// whose bytes these are allow: the build's own (<paramref name="built"/>) the
    /// <paramref name="variable"/>s its row lists, content a file or url read handed over
    /// (<paramref name="origin"/>) only the program's own, anything else none.</summary>
    public source(object value, global::app.type.@this type, IReadOnlyList<global::app.type.item.variable.@this>? variable = null,
        global::app.type.item.path.@this? origin = null, bool built = false, global::app.type.kind.@this? format = null)
        : base(type ?? throw new System.ArgumentNullException(nameof(type)))
    {
        _value = value ?? throw new System.ArgumentNullException(nameof(value));
        _format = format;
        _origin = origin;
        IsBuilt = built;
        // Only a value marked a template holds variables — the mark is the declaration's, never read off
        // the content: a string that merely looks like "%x%" stays literal.
        if (type.Template == null) return;
        // the variables written in it are read off its text face (a byte raw declared text is its UTF-8)
        var raw = Peek() as string;
        IReadOnlyList<global::app.type.item.variable.@this> all()
            => raw != null ? new global::app.type.item.variable.parser.@this(raw, type.Types ?? throw new System.InvalidOperationException(
                   $"a {type.Name} template is born holding the app's type — a bare one holds no types to read its variables with")).Variable : [];
        // THE door for content from outside: what a template holds, by whose bytes these are. Decided ONCE,
        // here — a variable not held is never resolved; it stays as written everywhere downstream.
        IEnumerable<global::app.type.item.variable.@this> variables =
            built ? variable ?? all()                               // the build's own .pr: every variable its row lists
            : origin != null ? all().Where(v => v.IsProgram)          // a file or url read with load vars: the program's own only
            : [];                                                   // anything else (a peer's value, the store): none
        _variable = variables.DistinctBy(v => v.Text).ToList();
        // A full-match %ref% on ANY declared type is a reference to a binding — resolved at
        // .Value(), never parsed through the type reader.
        IsVariable = _variable is [var only] && only.Text == raw;
    }

    /// <summary>The undecoded source form — <c>string</c> or <c>byte[]</c>.</summary>
    public object Raw => _value;

    /// <summary>The raw string face, when the source carries text (not bytes).</summary>
    public override string? RawText => _value as string;

    /// <summary>The raw byte face, when the source carries bytes (not text) — symmetric with RawText.</summary>
    public override byte[]? RawBytes => _value as byte[];


    /// <summary>
    /// In memory now = the raw source form. A byte raw whose kind is characters (<see
    /// cref="global::app.type.kind.@this.IsText"/> — plain text, json, any text format) decodes to its
    /// text face: the type's own answer about what its content is, not a content guess. A
    /// <c>binary</c>/<c>bytes</c> raw STAYS bytes: peeking never sniffs "is this valid UTF-8?"
    /// (access-driven resolution; binary's face is its bytes — decode to text is the explicit <c>as
    /// text</c>). A structured declaration (table/xlsx, image/png) keeps its bytes — the parse stays
    /// behind <see cref="Ready"/>.
    /// </summary>
    public override object? Peek()
    {
        // Text content decodes to its text face: a text-kinded raw (json — its kind answers IsText), or a
        // plain text whose own kind hasn't loaded its format yet (the empty kind reads IsText false until then,
        // so the type name is the floor that keeps a plain text's bytes decoding).
        if (_value is byte[] b && (Type.kind.IsText || Type.Name is "text"))
        {
            try { return new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(b); }
            catch (System.Text.DecoderFallbackException) { return b; }
        }
        return _value;
    }

    /// <summary>Shared by reference — a source is immutable (its raw + declared
    /// judgement are readonly; Ready parses into a NEW instance rather than
    /// mutating). Sharing the instance is safe precisely because nothing mutates it.</summary>
    protected internal override @this Clone() => this;

    /// <summary>
    /// The parse: raw → value via the reader registry for (type, kind). The answer
    /// is a new instance with this source as its prior; a raw with no reader and no
    /// type answers itself (the bytes are the value).
    /// </summary>
    /// <summary>Never final — the door parses the raw form into its value on read.</summary>
    internal override bool IsFinal => false;

    /// <summary>A source holding variables re-resolves every read (what they hold can change) — never
    /// cached by the holding Data; one holding none (a plain source, or a template whose content holds
    /// none it may fill) parses once and is kept. Mirrors text/dict/list.</summary>
    public override bool Cacheable => !HasVariable;

    /// <summary>A literal holds what a step quoted as the value it reads into answers it (a named separator holds its
    /// characters); one holding variables is never read here — its written form answers.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(string quoted, global::app.actor.context.@this context)
    {
        if (await base.Holds(quoted, context)) return true;
        if (HasVariable) return false;
        var read = new global::app.data.@this("", this, context: context);
        return await read.Value() is { } born && !ReferenceEquals(born, this) && read.Success && await born.Holds(quoted, context);
    }

    /// <summary>A literal holds a number a step wrote as the value it reads into holds it (a duration of 1s holds 1000,
    /// as milliseconds); one holding variables is never read here — its written form answers.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(global::app.type.item.number.@this number, global::app.actor.context.@this context)
    {
        if (await base.Holds(number, context)) return true;
        if (HasVariable) return false;
        var read = new global::app.data.@this("", this, context: context);
        return await read.Value() is { } born && !ReferenceEquals(born, this) && read.Success && await born.Holds(number, context);
    }

    public override async System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this data)
    {
        // The value loads with the context of the Data that asks — never the source's own: a
        // source in a program property is read by every run, and each run's Data carries its
        // own actor (its variables, its permissions).
        var asking = data.Context ?? throw new System.InvalidOperationException(
            $"'{data.Name}' has no context to load with — a value is read through a Data born with its run's context.");

        // A full-match %ref% names a binding — hand back what it holds through its OWN door,
        // never parse the name through the declared type's reader (a `list` reader on the string
        // "%!data%" has nothing to read). Same resolve door as text's full-match / variable.@this;
        // the declared {type,kind} was only the pre-resolution label. IsVariable decided at birth.
        if (IsVariable)
        {
            var resolved = await Get(asking);
            // Get returns a NotFound Data (IsInitialized == false) on a miss, never null for a
            // reference. A variable set to null IS initialized, so it resolves (to null); only a
            // genuinely-absent name fails here.
            if (resolved is null || !resolved.IsInitialized)
            {
                data.Fail(new global::app.error.@this(
                    $"{_value} is not set — nothing to answer for it.", "VariableNotFound", 404));
                return data.Empty;
            }
            return await resolved.Value();
        }

        try
        {
            // A partial template is a text's to render, as Output hands it to one: the text fills the
            // variables this source holds, and this type makes itself from the text it renders to (a path
            // from its text, as from any text). What a variable held is filled once, never read as a template.
            if (Text(asking) is { } template)
            {
                var value = Type.Make(await template.Value(data), asking);
                value.history.Add(this);
                return await value.Value(data);
            }

            // Kind-first materialization (most-specific owner): a kind that owns its own decode
            // answers on first touch (json → clr) — lazy until here, never at .pr read. The kind is
            // the REAL subclass via the one selection door; a decline (null) falls to the type
            // reader below (png→image, csv→table, …). Covers both entrances — a content source and
            // an inherited wire both parse here. A bad parse rides the catch → MaterializeFailed.
            if (Type.kind is { IsEmpty: false } kind
                && await asking.App.type.list.Kind(Type, asking).Load(_value, asking) is { } loaded)
            {
                // A kind that RESTS answers its materialize with an unread source (json): the courier
                // keeps it unread — this source answers itself, parsed only at a use (navigation / enumeration /
                // is / a typed ask). Any other kind resolves its value now.
                if (loaded.Peek() is global::app.type.item.source) return this;
                var decoded = await loaded.Value();
                decoded.history.Add(this);   // the source rides the materialized value's history
                return decoded;
            }
            // A value re-declared away from its format keeps what format its bytes are in (a dict slot given
            // an unread json — {item, json} re-declared {dict}): the format parses the raw,
            // and the declared type makes itself from what it parses (a dict from the parsed object). Only when
            // the declaration itself named no kind — a kinded declaration parses through its own kind above.
            if (Type.kind.IsEmpty && _format is { } encoded && encoded.Parse(_value, asking) is { } parsed)
            {
                var value = Type.Make(parsed, asking);
                value.history.Add(this);
                return await value.Value(data);
            }
            // what the read needs loaded first (a goal's .pr, in a snapshot), so the type's reader finds it held
            var held = await Load(asking);
            if (!held.Success)
            {
                data.Fail(held.Error!);
                return data.Empty;
            }
            var item = Read(asking);
            if (ReferenceEquals(item, this)) return this;
            // Container round-trip guard: a value DECLARED a container (dict/list) that
            // materialized to a non-container leaf is a round-trip loss (a json object that
            // came back an opaque scalar) — fail LOUD here, at the point of loss, not three
            // hops later as a null navigation: MaterializeFailed, named to the binding. (The apex
            // object/item is excluded — a scalar CAN be declared object; only genuine containers
            // must stay containers.)
            if (Type.Name is "dict" or "list"
                && item is not (global::app.type.item.dict.@this or global::app.type.item.list.@this or global::app.type.clr.@this))
            {
                data.Fail(new global::app.error.@this(
                    $"failed to read %{data.Name}% as {Type.Name}: it materialized to a non-container ({item.GetType().Name}) — round-trip loss",
                    "MaterializeFailed", 400));
                return data.Empty;
            }
            item.history.Add(this);
            // Resolve the materialized item — a template (text/dict/list) renders against live
            // variables; a plain leaf/container answers itself. The source layer is transparent:
            // "give me the value" returns the value, not an intermediate unrendered template.
            return await item.Value(data);
        }
        // the declared type declined the value: the read answers the reason the type gave
        catch (global::app.error.DeclinedException declined)
        {
            data.Fail(declined.Error);
            return data.Empty;
        }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException or System.FormatException or System.InvalidOperationException or System.NotSupportedException)
        {
            data.Fail(Unread(ex, data));
            return data.Empty;
        }
    }

    /// <summary>
    /// SOURCE authors its own failure story — the declared form did not parse as its declared
    /// <c>{type, kind}</c>. Keyed <c>MaterializeFailed</c> and naming the binding so navigation /
    /// enumeration / a typed ask all surface one actionable error at first touch, never thrown to a
    /// courier. A JsonException carries the JSON path + line STJ stamped (which slot in WHICH .pr, at
    /// what line) — appended so a malformed source is pinpointed, not just named. <see cref="Value"/>'s
    /// own parse and a use opening a resting source (<c>data.Opened</c>) both answer it.
    /// </summary>
    internal global::app.error.@this Unread(System.Exception ex, global::app.data.@this data)
    {
        var where = ex is System.Text.Json.JsonException je && (je.Path != null || je.LineNumber != null)
            ? $" [at {je.Path ?? "?"}, line {je.LineNumber?.ToString() ?? "?"}]"
            : "";
        return new global::app.error.@this(
            $"failed to read %{data.Name}% as {Type.Name}{(Type.kind.IsEmpty ? "" : $"/{Type.kind.Name}")}: {ex.Message}{where}",
            "MaterializeFailed", 400) { Exception = ex };
    }

    /// <summary>
    /// A resting kind (json) answered its own <see cref="Value"/> unread — a courier keeps it so. A USE
    /// opens it: the raw parses through the kind, once (navigation / enumeration), and the holding Data
    /// narrows to the parsed dict/list. Null when there is no kind to open through (an empty
    /// kind, or one the list doesn't carry): nothing to parse, the value is used as it is. A bad parse throws
    /// — the caller answers it through <see cref="Unread"/>.
    /// </summary>
    internal virtual global::app.type.item.@this? Parse(global::app.actor.context.@this ctx)
        => Type.kind.IsEmpty ? null : ctx.App.type.list.Kind(Type, ctx)?.Parse(_value, ctx);

    /// <summary>The text a partial template is, off its text face (a byte raw declared text is its UTF-8) —
    /// the one renderer fills it. Null for a plain source and a whole <c>%ref%</c> (a reference, resolved
    /// through the variable's own door); a wire's slice is no text (it overrides). Born holding the app's template
    /// text type, as <paramref name="context"/>'s types hold it.</summary>
    private protected virtual global::app.type.item.text.@this? Text(actor.context.@this context)
        => HasVariable && !IsVariable && Peek() is string template
            ? new(template, context.App.type.list[new global::app.type.@this("text", template: Type.Template), context], _variable)
            : null;

    /// <summary>The type reads its own raw form — the declaration is the whole selector. One
    /// token over the raw via <see cref="global::app.type.format.value.Reader"/>; the
    /// (type, kind) reader owns the decode (a scalar off its token, csv its text, an image its
    /// bytes). A structured value never rides here — it is a <see cref="wire.@this"/>, whose
    /// override reads through its held serializer. (source.Value owns the try/catch + the
    /// binding-named failure story.)</summary>
    internal virtual global::app.type.item.@this Read(actor.context.@this context)
    {
        var kind = Type.kind.IsEmpty ? null : Type.kind.Name;
        var ctx = new global::app.type.reader.ReadContext(context, Type.Template, Variable: _variable, Origin: _origin, IsBuilt: IsBuilt);
        var reader = new global::app.type.format.value.Reader(_value);
        return ctx.View.Reader(Type, kind, context).Read(ref reader, kind, ctx);
    }

    /// <summary>What the raw needs loaded before it is read, in the view it was written for; a plain source's content
    /// needs nothing (a wire's slice is read in its view, which may: a goal's <c>.pr</c> in a snapshot).</summary>
    private protected virtual System.Threading.Tasks.Task<global::app.data.@this> Load(actor.context.@this context)
        => System.Threading.Tasks.Task.FromResult(context.Ok());

    /// <summary>Re-birth under a new declaration — the source owns its own re-typing (kills the
    /// type entity reaching into a source's raw/format). The wire override carries its captured
    /// serializer across, so a re-declared wire still decodes through its capturer. Whose bytes these
    /// are rides along.</summary>
    internal virtual source Declared(global::app.type.@this type)
        // The raw's format is the raw's own fact — the kind object the raw was decoded by — so it rides the
        // re-declaration: a {item, json} re-declared a dict keeps json, and is asked to Parse directly.
        => type.kind.Make(_value, type, _origin, _variable, IsBuilt, _format);

    /// <summary>
    /// Navigation is first-touch: a source is still its raw form (bytes / json text),
    /// so it parses itself via the Data door — which caches the parsed value back onto
    /// <paramref name="parent"/> (a <c>%cfg%</c> source BECOMES the dict, parse-once) —
    /// then navigates the parsed value. A bad parse fails <paramref name="parent"/> with
    /// <c>MaterializeFailed</c> (the declared <c>{type, kind}</c> could not be built from
    /// the raw form); surface that, not a misleading NotFound.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
    {
        // Navigation is a USE: a resting source (json) opens here — parsed once through its kind, the Data
        // narrowing to the parsed dict/list — then the parsed value is navigated. A bad parse
        // fails parent (MaterializeFailed), surfaced here, never a throw to the courier.
        var materialized = await parent.Open();
        if (!parent.Success) return parent;
        return await materialized.Get(parent, key);
    }

    /// <summary>A fact read with <c>!</c> is a use, as navigation is: a resting source opens, the Data narrowing to
    /// what it parses to, and that value answers its own facts (a dict's <c>!key</c>).</summary>
    internal override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Fact(string key, global::app.data.@this parent)
    {
        var opened = await parent.Open();
        if (!parent.Success || opened is source) return null;
        return await opened.Fact(key, parent);
    }

    public override bool IsTruthy() => _value switch
    {
        string s => s.Length > 0,
        byte[] b => b.Length > 0,
        _ => true,
    };

    public override bool IsLeaf => true;

    /// <summary>
    /// A source writes itself as written — its raw content directly, a <c>string</c> or a <c>byte[]</c>, never a
    /// structured value: bytes as bytes of its declared kind, characters as characters. No materialize-on-write.
    /// Whether a template source is kept as written or used is the writer's view's, where its Data is written. (A
    /// <see cref="wire.@this"/> and json content paste their characters into json their own way.)
    /// </summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        // The raw is content of its declared kind, as bytes or as characters: the writer decides how it looks.
        if (_value is byte[] b) { writer.Bytes(b, Type.kind); return; }
        // A whole %ref% in a slot not typed text is the variable itself (`Value: item = %!data%`); inside a
        // text it stays the text it is (`"Total: %x%"`).
        if (Type.Template != null && IsVariable && !Type.Is("text"))
        {
            await _variable[0].Output(writer);
            return;
        }
        writer.String((string)_value);
    }


    internal override object? Clr(System.Type target) => ClrConvert(_value, target);

    /// <summary>Display is the text face (<see cref="Peek"/>): text-kinded bytes decode to their
    /// characters, a binary blob shows as a size note, a string is itself.</summary>
    public override string ToString() => Peek() switch
    {
        string s => s,
        byte[] b => $"({b.Length} bytes)",
        var v => v?.ToString() ?? "",
    };
}
