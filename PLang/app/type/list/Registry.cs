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
    private volatile bool _loaded;

    /// <summary>Assemblies the startup scan reads. Defaults to the App assembly; callers can extend.</summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// Built-in type names a runtime-loaded assembly may not claim. Their bodies are signing- or
    /// transport-load-bearing: a DLL that replaced <c>identity</c>'s class or its renderer could
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

    // Every type, loaded on first ask.
    private IEnumerable<global::app.type.@this> Types
    {
        get
        {
            Load();
            return Items();
        }
    }

    // The startup scan: every item class in Assemblies comes in under its name, then each gets its
    // facts once every name is known (a fact names its property types), then each type takes its
    // kinds. A clash here is plang's own.
    private void Load()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            foreach (var assembly in Assemblies)
                foreach (var clr in assembly.GetTypes())
                    if (NameOf(clr) is { } name) base.Add(new global::app.type.@this(name, clr, null));
            var faceted = Items().Select(t => new global::app.type.@this(t.Name, t.ClrType!, this)).ToArray();
            while (CountRaw > 0) RemoveAt(0);
            foreach (var type in faceted) base.Add(type);
            foreach (var assembly in Assemblies) Enlist(assembly);
            Guard();
            _loaded = true;
        }
    }

    /// <summary>The guard every slot passes: the types hold types, and every name a type answers to — its
    /// word, its namespace, its aliases — names one class. Only plang's own code reaches the slots around
    /// <see cref="Add(System.Type, actor.context.@this, string?)"/>, so a refusal here is plang broken, and throws.</summary>
    protected override void Admit(object? slot)
    {
        if (slot is not global::app.type.@this type)
            throw new InvalidOperationException($"the types hold types, not {slot?.GetType().Name ?? "null"}.");
        foreach (var claim in type.Claims)
            if (Items().FirstOrDefault(t => t.Names(claim)) is { } owner && owner.ClrType != type.ClrType)
                throw new InvalidOperationException(
                    $"type name '{claim}' is claimed by both {owner.ClrType?.FullName} and {type.ClrType?.FullName} — one name, one class.");
    }

    protected override void Admit(global::app.type.item.list.@this other)
    {
        foreach (var slot in other.Slots()) Admit(slot);
    }

    /// <summary>
    /// Adds the plang type <paramref name="clr"/> defines — under <paramref name="name"/>, or the
    /// name the class reports. A name another class owns, a sealed name, or an instance property
    /// under a reserved name is an error; the set is unchanged.
    /// </summary>
    public data.@this Add(System.Type clr, actor.context.@this context, string? name = null)
    {
        if ((name ?? NameOf(clr)) is not { } claimed)
            return context.Error(new error.Error($"{clr.FullName} is not a plang type — only items are.", "TypeLoadNotAType", 400));
        // The class that already owns the name adds nothing new: the same answer, whatever the name.
        if (Types.FirstOrDefault(t => t.Names(claimed)) is { } held && held.ClrType == clr) return context.Ok(held);
        var aliases = clr.GetProperty("Alias", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            ?.GetValue(null) as IReadOnlyList<string> ?? [];
        if (aliases.Prepend(claimed).FirstOrDefault(Sealed.Contains) is { } sealedWord)
            return context.Error(new error.Error(
                $"'{sealedWord}' is on the sealed built-in list and may not be claimed by a runtime-loaded type.", "TypeLoadCollision", 400));
        if (Array.Find(clr.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => Reserved.Contains(p.Name)) is { } shadow)
            return context.Error(new error.Error(
                $"Type '{clr.FullName}' declares instance property '{shadow.Name}' — `type`/`error`/`success`/`@schema` are the reserved navigation core and may not be shadowed by a value type.",
                "TypeLoadReservedShadow", 400));
        lock (_lock)
        {
            Load();
            if (Items().FirstOrDefault(t => t.Names(claimed)) is { } owner)
                return owner.ClrType == clr
                    ? context.Ok(owner)
                    : context.Error(new error.Error(
                        $"type name '{claimed}' is claimed by both {owner.ClrType?.FullName} and {clr.FullName} — one name, one class.",
                        "TypeLoadCollision", 400));
            // Its own name first, so its facts can name a property of its own type.
            base.Add(new global::app.type.@this(claimed, clr, null));
            var added = new global::app.type.@this(claimed, clr, this);
            RemoveAt(CountRaw - 1);
            base.Add(added);
            return context.Ok(added);
        }
    }

    /// <summary>
    /// Adds every plang type <paramref name="assembly"/> exports, the kinds it brings (kind classes,
    /// the closed sets its <c>choice&lt;T&gt;</c> properties draw on), and its renderers
    /// (<see cref="ITypeRenderer"/>). Every type added must render — its own renderer or one already
    /// known. The first error stops the load; what was added before it stays.
    /// </summary>
    public data.@this Add(Assembly assembly, actor.context.@this context)
    {
        // An assembly the startup scan read brings nothing new: its types, kinds and renderers are in.
        if (Assemblies.Contains(assembly)) return context.Ok(new List<global::app.type.@this>());

        System.Type[] exported;
        try { exported = assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            // a type the assembly can't load is the load's error, not a partial set of its types
            return context.Error(new error.Error(
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
                return context.Error(new error.Error(
                    $"'{claims}' is on the sealed built-in list and may not be claimed by a runtime-loaded type ({clr.FullName}).", "TypeLoadCollision", 400));
            if (NameOf(clr) is null) continue;
            var result = Add(clr, context);
            if (!result.Success) return result;
            added.Add(this[NameOf(clr)!]);
        }
        Enlist(assembly);
        Guard();

        foreach (var clr in exported)
        {
            if (clr.IsAbstract || clr.IsInterface || !typeof(ITypeRenderer).IsAssignableFrom(clr)) continue;
            if (clr.GetConstructor(System.Type.EmptyTypes) is not { } ctor) continue;
            var renderer = (ITypeRenderer)ctor.Invoke(null);
            if (Sealed.Contains(renderer.TypeName))
                return context.Error(new error.Error(
                    $"ITypeRenderer for '{renderer.TypeName}' rejected — '{renderer.TypeName}' is on the sealed built-in list and its rendering may not be replaced by a runtime-loaded DLL.",
                    "TypeLoadCollision", 400));
            Renderer.Register(renderer.TypeName, renderer.Format, (value, writer) => renderer.Write(value, writer));
        }

        if (added.Find(t => !Renderer.Has(t.Name)) is { } bare)
            return context.Error(new error.Error(
                $"type '{bare.Name}' loaded with no covering renderer (need a Default ITypeRenderer or per-format coverage).",
                "TypeLoadCoverage", 400));
        return context.Ok(added);
    }

    /// <summary>Replaces the type of <paramref name="type"/>'s name with <paramref name="type"/>
    /// itself — the app's own type object for its concept (<c>app.type</c> is the entry named
    /// <c>type</c>). Same name, same class: the guard holds. The kinds the replaced entry held (goal's
    /// .pr format) go over to the new one.</summary>
    internal void Replace(global::app.type.@this type)
    {
        lock (_lock)
        {
            Load();
            var index = Items().ToList().FindIndex(t => t.Names(type.Name));
            if (index >= 0)
            {
                if (Items().ElementAt(index).kind is global::app.type.kind.empty.@this held
                    && type.kind is global::app.type.kind.empty.@this root)
                    foreach (var kind in held.Kinds) root.Add(kind);
                RemoveAt(index);
            }
            base.Add(type);
        }
    }

    /// <summary>Adds a kind to the type it is a kind of — a closed set to choice, a scheme to path.
    /// A kind of the same name on that type is replaced.</summary>
    public void Add(global::app.type.kind.@this kind)
    {
        Load();
        foreach (var key in kind.Mime.Concat(kind.Extension))
            foreach (var type in Items())
                if (type.kind[key] is { } taken
                    && !(string.Equals(taken.Owner, kind.Owner, StringComparison.OrdinalIgnoreCase) && string.Equals(taken.Name, kind.Name, StringComparison.OrdinalIgnoreCase)))
                    throw Taken(key, taken, kind);
        Hold(kind);
    }

    // Puts a kind on its type's empty kind, reading the list as it stands (the startup scan calls it
    // while loading).
    private void Hold(global::app.type.kind.@this kind)
    {
        if (kind.Owner is not { } owner
            || Items().FirstOrDefault(t => t.Names(owner))?.kind is not global::app.type.kind.empty.@this root)
            throw new InvalidOperationException($"kind '{kind.Name}' names no type it is a kind of.");
        root.Add(kind);
    }

    // The guard every format passes once a scan has held its kinds: a MIME or an extension is one format's —
    // a second kind answering to it would make the walk's answer depend on the order the types came in.
    // One pass over the held kinds; what it sees is dropped when it returns.
    private void Guard()
    {
        var seen = new Dictionary<string, global::app.type.kind.@this>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in Items())
            if (type.kind is global::app.type.kind.empty.@this root)
                foreach (var kind in root.Kinds)
                    foreach (var key in kind.Mime.Concat(kind.Extension))
                        if (!seen.TryAdd(key, kind) && !ReferenceEquals(seen[key], kind))
                            throw Taken(key, seen[key], kind);
    }

    private InvalidOperationException Taken(string key, global::app.type.kind.@this taken, global::app.type.kind.@this kind)
        => new($"'{key}' is already the format '{taken.Owner}{(taken.IsEmpty ? "" : "/" + taken.Name)}' — "
               + $"'{kind.Owner}{(kind.IsEmpty ? "" : "/" + kind.Name)}' cannot answer to it too.");

    // How a type's class writes its formats — its IEncode, bound once into a delegate (no reflection per
    // write); null when the class writes none.
    private global::app.type.kind.@this.Encoder? Encoder(System.Type clr)
        => clr.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(global::app.type.item.IEncode<>))
            ? (global::app.type.kind.@this.Encoder)GetType().GetMethod(nameof(Bound), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(clr).Invoke(this, null)!
            : null;

    private global::app.type.kind.@this.Encoder Bound<T>() where T : global::app.type.item.@this, global::app.type.item.IEncode<T>
        => T.Encode;

    // The kinds an assembly brings onto their types: every kind class (born from nothing), and the
    // closed set every choice<T> in it draws on, each with its reader. A set is only identifiable
    // by its usage, so this reflects the assembly's property types.
    private void Enlist(Assembly assembly)
    {
        var seen = new HashSet<System.Type>();
        foreach (var t in assembly.GetTypes())
        {
            if (typeof(global::app.type.kind.@this).IsAssignableFrom(t) && t is { IsAbstract: false }
                && t != typeof(global::app.type.kind.@this) && t.GetConstructor(System.Type.EmptyTypes) != null)
            {
                var kind = (global::app.type.kind.@this)Activator.CreateInstance(t)!;
                if (kind.Owner is { } owner && Items().Any(type => type.Names(owner))) Hold(kind);
            }
            // each format a type's class declares, a kind of that type — written by the class's own encode
            if (t.IsDefined(typeof(global::app.Attributes.FormatAttribute), inherit: false)
                && global::app.type.item.@this.NameOf(t) is { } reads && Items().Any(type => type.Names(reads)))
            {
                var encode = Encoder(t);
                foreach (var format in t.GetCustomAttributes<global::app.Attributes.FormatAttribute>(inherit: false))
                    Hold(new global::app.type.kind.@this(format, reads, encode));
            }
            // each class of settings, a kind of setting by its path (one of it says the path)
            if (t != typeof(global::app.type.item.setting.@this) && typeof(global::app.type.item.setting.@this).IsAssignableFrom(t)
                && t is { IsAbstract: false } && t.GetConstructor(System.Type.EmptyTypes) != null
                && Items().Any(type => type.Names(global::app.type.item.@this.NameOf(typeof(global::app.type.item.setting.@this)))))
                Hold(new global::app.type.item.setting.kind.@this((global::app.type.item.setting.@this)Activator.CreateInstance(t)!, this));
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var held = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (held.IsGenericType && held.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
                    held = held.GetGenericArguments()[0];
                if (!held.IsGenericType || held.GetGenericTypeDefinition() != typeof(global::app.type.item.choice.@this<>)
                    || !seen.Add(held)) continue;
                var inner = held.GetGenericArguments()[0];
                var set = new global::app.type.item.choice.set.@this(inner);
                if (!set.IsClosed)
                    throw new InvalidOperationException($"{inner.FullName} is not a closed set — no enum members, no Choices(context?).");
                Hold(set);
                // the closed reader for this set — one reflective instantiation, then typed reads.
                Reader.Register("choice", set.Name,
                    (global::app.type.reader.ITypeReader)Activator.CreateInstance(
                        typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(inner), set.Name)!);
            }
        }
    }

    // The name a class's type goes by (its declared word, else its namespace), or null when it is no type
    // of its own: not an item, a kind of a family, or a class that neither declares itself a type nor is an @this.
    private static string? NameOf(System.Type type)
    {
        if (!typeof(app.type.item.@this).IsAssignableFrom(type) || FamilyName(type) != null) return null;
        if (type.GetCustomAttribute<PlangTypeAttribute>(inherit: false) == null
            && (!IsThisClass(type) || type.ContainsGenericParameters)) return null;
        return global::app.type.item.@this.NameOf(type);
    }

    private static bool IsThisClass(System.Type type) =>
        string.Equals(type.Name, "this", StringComparison.Ordinal);

    /// <summary>
    /// The family a class is a KIND of — only when it says so. A path scheme declares it
    /// (<c>[PathScheme("file")]</c> → {path, kind: file}); a typed program list is one through its
    /// generic base (<c>list&lt;step&gt;</c> → {list, kind: step}). Null for every other class:
    /// by default a class's name is its own (modifier is "modifier", never a kind of action).
    /// </summary>
    private static string? FamilyName(System.Type type)
    {
        if (type.IsDefined(typeof(app.type.item.path.PathSchemeAttribute), inherit: false))
            return global::app.type.item.@this.NameOf(typeof(app.type.item.path.@this));
        // a class of settings is a kind of setting, named by its path
        if (type != typeof(app.type.item.setting.@this) && typeof(app.type.item.setting.@this).IsAssignableFrom(type))
            return global::app.type.item.@this.NameOf(typeof(app.type.item.setting.@this));
        for (var b = type.BaseType; b != null; b = b.BaseType)
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(app.type.item.list.@this<>))
                return global::app.type.item.@this.NameOf(typeof(app.type.item.list.@this));
        return null;
    }
}
