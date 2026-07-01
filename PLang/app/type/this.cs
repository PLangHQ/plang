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
/// stamps <c>Context</c>, while <c>type.catalog.@this.BuildTypeEntries</c> walks
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
        if (!string.IsNullOrEmpty(Kind)) { writer.Name("kind"); writer.String(Kind!); }
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
    public string? Kind { get; set; }

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
    /// <c>%x%</c> is data, not a template). Null = a plain value. Mirrors <c>text.@this.Template</c>.
    /// </summary>
    public string? Template { get; init; }

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
    public @this(string name, string? kind = null, bool strict = false, string? template = null)
    {
        Name = Canonicalise(name);
        Kind = kind;
        Strict = strict;
        Template = template;
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
        // The mirror direction, numbers only: a precision kind stamps the exact
        // CLR mate ({number, int} → Int32) — the name alone collapses the tower
        // and can't answer it. Other families' kinds are formats ({file, json}),
        // never CLR mates.
        if (_clrType == null && Name == "number" && Kind != null)
            StampPrimitive(Kind);
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
            var family = (Kind != null ? format.TypeOf(Kind) : null) ?? format.FamilyOf(Name);
            return family != null && format.Compressible(family);
        }
    }

    /// <summary>
    /// Produce a value OF THIS TYPE from <paramref name="value"/>, kind-aware.
    /// OBP: a type owns its own construction — callers ask the type to make the
    /// value instead of reaching for <c>Convert.ChangeType</c> themselves. Returns
    /// the converted value, or an Error when the value cannot honestly become this
    /// type. <c>Convert.ChangeType</c> survives only as a leaf inside the general
    /// converter for genuine primitive coercions.
    /// </summary>
    public global::app.type.item.@this Convert(object? value, actor.context.@this context)
    {
        Context ??= context;
        if (value is null) return new global::app.type.@null.@this(Name, Kind);

        // A born-native scalar source (`set %d% = "2026-01-01" as date` makes the literal a
        // text.@this first) — unwrap the leaf wrapper to its raw form so the target family's
        // Convert hook, which speaks raw, can parse it. Mirrors the catalog's item.Clr step;
        // containers (dict/list) are NOT leaves and convert as wholes.
        if (value is global::app.type.item.@this { IsLeaf: true } leaf) value = leaf.Clr<object>();

        // OBP: the concrete type owns its own construction. Resolve the family
        // class (text.@this, number.@this, …) and ask IT to make the value from
        // ours, passing our Kind. This entity only routes — it holds no per-type
        // ("if text", "if number") knowledge. The hub still answers Data (Ok/Error);
        // this door is the throw boundary — a bad conversion throws so it rides the
        // same MaterializeFailed path as a bad reader parse (source.Value catches it).
        var familyClass = context.App.Type[Name]?.ClrType;
        var owned = context.App.Type.Conversions.Of(familyClass, value, Kind, context);
        if (owned != null)
            return owned.Success ? owned.Peek() : throw Failed(owned.Error);

        var target = ClrType
            ?? throw new System.InvalidOperationException($"Unknown type '{Name}'");

        // No family hook — a non-leaf value (dict/list) lowers ITSELF to the CLR mate
        // (dict→record deserialize, list→collection). The value owns it; no hub. Clr is
        // terminal; a real failure rethrows as the convert door's throw.
        if (value is global::app.type.item.@this iv)
        {
            try { return Create(iv.Clr(target), context); }
            catch (System.Exception ex) when (ex is System.InvalidCastException or System.FormatException
                                               or System.NotSupportedException or System.Text.Json.JsonException)
            { throw new System.InvalidOperationException(ex.Message, ex); }
        }

        // A raw CLR input (a wire string → record, a primitive) — the raw→CLR deserialize
        // leaf, the last TryConvert use here; folds into the wire serializer next.
        var (c, err) = global::app.type.catalog.@this.TryConvert(value, target, context);
        if (err != null) throw Failed(err);
        return Create(c, context);

        // The hub/TryConvert report failure as an Error; this door reports it as a throw.
        // source.Value re-authors the identity as MaterializeFailed at the binding, so the
        // per-Error Key is not carried — the message is.
        static System.Exception Failed(global::app.error.IError? error)
            => new System.InvalidOperationException(error?.Message ?? "conversion failed");
    }

    /// <summary>
    /// Build a born-native plang VALUE of this type from a plain value — the read's
    /// one creator. The type owns its construction (kind-aware), and its context
    /// comes from the entity itself (stamped at read time), so the caller just hands
    /// the value: <c>typeRef.Build(5)</c> with <c>{number,int}</c> → <c>number(int 5)</c>,
    /// in one step. A container / domain value is already its native form and rides
    /// through whole; a scalar is built (and re-kinded if it arrived at the wrong
    /// precision) by its family. No lift-then-fix, no <c>clr</c> label.
    /// </summary>
    /// <remarks>Named <c>Build</c>, not <c>Create</c>: the static
    /// <see cref="Create(string, string?, bool, actor.context.@this?)"/> already owns
    /// that name for making a type ENTITY from a name. (A string is an object, so an
    /// instance <c>Create(object)</c> would be ambiguous with it.)</remarks>
    public item.@this Build(object? value, actor.context.@this context, string? format = null)
    {
        // context-never-null: a value is born WITH context. A null here is a construction site
        // (a Data ctor / FromRaw) that forgot to pass one — fail with a pointer, not an NRE deep
        // in materialization. (One-liner to delete once every call site is fixed.)
        if (context is null) throw new System.InvalidOperationException(
            $"context-never-null: building a '{Name}' value without a context — pass the actor context at the Data/FromRaw construction site.");

        // Typed absence — no value to lift; the declaration survives (a typed null,
        // a tool-parameter slot). A JSON-null literal lands here too (the null citizen).
        if (value is null or global::app.type.@null.@this) return new global::app.type.@null.@this(Name, Kind);

        // A raw form (string / byte[]) → defer through a source declared as THIS type,
        // born WITH context. The format the type carries picks the reader (scalar → text,
        // container → json, bytes → kind→mime); a caller may override it (the wire knows the
        // slot's encoding from its token). Parsed once, lazily, on first use. The source carries
        // THIS type's template flag — the value resolves its %refs% only when the build marked
        // it a template (never inferred from content). This is the ONE source-maker (FromRaw
        // delegates here).
        if (value is string or byte[])
            return new item.source(value, Name, Kind, Strict, format ?? RawFormat(value, context), template: Template) { Context = context };

        // A container / domain value is already its native form (dict, list, path, image, …) — hold it.
        if (value is item.@this { IsLeaf: false } native) return native;

        // A built leaf (text/number/… or a source carrying its raw):
        if (value is item.@this leaf)
        {
            // The leaf answers its own raw string face (text's chars, a source's raw); null if it has none.
            var backing = leaf.RawText;
            // A raw-name declared type (variable) NAMES a thing — `%s%` is the variable s, a
            // write-target, not a value to render. Born as the resolved name.
            if (typeof(app.variable.IRawNameResolvable).IsAssignableFrom(context.App.Type[Name]?.ClrType) && backing != null)
                return app.variable.@this.Resolve(backing, context);

            // Already this type → hold; refine a matching leaf to the declared kind /
            // template. The type carries the flags — the builder decided them; runtime
            // trusts them and never inspects the content for %var%.
            var minted = leaf.Mint();
            if (string.Equals(Name, minted.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                var refined = Kind != null && minted.Kind == null ? leaf.Kinded(Kind) : leaf;
                if (Template != null && refined is text.@this rt && rt.Template == null)
                    refined = new text.@this(rt.ToString(), Template) { Kind = rt.Kind };
                return refined;
            }
            // The value already carries this type as a facet (an image satisfies a path slot) → hold.
            if (leaf.Facet(Name) != null) return leaf;
            // A different type → re-type via the per-type hook. Convert is the throw boundary.
            return Convert(leaf, context);
        }

        // A raw CLR scalar handed straight from C# (rare) → convert via the hook.
        return Convert(value, context);
    }

    /// <summary>
    /// The wire format the type's raw form carries — picks the reader a minted
    /// <see cref="item.source"/> materialises through. A container's raw is JSON
    /// (the json reader streams it); a byte-backed family's raw is its bytes under
    /// the kind's mime; everything else is a scalar text token.
    /// </summary>
    private string RawFormat(object raw, actor.context.@this context)
        => raw is byte[]
            ? context.App.Format.Mime("." + (Kind ?? "")) ?? "application/octet-stream"
            : string.Equals(Name, "dict", System.StringComparison.OrdinalIgnoreCase)
              || string.Equals(Name, "list", System.StringComparison.OrdinalIgnoreCase)
                ? "application/plang"
                : global::app.channel.serializer.Text.Mime;

    /// <summary>
    /// The driving type for a comparison between a value of this type and
    /// <paramref name="other"/> — the higher-ranked (more specific) of the two
    /// (number outranks text, the date family outranks text, text is the floor).
    /// Rank is decided from the types alone: this never reads a value, so a pending
    /// source stays unread. Same driver regardless of operand order ⇒ antisymmetry.
    /// </summary>
    public @this Rank(global::app.data.@this other)
    {
        var otherType = other.Type;
        var compares = Context?.App.Type.Compares ?? otherType.Context?.App.Type.Compares ?? new compare.@this();
        var mine = compares.RankOf(FamilyForCompare(this, other: null));
        var theirs = compares.RankOf(FamilyForCompare(otherType, other));
        return theirs > mine ? otherType : this;
    }

    /// <summary>
    /// Compares two already-materialised values as THIS type (the driver): the
    /// family's hook coerces whichever side isn't of this kind into it, then
    /// orders/equates in caller order — <c>a</c> is left, so <c>Less</c> means
    /// <c>a &lt; b</c>, no sign flip. A family with no hook (or a failed coercion)
    /// answers <see cref="global::app.data.Comparison.Incomparable"/>.
    /// </summary>
    public global::app.data.Comparison Compare(object? a, object? b)
    {
        var compares = Context?.App.Type.Compares ?? new compare.@this();
        // Name-keyed family first; an unknown name (a choice surfaces under its
        // enum's name, a perimeter raw scalar under a CLR-ish name) falls back to
        // the VALUE's owner — the closed generic carrying the hooks, or the family
        // that declares the raw CLR type (ushort → number) via convert's ownership.
        // Only a HOOK-BEARING name-keyed family drives; a hookless family class
        // (a reference type like `file`, whose scalar form is its content) defers
        // to the materialised value's own family (the content's kind).
        var named = compare.@this.FamilyOf(Name);
        var family = (named != null && HasHook(named) ? named : null)
            ?? FamilyOfValue(a) ?? FamilyOfValue(b);
        return compares.Of(family, a, b) ?? global::app.data.Comparison.Incomparable;

        static System.Type? FamilyOfValue(object? v)
        {
            if (v == null) return null;
            var t = v.GetType();
            if (HasHook(t)) return t;
            var (owner, _) = convert.@this.OwnerOf(t);
            return owner != null && HasHook(owner) ? owner : null;
        }

        static bool HasHook(System.Type t) => t.GetMethod("Compare",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.FlattenHierarchy,
            null, new[] { typeof(object), typeof(object) }, null) != null;
    }

    // The family class driving rank/compare for a type entity. Name-keyed via the
    // static map; a name the map doesn't know (a choice surfaces under its enum's
    // name) falls back to the in-memory value's own class — closed generics carry
    // the static hooks, and Peek forces nothing.
    private static System.Type? FamilyForCompare(@this entity, global::app.data.@this? other)
    {
        var family = compare.@this.FamilyOf(entity.Name);
        if (family != null) return family;
        return other?.Peek()?.GetType();
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
    public static @this Int => new("number", typeof(int)) { Kind = "int" };
    public static @this Long => new("number", typeof(long)) { Kind = "long" };
    public static @this Decimal => new("number", typeof(decimal)) { Kind = "decimal" };
    public static @this Double => new("number", typeof(double)) { Kind = "double" };
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
    public static item.@this Create(object? raw, global::app.actor.context.@this? context = null)
    {
        // The null VALUE is a typed citizen — the instance member is never
        // C# null, so no consumer ever null-checks the value slot.
        if (raw is null) return global::app.type.@null.@this.Instance;
        if (raw is global::app.type.item.@this already) return already;
        if (raw is global::app.data.@this)
            throw new System.InvalidOperationException(
                "A bare Data may not be stored as a value — nested Data always rides inside an owning wrapper type. "
                + "This is the implicit-operator double-wrap accident: return the inner value via its own factory, never `return innerDataInstance;`."
                + System.Environment.StackTrace);

        // A value cannot be born without a context. There is no context-less value
        // in the codebase — period. A null context here is a caller that constructed
        // a value (or a Data) without one, or used construct-then-stamp ({ Context = …}
        // runs after the ctor builds the value). Born-with-context: pass the context at
        // construction. The throw flags the offending caller via the stack trace.
        if (context == null)
            throw new System.InvalidOperationException(
                $"A {raw.GetType().Name} value cannot be born without a context. "
                + "type.Create(raw, context) requires a non-null context — born-with-context, never construct-then-stamp "
                + "({ Context = … } sets the wrapper after the value is already built). Fix the caller that passed null.\n"
                + System.Environment.StackTrace);

        // A sequence of Data builds a native list DIRECTLY, preserving the actual
        // Data instances — their names, types and signatures.
        if (raw is System.Collections.Generic.IEnumerable<global::app.data.@this> dataSeq)
            return new global::app.type.list.@this(dataSeq) { Context = context! };

        // A sequence of native plang VALUES (item.@this) narrows to a native list
        // that owns the wrapping (no JSON round-trip that would degrade strong values).
        if (raw is System.Collections.Generic.IEnumerable<global::app.type.item.@this> itemSeq)
            return new global::app.type.list.@this(itemSeq) { Context = context! };

        // Foreign C# containers narrow to their native plang type. The common handoff
        // shapes alias their backing BY REFERENCE — O(1), no walk, no JSON (a
        // million-row List<object?> costs one pointer copy); other shapes narrow off
        // the wire. byte[] is excluded — bytes are the binary leaf, not a list.
        if (raw is System.Collections.Generic.List<object?> objList)
            return new global::app.type.list.@this(objList) { Context = context! };
        if (raw is System.Collections.Generic.Dictionary<string, object?> objDict)
            return new global::app.type.dict.@this(objDict) { Context = context! };
        if (raw is System.Collections.IDictionary
            || (raw is System.Collections.IList && raw is not byte[]))
            return new global::app.type.item.serializer.json(context).Parse(
                       System.Text.Json.JsonSerializer.SerializeToElement(raw))
                   as global::app.type.item.@this
               ?? throw new System.InvalidOperationException(
                   $"A raw C# container ({raw.GetType().Name}) could not be narrowed to a native plang list/dict.");

        var (family, _) = global::app.type.convert.@this.OwnerOf(raw.GetType());
        if (family != null && typeof(global::app.type.item.@this).IsAssignableFrom(family))
        {
            var lifted = global::app.type.convert.@this.OfStatic(family, raw, kind: null, context: context);
            if (lifted is { Success: true } && lifted.Peek() is global::app.type.item.@this wrapper)
                return wrapper;
        }
        // A CLR enum IS plang's choice (a closed named set) — build choice<T> for the enum.
        if (raw is System.Enum)
        {
            var choiceType = typeof(global::app.type.choice.@this<>).MakeGenericType(raw.GetType());
            return (global::app.type.item.@this)System.Activator.CreateInstance(choiceType, raw)!;
        }
        // Unowned — rung 2: a strongly-typed C# object rides as item with kind naming
        // the class; the carrier's Peek answers the real instance.
        return new Clr(raw) { Context = context };
    }
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
        && string.Equals(Kind, other.Kind, System.StringComparison.OrdinalIgnoreCase)
        && Strict == other.Strict;

    public override int GetHashCode() => System.HashCode.Combine(
        Name.ToLowerInvariant(), Kind?.ToLowerInvariant(), Strict);

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
        var other = Context?.App.Type[typeName] ?? FromName(typeName);
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
    public global::app.type.path.scheme.@this? Scheme
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
