using System.Reflection;

namespace app.type.kind.list;

/// <summary>
/// The kinds — the collection of every <see cref="app.type.kind.@this"/> (json, list, dict,
/// <c>*</c>, number's precisions, hash's algorithms, later yaml/xml), reached as
/// <c>app.type.Kind[name|clrType|type]</c>. Owns SELECTION + LIFECYCLE: a value asks for its kind
/// by name or alias (a wire descriptor <c>kind:"json"</c>), by its host's CLR type (a <c>clr</c> at
/// birth), or a type asks for its kinds. Per-App, born with the App's context, so the kinds it
/// mints are stamped.
///
/// <para>Never a static factory (<c>kind.Of</c>) and never an implicit <c>(kind)"json"</c> — one
/// door, reached by navigation. The name and C# class indexers never return null: an unknown NAME
/// mints a base instance carrying the name (its verb defaults are its behavior); an unclaimed CLR
/// type is the <c>*</c> reflection kind (the catch-all for any object).</para>
/// </summary>
public sealed class @this
{
    // Kind logic is app-independent, so the type discovery runs ONCE per process.
    private static readonly Discovered _shared = new(typeof(@this).Assembly);

    private readonly global::app.actor.context.@this? _context;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.type.kind.@this> _byName
        = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, global::app.type.kind.@this> _byClr
        = new();
    // Kinds that are instances, not classes of their own: closed sets (choice), path schemes.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.type.kind.@this> _added
        = new(System.StringComparer.OrdinalIgnoreCase);

    public @this(global::app.actor.context.@this? context) => _context = context;

    /// <summary>The kind for a name or alias — a kind class; else a kind added as an instance; else
    /// a base instance carrying the name (the defaults are its behavior). Never null.</summary>
    public global::app.type.kind.@this this[string name]
        => _shared[name] is { } t ? _byClr.GetOrAdd(t, Mint)
            : _added.TryGetValue(name, out var added) ? added
            : _byName.GetOrAdd(name, n => new global::app.type.kind.@this(n, _context));

    /// <summary>Adds a kind that is an instance rather than a class of its own — a closed set, a
    /// path scheme. A second kind under the same name replaces the first.</summary>
    public void Add(global::app.type.kind.@this kind)
    {
        _added[kind.Name] = kind;
        foreach (var alias in kind.Alias) _added[alias] = kind;
    }

