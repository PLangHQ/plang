using System.Reflection;
using app.Attributes;

namespace app.type.list;

/// <summary>
/// The types — every plang type, one set, each type owning its name, aliases, C# class and facts
/// (<c>app.Type</c>). Distinct from <c>app.type.item.list</c> (the plang list VALUE). A lookup walks
/// the set: by name or alias (the type's own <see cref="app.type.@this.Match"/>), by C# class, by
/// identity. How a type comes in lives in the <c>Registry</c> partial.
///
/// A file format is a kind of the type that reads it (png of image, md of text), declared on the type's
/// class with <c>[Format]</c>; the kind answers to its MIMEs and extensions, so the one walk
/// (<see cref="Kind(string)"/>) finds a format by any of them.
/// </summary>
public sealed partial class @this
{
    /// <summary>The types hold no context and no app: a lookup that needs the app's formats takes
    /// the caller's context.</summary>
    public @this() { }

    /// <summary>
    /// The kind <paramref name="name"/> names when the caller holds only the name — the one kind any type
    /// answers to it with (by name, alias, MIME or extension); a name no type holds is a kind with no class
    /// of its own, minted by name. A kind's name is its type's, so two types may each hold one by the same
    /// name (<c>{hash, sha256}</c>, <c>{binary, sha256}</c>): asked by that bare name, it is ambiguous — an
    /// error naming both, never the first that answers. A caller that holds the type asks the type.
    /// </summary>
    public global::app.type.kind.@this Kind(string name)
        => Held(name) ?? new global::app.type.kind.@this(name);

    /// <summary>
    /// The kind of <paramref name="type"/> — the type's own kind by its kind's name. A kind the type holds
    /// none of (a stamp written before the format was its type's: <c>{binary, json}</c>) is only a name, so
    /// it is found as <see cref="Kind(string)"/> finds a name.
    /// </summary>
    public global::app.type.kind.@this Kind(app.type.@this type, actor.context.@this context)
        => this[type, context].kind is { Owner: not null } held ? held : Kind(type.kind.Name);

    // The kind a type holds that answers to the key — by name, alias, MIME or extension; a type's own
    // format answers as its empty kind. Null when no type holds one. Registration keeps a MIME or an
    // extension to one kind; a name two types hold is refused here.
    private global::app.type.kind.@this? Held(string key)
    {
        global::app.type.kind.@this? found = null;
        foreach (var type in Types)
        {
            if (type.kind[key] is not { } kind) continue;
            if (found != null)
                throw new System.InvalidOperationException(
                    $"'{key}' is a kind of more than one type ({found.Owner}, {kind.Owner}) — ask the type for its kind.");
            found = kind;
        }
        return found;
    }

    /// <summary>
    /// The kind a host object of <paramref name="clr"/> is — one of item's kinds by the C# form it
    /// claims (exact wins, then the most derived assignable: <c>JsonElement</c>→json,
    /// <c>IDictionary</c>→dict, <c>IList</c>→list), else item's <c>*</c> reflection kind. Never null.
    /// </summary>
    public global::app.type.kind.@this Kind(System.Type clr)
    {
        var item = this["item"].kind;
        return item[clr] ?? item["*"]!;
    }

    /// <summary>
    /// Per-(type, kind) reader dispatch — the read side; a value writes itself. Discovers
    /// <c>app/type/&lt;name&gt;/serializer/&lt;kind&gt;.cs</c>
    /// classes exposing a static <c>Read(object, string?, ReadContext)</c> and
    /// exposes the same runtime-registration seam. The single json
    /// <c>Converter</c> routes mid-graph typed fields through here.
    /// </summary>
    public reader.@this Reader { get; } = new();

    /// <summary>
    /// The type <paramref name="name"/> names — its name or one of its aliases (<c>string</c> →
    /// text). The type carries its facts: Property / Values / Shape / Example / Description and its
    /// C# class. A spelled kind is not a name: <c>{text, md}</c> is asked by identity. Throws on a miss.
    /// </summary>
    public app.type.@this this[string name]
    {
        get
        {
            // one walk of the list, no closure — events look their type up here on every start
            foreach (var type in Types)
                if (type.Names(name)) return type;
            throw new KeyNotFoundException($"No PLang type registered under name '{name}'.");
        }
    }

    /// <summary>True when <paramref name="name"/> names a plang type — the presence question
    /// beside the indexer, which selects and throws on a miss.</summary>
    public bool Contains(string name) => Types.Any(t => t.Names(name));

