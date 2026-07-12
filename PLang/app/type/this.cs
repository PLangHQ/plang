using System.Text.Json;
using System.Text.Json.Serialization;
using app;
using app.actor.context;

namespace app.type;

/// <summary>
/// PLang type entity carrying the <c>{Name, Kind, Strict}</c> identity plus the
/// folded catalog knowledge (Fields, Values, Properties, Shape,
/// ConstructorSignature, Example, Description, Kinds).
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
[JsonConverter(typeof(json))]
public sealed class @this : item.@this
{
    /// <summary>Self-write: the type entity's <c>{name, kind?, strict?}</c> identity — the same
    /// shape Data writes for its <c>type</c> field, used when a type entity rides as a VALUE
    /// (e.g. a <c>variable.set</c> <c>Type</c> default).</summary>
    public override System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        writer.Name("name"); writer.String(Name);
        if (Kind != null) { writer.Name("kind"); writer.String(Kind.Name); }
        if (Strict) { writer.Name("strict"); writer.Bool(true); }
        if (!string.IsNullOrEmpty(Template)) { writer.Name("template"); writer.String(Template!); }
        writer.EndObject();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>
    /// Build-time subtype refinement ("md", "gif", "int"). Null when the type
    /// has no sub-kind. Mutable: <c>Data.Kind</c> delegates set-through to this
    /// slot so the entity is the single owner. Serialized as part of the
    /// entity's <c>{name, kind?, strict?}</c> JSON form (see
    /// <see cref="json"/>).
    /// </summary>
    public global::app.type.kind.@this? Kind { get; set; }

    /// <summary>
    /// When true, <see cref="Kind"/> is a requirement (enforced at build for
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

    // Context is the *runtime* invariant — once a Data is stamped (Variables.Set,
    // Action.RunAsync), the entity reads through the registry.  Before stamping,
    // the entity falls through to the static primitive surface so type-entity
    // instantiated purely for its identity (e.g. `new type("string")`, file
    // mime-deriving an extension before any Data wraps it) still answers the
    // ClrType question without an App in scope.
    [JsonIgnore]
    internal actor.context.@this? Context { get; set; }

    [JsonConstructor]
    public @this(string name, string? kind = null, bool strict = false, string? template = null)
    {
        Name = Canonicalise(name);
        Kind = kind is null ? null : new global::app.type.kind.@this(kind);
        Strict = strict;
        Template = template;
        // The Create doors start pointing at the one-shot binder, which swaps itself for the
        // closed thunk (or the decline) on first use — every later call is a bare delegate
        // invocation, no null check. Field initializers can't reference `this`, so bind here.
        _byContext = Bind;
        _byData = Bind;
        StampPrimitive(name);
        // Numeric precision tokens collapse into Kind when used as a name.
        // `new type("int")` → {Name:"number", Kind:"int"}: the precision
        // wasn't lost in the canonicalisation. Only applies when the caller
        // didn't supply an explicit kind.
        if (Kind == null && Name == "number")
        {
            var lower = name.ToLowerInvariant();
            if (lower is "int" or "integer" or "long" or "float" or "double" or "decimal")
                Kind = new global::app.type.kind.@this(lower == "integer" ? "int" : lower);
        }
        // The mirror direction, numbers only: a precision kind stamps the exact
        // CLR mate ({number, int} → Int32) — the name alone collapses the tower
        // and can't answer it. Other families' kinds are formats ({file, json}),
        // never CLR mates.
        if (_clrType == null && Name == "number" && Kind != null)
            StampPrimitive(Kind.Name);
    }

    private void StampPrimitive(string rawName)
    {
        // When the canonical name folds away the original CLR mate hint
        // (e.g. `int`→`number`, `text` whose CLR is typeof(string)), stamp
        // ClrType from the alias the caller passed in so the entity still
        // answers the .ClrType question without a registry round-trip.
        // Primitives have no catalog fold data, so mark fold as loaded too —
        // keeps Promote()'s Context guard from firing on fold-prop reads.
        if (app.type.primitive.@this.Aliases.TryGetValue(rawName.ToLowerInvariant(), out var clr))
        {
            _clrType = clr;
            _foldLoaded = true;
        }
    }