    /// <summary>
    /// Adds the closed sets every <c>choice&lt;T&gt;</c> in <paramref name="assembly"/> draws on, each a
    /// kind of choice with its reader. A set is only identifiable by its usage, so this reflects the
    /// assembly's property types; it runs when an assembly is discovered (boot, <c>code.load</c>).
    /// </summary>
    public void Add(Assembly assembly)
    {
        var seen = new System.Collections.Generic.HashSet<System.Type>();
        foreach (var t in SafeTypes(assembly))
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var held = System.Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (held.IsGenericType && held.GetGenericTypeDefinition() == typeof(global::app.data.@this<>))
                    held = held.GetGenericArguments()[0];
                if (!held.IsGenericType || held.GetGenericTypeDefinition() != typeof(global::app.type.item.choice.@this<>)
                    || !seen.Add(held)) continue;
                var inner = held.GetGenericArguments()[0];
                var set = new global::app.type.item.choice.set.@this(inner, _context);
                if (!set.IsClosed)
                    throw new System.InvalidOperationException($"{inner.FullName} is not a closed set — no enum members, no Choices(context?).");
                Add(set);
                // the closed reader for this set — one reflective instantiation, then typed reads.
                _context!.App.Type.Reader.Register("choice", set.Name,
                    (global::app.type.reader.ITypeReader)System.Activator.CreateInstance(
                        typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(inner), set.Name)!);
            }
    }

    // A code.load'd assembly may reference types it can't fully load; keep the ones that resolve.
    private static System.Collections.Generic.IEnumerable<System.Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    /// <summary>The kind a host object of <paramref name="clrType"/> is — a claimed CLR form
    /// (exact wins, then assignable: <c>JsonElement</c>→json, <c>IList</c>→list, <c>IDictionary</c>
    /// →dict), else the <c>*</c> reflection kind (any other object). Never null.</summary>
    public global::app.type.kind.@this this[System.Type clrType]
        => _byClr.GetOrAdd(_shared[clrType] ?? _shared.ReflectionType, Mint);

    /// <summary>The kinds of <paramref name="type"/>: the kind classes that declare it, then the
    /// formats of its family (a file extension that is text is a kind of text).</summary>
    public System.Collections.Generic.IEnumerable<global::app.type.kind.@this> this[global::app.type.@this type]
    {
        get
        {
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in _shared.Of(type.Name))
                if (_byClr.GetOrAdd(t, Mint) is var kind && seen.Add(kind.Name)) yield return kind;
            foreach (var added in _added.Values)
                if (string.Equals(added.Owner, type.Name, System.StringComparison.OrdinalIgnoreCase) && seen.Add(added.Name))
                    yield return added;
            if (_context?.App.Format.KindsByFamily() is { } families && families.TryGetValue(type.Name, out var formats))
                foreach (var name in formats)
                    if (seen.Add(name)) yield return this[name];
        }
    }

    private global::app.type.kind.@this Mint(System.Type kindType)
        => (global::app.type.kind.@this)System.Activator.CreateInstance(kindType, new object?[] { _context })!;

    // A scanned set of kind CLR types + their name / alias / CLR-form / owner claims. Immutable once built.
    private sealed class Discovered
    {
        private readonly System.Collections.Generic.Dictionary<string, System.Type> _byName
            = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.List<(System.Type ClrForm, System.Type KindType)> _byClrForm = new();
        private readonly System.Collections.Generic.List<(string Owner, System.Type KindType)> _byOwner = new();
        public System.Type ReflectionType { get; } = typeof(global::app.type.item.kind.reflection.@this);

        public Discovered(Assembly assembly)
        {
            // A kind class is born from the context alone; a kind that is an instance (a closed set,
            // a path scheme) is added to its app's kinds, not discovered.
            foreach (var t in assembly.GetTypes())
                if (typeof(global::app.type.kind.@this).IsAssignableFrom(t)
                    && t is { IsAbstract: false } && t != typeof(global::app.type.kind.@this)
                    && t.GetConstructor([typeof(global::app.actor.context.@this)]) != null)
                {
                    var probe = (global::app.type.kind.@this)System.Activator.CreateInstance(t, new object?[] { null })!;
                    _byName[probe.Name] = t;
                    foreach (var alias in probe.Alias) _byName[alias] = t;
                    if (probe.ClrForm is { } cf) _byClrForm.Add((cf, t));
                    if (probe.Owner is { } owner) _byOwner.Add((owner, t));
                }
        }

        // The kind class claiming a name or alias — a keyed lookup, so an indexer, not a verb+noun method.
        public System.Type? this[string name] => _byName.TryGetValue(name, out var t) ? t : null;

        // The kind classes that declare they are kinds of the type named owner, in discovery order.
        public System.Collections.Generic.IEnumerable<System.Type> Of(string owner)
            => _byOwner.Where(o => string.Equals(o.Owner, owner, System.StringComparison.OrdinalIgnoreCase)).Select(o => o.KindType);

        // The kind class claiming a CLR type — exact ClrForm, then the most-derived assignable.
        public System.Type? this[System.Type clr]
        {
            get
            {
                if (clr == typeof(string)) return null;                                 // string is a scalar, never a sequence kind
                foreach (var (cf, kt) in _byClrForm) if (cf == clr) return kt;          // exact wins
                // Then the MOST-DERIVED assignable claim (IDictionary → dict beats IEnumerable →
                // list for a Dictionary; a claim `cf` beats the best when best is assignable FROM it).
                System.Type? bestCf = null, bestKt = null;
                foreach (var (cf, kt) in _byClrForm)
                    if (cf.IsAssignableFrom(clr) && (bestCf is null || bestCf.IsAssignableFrom(cf)))
                        (bestCf, bestKt) = (cf, kt);
                return bestKt;
            }
        }
    }
}
