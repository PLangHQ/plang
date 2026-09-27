using System.Text.Json;
using System.Text.Json.Serialization;
using app;
using app.actor.context;

namespace app.type;

/// <summary>
/// PLang type entity carrying the <c>{Name, Kind, Strict}</c> identity plus the
/// folded catalog knowledge (Property, Values, Shape,
/// ConstructorSignature, Example, Description).
///
/// <para><c>Name</c> is the family/primitive ("text", "number", "image", "long",
/// "datetime"). <c>Kind</c> is the optional subtype ("md", "gif", "int") —
/// folded from the historical <c>Data.Kind</c> field so the entity is the
/// single owner of the build-time refinement. <c>Strict</c> turns the kind
/// into a requirement; per-family enforcement is gated on the
/// <c>app.data.IKindValidatable</c> marker (image sniffs bytes; text degrades
/// to "kind-name-accepted").</para>
///
/// <para>Both doors — <c>data.Type</c> and <c>app.Type[name]</c> — return the
/// same entity shape; <c>app.Type</c> resolves names through the registry and
/// stamps <c>Context</c>, while <c>type.list.@this.BuildTypeEntries</c> walks
/// the action catalog and populates the catalog properties at construction.
/// Entities minted outside <c>BuildTypeEntries</c> lazily resolve the catalog
/// properties on first read via <see cref="Promote"/>.</para>
/// </summary>
// `type` is an item (settled in the value model): the type entity is a plang
// value — authored in the language (`as image/gif, strict`), riding in the .pr,
// holdable in a variable (`set %t% = %x!type%`). TypeName derives from the
// namespace ("type"); behavior defaults from the item base.
public sealed class @this : item.@this, item.IMatch<@this>
{
    /// <summary>Self-write (the sync core; base <c>Output</c> wraps it): the type entity's
    /// <c>{name, kind?, strict?}</c> identity — the same shape Data writes for its <c>type</c>
    /// slot. Used both when a type entity rides as a VALUE (a <c>variable.set</c> <c>Type</c>
    /// default) and for the Data envelope's <c>type</c> field (<c>json.Writer.BeginRecord</c>).
    /// Descriptor metadata is pure sync primitives — no <c>%var%</c>, no async — so it lives on
    /// <c>Write</c>, not an <c>Output</c> override.</summary>
    public override void Write(global::app.channel.serializer.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("name"); writer.String(Name);
        if (!kind.IsEmpty) { writer.Name("kind"); writer.String(kind.Name); }
        if (Strict) { writer.Name("strict"); writer.Bool(true); }
        if (!string.IsNullOrEmpty(Template)) { writer.Name("template"); writer.String(Template!); }
        writer.EndObject();
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>
    /// The subtype refinement ("md", "gif", "int"). Never null: a type with no kind has its empty
    /// kind (<see cref="global::app.type.kind.@this.IsEmpty"/>), which knows this type and is never
    /// written. Read-only once born: a type object is shared (a program row's declared type, the
    /// registry's entries), so a different kind is a different type object. Given no kind, the type
    /// takes its empty kind.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public global::app.type.kind.@this kind
    {
        get => _kind;
        init => _kind = value ?? new global::app.type.kind.empty.@this(Name);
    }
    private readonly global::app.type.kind.@this _kind;

    /// <summary>
    /// When true, <see cref="kind"/> is a requirement (enforced at build for
    /// literals via <c>app.data.IKindValidatable</c>; deferred to runtime for
    /// <c>%var%</c>). Default false — kind is a hint.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// Authored-template mode ("plang") — set by the BUILD when the value is a developer-authored
    /// <c>%ref%</c> template, carried in the <c>.pr</c> so the read honors it EXPLICITLY. The read
    /// never infers it from value content (a runtime-ingested string that happens to contain
    /// <c>%x%</c> is data, not a template). Null = a plain value. Mirrors <c>global::app.type.item.text.@this.Template</c>.
    /// </summary>
    public string? Template { get; init; }

    /// <summary>
    /// Catalog teaching for the <c>type</c> entry — the LLM-facing description
    /// surfaced through <c>app.type.list.view.TypeSchemas</c>. The instance
    /// property pulls from <see cref="Promote"/>'s catalog fold; for the entity
    /// itself there's no catalog row, so the LLM teaching falls into the static
    /// renderer via <see cref="TypeDescription"/> below.
    /// </summary>
    public const string TypeDescription =
        "A PLang type value, emitted as a JSON dict {name, kind?, strict?}. "
        + "`name` is the canonical family/primitive — text, number, bool, datetime, image, "
        + "etc. — drawn from the per-step `Primitive types:` list. NEVER a CLR name like "
        + "`string`, `int`, or `long` (use `text` and `number` instead — int/long/decimal/"
        + "double are kinds of number, not top-level names). "
        + "`kind` is the optional subtype: a file extension for text/image/audio/video "
        + "(`md`, `csv`, `jpg`, `mp3`), the numeric precision for number (`int`, `long`, "
        + "`decimal`, `double`), or a free string. For literals, the runtime stamps the "
        + "kind from the value when possible (a `.md` filename → kind `md`); only include "
        + "`kind` when the step text spells it out (`as text/markdown`). "
        + "`strict` (default false) turns kind into a build-time requirement for "
        + "verifiable formats (image checks magic bytes); a `%var%` value defers the "
        + "check to runtime; unverifiable families like `text` accept the kind name "
        + "without probing content. "
        + "Emit as a JSON object, NEVER a slash string. Wrong: `\"text/md\"`. "
        + "Right: `{\"name\":\"text\",\"kind\":\"md\"}`. The slash form leaks past the wire.";

    [JsonConstructor]
    /// <summary>A type object holds an already-canonical name — <c>app.Type[name]</c> is the one door
    /// that turns a spelled name (<c>string</c>, <c>int</c>) into its canonical type.</summary>
    public @this(string name, string? kind = null, bool strict = false, string? template = null)
    {
        Name = name.ToLowerInvariant();
        _kind = string.IsNullOrEmpty(kind) ? new global::app.type.kind.empty.@this(Name) : new global::app.type.kind.@this(kind);
        Strict = strict;
        Template = template;
        // The Create doors start pointing at the one-shot binder, which swaps itself for the
        // closed thunk (or the decline) on first use — every later call is a bare delegate
        // invocation, no null check. Field initializers can't reference `this`, so bind here.
        _byContext = Bind;
        _byData = Bind;
    }

    /// <summary>
    /// The item class values of this type are (<c>text.@this</c> for text, <c>number.@this</c> for
    /// {number, int}) — never the C# value underneath, which is its owner's (a number kind's storage,
    /// an item's OwnedClrTypes). A type from the types' list and an item's own type carry it; null
    /// only for a name no type answers to.
    /// </summary>
    [JsonIgnore]
    internal System.Type? ClrType => _clrType;
    private System.Type? _clrType;

    /// <summary>This type's empty value — what a value of it holds before anything is in it: the value
    /// class's own parameterless construction (an empty list or dict, 0, "", false). A type with none
    /// (a goal, an item, a host) answers its typed null: still null, still this type.</summary>
    public item.@this Empty(global::app.actor.context.@this context)
    {
        var clr = ClrType;
        if (clr == null || !typeof(item.@this).IsAssignableFrom(clr) || clr.IsAbstract || clr.ContainsGenericParameters)
            return new item.@null.@this(this);
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        // a kinded type is born with its kind (an empty list<goal>) when it takes one
        if (!kind.IsEmpty && clr.GetConstructor(any, [typeof(global::app.type.kind.@this)]) is { } kinded)
            return (item.@this)kinded.Invoke([kind]);
        if (clr.GetConstructor(any, System.Type.EmptyTypes) is { } empty)
            return (item.@this)empty.Invoke(null);
        return new item.@null.@this(this);
    }


    /// <summary>
    /// The "null" type — the type of a Data whose Value is null and no explicit
    /// Type was set.  Replaces the historical <c>Data.Type == null</c> sentinel
    /// so the property can be non-null end-to-end.  Wire serialization skips it
    /// (no "type": "null" emitted) to keep the wire shape identical to the
    /// pre-flip world.  ClrType is <c>typeof(object)</c>, the closest CLR mate.
    /// </summary>
    public static @this Null { get; } = new("null", typeof(object));

    /// <summary>True when this is the <see cref="Null"/> sentinel type. Overrides the
    /// value-level <c>item.IsNull</c>: a type-entity's null-ness is "names the null type".</summary>
    [JsonIgnore]
    public override bool IsNull => Name == "null";

    /// <summary>
    /// True for the bare polymorphic stamp ({item}, no kind, not strict) — "any value" is a
    /// shape note, not a judgement; the entry fold skips it and the value's own truth stands.
    /// </summary>
    [JsonIgnore]
    public bool Polymorphic => kind.IsEmpty && !Strict
        && string.Equals(Name, "item", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The type-system value factory: a raw CLR value → its plang value (the one
    /// owner of "what plang type is this"). The CLR→plang family map lives HERE in
    /// the type system, not on Data — Data only USES it. null→the null citizen, an
    /// already-plang value passes through, a sequence of values narrows to a native
    /// list, a foreign container narrows to list/dict, a scalar borns to its family
    /// wrapper (via <c>convert.OwnerOf</c>), an enum→choice, anything unowned rides
    /// as the <c>item</c> apex. A TYPE-SYSTEM concern, not serialization — json
    /// converts its own tokens then calls here for the leaves.
    /// </summary>
    // The two Create doors' bound thunks. Both start as the one-shot `Bind` (set in the ctor) and
    // self-replace with the closed generic (or the decline) on first use — a non-ICreate entity
    // (primitive/host name) binds a null-thunk so the collection perimeter falls to the next rung.
    // Named for the discriminating parameter (fields can't overload); NOT `_context`/`_data` — a
    // context-named field on the deliberately context-free shared entity would read as a late-stamp.
    private System.Func<object?, global::app.actor.context.@this?, item.@this?> _byContext;
    private System.Func<object?, global::app.data.@this, item.@this?> _byData;

    // THE born-native door — the ENTITY builds a plang VALUE of itself from a raw value, in one
    // step: null → typed absence; a variable-named type → the variable; wire-raw (string/bytes) →
    // a lazy source (parse on first touch); an already-native container → held; a built leaf →
    // refined to the declared kind/template, or re-typed through its family courier; a raw CLR
    // scalar → born through the family lift, then refined. ALWAYS returns a value (never null); a
    // bad conversion throws (the throw boundary — rides MaterializeFailed like a reader parse).
    // `new`: app.type.@this : item.@this, so this instance door (build the DECLARED type) deliberately
    // hides item's static apex lift (build WHATEVER the raw is). Different questions, distinguished by
    // receiver — an entity instance calls this; item.@this.Create(...) reaches the apex.
    public new item.@this Create(object? raw, global::app.actor.context.@this? context)
    {
        // context-never-null: a value is born WITH context. A null here is a construction site that
        // forgot to pass one — fail with a pointer, not an NRE deep in materialization.
        if (context is null) throw new System.InvalidOperationException(
            $"context-never-null: building a '{Name}' value without a context — pass the actor context at the construction site.");

        // Typed absence — the declaration survives (a typed null, a tool-parameter slot; a JSON-null too).
        if (raw is null or global::app.type.item.@null.@this) return new global::app.type.item.@null.@this(this);

        // A raw-name declared type (variable) NAMES a thing — the name IS the variable (a write-target),
        // not a value to defer. Before the string→source branch (else the name becomes a deferred source).
        if (raw is string rawName && ClrType == typeof(app.variable.@this))
            return app.variable.@this.Resolve(rawName, context);

        // Wire-raw (string / byte[]) → defer through a source declared as THIS type, parsed lazily on
        // first use. The source carries the type's Name/Kind/Strict/template and reads its own raw.
        if (raw is string or byte[])
            return new item.source(raw, this);

        // A container / domain value is already native (dict, list, path, image, …) — hold it. A
        // template=plang declaration stamps the container so its .Value() resolves nested %var% leaves
        // (mirrors how a text carries the template) — the source path below already carries it.
        if (raw is item.@this { IsLeaf: false } native)
        {
            if (Template != null && native.Template == null) native.Template = Template;
            return native;
        }

        // A source (declared, unparsed) re-declared → the source RE-BIRTHS itself over the same
        // unread raw with THIS declaration (which carries the build's stamped kind/template). The
        // value stays immutable and lazy — no mutation, no parse — so an authored %ref% is still
        // unread bytes until read. A wire's override carries its capturing serializer across.
        if (raw is item.source src)
            return src.Declared(this);

        // A built leaf (text/number/… carrying its raw):
        if (raw is item.@this leaf)
        {
            // A raw-name declared type (variable) NAMES a thing — the leaf's raw string is the variable.
            var backing = leaf.RawText;
            if (ClrType == typeof(app.variable.@this) && backing != null)
                return app.variable.@this.Resolve(backing, context);

            // Already this type → hold; refine a matching leaf to the declared kind.
            var minted = leaf.Type;
            if (string.Equals(Name, minted.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                var refined = !kind.IsEmpty && minted.kind.IsEmpty ? leaf.Kinded(kind.Name) : leaf;
                return refined;
            }
            // The value's type history already contains this type (an image born from a path
            // satisfies a path slot) → hold it, don't downgrade.
            if (leaf.Is(this)) return leaf;
            // A different type → unwrap to the leaf's raw CLR form, then re-type EAGERLY via the family
            // courier (kind-aware build — path parses a string, number parses a token). A decline lands
            // its reason on the carrier's Error — this door is the throw boundary (rides MaterializeFailed).
            var lowered = leaf.Clr<object>();
            var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(this), context: context);
            if (Create(lowered, carrier) is { } made) return made;
            if (carrier.Error != null) throw Failed(carrier.Error);
            // No family hook AND no error — nothing can build this shape (architect ruling: the
            // general CLR-target converter fallback dies; a leaf no family retypes is a producer bug).
            throw new System.InvalidOperationException(
                $"cannot build a '{Name}' from a {leaf.Type.Name} value — no family hook");
        }

        // A raw CLR scalar (int, DateOnly, …) → born through THIS family's own lift, then refine to the
        // declared type/kind. A non-family declared type routes the raw through the collection perimeter
        // (the owner's lift or a clr carrier). The family lift speaks raw natively; refine re-enters here.
        if (_byContext(raw, context) is { } lifted)
            return string.Equals(Name, lifted.Type.Name, System.StringComparison.OrdinalIgnoreCase)
                ? lifted : Create(lifted, context);
        return Create(global::app.type.item.@this.Create(raw, context), context);

        static System.Exception Failed(global::app.error.Error? error)
            => new System.InvalidOperationException(error?.Message ?? "conversion failed");
    }

    /// <summary>A still-encoded slice + the serializer that sliced it — the capture hands over
    /// itself. Mints the lazy <see cref="item.wire.@this"/>; the parse stays at first touch. The
    /// capture door beside the content <see cref="Create(object?, actor.context.@this?)"/> door —
    /// same verb, the capture's knowledge as an argument, never a format name.</summary>
    public item.@this Create(string slice, global::app.channel.serializer.ITransport reader)
        => new item.wire.@this(slice, this, reader);

    /// <summary>Reads a value slot of this type off the reader — the one door for a
    /// <c>{name, type, value}</c> row's value, a Data's or an action property's. The slot is exactly
    /// its row's declared type: a value is a template only when that type carries the marker (born at
    /// build), never because of what it holds. A type whose values are STRUCTURE (an action, a
    /// goal.call) is read eagerly through its own reader; a variable name or a template takes the
    /// content door; every other slot is a lazy wire over its verbatim bytes.</summary>
    public item.@this Read(ref global::app.channel.serializer.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        // Which types are structure is the TYPE's declaration (ITypeReader.IsEager), never a list of names.
        if (ctx.Context.App.Type.Reader.Typed(Name, null) is { IsEager: true } eager)
            return eager.Read(ref reader, null, ctx);

        var transport = ctx.Context.Actor?.Channel.Serializers?.Transport
            ?? throw new JsonException(
                "wire capture reached before the actor channel wired its transport serializer — "
                + "cannot decode a .pr value slot.");

        if (reader.Peek() == global::app.channel.serializer.TokenKind.String)
        {
            var slice = System.Text.Encoding.UTF8.GetString(reader.Slice());
            // A SEMANTIC string — a template (its row's marker; the IsVariable birth gate needs the
            // decoded content) or a variable NAME (type.Create resolves it to its binding) — takes the
            // content door; the kind-parse stays lazy on the content source. A literal string under
            // any other type rides the wire (strict, byte-identical).
            return Template != null
                    || ClrType == typeof(global::app.variable.@this)
                ? Create(JsonSerializer.Deserialize<string>(slice)!, ctx.Context)
                : Create(slice, transport);
        }
        // EVERY other slot is a wire: a VERBATIM Slice with the capturing transport named at the
        // mint site. Face validation is free — the type's own pull IS the validator on first touch.
        // A container is a template only by its own row's marker.
        return Create(System.Text.Encoding.UTF8.GetString(reader.Slice()), transport);
    }

    // The data door — the kind-aware build: THIS type makes itself from a value, reading the declared
    // kind off the carrier's Type and landing a decline on data.Fail (the retype path Convert owned).
    public item.@this? Create(object? raw, global::app.data.@this data) => _byData(raw, data);

    // The one-shot binders — same overload trick, one verb: on first use each swaps its field for the
    // closed thunk (or the decline) and forwards, so every later door call is a bare invocation.
    private item.@this? Bind(object? raw, global::app.actor.context.@this? ctx)
    {
        _byContext = Creatable is { } clr
            ? _openByContext.MakeGenericMethod(clr)
                .CreateDelegate<System.Func<object?, global::app.actor.context.@this?, item.@this?>>()
            : static (_, _) => null;
        return _byContext(raw, ctx);
    }

    private item.@this? Bind(object? raw, global::app.data.@this data)
    {
        _byData = Creatable is { } clr
            ? _openByData.MakeGenericMethod(clr)
                .CreateDelegate<System.Func<object?, global::app.data.@this, item.@this?>>()
            : static (_, _) => null;
        return _byData(raw, data);
    }

    // The one eligibility check both binders share: this type's class when it is an ICreate<clr>
    // family — ICreate<clr> SPECIFICALLY (a subtype implementing ICreate<base>, e.g.
    // FilePath : ICreate<path>, can't close Create<subtype>); null for a host entity, whose doors
    // decline so the collection perimeter falls to the next rung.
    private System.Type? Creatable
        => ClrType is { } clr
           && typeof(item.@this).IsAssignableFrom(clr)
           && System.Array.Exists(clr.GetInterfaces(),
                  i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.ICreate<>)
                       && i.GenericTypeArguments[0] == clr)
           ? clr : null;

    // Both generic thunks are Create<T> — the context/data difference lives in the PARAMETER LIST,
    // where overload resolution can see it (a parameterless factory pair differing only by RETURN type
    // is CS0111 — the reason a second name once existed here). Logic-free: the raw rides straight into
    // the type's own Create.
    private static item.@this? Create<T>(object? raw, global::app.actor.context.@this? ctx)
        where T : item.@this, global::app.type.item.ICreate<T>
        => T.Create(raw, ctx);

    private static item.@this? Create<T>(object? raw, global::app.data.@this data)
        where T : item.@this, global::app.type.item.ICreate<T>
        => T.Create(raw, data);

    // The two opens, disambiguated by the second parameter type (not by name — both are Create):
    private static readonly System.Reflection.MethodInfo _openByContext = Open(typeof(global::app.actor.context.@this));
    private static readonly System.Reflection.MethodInfo _openByData = Open(typeof(global::app.data.@this));

    private static System.Reflection.MethodInfo Open(System.Type second)
        => System.Array.Find(
               typeof(@this).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
               m => m.Name == nameof(Create) && m.IsGenericMethodDefinition
                    && m.GetParameters()[1].ParameterType == second)!;

    // The entity's face — the kind rides IN the name for a family whose kind is its content (a
    // list<path>, a dict<number>, a choice<operator>), and stands alone for a scalar sub-kind (a
    // "text" with kind "md" is still "text" to the vocabulary). Templates and catalog text print this.
    public override string ToString()
        => !kind.IsEmpty && (Name == "list" || Name == "dict" || Name == "choice") ? $"{Name}<{kind.Name}>" : Name;

    /// <summary>
    /// Value equality — the entity is minted on ask now, so two asks yield two
    /// instances; identity is {Name, Kind, Strict}, case-insensitive.
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is @this other
        && string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase)
        && string.Equals(kind.Name, other.kind.Name, System.StringComparison.OrdinalIgnoreCase)
        && Strict == other.Strict;

    public override int GetHashCode() => System.HashCode.Combine(
        Name.ToLowerInvariant(), kind.Name.ToLowerInvariant(), Strict);

    /// <summary>
    /// Does this type stand in for <paramref name="other"/> — is it the same
    /// type, or does it compose <paramref name="other"/> as a facet? An image
    /// has-a path, so <c>imageType.Is(pathType)</c> is true. The composing types
    /// are declared, self included, on the concrete type's <c>static
    /// IReadOnlyList&lt;string&gt; Type</c> (image → <c>["image","path"]</c>);
    /// a type that declares none satisfies only its own name.
    ///
    /// <para>Used by <c>variable.set</c>: a value whose type already <c>Is</c>
    /// the declared type is kept as-is (image wins over a <c>path</c> hint)
    /// rather than converted/downgraded.</para>
    /// </summary>
    // Provenance ("a narrowed value still IS what it was") is NOT baked onto the type entity —
    // it lives on the VALUE's own Prior chain and is answered by item.@this.Is walking it. A type
    // entity answers only for its OWN identity (name / apex / CLR lattice); the value composes the
    // narrow history. So no _priors / Accumulate / List here — the entity is a pure identity.

    /// <summary>Does this type entity answer to <paramref name="other"/> by its OWN identity? Name
    /// match, or the <c>item</c> apex. Composition ("an image is-a path") is NOT answered here — it
    /// lives on the VALUE's type history (a value born from a path carries a "path" entry), asked via
    /// <see cref="item.@this.Is"/>. No CLR-inheritance lattice, no reflection.</summary>
    public bool Is(@this? other)
        => other != null
           && (string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(other.Name, "item", System.StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Name-string IS-A query — resolves <paramref name="typeName"/> to a type and
    /// asks <see cref="Is(@this)"/>. Lets <c>if %x% is dict</c> / <c>is number</c> /
    /// <c>is item</c> resolve from a PLang type name without the caller minting a
    /// comparison entity. <c>item</c> is the apex: true for any value.
    /// </summary>
    public bool Is(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return false;
        if (string.Equals(typeName, "item", System.StringComparison.OrdinalIgnoreCase)) return true;
        return string.Equals(Name, typeName, System.StringComparison.OrdinalIgnoreCase);
    }

    // --- The facts ---
    // Set by app.type when it builds a full type; a value's bare type carries none. Navigation
    // (%x!type.Description%) answers them through the full type — see Get below. The wire form is
    // Write's {name, kind?, strict?, template?}; these never ride it.

    /// <summary>The type's properties — a record's fields, a scalar's navigable members; each a
    /// named slot carrying its type. Null when the type declares none. A record is a type with
    /// properties and no <see cref="Shape"/>.</summary>
    public property.list.@this? Property { get; init; }

    /// <summary>Enum values. Non-null marks this as an enum-shape type.</summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>Scalar wire shape (the underlying primitive form, e.g. "string" for path).</summary>
    public string? Shape { get; init; }

    /// <summary>Constructor signature for scalar types (<c>"name: shape"</c>).</summary>
    public string? ConstructorSignature { get; init; }

    /// <summary>Canonical example from a static <c>Example</c> property on the type.</summary>
    public string? Example { get; init; }

    /// <summary>Semantic description from a static <c>Description</c> property on the type.</summary>
    public string? Description { get; init; }

    /// <summary>The other names this type answers to (<c>string</c> for text, <c>map</c> for dict),
    /// declared by its class as a static <c>Alias</c>. Never null.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Alias { get; init; } = [];

    /// <summary>The C# shapes this type owns (<c>int</c> → number, kind int), declared by its class
    /// as a static <c>OwnedClrTypes</c>: a raw C# value of one of them is a value of this type.</summary>
    [JsonIgnore]
    internal IReadOnlyList<global::app.type.convert.OwnedClr> Owned { get; init; } = [];

    /// <summary>A type born knowing its C# class — the registry's entries and the full types it
    /// builds for an identity.</summary>
    internal @this(string name, System.Type? clrType, string? kind = null, bool strict = false, string? template = null)
        : this(name, kind, strict, template)
    {
        _clrType = clrType;
    }

    /// <summary>
    /// The type a class defines: its name, its C# class, its aliases and owned C# shapes, and — when
    /// <paramref name="types"/> is given — the facts the class declares: a closed set's options, a
    /// scalar's wire shape and constructor, a record's <c>[LlmBuilder]</c> properties, its static
    /// <c>Description</c> and <c>Example</c>. The facts name property types through
    /// <paramref name="types"/>, so they are read once every type's name is known; without it the
    /// type is its identity alone.
    /// </summary>
    internal @this(string name, System.Type clr, list.@this? types) : this(name, clr)
    {
        Alias = Declared<IReadOnlyList<string>>("Alias") ?? [];
        Owned = Declared<IReadOnlyList<global::app.type.convert.OwnedClr>>("OwnedClrTypes") ?? [];
        // The type entity's own wire shape and kinds are taught by the prompt's type reference, not as facts.
        if (types == null || clr == typeof(@this)) return;

        var example = Declared<string>("Example");
        var description = Declared<string>("Description");
        if (new global::app.type.item.choice.set.@this(clr) is { IsClosed: true } set)
        {
            Values = set.Values;
            Description = description;
            Example = example;
            return;
        }

        var shape = Declared<string>("Shape");
        string? signature = null, derived = null;
        if (clr.GetMethod("Resolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                ?.GetParameters() is [var first, ..])
        {
            derived = types.Face(first.ParameterType);
            signature = $"{first.Name}: {derived}";
        }

        var property = new property.list.@this();
        // A member that needs the asker's context is a one-context method; it is the same property to
        // the catalog, listed where it is declared among the properties.
        var methods = new Queue<System.Reflection.MethodInfo>(clr.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => System.Attribute.IsDefined(m, typeof(global::app.LlmBuilderAttribute)) && m.ReturnType != typeof(void)
                && m.GetParameters() is [{ ParameterType: var p }] && p == typeof(actor.context.@this))
            .OrderBy(m => m.MetadataToken));
        foreach (var prop in clr.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.Name == "EqualityContract") continue;
            if (!System.Attribute.IsDefined(prop, typeof(global::app.LlmBuilderAttribute))) continue;
            while (methods.TryPeek(out var m) && m.DeclaringType == prop.DeclaringType
                && m.MetadataToken < prop.GetMethod!.MetadataToken)
                property.Add(types.Property(methods.Dequeue().Name, m.ReturnType));
            property.Add(types.Property(prop.Name, prop.PropertyType));
        }
        while (methods.TryDequeue(out var m)) property.Add(types.Property(m.Name, m.ReturnType));

        // A scalar has a constructor, a declared wire shape, or is a named type with no builder
        // properties (a domain wrapper around a primitive); a record has builder properties.
        if (signature != null || shape != null || property.Count == 0)
        {
            Shape = derived ?? shape ?? "string";
            ConstructorSignature = signature;
            Property = property.Count > 0 ? property : null;
        }
        else Property = property;
        Description = description;
        Example = example;

        T? Declared<T>(string member) where T : class
        {
            var p = clr.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                            | System.Reflection.BindingFlags.FlattenHierarchy);
            return p?.GetValue(null) as T;
        }
    }

    /// <summary>This type answers for its name or one of its aliases, case-insensitive.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key) => new(Names(key) ? this : null);

    /// <summary>True when <paramref name="key"/> is this type's name or one of its aliases — the
    /// in-memory answer <see cref="Match"/> gives, for the registry's synchronous walk.</summary>
    internal bool Names(string key)
        => string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase)
           || Alias.Contains(key, System.StringComparer.OrdinalIgnoreCase);

    /// <summary>A type answers navigation as its full type — the registry's, found with the
    /// asker's context. A full type is its own answer.</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => new global::app.type.clr.@this(parent.Context.App.Type[this], parent.Context).Get(parent, key);
}
