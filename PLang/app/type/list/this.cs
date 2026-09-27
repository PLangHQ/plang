using System.Reflection;
using app.Attributes;

namespace app.type.list;

/// <summary>
/// The types — every plang type, one set, each type owning its name, aliases, C# class and facts
/// (<c>app.Type</c>). Distinct from <c>app.type.item.list</c> (the plang list VALUE). A lookup walks
/// the set: by name or alias (the type's own <see cref="app.type.@this.Match"/>), by C# class, by
/// identity. How a type comes in lives in the <c>Registry</c> partial.
///
/// File-format characteristics (extension → Kind, extension → MIME, Kind → compressibility) live
/// separately on <see cref="app.format.list.@this"/> at <c>app.Format</c>.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// The context this catalog births values from. The type catalog is a
    /// system-owned collection, born with the App's system context.
    /// </summary>
    internal actor.context.@this Context { get; }

    public @this(actor.context.@this context) : this()
    {
        Context = context;
        Kind = new kind.list.@this(context);   // per-App, born with context → its kinds are stamped
    }

    public @this() { }

    /// <summary>
    /// The kinds: every kind class (json, list, dict, <c>*</c>, number's precisions, hash's
    /// algorithms), the kinds added as instances (choice's closed sets, path's schemes), and the
    /// formats. A kind knows the type it is a kind of.
    /// </summary>
    public kind.list.@this Kind { get; private set; } = new(null);

    /// <summary>
    /// Per-(type, format) renderer table. Vestigial now that a value renders
    /// itself via <c>item.Write</c> — only its membership check feeds Normalize's
    /// "renders-itself, don't reflect" signal. Discovers
    /// <c>app/types/&lt;name&gt;/serializer/&lt;format&gt;.cs</c> classes via
    /// reflection over <see cref="renderer.@this.Assemblies"/> and exposes a
    /// runtime-registration seam for DLLs loaded at runtime.
    /// </summary>
    public renderer.@this Renderer { get; } = new();

    /// <summary>
    /// Per-(type, kind) reader dispatch — the read-side mirror of
    /// <see cref="Renderers"/>. Discovers <c>app/type/&lt;name&gt;/serializer/&lt;kind&gt;.cs</c>
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
        => Array.Find(Types, t => t.Names(name))
           ?? throw new KeyNotFoundException($"No PLang type registered under name '{name}'.");

    /// <summary>True when <paramref name="name"/> names a plang type — the presence question
    /// beside the indexer, which selects and throws on a miss.</summary>
    public bool Contains(string name) => Array.Exists(Types, t => t.Names(name));

    /// <summary>The C# class of the type <paramref name="name"/> names, or null when it names none.</summary>
    public System.Type? Clr(string name) => Array.Find(Types, t => t.Names(name))?.ClrType;

    /// <summary>
    /// The type content of this MIME arrives as. Content off I/O is raw bytes — it IS binary; the
    /// MIME's subtype is the kind, the decode hint that narrows it on access (json→item, jpg→image,
    /// csv→table). <c>image/png</c> → {binary, png}; opaque bytes (octet-stream) → {binary}. Not the
    /// string door: "text/markdown" spelled as a type is {text, md}, as a MIME it is {binary, md}.
    /// </summary>
    public app.type.@this Mime(string mime)
        => this[new app.type.@this("binary", Context.App.Format.Subtype(mime))];

    /// <summary>
    /// The type a file of this extension holds — binary, the extension itself its kind (the
    /// authoritative subtype for a file): <c>.md</c> → {binary, md}, agreeing with its MIME.
    /// The null type for no extension.
    /// </summary>
    public app.type.@this Extension(string extension)
        => string.IsNullOrEmpty(extension) ? app.type.@this.Null
            : this[new app.type.@this("binary", extension.TrimStart('.'))];

    /// <summary>
    /// The full type for a value's type — its identity (name, kind, strict, template) with the
    /// type's facts. A choice's kind names its set, so a <c>{choice, operator}</c> carries that
    /// set's options. The kind is canonicalised (<c>markdown</c> → <c>md</c>). A name no type
    /// answers to is its bare identity. Holds no context.
    /// </summary>
    public app.type.@this this[app.type.@this type]
    {
        get
        {
            // The kind by its own name: a format's canonical spelling (markdown → md), then the name
            // of the kind an alias answers to (integer → int).
            var kind = type.Kind?.Name is { } k ? Kind[Context?.App.Format.CanonicaliseKind(k) ?? k].Name : null;
            if (Array.Find(Types, t => t.Names(type.Name)) is not { } entry)
                return new app.type.@this(type.Name, kind, type.Strict, type.Template);
            if (kind == null && !type.Strict && type.Template == null) return entry;
            return new app.type.@this(entry.Name, entry.ClrType, kind, type.Strict, type.Template)
            {
                Alias = entry.Alias,
                Owned = entry.Owned,
                Property = entry.Property,
                Values = kind != null && Kind[kind] is global::app.type.item.choice.set.@this set ? set.Values : entry.Values,
                Shape = entry.Shape,
                ConstructorSignature = entry.ConstructorSignature,
                Example = entry.Example,
                Description = entry.Description,
            };
        }
    }

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
            // its kind; a choice carries its set's options; every other name is the named entity.
            var (name, kind) = PlangName(clrType);
            return kind == null ? this[name] : this[new app.type.@this(name, kind)];
        }
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
        var types = _types;
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
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(app.type.item.choice.@this<>))
            return ("choice", new global::app.type.item.choice.set.@this(type.GetGenericArguments()[0]).Name);
        if (typeof(app.type.item.@this).IsAssignableFrom(type)
            && (Array.Find(types, t => t.ClrType == type)?.Name ?? FamilyName(type)) is { } declared)
            return (declared, null);
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
            Type = new app.type.@this(typeName, Array.Find(_types, t => t.Names(typeName))?.ClrType, kind),
        };
    }

    // --- Type-kind queries ---

    // A CLR scalar the catalog fold never walks into (it has no fields to list).
    private bool IsPrimitive(System.Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid);
    }

    // --- Catalog support ---

    /// <summary>
    /// Walks action parameter types and returns structured catalog entries.
    /// Discovery is transitive: every type referenced in a schema is itself surfaced.
    ///   - Enum (or ValidValues) → TypeEntry with Values populated.
    ///   - Record                → TypeEntry with Property built from [LlmBuilder] props.
    ///   - Opaque (no markers)   → not surfaced.
    /// </summary>
    [System.Obsolete("Type/module discovery moves to list<type>/list<module> + a Fluid render — do not add new callers.")]
    public List<app.type.@this> BuildTypeEntries(app.module.list.@this modules)
    {
        Load();
        var entries = new List<app.type.@this>();
        var seen = new HashSet<System.Type>();
        var queue = new Queue<System.Type>();

        void Enqueue(System.Type? t)
        {
            if (t == null || seen.Contains(t)) return;
            if (IsPrimitive(t) || t == typeof(object)) return;
            if (t.IsArray || t.IsGenericType) return;
            queue.Enqueue(t);
        }

        foreach (var ns in modules.Names)
        {
            foreach (var actionName in modules.GetActions(ns))
            {
                var actionType = modules.GetActionType(ns, actionName);
                if (actionType == null) continue;

                foreach (var prop in actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;
                    var unwrapped = UnwrapType(prop.PropertyType);
                    Enqueue(unwrapped);
                    // A native typed list<T> carries its element type intrinsically;
                    // walk it so the builder gets T's schema (e.g. list<LlmMessage>).
                    if (unwrapped is { IsGenericType: true } u
                        && u.GetGenericTypeDefinition() == typeof(app.type.item.list.@this<>))
                        Enqueue(u.GetGenericArguments()[0]);
                }
            }
        }

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type)) continue;

            var typeName = Face(type);
            // A CLR type no plang type owns is not a catalog entry.
            if (typeName == "clr") continue;
            // Skip `data.@this` — actions with polymorphic Value slots (variable.set
            // etc.) declare it as `object`; surfacing it again as a scalar
            // ("object: string") in the catalog is redundant and confusing.
            if (type == typeof(data.@this) || type == typeof(app.type.@this)) continue;

            var entry = new app.type.@this(typeName,
                Clr(typeName) is { IsAbstract: true } baseClr && baseClr.IsAssignableFrom(type) ? baseClr : type, this);
            if (entry.Values == null && entry.Shape == null && entry.Property == null) continue;
            entries.Add(entry);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (Attribute.IsDefined(prop, typeof(LlmBuilderAttribute))) Enqueue(UnwrapType(prop.PropertyType));
            foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (Attribute.IsDefined(m, typeof(LlmBuilderAttribute))) Enqueue(UnwrapType(m.ReturnType));
        }

        return entries;
    }

    /// <summary>
    /// Unwraps generic wrappers (List&lt;T&gt;, Nullable&lt;T&gt;) to get the inner type.
    /// </summary>
    private System.Type? UnwrapType(System.Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return UnwrapType(underlying);

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(data.@this<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
                return null;
        }

        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (listIface != null)
            return UnwrapType(listIface.GetGenericArguments()[0]);

        if (type.IsArray)
            return UnwrapType(type.GetElementType()!);

        if (IsPrimitive(type)) return null;
        return type;
    }
}