    /// <summary>
    /// CLR mate for this type's name. Internal — the public PLang surface is
    /// name-keyed (the registry's <c>App.Type.Clr(name)</c> / <c>Get(name)</c>
    /// is the door for interior consumers).
    /// </summary>
    [JsonIgnore]
    internal System.Type? ClrType => _clrType ?? Context?.App.Type.Clr(Name) ?? AppTypes.GetPrimitiveOrMime(Name);
    private System.Type? _clrType;

    /// <summary>True when content of this type benefits from compression.</summary>
    [JsonIgnore]
    public bool Compressible
    {
        get
        {
            var format = Context?.App.Format;
            if (format == null) return false;
            // Binary content carries its true family in the Kind (jpg→image,
            // mp3→audio): the Name is just "binary". A native value carries it
            // in the Name (text, archive). Resolve the Kind's family first, fall
            // back to the Name's. Null family → not compressible.
            var family = (Kind != null ? format.TypeOf(Kind.Name) : null) ?? format.FamilyOf(Name);
            return family != null && format.Compressible(family);
        }
    }


    /// <summary>
    /// The wire format the type's raw form carries — picks the reader a minted
    /// <see cref="item.source"/> materialises through. A container's raw is JSON
    /// (the json reader streams it); a byte-backed family's raw is its bytes under
    /// the kind's mime; everything else is a scalar text token.
    /// </summary>
    internal string RawFormat(object raw, actor.context.@this context)
        => raw is byte[]
            ? context.App.Format.Mime("." + (Kind?.Name ?? "")) ?? "application/octet-stream"
            : string.Equals(Name, "dict", System.StringComparison.OrdinalIgnoreCase)
              || string.Equals(Name, "list", System.StringComparison.OrdinalIgnoreCase)
                ? "application/plang"
                : global::app.channel.serializer.Text.Mime;

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
    /// True for the bare polymorphic stamps ({object} / {item}, no kind, not
    /// strict) — "any value" is a shape note, not a judgement; the entry fold
    /// skips it and the value's own truth stands.
    /// </summary>
    [JsonIgnore]
    public bool Polymorphic => Kind == null && !Strict
        && (string.Equals(Name, "object", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(Name, "item", System.StringComparison.OrdinalIgnoreCase));

    // Static helpers — names match the new canonical primitives. The numeric
    // helpers carry their kind so callers don't have to re-stamp it: Int/Long/
    // Decimal/Double all surface as `number` with a precision kind.
    public static @this String => new("text", typeof(string));
    public static @this Int => new("number", typeof(int)) { Kind = new kind.@this("int") };
    public static @this Long => new("number", typeof(long)) { Kind = new kind.@this("long") };
    public static @this Decimal => new("number", typeof(decimal)) { Kind = new kind.@this("decimal") };
    public static @this Double => new("number", typeof(double)) { Kind = new kind.@this("double") };
    public static @this Bool => new("bool", typeof(bool));
    public static @this DateTime => new("datetime", typeof(System.DateTimeOffset));
    public static @this Object => new("object", typeof(object));

    public static @this FromMime(string mimeType) => new(mimeType);

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
    public item.@this Create(object? raw, global::app.actor.context.@this? context, string? format = null)
    {
        // context-never-null: a value is born WITH context. A null here is a construction site that
        // forgot to pass one — fail with a pointer, not an NRE deep in materialization.
        if (context is null) throw new System.InvalidOperationException(
            $"context-never-null: building a '{Name}' value without a context — pass the actor context at the construction site.");

        // Typed absence — the declaration survives (a typed null, a tool-parameter slot; a JSON-null too).
        if (raw is null or global::app.type.item.@null.@this) return new global::app.type.item.@null.@this(Name, Kind?.Name);

        // A raw-name declared type (variable) NAMES a thing — the name IS the variable (a write-target),
        // not a value to defer. Before the string→source branch (else the name becomes a deferred source).
        if (raw is string rawName && context.App.Type[Name]?.ClrType == typeof(app.variable.@this))
            return app.variable.@this.Resolve(rawName, context);

        // Wire-raw (string / byte[]) → defer through a source declared as THIS type, parsed lazily on
        // first use. The source carries the type's Name/Kind/Strict/template; the wire may override format.
        if (raw is string or byte[])
            return new item.source(raw, this, context, format);

        // A container / domain value is already native (dict, list, path, image, …) — hold it.
        if (raw is item.@this { IsLeaf: false } native) return native;

        // A source (declared, unparsed) re-declared → RE-BIRTH over the same unread raw with THIS
        // declaration (which carries the build's stamped kind/template). The value stays immutable
        // and lazy — no mutation, no parse — so an authored %ref% is still unread bytes until read.
        // This is the build's template stamp: Declare → Create → here, a fresh source born correct.
        if (raw is item.source src)
            return new item.source(src.Raw, this, context, src.Format);

        // A built leaf (text/number/… carrying its raw):
        if (raw is item.@this leaf)
        {
            // A raw-name declared type (variable) NAMES a thing — the leaf's raw string is the variable.
            var backing = leaf.RawText;
            if (context.App.Type[Name]?.ClrType == typeof(app.variable.@this) && backing != null)
                return app.variable.@this.Resolve(backing, context);

            // Already this type → hold; refine a matching leaf to the declared kind.
            var minted = leaf.Mint();
            if (string.Equals(Name, minted.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                var refined = Kind != null && minted.Kind == null ? leaf.Kinded(Kind.Name) : leaf;
                return refined;
            }
            // The value already carries this type as a facet (an image satisfies a path slot) → hold.
            if (leaf.Facet(Name) != null) return leaf;
            // A different type → unwrap to the leaf's raw CLR form, then re-type EAGERLY via the family
            // courier (kind-aware build — path parses a string, number parses a token). A decline lands
            // its reason on the carrier's Error — this door is the throw boundary (rides MaterializeFailed).
            var lowered = leaf.Clr<object>();
            var carrier = new global::app.data.@this("", new global::app.type.item.@null.@this(Name, Kind?.Name), context: context);
            if (Create(lowered, carrier) is { } made) return made;
            if (carrier.Error != null) throw Failed(carrier.Error);
            // No family hook — the general CLR-target converter builds the mate; lift it back.
            var target = ClrType ?? throw new System.InvalidOperationException($"Unknown type '{Name}'");
            var (mate, mateErr) = global::app.type.list.@this.TryConvert(lowered, target, context);
            if (mateErr != null) throw Failed(mateErr);
            return Create(mate, context);
        }

        // A raw CLR scalar (int, DateOnly, …) → born through THIS family's own lift, then refine to the
        // declared type/kind. A non-family declared type routes the raw through the collection perimeter
        // (the owner's lift or a clr carrier). The family lift speaks raw natively; refine re-enters here.
        if (_byContext(raw, context) is { } lifted)
            return string.Equals(Name, lifted.Mint().Name, System.StringComparison.OrdinalIgnoreCase)
                ? lifted : Create(lifted, context);
        return Create(context.App.Type.Create(raw, context), context, format);

        static System.Exception Failed(global::app.error.IError? error)
            => new System.InvalidOperationException(error?.Message ?? "conversion failed");
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

    // The one eligibility check both binders share: the entity's ClrType when it is an ICreate<clr>
    // family — ICreate<clr> SPECIFICALLY (a subtype implementing ICreate<base>, e.g. FilePath :
    // ICreate<path>, can't close Create<subtype>); null for a primitive/host entity, whose doors
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

    /// <summary>
    /// Normalising factory — the single entry point the LLM, build pipeline,
    /// and tests reach. Canonicalises <paramref name="name"/> (currently
    /// preserves the input via <c>primitive.Aliases</c>; stage 2 lands
    /// <c>string</c>→<c>text</c>), splits a single-string slash form
    /// ("text/markdown" → name=text, kind=markdown) onto first slash, rejects
    /// empty/whitespace names. Multi-slash splits on the first; the rest is a
    /// free-string <paramref name="kind"/>.
    /// </summary>
    public static @this Create(string name, string? kind = null, bool strict = false,
        actor.context.@this? context = null, string? template = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new System.ArgumentException("type name is required (empty or whitespace not allowed).", nameof(name));

        // Single-string slash form: "text/markdown" → name=text, kind=markdown.
        // Only fold the slash into kind when caller did NOT pass a kind
        // explicitly — Create("text", "md") stays as-is, even if name itself
        // had no slash.
        if (kind == null && name.Contains('/'))
        {
            var slash = name.IndexOf('/');
            kind = name[(slash + 1)..];
            name = name[..slash];
            if (string.IsNullOrWhiteSpace(name))
                throw new System.ArgumentException("type name is required (empty or whitespace not allowed).", nameof(name));
        }

        // Canonicalise name through the primitive alias table — `Text`→`text`,
        // `STRING`→`text` (post-Stage-2). Unknown names lowercase through.
        name = Canonicalise(name);

        // Canonicalise kind through the format registry when a context is
        // available — `markdown`→`md`, `jpeg`→`jpg`. Unknown kinds pass through.
        if (kind != null && context != null)
            kind = context.App.Format.CanonicaliseKind(kind);

        return new @this(name, kind, strict, template) { Context = context };
    }

    private static string Canonicalise(string name)
    {
        // PLang type names are case-insensitive. Fold through the alias table
        // to land on the canonical name: `string`→`text`, `integer`→`number`,
        // etc. Unknown names lowercase through unchanged.
        var lower = name.ToLowerInvariant();
        if (app.type.primitive.@this.Aliases.TryGetValue(lower, out var clr))
        {
            if (app.type.primitive.@this.Canonical.TryGetValue(clr, out var canonical))
                return canonical;
        }
        return lower;
    }

    public override string ToString() => Name;

    /// <summary>
    /// Value equality — the entity is minted on ask now, so two asks yield two
    /// instances; identity is {Name, Kind, Strict}, case-insensitive.
    /// </summary>
    public override bool Equals(object? obj) =>
        obj is @this other
        && string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase)
        && string.Equals(Kind?.Name, other.Kind?.Name, System.StringComparison.OrdinalIgnoreCase)
        && Strict == other.Strict;

    public override int GetHashCode() => System.HashCode.Combine(
        Name.ToLowerInvariant(), Kind?.Name.ToLowerInvariant(), Strict);

    public object? Convert(string raw)
    {
        if (string.Equals(Name, "json", System.StringComparison.OrdinalIgnoreCase))
        {
            // Parse to the native value graph (dict / list / primitives), not a
            // raw Dictionary — collections hold Data, and an unexamined json
            // object narrows to `dict` on first touch.
            var parsed = JsonSerializer.Deserialize<object?>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new item.serializer.json(Context!).Parse(parsed);
        }

        // Kinded scalar read-back: a string-shaped type whose value carries a
        // sub-format the CLR type can't express (a hash's algorithm) reconstructs
        // through its own `static object? FromWire(string raw, string? kind)`.
        // Discovered by convention so the core type system rebuilds a kinded
        // value without referencing the (possibly module-owned) type — the Kind
        // on this entity is the only thing it can't read off the wire string.
        var clr = ClrType;
        if (clr != null)
        {
            var reader = WireReader(clr);
            if (reader != null)
                return reader.Invoke(null, new object?[] { raw, Kind });
        }

        return AppTypes.TryConvert(raw, clr ?? typeof(object), Context).Value;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, System.Reflection.MethodInfo?> _wireReaders = new();

    internal static System.Reflection.MethodInfo? WireReader(System.Type clrType)
        => _wireReaders.GetOrAdd(clrType, static t =>
        {
            var m = t.GetMethod("FromWire",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string), typeof(string) },
                modifiers: null);
            return m != null && m.ReturnType == typeof(object) ? m : null;
        });

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
    // --- Accumulated identity (the narrow chain) ---
    //
    // A reference narrows to its content type on examination: `read config.json`
    // is a `file`; navigating it parses the content and the SAME Data's type
    // becomes `dict` — with `file` RETAINED here. Identity accumulates, so
    // post-narrow the value `.Is(dict)` AND `.Is(file)`, and `!` resolves
    // chain-wide (`%config!file!path%` works on both branches).
    private System.Collections.Generic.List<@this>? _priors;

    /// <summary>
    /// The identity chain, newest (headline) first: post-narrow
    /// <c>[dict, file]</c>. A value that never narrowed is just <c>[self]</c>
    /// (the static lattice answers the rest). JsonIgnore — self-inclusive, so
    /// a reflective wire walk would cycle; the chain is a navigation surface
    /// (<c>%x!type.list%</c>), not a wire shape.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<@this> List
    {
        get
        {
            var chain = new List<@this> { this };
            if (_priors != null) chain.AddRange(_priors);
            return chain;
        }
    }

    /// <summary>
    /// Retain <paramref name="prior"/> (and its accumulated chain) behind this
    /// headline — the narrow calls this when the content type replaces the
    /// reference type on a Data.
    /// </summary>
    public void Accumulate(@this prior)
    {
        _priors ??= new List<@this>();
        foreach (var entry in prior.List)
            if (!_priors.Any(p => string.Equals(p.Name, entry.Name, System.StringComparison.OrdinalIgnoreCase))
                && !string.Equals(entry.Name, Name, System.StringComparison.OrdinalIgnoreCase))
                _priors.Add(entry);
    }

    /// <summary>The chain entry whose name matches, or null — the chain-wide
    /// <c>!</c> lookup (<c>%x!file%</c> reaches the file facet post-narrow).</summary>
    public new @this? Facet(string name) =>
        List.FirstOrDefault(t => string.Equals(t.Name, name, System.StringComparison.OrdinalIgnoreCase));

    public bool Is(@this? other)
    {
        if (other == null) return false;
        if (string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase)) return true;
        // `item` is the apex of the value-type lattice (≈ C# object) — every value
        // is-a item. The narrow (item+kind=json → dict/list) keeps this true.
        if (string.Equals(other.Name, "item", System.StringComparison.OrdinalIgnoreCase)) return true;
        // Accumulated identity — a narrowed value still IS what it was.
        if (_priors != null && _priors.Any(p => p.Is(other))) return true;
        var thisClr = ClrType;
        var otherClr = other.ClrType;
        if (thisClr == null || otherClr == null) return false;
        // Walk the inheritance chain by CLR-type identity (transitive: image : path,
        // path : X ⟹ image Is X), guarding against self/cycles.
        return Reaches(thisClr, otherClr, new HashSet<System.Type>());
    }

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
        if (string.Equals(Name, typeName, System.StringComparison.OrdinalIgnoreCase)) return true;
        var other = Context?.App.Type[typeName] ?? new @this(typeName);
        other.Context ??= Context;
        return Is(other);
    }

    private static bool Reaches(System.Type clr, System.Type target, HashSet<System.Type> seen)
    {
        if (!seen.Add(clr)) return false;
        foreach (var parent in Parents(clr))
        {
            // Assignability, not identity: a family's PLang name may resolve to a
            // VARIANT class (the "path" entity can carry FilePath as its CLR mate
            // — variants resolve to the family name), and the lattice declares the
            // family base. Either direction of the base/variant pair satisfies.
            if (parent == target || parent.IsAssignableFrom(target) || target.IsAssignableFrom(parent)) return true;
            if (Reaches(parent, target, seen)) return true;
        }
        return false;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, IReadOnlyList<System.Type>> _parentsByClr = new();

    /// <summary>The types a concrete type inherits, from its <c>static IReadOnlyList&lt;System.Type&gt; Type</c> (self included).</summary>
    private static IReadOnlyList<System.Type> Parents(System.Type clrType)
        => _parentsByClr.GetOrAdd(clrType, static t =>
        {
            var prop = t.GetProperty("Type",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy);
            var raw = prop?.GetValue(null);
            if (raw is IReadOnlyList<System.Type> list) return list;
            if (raw is IEnumerable<System.Type> seq) return seq.ToList();
            return System.Array.Empty<System.Type>();
        });

    // --- Catalog properties (init-only; promoted lazily) ---

    private IReadOnlyList<Field>? _fields;
    private IReadOnlyList<string>? _values;
    private IReadOnlyList<Field>? _properties;
    private string? _shape;
    private string? _constructorSignature;
    private string? _example;
    private string? _description;
    private IReadOnlyList<string>? _kinds;
    private bool _foldLoaded;

    // --- Catalog fold props ---
    // The entity's JSON wire form is owned by JsonConverter ({name, kind?,
    // strict?}); STJ never reflects these. They are in-memory catalog
    // navigation (%x.Type.Fields%) + the builder schema's typed reads.

    /// <summary>Record fields. Non-null marks this as a record-shape type.</summary>
    public IReadOnlyList<Field>? Fields { get => Promote()._fields; init => _fields = value; }

    /// <summary>Enum values. Non-null marks this as an enum-shape type.</summary>
    public IReadOnlyList<string>? Values { get => Promote()._values; init => _values = value; }

    /// <summary>Read-only navigation properties for scalar types.</summary>
    public IReadOnlyList<Field>? Properties { get => Promote()._properties; init => _properties = value; }

    /// <summary>Scalar wire shape (the underlying primitive form, e.g. "string" for path).</summary>
    public string? Shape { get => Promote()._shape; init => _shape = value; }

    /// <summary>Constructor signature for scalar types (<c>"name: shape"</c>).</summary>
    public string? ConstructorSignature { get => Promote()._constructorSignature; init => _constructorSignature = value; }

    /// <summary>Canonical example from a static <c>Example</c> property on the type.</summary>
    public string? Example { get => Promote()._example; init => _example = value; }

    /// <summary>Semantic description from a static <c>Description</c> property on the type.</summary>
    public string? Description { get => Promote()._description; init => _description = value; }

    /// <summary>
    /// Developer-meaningful kind vocabulary from a static <c>Kinds</c> property.
    /// Advertised vocabulary the LLM may emit; distinct from <see cref="Kind"/>
    /// (the per-value subtype) and the build-hook dispatcher
    /// <c>App.Type.KindHooks</c>.
    /// </summary>
    public IReadOnlyList<string>? Kinds { get => Promote()._kinds; init => _kinds = value; }

    /// <summary>Alias for <see cref="Values"/> — enum members the LLM may emit.</summary>
    [JsonIgnore]
    public IReadOnlyList<string>? ValidValues => Values;

    /// <summary>Path-scheme registry for the path entity. Null when this type is not path.</summary>
    [JsonIgnore]
    public global::app.type.item.path.scheme.@this? Scheme
        => Name == "path" ? Context?.App.Type.Scheme : null;

    // Construct with a stamped ClrType (used by BuildTypeEntries and by the
    // type-list indexer's primitive fallback path; both spare the registry
    // round-trip).  The primitive path also has no fold data — primitives are
    // not in ComplexSchemas — so mark fold as already-loaded; that keeps
    // Promote()'s Context check from firing for `app.Type["string"]`.
    internal @this(string name, System.Type? clrType) : this(name)
    {
        _clrType = clrType;
        _foldLoaded = true;
    }

    private @this Promote()
    {
        if (_foldLoaded) return this;
        // Already populated by an init-only setter — no promotion needed.
        if (_fields != null || _values != null || _properties != null
            || _shape != null || _constructorSignature != null
            || _example != null || _description != null || _kinds != null)
        {
            _foldLoaded = true;
            return this;
        }
        _foldLoaded = true;
        // Fold properties (Fields/Values/Example/Shape/...) are App-keyed —
        // resolving them requires the registry, which requires Context. An
        // unstamped entity reaching this point means a producer forgot to
        // propagate Context onto a `type.@this` minted without a context;
        // returning null silently would mask the bug at the read site and
        // surface it as wrong LLM prompts / wrong schema decisions far away.
        if (Context == null)
            throw new System.InvalidOperationException(
                $"type.@this(\"{Name}\") has no Context — schema properties "
                + "(Fields/Values/Example/Shape/etc.) require a stamped entity. "
                + "This is a producer bug: whoever minted this type without a context "
                + "did not propagate Context. Primitive identity reads "
                + "(.Name/.ClrType) do not hit this path.");
        if (!Context.App.Type.ComplexSchemas().TryGetValue(Name, out var match)) return this;
        _fields = match._fields;
        _values = match._values;
        _properties = match._properties;
        _shape = match._shape;
        _constructorSignature = match._constructorSignature;
        _example = match._example;
        _description = match._description;
        _kinds = match._kinds;
        if (_clrType == null) _clrType = match._clrType;
        return this;
    }
}
