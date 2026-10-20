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
/// stamps <c>Context</c>. A type reads its facts (Description, Property, Values,
/// …) through its <see cref="Family"/> entry and its teaching files, each on
/// first read.</para>
/// </summary>
// `type` is an item (settled in the value model): the type entity is a plang
// value — authored in the language (`as image/gif, strict`), riding in the .pr,
// holdable in a variable (`set %t% = %x!type%`). TypeName derives from the
// namespace ("type"); behavior defaults from the item base.
[global::app.Attributes.PlangType("type")]
public class @this : item.@this, item.ICreate<@this>, item.IMatch<@this>, item.ILoad<@this>,
    item.IList<@this, list.@this>
{
    /// <summary>The types' list — the app's types, with the lookups by other keys.</summary>
    public static list.@this List(global::app.@this app) => new();

    /// <summary>Writes the type's identity — <c>{name, kind?, strict?, template?}</c>: what a type is written as in json
    /// and plang (a Data's and a property row's type slot, a type held as a value). A nested kind is written as its own
    /// identity, not as a kind held as a value (its name).</summary>
    private protected void Identify(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("name"); writer.String(Name);
        // a kind nests (774): a kind that is itself kinded writes as the object it is ({list, kind: {list, kind:
        // path}}), so read and write are symmetric; a plain kind writes its name.
        if (!kind.IsEmpty) { writer.Name("kind"); if (kind.kind.IsEmpty) writer.String(kind.Name); else kind.Identify(writer); }
        if (Strict) { writer.Name("strict"); writer.Bool(true); }
        if (Template != null) { writer.Name("template"); writer.String(Template.Name); }
        writer.EndObject();
    }

    /// <summary>A type is its identity, in every format it has no look of its own for.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        Identify(writer);
        return default;
    }

    /// <summary>A type in text is the word it goes by with its kind (<c>text</c>, <c>list&lt;path&gt;</c>), as a
    /// <c>.goal</c> writes it — what a program sees when it writes <c>%!app.type.text%</c>. Its facts (name, description,
    /// example, alias, kind) are reached by navigation (<c>%!app.type.text.description%</c>).</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.item.text.Writer writer)
    {
        writer.String(Face);
        return default;
    }

    /// <summary>The word a type goes by with its kind (<c>text</c>, <c>list&lt;path&gt;</c>): what text writes, and what a
    /// formal row names its type by (<c>Path: path = …</c>).</summary>
    internal virtual string Face => kind.IsEmpty ? Name : $"{Name}<{kind.Name}>";

    /// <summary>The name the type goes by — the word its class declares (<c>text</c>), else its
    /// <see cref="Namespace"/>. What the wire, a <c>.pr</c> slot and the prompts write.</summary>
    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>The type's identity: its class's namespace (<c>app.type.item.text</c>,
    /// <c>app.channel.type.goal</c>) — the type answers to it as to its name. Null for a type known only by
    /// the name a slot spelled (<c>{"name":"text"}</c>), which the types resolve to their entry.</summary>
    [JsonIgnore]
    public string? Namespace { get => Family._namespace; init => _namespace = value; }
    private string? _namespace;

    /// <summary>
    /// The subtype refinement ("md", "gif", "int"). Never null: a type with no kind has its empty
    /// kind (<see cref="global::app.type.kind.@this.IsEmpty"/>), which knows this type and is never
    /// written. Read-only once born: a type object is shared (a program row's declared type, the
    /// registry's entries), so a different kind is a different type object. Given no kind, the type
    /// takes its empty kind.
    /// </summary>
    // JsonIgnore: a kind is a type now, so walking this member in raw STJ reflection recurses
    // (kind.kind.kind…). The kind's wire form is its name, written by Write/Output, never by STJ.
    [System.Text.Json.Serialization.JsonIgnore]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public global::app.type.kind.@this kind
    {
        get => _kind;
        init => _kind = value ?? BornKind();
    }
    private readonly global::app.type.kind.@this _kind;

    /// <summary>This type's empty kind — a plain type mints one for its name. A kind is itself a type
    /// (<c>kind : type</c>); the empty kind answers itself here (never a new one), ending the ctor's
    /// kind-minting chain. Serializers are kept off <see cref="kind"/> (JsonIgnore), so the self-kind
    /// never loops a walk.</summary>
    protected virtual global::app.type.kind.@this BornKind() => new global::app.type.kind.empty.@this(this);

    /// <summary>The kind a value class declares for <paramref name="name"/> — or its own format for the empty
    /// name — read from the class's <c>[Format]</c>: the facts (MIME, extension, whether it is characters) a
    /// value carries without asking the app, a kind of <paramref name="owner"/>, the class-built type asking. Null
    /// when the class declares no such format, so the caller keeps the bare by-name kind. Only a class-built type
    /// asks; it goes with the implicit-conversion branch.</summary>
    private static global::app.type.kind.@this? KindOfClass(System.Type clr, string? name, @this owner)
    {
        var wanted = name ?? "";
        global::app.Attributes.FormatAttribute? format = null;
        foreach (var attribute in clr.GetCustomAttributes(typeof(global::app.Attributes.FormatAttribute), inherit: false))
            if (attribute is global::app.Attributes.FormatAttribute fmt
                && string.Equals(fmt.Name, wanted, System.StringComparison.OrdinalIgnoreCase)) { format = fmt; break; }
        if (format == null) return null;
        var facts = (global::app.type.kind.@this)System.Activator.CreateInstance(
            typeof(global::app.type.kind.format.@this<>).MakeGenericType(clr), format, owner, null, null)!;
        return wanted.Length == 0 ? new global::app.type.kind.empty.@this(owner, facts) : facts;
    }

    /// <summary>
    /// When true, <see cref="kind"/> is a requirement (enforced at build for
    /// literals via <c>app.data.IKindValidatable</c>; deferred to runtime for
    /// <c>%var%</c>). Default false — kind is a hint.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// The kind of template the value is (<c>plang</c>) — set by the BUILD when the value is a developer-authored
    /// <c>%ref%</c> template, carried in the <c>.pr</c> as the kind's name, and by a read that asks for its
    /// content's variables to be filled (<c>read … load vars</c>). Never inferred from content (a runtime-ingested
    /// string that happens to contain <c>%x%</c> is data). It says "may hold variables"; which ones it holds is
    /// decided at the value's birth by whose bytes these are (<see cref="global::app.type.item.source"/>).
    /// Null = a plain value. Mirrors <c>global::app.type.item.text.@this.Template</c>.
    /// </summary>
    public global::app.type.item.template.kind.@this? Template { get; init; }


    [JsonConstructor]
    /// <summary>A type object holds an already-canonical name — <c>app.Type[name]</c> is the one door
    /// that turns a spelled name (<c>string</c>, <c>int</c>) into its canonical type.</summary>
    public @this(string name, string? kind = null, bool strict = false, global::app.type.item.template.kind.@this? template = null)
    {
        Name = name.ToLowerInvariant();
        _kind = string.IsNullOrEmpty(kind) ? BornKind() : new global::app.type.kind.@this(kind);
        Strict = strict;
        Template = template;
        // The birth door starts pointing at the one-shot binder, which swaps itself for the closed thunk (or
        // the decline) on first use — every later call is a bare delegate invocation, no null check. Field
        // initializers can't reference `this`, so bind here. The "made from" answer binds the same way.
        _lift = Bind;
        _takes = Bind;
    }

    /// <summary>A type named <paramref name="name"/> whose facts fall back to <paramref name="family"/>'s
    /// (a kind's parent — so a kind answers its own, else its parent's). The kind's own wins; what it does
    /// not answer reads the family's stored fields, which terminate.</summary>
    protected @this(string name, @this? family) : this(name) => _family = family;

    /// <summary>
    /// The item class values of this type are (<c>text.@this</c> for text, <c>number.@this</c> for
    /// {number, int}) — never the C# value underneath, which is its owner's (a number kind's storage,
    /// an item's OwnedClrTypes). A type from the types' list and an item's own type carry it; null
    /// only for a name no type answers to.
    /// </summary>
    [JsonIgnore]
    internal virtual System.Type? ClrType => _clrType;
    private System.Type? _clrType;

    /// <summary>This type's empty value — what a value of it holds before anything is in it: the value
    /// class's own parameterless construction (an empty list or dict, 0, "", false). A type with none
    /// (a goal, an item, a host) answers its typed null: still null, still this type.</summary>
    public item.@this Empty(global::app.actor.context.@this context)
    {
        if (_empty is { } made) return made();
        var clr = ClrType;
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        // a value class born holding its type is handed this one (an empty list<goal> holds {list, goal})
        _empty = clr == null || !typeof(item.@this).IsAssignableFrom(clr) || clr.IsAbstract || clr.ContainsGenericParameters
            ? () => new item.@null.@this(this)
            : clr.GetConstructor(any, [typeof(@this)]) is { } typed ? () => (item.@this)typed.Invoke([this])
            : clr.GetConstructor(any, System.Type.EmptyTypes) is { } empty ? () => (item.@this)empty.Invoke(null)
            : () => new item.@null.@this(this);
        // an empty value no holder's change can reach (its Copy is itself — a null, 0, "") is one value, its type's
        // own: the app's null is one per app; a container's empty is made fresh for each holder
        var first = _empty();
        if (ReferenceEquals(first.Copy(), first)) _empty = () => first;
        return first;
    }

    // How this type makes its empty value — its class's constructor, found once (every value-less Data asks); for an
    // empty value that can be shared, that one value.
    private System.Func<item.@this>? _empty;


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
    // The bound birth thunk. It starts as the one-shot `Bind` (set in the ctor) and self-replaces with the
    // closed generic (or the decline) on first use — a non-ICreate entity (primitive/host name) binds a
    // null-thunk so the collection perimeter falls to the next rung.
    private System.Func<object?, @this, global::app.data.@this, item.@this?> _lift;

    /// <summary>
    /// A program value of this type is born: <paramref name="raw"/> made into it, through this type's
    /// <c>on.create</c> (<c>%!app.type.text.on.create%</c>) — before is handed the raw (a refusal or a handled
    /// answer is the result, and nothing is made), after is handed the value.
    /// <para>A birth is a value coming into being from outside the type system: an action making a value, or
    /// content decoded into a new value (a file's bytes read into an image). A lazy value materializing — the
    /// same value's delayed parse, a source or wire becoming its item — is not a birth, nor reading a
    /// <c>.pr</c> slot, nor a re-type inside the type system: those are <see cref="Make(object?, actor.context.@this?)"/>,
    /// which fires nothing.</para>
    /// </summary>
    // `new`: app.type.@this : item.@this, so this instance door (birth of the DECLARED type) deliberately
    // hides item's static apex lift (build WHATEVER the raw is) — an entity instance calls this;
    // item.@this.Create(...) reaches the apex.
    public new System.Threading.Tasks.ValueTask<global::app.data.@this> Create(object? raw, global::app.actor.context.@this context)
        => Create(raw, context, "");

    /// <summary>A type is made from what names it, among the app's types: a type passes; a text is the type it names
    /// (<c>path</c>); a dict describes one (<c>{name: "number", kind: "int", strict: true}</c>). Anything else declines.</summary>
    public static @this? Create(object? raw, @this? type, global::app.data.@this data)
    {
        if (raw is @this self) return self;
        var context = data.Context!;
        if (raw is item.dict.@this dict && dict.Get<item.text.@this>("name", context)?.ToString() is { Length: > 0 } described)
            return context.App.type.list[new @this(described, dict.Get<item.text.@this>("kind", context)?.ToString(),
                dict.Get<item.@bool.@this>("strict", context)?.Value == true), context];
        if (raw is item.@this { IsLeaf: true } leaf && leaf.RawText is { Length: > 0 } named)
            return context.App.type.list[new @this(named), context];
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as item.@this)?.Type.Name ?? raw?.GetType().Name} — a type is its name (path) or {{name, kind?, strict?}}",
            "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>The birth (<see cref="Create(object?, actor.context.@this)"/>) of a value named <paramref name="name"/>
    /// — content read off <paramref name="origin"/> (a file) is born knowing it.</summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Create(object? raw,
        global::app.actor.context.@this context, string name, global::app.type.item.path.@this? origin = null)
    {
        // a value that already is this type is no birth: it is answered as it is, nothing fired, nothing read
        if (Held(raw) is { } held) return new(new global::app.data.@this(name, held, context: context));
        // nothing bound: the value, made — nothing awaited, nothing else allocated
        var events = on.create;
        return events.IsBound(this, context)
            ? Born(events, raw, context, name, origin)
            : new(Made(raw, context, name, origin));
    }

    // What a value that already is this type is, as it is — a source declared this type (unread), a value of this
    // type with its kind, a value whose history is this type (an image is-a path); null when making it into this
    // type is a birth (a structure this type doesn't take is a birth attempt). A strict declaration always checks
    // and a template is a birth fact, so either holds nothing.
    private item.@this? Held(object? raw) => Strict || Template != null ? null : raw switch
    {
        item.source src when Same(src.Type) => src,
        item.@this { IsLeaf: true } leaf when Same(leaf.Type) || (!Names(leaf.Type.Name) && leaf.Is(this)) => leaf,
        _ => null,
    };

    // This type by name, with a kind that agrees (none declared, or the same one).
    private bool Same(@this other) => Names(other.Name)
        && (kind.IsEmpty || string.Equals(other.kind.Name, kind.Name, System.StringComparison.OrdinalIgnoreCase));

    // The birth with something bound: before is handed the raw, after the value.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> Born(global::app.@event.on.create events,
        object? raw, global::app.actor.context.@this context, string name, global::app.type.item.path.@this? origin)
    {
        if (await events.Before(this, context, new global::app.data.@this(name, raw, context: context)) is { } answer
            && (!answer.Success || answer.Handled))
            return answer;
        var made = Made(raw, context, name, origin);
        return made.Success ? await events.After(this, made, context) : made;
    }

    // The value a birth makes, or the reason this type declined to make it.
    private global::app.data.@this Made(object? raw, global::app.actor.context.@this context, string name,
        global::app.type.item.path.@this? origin)
    {
        try { return new global::app.data.@this(name, Make(raw, context, origin), context: context); }
        catch (global::app.error.DeclinedException declined) { return context.Error(declined.Error); }
    }

    // THE born-native build — the ENTITY builds a plang VALUE of itself from a raw value, in one
    // step: null → typed absence; a variable-named type → the variable; wire-raw (string/bytes) →
    // a lazy source (parse on first touch); an already-native container → held; a built leaf →
    // refined to the declared kind/template, or re-typed through its family courier; a raw CLR
    // scalar → born through the family lift, then refined. ALWAYS returns a value (never null); a
    // bad conversion throws (the throw boundary — rides MaterializeFailed like a reader parse).
    // Fires nothing: it is the build a birth runs, and every re-type inside the type system.
    internal item.@this Make(object? raw, global::app.actor.context.@this context, global::app.type.item.path.@this? origin = null)
    {
        // context-never-null: a value is born WITH context. A null here is a construction site that
        // forgot to pass one — fail with a pointer, not an NRE deep in materialization.
        if (context is null) throw new System.InvalidOperationException(
            $"context-never-null: building a '{Name}' value without a context — pass the actor context at the construction site.");

        // Typed absence — the declaration survives (a typed null, a tool-parameter slot; a JSON-null too).
        if (raw is null or global::app.type.item.@null.@this) return new global::app.type.item.@null.@this(this);

        // A name type's text IS the name (a write target), not content to defer: its own reader reads it now,
        // as it reads a wire slot. A raw string, or the text a built leaf of another type holds; an unread
        // source stays deferred (re-declared below).
        if (IsName && raw switch
            {
                string s => s,
                item.source => null,
                item.@this { IsLeaf: true } other when !string.Equals(other.Type.Name, Name, System.StringComparison.OrdinalIgnoreCase) => other.RawText,
                _ => null,
            } is { } name)
        {
            var reader = new global::app.type.format.value.Reader(name);
            return context.App.type.list.Reader.Reader(Name, null, context)
                .Read(ref reader, null, new global::app.type.reader.ReadContext(context));
        }

        // A raw this bare type owns (text ← string, binary ← byte[]) has nothing to read: no kind to
        // parse it as, no template to render. It is its value now, through the family's lift.
        if (raw is string or byte[] && kind.IsEmpty && Template == null && Owns(raw.GetType())
            && Make(raw, new global::app.data.@this("", context: context)) is { } owned)
            return owned;

        // Wire-raw (string / byte[]) → defer through a source declared as THIS type, parsed lazily on
        // first use. The source carries the type's Name/Kind/Strict/template and reads its own raw —
        // knowing where the raw came from, when it was read off a file.
        if (raw is string or byte[])
            return new item.source(raw, this, origin: origin);

        // A container / domain value is already native (dict, list, path, image, …) — hold it; a value of a
        // type this type takes (a path declared a file, a list declared list<path>) is made into this type by
        // its own birth, handed this declaration — so a file declared a template is born one. A template is
        // the value's birth fact, never stamped here.
        if (raw is item.@this { IsLeaf: false } native)
        {
            if (!Takes(native.Type)) return native;
            // a decline lands its reason on the carrier — this door throws it, as a leaf's does
            var declined = new global::app.data.@this("", context: context);
            if (Make(raw, declined) is { } made) return made;
            if (declined.Error != null) throw Failed(declined.Error);
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
            // Already this type → hold; a kindless leaf declared a kind is made into this type by its own Create,
            // handed this declaration (its courier re-births a value of its class holding it).
            var minted = leaf.Type;
            if (string.Equals(Name, minted.Name, System.StringComparison.OrdinalIgnoreCase))
            {
                if (kind.IsEmpty || !minted.kind.IsEmpty) return leaf;
                var data = new global::app.data.@this("", context: context);
                if (Make(leaf, data) is { } value) return value;
                if (data.Error != null) throw Failed(data.Error);
                return leaf;
            }
            // The value's type history already contains this type (an image born from a path
            // satisfies a path slot) → hold it, don't downgrade.
            if (leaf.Is(this)) return leaf;
            // A different type → re-typed EAGERLY via the family courier, handed the leaf whole (kind-aware
            // build — path parses a text, number parses a token, binary takes a text's bytes). A decline lands
            // its reason on the carrier's Error — this door is the throw boundary (rides MaterializeFailed).
            var carrier = new global::app.data.@this("", context: context);
            if (Make(leaf, carrier) is { } made) return made;
            if (carrier.Error != null) throw Failed(carrier.Error);
            // Nothing in this type builds it from that value (`set %x% as dict = "hello"`): the type refuses the
            // conversion, as its own answer.
            throw Failed(new global::app.error.@this(
                $"cannot make a '{Name}' from a {leaf.Type.Name} value", "TypeConversionFailed", 400));
        }

        // A raw CLR scalar (int, DateOnly, …) → born through THIS family's own lift, then refine to the
        // declared type/kind. A non-family declared type routes the raw through the collection perimeter
        // (the owner's lift or a clr carrier). The family lift speaks raw natively; refine re-enters here.
        if (Make(raw, new global::app.data.@this("", context: context)) is { } lifted)
            return string.Equals(Name, lifted.Type.Name, System.StringComparison.OrdinalIgnoreCase)
                ? lifted : Make(lifted, context);
        return Make(global::app.type.item.@this.Create(raw, context), context);

        static System.Exception Failed(global::app.error.@this error) => new global::app.error.DeclinedException(error);
    }

    /// <summary>A still-encoded slice + the serializer that sliced it — the capture hands over
    /// itself. Mints the lazy <see cref="item.wire.@this"/>, or — when this declaration is marked a
    /// template — a <see cref="item.wire.template"/> holding the <paramref name="variable"/>s its row lists;
    /// the parse stays at first touch. The one place a slice is made (a re-declared slice comes back here).
    /// The capture build beside the content <see cref="Make(object?, actor.context.@this?)"/> build —
    /// same verb, the capture's knowledge as an argument, never a format name. Not a birth.
    /// <paramref name="built"/>: the slice is the build's own bytes (its read granted it).</summary>
    internal item.wire.@this Make(string slice, global::app.type.item.wire.kind.plang.@this reader,
        IReadOnlyList<global::app.type.item.variable.@this>? variable = null, bool built = false)
        => Template != null ? new item.wire.template(slice, this, reader, variable ?? [], built) : new item.wire.@this(slice, this, reader, built: built);

    /// <summary>Reads a value slot of this type off the reader — the one door for a
    /// <c>{name, type, value}</c> row's value, a Data's or an action property's. The slot is exactly
    /// its row's declared type: a value is a template only when that type carries the marker (born at
    /// build), never because of what it holds. A type whose values are STRUCTURE (a snapshot, a variable name, an
    /// error) is read eagerly through its own reader when the value stands in a form that reader reads; anything else
    /// (a snapshot slot holding <c>%snap%</c>) takes the content door like any other slot — a template its row's
    /// variables, every other slot a lazy wire over its verbatim bytes.</summary>
    public item.@this Read(ref global::app.type.item.kind.json.Reader reader,
        global::app.type.reader.ReadContext ctx)
    {
        // Which types are structure is the TYPE's declaration (ITypeReader.IsEager), never a list of names; which
        // forms it reads is the reader's own (ITypeReader.Reads).
        if (ctx.Context.App.type.list.Reader.Typed(Name, null) is { IsEager: true } eager && eager.Reads(reader.Peek()))
            return eager.Read(ref reader, null, ctx);

        // The slot is captured in plang's own format — the wire type's plang kind, which reads it on first touch.
        var transport = (global::app.type.item.wire.kind.plang.@this)ctx.Context.App.type.list["wire"].kind["plang"]!;

        if (reader.Peek() == global::app.type.format.TokenKind.String)
        {
            var slice = System.Text.Encoding.UTF8.GetString(reader.Slice());
            // A template (its row's marker) takes the content door, with the variables its row's list says
            // it holds — when the read is the build's own (the source decides); the kind-parse stays lazy on
            // the content source. A literal string under any other type rides the wire (strict,
            // byte-identical). A name was read above, by its own eager reader.
            return Template != null
                ? new item.source(JsonSerializer.Deserialize<string>(slice)!, this, ctx.Variable, built: ctx.IsBuilt)
                : Make(slice, transport, built: ctx.IsBuilt);
        }
        // EVERY other slot is a wire: a VERBATIM Slice with the capturing transport named at the
        // mint site. Face validation is free — the type's own pull IS the validator on first touch.
        // A container is a template only by its own row's marker, holding its row's variables.
        return Make(System.Text.Encoding.UTF8.GetString(reader.Slice()), transport, ctx.Variable, ctx.IsBuilt);
    }

    // The birth build — THIS type makes itself from a value, as it declares (its kind, its template), for the
    // binding data (a decline lands on data.Fail).
    internal item.@this? Make(object? raw, global::app.data.@this data) => _lift(raw, this, data);

    // The one-shot binder: on first use it swaps the field for the closed thunk (or the decline) and forwards,
    // so every later door call is a bare invocation.
    private item.@this? Bind(object? raw, @this type, global::app.data.@this data)
    {
        _lift = Creatable is { } clr
            ? _open.MakeGenericMethod(clr).CreateDelegate<System.Func<object?, @this, global::app.data.@this, item.@this?>>()
            : static (_, _, _) => null;
        return _lift(raw, type, data);
    }

    // Construct a value of this type from <paramref name="raw"/> THROUGH the ICreate&lt;T&gt; family its class
    // belongs to, even when the class is a subtype of that family (a settings class : setting is
    // ICreate&lt;setting&gt;, not ICreate&lt;itself&gt;, so its own <see cref="Make"/> declines): the family's
    // Create is handed THIS as the declaration, so the base's Create makes the subclass by its declared.ClrType.
    // Only the record reader asks this (decision 789); the general Make stays on the EXACT <see cref="Creatable"/>
    // so its CLR-value fallback (the `Make(item.Create(raw))` rung) keeps converging and never recurses. The
    // closed family thunk is a fact of the class — bound once, on first use, like <see cref="_lift"/>.
    internal item.@this? Construct(object? raw, global::app.data.@this data)
    {
        _construct ??= CreatableFamily is { } clr
            ? _open.MakeGenericMethod(clr).CreateDelegate<System.Func<object?, @this, global::app.data.@this, item.@this?>>()
            : static (_, _, _) => null;
        return _construct(raw, this, data);
    }

    private System.Func<object?, @this, global::app.data.@this, item.@this?>? _construct;

    /// <summary>Is a value of <paramref name="other"/>, declared this type, made into it — this type's class's own
    /// answer, asked without making (a path declared a file is; a dict declared a list is not).</summary>
    internal bool Takes(@this other) => _takes(other);

    private System.Func<@this, bool> _takes;

    private bool Bind(@this other)
    {
        _takes = Creatable is { } clr
            ? _taking.MakeGenericMethod(clr).CreateDelegate<System.Func<@this, bool>>()
            : static _ => false;
        return _takes(other);
    }

    private static bool Takes<T>(@this other)
        where T : item.@this, global::app.type.item.ICreate<T>
        => T.Takes(other);

    /// <summary>A value of this type is a name — this type's class's own answer (<c>ICreate.IsName</c>), read once. A
    /// family held by its open class (<c>choice&lt;&gt;</c> for a set the registry doesn't hold) names nothing.</summary>
    internal bool IsName => _isName ??= Creatable is { ContainsGenericParameters: false } clr
        && (bool)_naming.MakeGenericMethod(clr).Invoke(null, null)!;

    private bool? _isName;

    private static bool Named<T>() where T : item.@this, global::app.type.item.ICreate<T> => T.IsName;

    private static readonly System.Reflection.MethodInfo _naming = System.Array.Find(
        typeof(@this).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
        m => m.Name == nameof(Named) && m.IsGenericMethodDefinition)!;

    private static readonly System.Reflection.MethodInfo _taking = System.Array.Find(
        typeof(@this).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
        m => m.Name == nameof(Takes) && m.IsGenericMethodDefinition)!;

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

    // The ICreate<T> family this type's class makes through — T is the class itself when it implements
    // ICreate<itself> (the <see cref="Creatable"/> case), or the BASE a subtype implements ICreate of (a
    // settings class : setting is ICreate<setting>, not ICreate<itself>). Make closes Create<T> with THIS as
    // the declaration, so the base's Create makes the subclass by its declared.ClrType. Null for a host entity
    // whose class implements no ICreate. (Takes/IsName stay on the exact Creatable — a subtype is not its base.)
    private System.Type? CreatableFamily
    {
        get
        {
            if (ClrType is not { } clr || !typeof(item.@this).IsAssignableFrom(clr)) return null;
            // A subtype implements ICreate of EVERY level up its chain (a settings class is both
            // ICreate<item> and ICreate<setting>): take the MOST DERIVED family — the one every other is
            // assignable from (setting over item) — so its Create, not the apex item's, makes the value.
            var families = System.Array.FindAll(clr.GetInterfaces(),
                    i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.ICreate<>))
                .Select(i => i.GenericTypeArguments[0]).ToArray();
            return System.Array.Find(families, t => System.Array.TrueForAll(families, o => o.IsAssignableFrom(t)));
        }
    }

    // The generic thunk: the raw rides into the type's own birth, as this type declares it — unless the value
    // refuses to become one (a raw C# value, lifted here, is no item and refuses nothing).
    private static item.@this? Create<T>(object? raw, @this type, global::app.data.@this data)
        where T : item.@this, global::app.type.item.ICreate<T>
        => raw is item.@this value && value.Refuses(type, data) ? null : T.Create(raw, type, data);

    private static readonly System.Reflection.MethodInfo _open = System.Array.Find(
        typeof(@this).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
        m => m.Name == nameof(Create) && m.IsGenericMethodDefinition)!;

    // The entity's face — a kinded type writes its kind in its name (number<decimal>, list<path>, text<md>),
    // a kindless one its name alone. The face is the type's own; which faces a prompt shows is the builder's
    // business, not ToString's. Templates and catalog text print this.
    public override string ToString() => Face;

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
    /// match (or the <c>item</c> apex) AND, when <paramref name="other"/> names a kind, the same kind —
    /// <c>is item</c> is every value, but <c>is {item, json}</c> asks the kind (a text is an item, not
    /// <c>{item, json}</c>). Composition ("an image is-a path") is NOT answered here — it
    /// lives on the VALUE's type history (a value born from a path carries a "path" entry), asked via
    /// <see cref="item.@this.Is"/>. No CLR-inheritance lattice, no reflection.</summary>
    public bool Is(@this? other)
        => other != null
           && (string.Equals(Name, other.Name, System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(other.Name, "item", System.StringComparison.OrdinalIgnoreCase))
           && (other.kind.IsEmpty || string.Equals(kind.Name, other.kind.Name, System.StringComparison.OrdinalIgnoreCase));

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
    // Read from its class the first time each is asked, by a type the list holds; a value's bare type carries none. Navigation
    // (%x!type.Description%) answers them through the full type — see Get below. The wire form is
    // Write's {name, kind?, strict?, template?}; these never ride it. A kinded type ({text, md}) holds
    // its family's entry and reads every fact through it: one set of facts per class.

    /// <summary>The family entry this type's facts are — the registry's entry for its class; a family
    /// entry (or a type with none) is its own.</summary>
    internal @this Family => _family ?? this;
    private protected readonly @this? _family;

    /// <summary>The type's properties — a record's fields, a scalar's navigable members; each a
    /// named slot carrying its type. Null when the type declares none. A record is a type with
    /// properties and no <see cref="Shape"/>.</summary>
    public property.list.@this? Property
    {
        get => OwnProperty ?? kind.OwnProperty ?? (Member is { Count: > 0 } member ? member : null);
        init => _property = value;
    }
    private property.list.@this? _property;

    // A kind answers its own fact through these hooks, else the type falls back to its Family's STORED
    // field (never the virtual — a kind's Family is its parent entry, so reading the field terminates;
    // reading the member could loop type→kind→Family→kind). `kind.OwnX` reaches a kinded type's kind
    // ({choice, operator} → the set's options); `OwnX` a kind-as-type's own (a setting kind's options).
    protected virtual property.list.@this? OwnProperty => null;
    protected virtual IReadOnlyList<string>? OwnValues => null;
    protected virtual IReadOnlyList<string>? OwnAlias => null;
    protected virtual bool? OwnInternal => null;
    protected virtual item.prose.@this? OwnDescription(actor.context.@this context) => null;
    protected virtual item.prose.@this? OwnExample(actor.context.@this context) => null;

    /// <summary>Enum values — the kind's own when it is a set of options (a choice's), else the family's.
    /// Non-null marks this as an enum-shape type.</summary>
    public IReadOnlyList<string>? Values { get => OwnValues ?? kind.OwnValues ?? Family._values; init => _values = value; }
    private IReadOnlyList<string>? _values;

    /// <summary>What a value of this type can be in <paramref name="step"/>, as the decider is offered it — what its
    /// class declares it is offered (a permission: that the step gives one), else the kind's answer: a closed set its
    /// options, any other the step's own variables. The type <c>type</c> offers the types it is born among first.</summary>
    public virtual async System.Threading.Tasks.ValueTask<IReadOnlyList<item.@this>> Offers(global::app.goal.step.@this step)
        => Family._offer is { } declared ? declared
            : Family._types is { } types ? [.. await types.Offers(step), .. await kind.Offers(step)]
            : await kind.Offers(step);
    private IReadOnlyList<item.@this>? _offer;
    // The types the type `type` is born among — its values, what a type slot is offered; null for any other type.
    private list.@this? _types;

    /// <summary>Whether what this type offers is all a value of it can be — its kind says (a choice's options, a bool's
    /// pair); offers its class declares are a convenience, never closed.</summary>
    public virtual bool IsClosed => Family._offer is null && kind.IsClosed;

    /// <summary>Scalar wire shape (the underlying primitive form, e.g. "string" for path).</summary>
    public string? Shape { get => Family._shape ??= Family.Form; init => _shape = value; }
    private string? _shape;

    /// <summary>A value of this type is written as an object: a record (its builder properties, no shape of its own),
    /// or a type that declares the object shape (limit, permission).</summary>
    public bool IsRecord => Shape is null ? Property != null : Shape == "object";

    /// <summary>Constructor signature for scalar types (<c>"name: shape"</c>).</summary>
    public string? ConstructorSignature
    {
        get => Family._constructorSignature ??= Family.Source is { } source && Family._list is { } list
            ? $"{source.Name}: {list.Face(source.ParameterType)}" : null;
        init => _constructorSignature = value;
    }
    private string? _constructorSignature;

    /// <summary>Where this type's teaching lives — <c>/system/type/&lt;name&gt;/</c>, its family's (a kinded type's facts
    /// are its family's), each read through the path's gate as the asker.</summary>
    private item.path.@this Teaching(actor.context.@this context)
        => item.path.@this.Resolve($"/system/type/{Family.Name}", context);

    // a teaching file of this type's, born unread: an absent one is falsy
    private item.file.@this Taught(string file, actor.context.@this context) => new(Teaching(context).Combine(file), context);

    /// <summary>What a value of this type is, said once — the kind's own when it says one (a list of records names
    /// its element), else <c>type.description.md</c>; read when asked, empty when there is none.</summary>
    [LlmBuilder] public item.prose.@this Description(actor.context.@this context)
        => (OwnDescription(context) ?? kind.OwnDescription(context)) is { } its
            ? its.Or(new item.prose.@this(context.App.type.list["text"], Taught("type.description.md", context)))
            : new item.prose.@this(context.App.type.list["text"], Taught("type.description.md", context));

    /// <summary>A value of this type as a step writes one (<c>5m</c>, <c>42</c>) — the kind's own when it shows one (a
    /// list of records, one of its element), else <c>type.examples.md</c>; read when asked, empty when there is none.</summary>
    [LlmBuilder] public item.prose.@this Example(actor.context.@this context)
        => (OwnExample(context) ?? kind.OwnExample(context)) is { } its
            ? its.Or(new item.prose.@this(context.App.type.list["text"], Taught("type.examples.md", context)))
            : new item.prose.@this(context.App.type.list["text"], Taught("type.examples.md", context));

    /// <summary>What the builder is taught about this type — <c>type.notes.md</c>; falsy when it has none.</summary>
    [LlmBuilder] public item.file.@this Notes(actor.context.@this context) => Taught("type.notes.md", context);

    /// <summary>Prose for a learner — <c>type.guide.md</c>, shown on the type's page, never read by the builder; falsy
    /// when it has none.</summary>
    [LlmBuilder] public item.file.@this Guide(actor.context.@this context) => Taught("type.guide.md", context);

    /// <summary>The other names this type answers to (<c>string</c> for text, <c>map</c> for dict),
    /// declared by its class as a static <c>Alias</c>. Never null.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> Alias { get => OwnAlias ?? kind.OwnAlias ?? Family._alias; init => _alias = value; }
    private IReadOnlyList<string> _alias = [];

    /// <summary>The C# shapes this type owns (<c>int</c> → number, kind int), declared by its class
    /// as a static <c>OwnedClrTypes</c>: a raw C# value of one of them is a value of this type.</summary>
    [JsonIgnore]
    internal IReadOnlyList<global::app.type.convert.OwnedClr> Owned { get => Family._owned; init => _owned = value; }
    private IReadOnlyList<global::app.type.convert.OwnedClr> _owned = [];

    /// <summary>Whether a raw C# value of <paramref name="clr"/> is a value of this type — one of its
    /// <see cref="Owned"/> shapes, an assignable one owning every subclass too.</summary>
    internal bool Owns(System.Type clr)
        => Owned.Any(o => o.Assignable ? o.Clr.IsAssignableFrom(clr) : o.Clr == clr);

    /// <summary>True for a type plang's own machinery uses but a program never names (a wire slice,
    /// a C# host carrier), declared by its class as a static <c>Internal</c>. A kinded type answers it
    /// through its kind (item's <c>dict</c>/<c>list</c>/<c>*</c> host kinds are internal), like every
    /// other fact: its own, else its kind's, else its family's. It stays in the types — naming answers
    /// it — and stays out of their face.</summary>
    [JsonIgnore]
    public bool Internal { get => OwnInternal ?? kind.OwnInternal ?? Family._internal; init => _internal = value; }
    private bool _internal;

    /// <summary>A type built from a C# class where no app is in reach — the class-built fallbacks (the C# operator
    /// births, error, event): its kind carries its class's [Format] facts, read from the class, so the value's own
    /// type answers its MIME / extension / text without asking an app. A class that declares no such format keeps the
    /// bare by-name kind. Goes with the implicit-conversion branch, when every value is born from its app's list.</summary>
    internal @this(string name, System.Type? clrType, string? kind = null, bool strict = false, global::app.type.item.template.kind.@this? template = null)
        : this(name, clrType, kind, strict, template, known: true)
    {
        if (clrType != null && typeof(item.@this).IsAssignableFrom(clrType) && KindOfClass(clrType, kind, this) is { } declared)
            _kind = declared;
    }

    // A type born knowing its C# class — its identity: the class, and its namespace for an item class. The registry's
    // entries are born here; their own format and kinds are the app's, held at enlist.
    private @this(string name, System.Type? clrType, string? kind, bool strict, global::app.type.item.template.kind.@this? template, bool known)
        : this(name, kind, strict, template)
    {
        _clrType = clrType;
        if (clrType != null && typeof(item.@this).IsAssignableFrom(clrType))
            Namespace = item.@this.NamespaceOf(clrType);
    }

    /// <summary>A kinded type of <paramref name="family"/> ({text, md}, {choice, operator}): born holding the family
    /// entry, whose facts it reads; its class is the one <paramref name="kind"/> makes of the family's.</summary>
    internal @this(@this family, global::app.type.kind.@this? kind, bool strict, global::app.type.item.template.kind.@this? template)
        : this(family.Name, kind != null ? kind.Of(family.ClrType) : family.ClrType, kind?.Name, strict, template, known: true)
    {
        _family = family;
        _kind = kind ?? new global::app.type.kind.empty.@this(this);
    }

    /// <summary>A type whose own kind is a given <paramref name="ownKind"/> — the seam a nested kind rides
    /// (<c>{list, kind: {list, kind: path}}</c>): a kind, itself a type, is another type's kind. <paramref name="family"/>
    /// is the fallback entry. A null <paramref name="ownKind"/> leaves the born empty kind.</summary>
    internal @this(string name, global::app.type.kind.@this? ownKind, @this? family) : this(name)
    {
        if (ownKind != null) _kind = ownKind;
        _family = family;
    }

    /// <summary>The type of the item class <paramref name="clr"/> — the name the class goes by (its declared
    /// word, else its namespace) and its namespace, with <paramref name="kind"/>.</summary>
    internal @this(System.Type clr, string? kind = null) : this(item.@this.NameOf(clr), clr, kind) { }

    /// <summary>
    /// The type a class defines: its name, its C# class, its aliases and owned C# shapes, what it declares it is
    /// offered, and — when <paramref name="types"/> is given — the list it is in, through which its other facts are
    /// read when first asked (a scalar's wire shape and constructor, a record's <c>[LlmBuilder]</c> properties): a
    /// fact names its property types, so it is read once every type's name is known. Without the list the type is
    /// its identity alone.
    /// </summary>
    internal @this(string name, System.Type clr, list.@this? types) : this(name, clr, null, false, null, known: true)
    {
        Alias = Declared<IReadOnlyList<string>>("Alias") ?? [];
        Owned = Declared<IReadOnlyList<global::app.type.convert.OwnedClr>>("OwnedClrTypes") ?? [];
        // declared by the class itself: a base that is internal (reference) leaves its subclasses (file, url) named
        Internal = clr.GetProperty("Internal", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                               | System.Reflection.BindingFlags.DeclaredOnly)?.GetValue(null) is true;
        // The type entity's own wire shape and kinds are taught by the prompt's type reference, not as facts.
        if (types == null || clr == typeof(@this)) return;
        _offer = Declared<IReadOnlyList<item.@this>>("Offer");
        _list = types;

        T? Declared<T>(string member) where T : class
        {
            var p = clr.GetProperty(member, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                                            | System.Reflection.BindingFlags.FlattenHierarchy);
            return p?.GetValue(null) as T;
        }
    }

    // The list this type is in — its facts name their property types through it; null for a type that is its
    // identity alone.
    private readonly list.@this? _list;

    /// <summary>The app's type list this type is in — what a value holding it asks the app's other types of (a text
    /// reading the variables it holds); null for a type that is its identity alone.</summary>
    internal list.@this? Types => _list ?? Family._list;

    // The members of the class this type describes — the type list's one list per class, which a value of that class
    // reads its members from too. A class under it that is no kind of its own (an error's subclass) is read by its
    // value through its own class's list, but offered here as this type's. Empty for a type that is its identity alone.
    private property.list.@this Member
        => _property ?? Family._property
           ?? ((Family._list is { } list && (ClrType ?? Family.ClrType) is { } clr) ? list.Member(clr) : new());

    // What the class's Resolve takes first — a scalar is made from it; null when the class resolves nothing.
    private System.Reflection.ParameterInfo? Source
        => ClrType?.GetMethod("Resolve", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.GetParameters() is [var first, ..] ? first : null;

    // The shape a scalar is written in: what its Resolve takes, else the one its class declares, else — a named type
    // with no builder properties (a domain wrapper around a primitive) — a string; null for a record (builder
    // properties, no shape of its own) and for a type that is its identity alone.
    private string? Form
        => _list is not { } list ? null
            : Source is { } source ? list.Face(source.ParameterType)
            : ClrType!.GetProperty("Shape", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
                  | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(null) as string
              ?? (Member.Count == 0 ? "string" : null);

    /// <summary>
    /// The type <c>type</c>, made by the list of <paramref name="types"/> for its own entry. A type's values are the
    /// types, so this one type holds the list they are in — as a choice holds its set — and offers them; every other
    /// type knows no list.
    /// </summary>
    internal @this(list.@this types) : this(item.@this.NameOf(typeof(@this)), typeof(@this), (list.@this?)null) => _types = types;

    /// <summary>This type answers for its name or one of its aliases, case-insensitive.</summary>
    public System.Threading.Tasks.ValueTask<@this?> Match(string key) => new(Names(key) ? this : null);

    /// <summary>True when <paramref name="key"/> is this type's name, its namespace or one of its aliases —
    /// the in-memory answer <see cref="Match"/> gives, for the registry's synchronous walk.</summary>
    internal virtual bool Names(string key)
        => string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase)
           || string.Equals(Namespace, key, System.StringComparison.OrdinalIgnoreCase)
           || Alias.Contains(key, System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Every name this type answers to — its name, its namespace, its aliases; what no other type
    /// may claim.</summary>
    internal IEnumerable<string> Claims
    {
        get
        {
            yield return Name;
            if (Namespace != null && !string.Equals(Namespace, Name, System.StringComparison.OrdinalIgnoreCase)) yield return Namespace;
            foreach (var alias in Alias) yield return alias;
        }
    }

    /// <summary>A type answers navigation as its full type — the registry's, found with the
    /// asker's context. A full type is its own answer; past its members, its settings (<c>.setting</c>).</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
    {
        var full = parent.Context.App.type.list[this, parent.Context];
        // a type's own kind is the kind itself (an item, kind : type) — navigable (.name), rendering and
        // comparing as its name; nothing when it has none.
        if (string.Equals(key, "kind", System.StringComparison.OrdinalIgnoreCase))
            return full.kind.IsEmpty
                ? new global::app.data.@this(key, context: parent.Context)
                : new global::app.data.@this(key, full.kind, context: parent.Context);
        var member = await new global::app.type.clr.@this(full, parent.Context).Get(parent, key);
        return member.IsInitialized ? member : await Setting(parent, key) ?? member;
    }

    /// <summary>A type's settings are the ones its class names (<c>%!app.type.size.setting%</c>) — the registry's
    /// type, as the asker sees it.</summary>
    protected override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Setting(global::app.data.@this parent)
        => await parent.Context.Setting.Of(parent.Context.App.type.list[this, parent.Context]);
}
