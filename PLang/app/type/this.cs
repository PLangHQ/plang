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
[JsonConverter(typeof(JsonConverter))]
public sealed class @this
{
    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>
    /// Build-time subtype refinement ("md", "gif", "int"). Null when the type
    /// has no sub-kind. Mutable: <c>Data.Kind</c> delegates set-through to this
    /// slot so the entity is the single owner. Serialized as part of the
    /// entity's <c>{name, kind?, strict?}</c> JSON form (see
    /// <see cref="JsonConverter"/>).
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// When true, <see cref="Kind"/> is a requirement (enforced at build for
    /// literals via <c>app.data.IKindValidatable</c>; deferred to runtime for
    /// <c>%var%</c>). Default false — kind is a hint.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// Catalog teaching for the <c>type</c> entry — the LLM-facing description
    /// surfaced through <c>app.builder.type.TypeSchemas</c>. The instance
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
    public @this(string name, string? kind = null, bool strict = false)
    {
        Name = Canonicalise(name);
        Kind = kind;
        Strict = strict;
        StampPrimitive(name);
        // Numeric precision tokens collapse into Kind when used as a name.
        // `new type("int")` → {Name:"number", Kind:"int"}: the precision
        // wasn't lost in the canonicalisation. Only applies when the caller
        // didn't supply an explicit kind.
        if (Kind == null && Name == "number")
        {
            var lower = name.ToLowerInvariant();
            if (lower is "int" or "integer" or "long" or "float" or "double" or "decimal")
                Kind = lower == "integer" ? "int" : lower;
        }
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
            // Resolve family from Name through the format registry (the
            // family-Kind accessor went away with the rename; family-lookup
            // is now an explicit registry call). Null family → not compressible.
            var family = Context?.App.Format.FamilyOf(Name);
            return family != null && (Context?.App.Format.Compressible(family) ?? false);
        }
    }

    /// <summary>
    /// The "null" type — the type of a Data whose Value is null and no explicit
    /// Type was set.  Replaces the historical <c>Data.Type == null</c> sentinel
    /// so the property can be non-null end-to-end.  Wire serialization skips it
    /// (no "type": "null" emitted) to keep the wire shape identical to the
    /// pre-flip world.  ClrType is <c>typeof(object)</c>, the closest CLR mate.
    /// </summary>
    public static @this Null { get; } = new("null", typeof(object));

    /// <summary>True when this is the <see cref="Null"/> sentinel type.</summary>
    [JsonIgnore]
    public bool IsNull => Name == "null";

    // Static helpers — names match the new canonical primitives. The numeric
    // helpers carry their kind so callers don't have to re-stamp it: Int/Long/
    // Decimal/Double all surface as `number` with a precision kind.
    public static @this String => new("text", typeof(string));
    public static @this Int => new("number", typeof(int)) { Kind = "int" };
    public static @this Long => new("number", typeof(long)) { Kind = "long" };
    public static @this Decimal => new("number", typeof(decimal)) { Kind = "decimal" };
    public static @this Double => new("number", typeof(double)) { Kind = "double" };
    public static @this Bool => new("bool", typeof(bool));
    public static @this DateTime => new("datetime", typeof(System.DateTimeOffset));
    public static @this Object => new("object", typeof(object));

    public static @this FromMime(string mimeType) => new(mimeType);
    public static @this FromName(string typeName) => new(typeName);

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
        actor.context.@this? context = null)
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

        return new @this(name, kind, strict) { Context = context };
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

    public object? Convert(string raw)
    {
        return Name.ToLowerInvariant() switch
        {
            "json" => JsonSerializer.Deserialize<Dictionary<string, object?>>(raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }),
            _ => AppTypes.TryConvertTo(raw, ClrType ?? typeof(object)).Value
        };
    }

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
    public global::app.type.path.scheme.@this? Scheme
        => Name == "path" ? Context.App.Type.Scheme : null;

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
        // propagate Context onto a `type.@this` minted from FromName(...);
        // returning null silently would mask the bug at the read site and
        // surface it as wrong LLM prompts / wrong schema decisions far away.
        if (Context == null)
            throw new System.InvalidOperationException(
                $"type.@this(\"{Name}\") has no Context — schema properties "
                + "(Fields/Values/Example/Shape/etc.) require a stamped entity. "
                + "This is a producer bug: whoever minted this type via FromName "
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
