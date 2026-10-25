using System.Reflection;
using app.Attributes;

namespace app.type.list;

/// <summary>
/// Registry partial of <see cref="@this"/> — how a type comes in
/// (<see cref="Add(System.Type, actor.context.@this, string?)"/>, <see cref="Add(Assembly, actor.context.@this)"/>),
/// how a kind comes in (<see cref="Add(global::app.type.kind.@this)"/>), the startup scan, the guard
/// every slot passes, and the name a C# class reports.
///
/// A class's type:
///   1. Its identity is its namespace (<c>app.type.item.text</c>); it goes by the word its class declares
///      (<c>[PlangType("text")]</c>), else by its namespace. Nothing is guessed from a folder.
///   2. An item class is a type of its own when it is an @this, or declares [PlangType].
///   3. Only items (app.type.item.@this) are plang types; an engine class has no type name.
///   4. A path scheme or a typed program list (<c>list&lt;step&gt;</c>) is a kind of its family and
///      claims no name of its own.
///   Every word, namespace and alias names one class.
/// </summary>
public sealed partial class @this : global::app.type.item.list.@this<global::app.type.@this>
{
    private readonly object _lock = new();

    /// <summary>Assemblies the startup scan reads. Defaults to the App assembly; callers can extend.</summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// Built-in type names a runtime-loaded assembly may not claim. Their bodies are signing- or
    /// transport-load-bearing: a DLL that replaced <c>identity</c>'s class could
    /// produce authentically-signed values whose body was attacker-composed.
    /// </summary>
    public IReadOnlySet<string> Sealed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "identity", "signature", "signedoperation", "callback", "channel",
    };

    /// <summary>
    /// The reserved core of the navigation planes — <c>%x!type%</c>/<c>!error%</c>/<c>!success%</c>
    /// and the <c>@schema</c> wire marker always answer from the Data. A value type may not declare
    /// an instance property under these names: it would shadow (or be shadowed by) the Data on the
    /// <c>!</c> plane. Statics are fine; only the navigable instance surface can shadow.
    /// </summary>
    public IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "type", "error", "success", "@schema",
    };

    // Every type: the code's, scanned as its list was made, and what this list's app loaded.
    private IEnumerable<global::app.type.@this> Types => Items();

    /// <summary>The types a step can name: every plang type by its name (a type is the word that names it), never one
    /// plang keeps for itself.</summary>
    internal override System.Threading.Tasks.ValueTask<IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => new(Types.Where(t => !t.Internal).Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (global::app.type.item.@this)new global::app.type.item.text.@this(name, this["text"])).ToList());

    // Every name a type answers to — its word, its namespace, its aliases, case aside (type.Names) — to the type, the
    // first that answers, as the set stands. The guard keeps it: every type that comes in passes the guard, which
    // checks its names here and records them, so a name is one class's and a lookup by name never walks the set. A
    // snapshot, replaced whole on each add, so a reader never sees one half made.
    private volatile System.Collections.Immutable.ImmutableDictionary<string, global::app.type.@this> _named
        = System.Collections.Immutable.ImmutableDictionary.Create<string, global::app.type.@this>(StringComparer.OrdinalIgnoreCase);

    // The type a name names as the guard has recorded it so far — this list's own, else the list under's (the scan
    // reads it as it fills the set).
    private global::app.type.@this? Named(string name)
    {
        if (_named.GetValueOrDefault(name) is { } type) return type;
        foreach (var under in Under)
            if (under.Named(name) is { } named) return named;
        return null;
    }

    // The lists this one is a layer over, as type lists: the only list a type list admits as a row (Admit). None for the
    // code's, one for a layer.
    private @this[] Under => Chunks<@this>();

    // Every type's class to the type, the first that has it, as the set stands (naming reads the set while the scan
    // fills it). Kept by the guard beside the names, so a lookup by class never walks the set.
    private volatile System.Collections.Immutable.ImmutableDictionary<System.Type, global::app.type.@this> _classed
        = System.Collections.Immutable.ImmutableDictionary<System.Type, global::app.type.@this>.Empty;

    // The type a class defines: this list's own, else the list under's.
    private global::app.type.@this? Classed(System.Type clr)
    {
        if (_classed.GetValueOrDefault(clr) is { } type) return type;
        foreach (var under in Under)
            if (under.Classed(clr) is { } classed) return classed;
        return null;
    }

    /// <summary>Whether this list, a layer, holds types of its own — what its app loaded over the list under it.</summary>
    internal bool HasOwn => !_named.IsEmpty;

    // The entry is one this list holds itself, not one of the list it is a layer over.
    private bool Holds(global::app.type.@this entry) => ReferenceEquals(_named.GetValueOrDefault(entry.Name), entry);

    // The code's scan, as the code's list is made: every item class in Assemblies comes in under its name, then each
    // type takes its kinds; a type's facts are read when first asked, once every name is known (a fact names its
    // property types). A clash here is plang's own.
    private void Scan()
    {
        // each type is made holding this list, its facts read when asked; the type `type` is made holding this
        // list as its values
        foreach (var assembly in Assemblies)
            foreach (var (name, clr) in code[assembly].Item)
                base.Add(clr == typeof(global::app.type.@this)
                    ? new global::app.type.@this(this)
                    : new global::app.type.@this(name, clr, this));
        // plang's own assemblies: a refusal here is plang broken
        foreach (var assembly in Assemblies)
            if (Enlist(assembly) is { } refused) throw new InvalidOperationException(refused.Message);
        if (Guard() is { } clash) throw new InvalidOperationException(clash.Message);
    }

    /// <summary>The guard every slot passes: the types hold types, and every name a type answers to — its
    /// word, its namespace, its aliases — names one class. Only plang's own code reaches the slots around
    /// <see cref="Add(System.Type, actor.context.@this, string?)"/>, so a refusal here is plang broken, and throws.
    /// A type it lets in is recorded by its names and its class, the first that holds each keeping it.</summary>
    protected override void Admit(object? slot)
    {
        if (slot is not global::app.type.@this type)
            throw new InvalidOperationException($"the types hold types, not {slot?.GetType().Name ?? "null"}.");
        if (Clash(type) is { } clash) throw new InvalidOperationException(clash.Message);
        var named = _named;
        foreach (var claim in type.Claims)
            if (!named.ContainsKey(claim)) named = named.Add(claim, type);
        _named = named;
        if (type.ClrType is { } clr && !_classed.ContainsKey(clr)) _classed = _classed.Add(clr, type);
    }

    // One name, one class: the first name <paramref name="type"/> answers to — its word, its namespace, its
    // aliases — that another class already owns; null when every one is its own.
    private error.@this? Clash(global::app.type.@this type)
    {
        foreach (var claim in type.Claims)
            if (Named(claim) is { } owner && owner.ClrType != type.ClrType)
                return new error.@this(
                    $"type name '{claim}' is claimed by both {owner.ClrType?.FullName} and {type.ClrType?.FullName} — one name, one class.",
                    "TypeLoadCollision", 400);
        return null;
    }

    // A list comes in only as the one this list is a layer over, its first row, and is never recorded here: its names
    // are its own, answered through it.
    protected override void Admit(global::app.type.item.list.@this other)
    {
        if (other is not @this)
            throw new InvalidOperationException($"the types are a layer over types, not {other.GetType().Name}.");
    }

    /// <summary>
    /// Adds the plang type <paramref name="clr"/> defines — under <paramref name="name"/>, or the
    /// name the class reports. A name another class owns, a sealed name, or an instance property
    /// under a reserved name is an error; the set is unchanged.
    /// </summary>
    public data.@this Add(System.Type clr, actor.context.@this context, string? name = null)
    {
        if ((name ?? NameOf(clr)) is not { } claimed)
            return context.Error(new error.@this($"{clr.FullName} is not a plang type — only items are.", "TypeLoadNotAType", 400));
        // The class that already owns the name adds nothing new: the same answer, whatever the name.
        if (Types.FirstOrDefault(t => t.Names(claimed)) is { } type && type.ClrType == clr) return context.Ok(type);
        // what the class declares of itself, as the code's facts read it
        var aliases = code[clr].Alias;
        if (aliases.Prepend(claimed).FirstOrDefault(Sealed.Contains) is { } sealedWord)
            return context.Error(new error.@this(
                $"'{sealedWord}' is on the sealed built-in list and may not be claimed by a runtime-loaded type.", "TypeLoadCollision", 400));
        if (Array.Find(clr.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => Reserved.Contains(p.Name)) is { } shadow)
            return context.Error(new error.@this(
                $"Type '{clr.FullName}' declares instance property '{shadow.Name}' — `type`/`error`/`success`/`@schema` are the reserved navigation core and may not be shadowed by a value type.",
                "TypeLoadReservedShadow", 400));
        lock (_lock)
        {
            if (Items().FirstOrDefault(t => t.Names(claimed)) is { } owner && owner.ClrType == clr)
                return context.Ok(owner);
            var added = new global::app.type.@this(claimed, clr, this);
            if (Clash(added) is { } clash) return context.Error(clash);
            base.Add(added);
            return context.Ok(added);
        }
    }

    /// <summary>
    /// Adds every plang type <paramref name="assembly"/> exports and the kinds it brings (kind classes,
    /// the closed sets its <c>choice&lt;T&gt;</c> properties draw on, its formats). A value writes itself.
    /// The first error stops the load; what was added before it stays.
    /// </summary>
    public data.@this Add(Assembly assembly, actor.context.@this context)
    {
        // An assembly the startup scan read brings nothing new: its types and kinds are in.
        if (Assemblies.Contains(assembly)) return context.Ok(new List<global::app.type.@this>());

        System.Type[] exported;
        try { exported = assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            // a type the assembly can't load is the load's error, not a partial set of its types
            return context.Error(new error.@this(
                $"{assembly.GetName().Name}: types failed to load — {string.Join("; ", ex.LoaderExceptions.Select(e => e?.Message).Distinct())}",
                "TypeLoadFailed", 400) { Exception = ex });
        }

        var added = new List<global::app.type.@this>();
        foreach (var clr in exported)
        {
            if (clr.IsAbstract || clr.IsInterface) continue;
            // A class that claims a sealed name is refused whether or not it is an item: the name is
            // what a loaded assembly may not take.
            var claims = clr.GetCustomAttribute<PlangTypeAttribute>(inherit: false) != null || IsThisClass(clr)
                ? global::app.type.item.@this.NameOf(clr) : null;
            if (claims != null && Sealed.Contains(claims) && Items().FirstOrDefault(t => t.Names(claims))?.ClrType != clr)
                return context.Error(new error.@this(
                    $"'{claims}' is on the sealed built-in list and may not be claimed by a runtime-loaded type ({clr.FullName}).", "TypeLoadCollision", 400));
            if (NameOf(clr) is null) continue;
            var result = Add(clr, context);
            if (!result.Success) return result;
            added.Add(this[NameOf(clr)!]);
        }
        // a loaded assembly's kinds are the program's: a refusal is the load's answer
        if ((Enlist(assembly) ?? Guard()) is { } refused) return context.Error(refused);
        return context.Ok(added);
    }

    /// <summary>Adds a kind to the type it is a kind of — a closed set to choice, a scheme to path.
    /// A kind of the same name on that type is replaced.</summary>
    public void Add(global::app.type.kind.@this kind)
    {
        foreach (var key in kind.Mime.Concat(kind.Extension))
            foreach (var type in Items())
                if (type.kind[key] is { } taken
                    && !(ReferenceEquals(taken.Owner, kind.Owner) && string.Equals(taken.Name, kind.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(Taken(key, taken, kind).Message);
        if (Hold(kind) is { } refused) throw new global::app.error.AppException(refused);
    }

    // Holds a kind (the startup scan calls it while loading): on a type this list holds itself, here; on a type below
    // this layer, as the app's origin answers it. Answers the refusal; null when the kind is held.
    private error.@this? Hold(global::app.type.kind.@this kind)
    {
        if (kind.Owner is { } owner && !Holds(owner)) return _origin.Hold(kind);
        Put(kind);
        return null;
    }

    /// <summary>Puts a kind on the empty kind of the type it was born holding.</summary>
    internal void Put(global::app.type.kind.@this kind)
    {
        if (kind.Owner is not { kind: global::app.type.kind.empty.@this root })
            throw new InvalidOperationException($"kind '{kind.Name}' names no type it is a kind of.");
        root.Add(kind);
    }

    // The guard every format passes once a scan has held its kinds: a MIME or an extension is one format's —
    // a second kind answering to it would make the walk's answer depend on the order the types came in.
    // One pass over the held kinds; what it sees is dropped when it returns. Answers the first clash.
    private error.@this? Guard()
    {
        var seen = new Dictionary<string, global::app.type.kind.@this>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in Items())
            if (type.kind is global::app.type.kind.empty.@this root)
                foreach (var kind in root.Kinds)
                    foreach (var key in kind.Mime.Concat(kind.Extension))
                        if (!seen.TryAdd(key, kind) && !ReferenceEquals(seen[key], kind))
                            return Taken(key, seen[key], kind);
        return null;
    }

    private static error.@this Taken(string key, global::app.type.kind.@this taken, global::app.type.kind.@this kind)
        => new($"'{key}' is already the format '{taken.Owner?.Name}{(taken.IsEmpty ? "" : "/" + taken.Name)}' — "
               + $"'{kind.Owner?.Name}{(kind.IsEmpty ? "" : "/" + kind.Name)}' cannot answer to it too.", "TypeLoadCollision", 400);

    // The kinds an assembly brings onto their types: every kind class (born from nothing), and every closed set its
    // classes mark a choice ([Choice]), each with its reader. Answers what it refuses (a class marked a choice that is
    // no closed set); null when every kind is held.
    private error.@this? Enlist(Assembly assembly)
    {
        // the assembly's facts, read once per code; the types by every name they answer to, as the guard has recorded
        // them here and below
        var library = code[assembly];
        // each kind class, born holding the entry of the type it is a kind of (the T of its kind<T>), when this list
        // holds that type
        foreach (var (clr, type) in library.Kind)
            if (Named(type) is { } owner
                && Hold((global::app.type.kind.@this)System.Activator.CreateInstance(clr, owner)!) is { } refused)
                return refused;
        foreach (var mark in library.Marked)
        {
            var t = mark.Clr;
            // each format a type's class declares, a kind of that type — written by the class's own encode, read by
            // its own decode when it has one. The type is found as the list finds any class's: by its name, else as
            // the list names the class (a list of actions is a list); a declared format no type reads is plang broken
            if (mark.Codec is { } format)
            {
                if ((Named(global::app.type.item.@this.NameOf(t)!) ?? Named(PlangName(t).Name))
                    is not { } declaring)
                    return new error.@this($"{t.FullName} declares a format, but no type reads it — it is no type of its own "
                        + "and the list names none for it.", "TypeLoadFormat", 400);
                // each format is a kind<declaring-type> — one concrete class over the declaring type
                foreach (var attribute in format.Attribute)
                    if (Hold((global::app.type.kind.@this)System.Activator.CreateInstance(format.Kind, attribute, declaring, format.Encode, format.Decode)!) is { } refused)
                        return refused;
            }
            // a list's element kinds, made as a list names its element (list<path>)
            if (mark.List && Named(global::app.type.item.@this.NameOf(t)!) is { } list
                && Hold(new global::app.type.item.list.kind.@this(list, this)) is { } unheld)
                return unheld;
            // each class marked a choice ([Choice]) is the closed set a choice<T> draws on, a kind of choice, with its reader
            if (mark.Choice)
            {
                if (global::app.type.item.choice.set.@this.For(t, this) is not { } set)
                    return new error.@this($"{t.FullName} is marked a choice but is no closed set — no enum members, no Choices(context?), no kinds of its own.", "TypeLoadOpenSet", 400);
                // one name, one set: a second class named as a set is (two enums both `level`) would take its reader
                if (set.Owner?.kind[set.Name] is global::app.type.item.choice.set.@this taken
                    && taken.Of(null) != set.Of(null))
                    return new error.@this(
                        $"choice set name '{set.Name}' is claimed by both {taken.Of(null)?.GetGenericArguments()[0].FullName} and {t.FullName} — one name, one set.",
                        "TypeLoadCollision", 400);
                if (Hold(set) is { } refused) return refused;
                // the closed reader for this set, over the class its members are — one reflective instantiation, then
                // typed reads.
                Reader.Register("choice", set.Name,
                    (global::app.type.reader.ITypeReader)Activator.CreateInstance(
                        typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(set.Of(null)!.GetGenericArguments()[0]), set.Name)!);
            }
        }
        // each class under a family that has kinds ([Kinds]) is one of its kinds, by its own name — but a kind the
        // family already holds (path's schemes, file and http)
        foreach (var subclass in library.Subclasses)
            if (Named(subclass.Family) is { } owner && owner.kind[subclass.Kind] == null)
            {
                if (Hold((global::app.type.kind.@this)System.Activator.CreateInstance(subclass.Made, subclass.Kind, subclass.Clr, owner)!) is { } refused)
                    return refused;

                // a subclass kind that ships its own reader (separator) reads under its family and kind — a
                // {text, separator} reads via (text, separator), and the family's own reader would make the
                // family's value, not the subclass's. Its namespace-keyed auto-registration is never consulted.
                if (subclass.Reader is { } readerType)
                    Reader.Register(subclass.Family, subclass.Kind,
                        (global::app.type.reader.ITypeReader)System.Activator.CreateInstance(readerType)!);
            }
        return null;
    }

    // The name a class's type goes by (its declared word, else its namespace), or null when it is no type
    // of its own: not an item, a kind of a family, or a class that neither declares itself a type nor is an @this.
    private static string? NameOf(System.Type type) => NameOf(type, FamilyName(type));

    // The same, for a class whose family's name (null: no family) is already known.
    private static string? NameOf(System.Type type, string? family)
    {
        if (!typeof(app.type.item.@this).IsAssignableFrom(type) || family != null) return null;
        // A kind is a type under its parent, found only through it — never a top-level type,
        // though `kind : type : item` makes it an @this. Kinds are enlisted onto their parent by Enlist.
        if (typeof(global::app.type.kind.@this).IsAssignableFrom(type)) return null;
        if (type.GetCustomAttribute<PlangTypeAttribute>(inherit: false) == null
            && (!IsThisClass(type) || type.ContainsGenericParameters)) return null;
        return global::app.type.item.@this.NameOf(type);
    }

    private static bool IsThisClass(System.Type type) =>
        string.Equals(type.Name, "this", StringComparison.Ordinal);

    /// <summary>
    /// The family a class is a KIND of: the nearest base that declares it has kinds (<c>[Kinds]</c>) — a file is
    /// a kind of path, a class of settings a kind of setting, a typed list (<c>list&lt;step&gt;</c>) a kind of
    /// list, a where a kind of query. Null for every other class: by default a class's name is its own
    /// (modifier is "modifier", never a kind of action).
    /// </summary>
    private static string? FamilyName(System.Type type)
        => global::app.type.item.@this.FamilyOf(type) is { } kind ? global::app.type.item.@this.NameOf(kind.Family) : null;
}