    /// <summary>
    /// The format content of this MIME is — a kind of the type that reads it: <c>image/png</c> → image's png,
    /// <c>text/plain</c> → text's own, <c>application/json; charset=utf-8</c> → item's json, <c>video/mp4</c> →
    /// binary's mp4. A MIME no format answers to is opaque bytes: binary's own. The format decodes and encodes
    /// its content.
    /// </summary>
    public global::app.type.kind.@this Mime(string mime)
        => !string.IsNullOrEmpty(mime) && Held(mime) is { } kind ? kind : this["binary"].kind;

    /// <summary>
    /// The type a file of this extension holds — the format with that extension, as a kind of the type that
    /// reads it: <c>.md</c> → {text, md}, agreeing with its MIME. An extension no format has is bytes of that
    /// kind (<c>.xyz</c> → {binary, xyz}); the null type for no extension.
    /// </summary>
    public app.type.@this Extension(string extension, actor.context.@this context)
    {
        if (string.IsNullOrEmpty(extension)) return app.type.@this.Null;
        return Held(extension.StartsWith('.') ? extension : "." + extension) is { } kind ? Of(kind, context)
            : this[new app.type.@this("binary", extension.TrimStart('.')), context];
    }

    // The full type a held kind makes: its owner, with the kind unless it is the owner's own format.
    private app.type.@this Of(global::app.type.kind.@this kind, actor.context.@this context)
        => this[new app.type.@this(kind.Owner!, kind.IsEmpty ? null : kind.Name), context];

    /// <summary>
    /// The full type for a value's type — its identity (name, kind, strict, template) with the
    /// type's facts. A choice's kind names its set, so a <c>{choice, operator}</c> carries that
    /// set's options. The kind is the type's own kind that answers to the spelled name — by name, alias or
    /// extension (<c>{text, markdown}</c> → md, <c>{image, jpeg}</c> → jpg, <c>{number, integer}</c> → int);
    /// a name the type holds no kind by is a kind of that name with no class. A name no type answers to is
    /// its bare identity.
    /// </summary>
    public app.type.@this this[app.type.@this type, actor.context.@this context]
        => Full(type, type.kind is { IsEmpty: false } k ? k.Name : null);

    /// <summary>
    /// Index by CLR type — the type entity for a live CLR type's plang identity, or null when
    /// the CLR type names no plang type (a raw POCO). Null on miss (a CLR type MAY not be plang
    /// vocabulary), unlike the name door's throw-on-miss.
    /// </summary>
    public app.type.@this this[System.Type clrType]
    {
        get
        {
            Load();
            // The name comes from PlangName — the same step the facts name through, so the
            // entity's face and the facts can never disagree. A kinded family is born here with
            // its kind (already the canonical name); every other name is the named entity.
            var (name, kind) = PlangName(clrType);
            return kind == null ? this[name] : Full(new app.type.@this(name), kind);
        }
    }

    // The full type for an identity and its kind's name — the type carries its own kind of that name, its
    // behaviour with it; a choice's kind (its set) carries the set's options. The type's own format
    // answering to the name (`{text, txt}`) is the type itself, no kind.
    private app.type.@this Full(app.type.@this type, string? name)
    {
        var entry = Types.FirstOrDefault(t => t.Names(type.Name));
        var kind = name == null ? null
            : entry?.kind[name] is { } held ? (held.IsEmpty ? null : held)
            : entry?.kind.Coin(name) ?? new global::app.type.kind.@this(name);
        if (entry == null)
            return new app.type.@this(type.Name, kind?.Name, type.Strict, type.Template) { kind = kind };
        if (kind == null && !type.Strict && type.Template == null) return entry;
        return new app.type.@this(entry.Name, kind != null ? kind.Of(entry.ClrType) : entry.ClrType, kind?.Name, type.Strict, type.Template)
        {
            kind = kind,
            Namespace = entry.Namespace,
            Alias = entry.Alias,
            Owned = entry.Owned,
            From = entry.From,
            Internal = entry.Internal,
            Property = entry.Property,
            Values = kind is global::app.type.item.choice.set.@this set ? set.Values : entry.Values,
            Shape = entry.Shape,
            ConstructorSignature = entry.ConstructorSignature,
            Example = entry.Example,
            Description = entry.Description,
        };
    }

    // The container families whose element rides as the KIND (list<path> = {list, kind:path}).
    // The one generic recognition (PlangName names through it) — the element is the kind; dict's kind is the VALUE
    // (key defaults text; surface a keyed axis only if a real param needs one). Returns null for
    // non-containers (they resolve on the item/clr rungs).
    private (string Family, System.Type Element)? ContainerFamily(System.Type type)
    {
        if (type.IsArray)
        {
            var arr = type.GetElementType()!;
            return arr == typeof(byte) ? null : ("list", arr);   // byte[] is "bytes", not a list
        }
        if (type.IsGenericType)
        {
            var g = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (g == typeof(app.type.item.list.@this<>)) return ("list", args[0]);
            if (g == typeof(List<>) || g == typeof(IList<>) || g == typeof(IEnumerable<>) || g == typeof(ICollection<>)
                || g == typeof(IReadOnlyCollection<>) || g == typeof(IReadOnlyList<>) || g == typeof(HashSet<>)
                || (g.FullName?.StartsWith("System.Collections.Immutable.ImmutableList`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.Generic.ISet`", StringComparison.Ordinal) ?? false))
                return ("list", args[0]);
            if (g == typeof(Dictionary<,>) || g == typeof(IDictionary<,>)
                || (g.FullName?.StartsWith("System.Collections.Concurrent.ConcurrentDictionary`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.ObjectModel.ReadOnlyDictionary`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.Generic.SortedDictionary`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.Immutable.ImmutableDictionary`", StringComparison.Ordinal) ?? false))
                return ("dict", args[^1]);
        }
        // A plang list<T> NODE (action.list : list<action>, step.list : list<step>) is a non-generic
        // subclass of list.@this<T> — walk to that base and take its element as the kind, so the
        // reader dispatches the element's own reader.
        for (var t = type.BaseType; t != null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(app.type.item.list.@this<>))
                return ("list", t.GetGenericArguments()[0]);
        // Any other concrete CLR collection (a third-party IList<T>/IReadOnlyList<T>) IS a list<element>.
        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IList<>)
                             || i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)));
        return listIface != null ? ("list", listIface.GetGenericArguments()[0]) : null;
    }

    // The born-native lift moved to its rightful owner — the produced type. "Build whatever this raw
    // is" is item.@this.Create(raw, ctx) (item's own ICreate face); the registry keeps only SELECTION
    // (the identity indexer this[System.Type], the name door), never construction.

    // --- CLR type → PLang name ---

    // The plang name of a CLR type — {name, kind} — read off the types' own facts. The one naming
    // step: the entity door (this[System.Type]) builds its entity from it, and a type's facts name
    // their property types through it. Reads the set as it stands: the startup scan names property
    // types while the set is being filled. Nullability is the slot's fact, never part of a name; a
    // Data<T> slot names T; plain Data is the open item slot; a family names its content as the
    // kind; a C# shape a type owns is that type (int → number); a raw CLR type no plang type owns is clr.
    private (string Name, string? Kind) PlangName(System.Type type)
    {
        var types = Items().ToArray();
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(data.@this<>))
            type = type.GetGenericArguments()[0];
        if (type == typeof(data.@this)) return ("item", null);
        // A plang list<T> NODE (action.list : list<action>) is {list, kind: element} — named before
        // the item classes, which would answer a plain "list" with no element.
        if (typeof(app.type.item.list.@this).IsAssignableFrom(type) && ContainerFamily(type) is { } node)
            return (node.Family, Face(PlangName(node.Element)));
        if (Array.Find(types, t => t.Owned.Any(o => !o.Assignable && o.Clr == type)) is { } owner)
            return (owner.Name, null);
        // A collected type (type<goal>, type<type>) is a type.
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(app.type.@this<,>)) return ("type", null);
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(app.type.item.choice.@this<>))
            return ("choice", new global::app.type.item.choice.set.@this(type.GetGenericArguments()[0]).Name);
        if (typeof(app.type.item.@this).IsAssignableFrom(type) && Array.Find(types, t => t.ClrType == type) is { } own)
            return (own.Name, null);
        // a kind of a family: the family, with the kind whose values are this class (a setting class)
        if (typeof(app.type.item.@this).IsAssignableFrom(type) && FamilyName(type) is { } family)
            return (family, Array.Find(types, t => t.Names(family))?.kind[type]?.Name);
        if (ContainerFamily(type) is { } fam) return (fam.Family, Face(PlangName(fam.Element)));
        return ("clr", null);
    }

    /// <summary>The name a C# class prints as — the kind rides in the name for a family
    /// (<c>list&lt;path&gt;</c>).</summary>
    internal string Face(System.Type type) => Face(PlangName(type));

    // A {name, kind} as the entity's face prints it — the kind rides in the name for a family.
    private string Face((string Name, string? Kind) named)
        => named.Kind == null ? named.Name : $"{named.Name}<{named.Kind}>";

    /// <summary>A type's property <paramref name="name"/> of C# class <paramref name="clr"/> — its
    /// type born from its name and class directly, since a type's facts are read while the set fills.</summary>
    internal property.@this Property(string name, System.Type clr)
    {
        var (typeName, kind) = PlangName(clr);
        return new property.@this
        {
            Name = char.ToLower(name[0]) + name[1..],
            Type = new app.type.@this(typeName, Items().FirstOrDefault(t => t.Names(typeName))?.ClrType, kind),
        };
    }

}
