using System.Reflection;

namespace app.type.item.choice.list;

/// <summary>
/// The closed sets — one <see cref="set.@this"/> per CLR type that carries options (an enum, or a
/// class declaring a static <c>Choices(context?)</c>). A set's plang name is its KIND, never a type:
/// a choice drawn from <c>Operator</c> is <c>{choice, kind: operator}</c>. Reachable as
/// <c>app.Type.Choice</c>. Selection by CLR type: the indexer selects and throws on a miss,
/// <see cref="Contains"/> answers presence.
/// </summary>
public sealed class @this
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, set.@this> _sets = new();

    // The owning type registry — a closed set registers its reader through it.
    private readonly global::app.type.list.@this _owner;
    internal @this(global::app.type.list.@this owner) { _owner = owner; }

    /// <summary>The closed set <paramref name="clr"/> draws from — through <c>Nullable</c>,
    /// <c>Data&lt;T&gt;</c> and <c>choice&lt;T&gt;</c>. Throws when the type carries no options.</summary>
    public set.@this this[System.Type clr]
    {
        get
        {
            var inner = Unwrap(clr);
            var found = _sets.GetOrAdd(inner, t => new set.@this(t));
            return found.IsClosed ? found
                : throw new KeyNotFoundException($"{inner.FullName} is not a closed set — no enum members, no Choices(context?).");
        }
    }

    /// <summary>True when <paramref name="clr"/> (through its wrappers) carries options.</summary>
    public bool Contains(System.Type clr) => _sets.GetOrAdd(Unwrap(clr), t => new set.@this(t)).IsClosed;

    /// <summary>The closed set named <paramref name="kind"/> — a choice's kind names its set
    /// (<c>{choice, operator}</c>). Sets are known once met (boot, <c>code.load</c>, a CLR ask).
    /// Throws on a miss.</summary>
    public set.@this this[string kind]
        => Named(kind) ?? throw new KeyNotFoundException($"No closed set named '{kind}'.");

    /// <summary>True when a closed set named <paramref name="kind"/> is known.</summary>
    public bool Contains(string kind) => Named(kind) != null;

    private set.@this? Named(string kind)
        => _sets.Values.FirstOrDefault(s => s.IsClosed && string.Equals(s.Name, kind, System.StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Registers every closed set drawn on by a <c>choice&lt;T&gt;</c> reachable in
    /// <paramref name="assembly"/> — a reader per <c>(choice, kind)</c>. A set is only identifiable by
    /// its usage, so this reflects the assembly's property types. Fired when an assembly is
    /// discovered: boot (the PLang assembly) and <c>code.load</c>. A set that declares no name fails
    /// here, loud. Idempotent — re-registering the same set overwrites.
    /// </summary>
    public void Register(System.Reflection.Assembly assembly)
    {
        // The choice FAMILY — a wire type {name:"choice", kind:"operator"} resolves to choice<T>.
        _owner.Register("choice", typeof(global::app.type.item.choice.@this<>));

        var seen = new HashSet<System.Type>();
        foreach (var t in SafeTypes(assembly))
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var u = UnwrapHolder(prop.PropertyType);
                if (!u.IsGenericType || u.GetGenericTypeDefinition() != typeof(global::app.type.item.choice.@this<>))
                    continue;
                if (!seen.Add(u)) continue;

                var inner = u.GetGenericArguments()[0];
                var kind = this[inner].Name;
                // the closed reader for this set — one reflective instantiation, then typed reads.
                _owner.Reader.Register("choice", kind,
                    (global::app.type.reader.ITypeReader)System.Activator.CreateInstance(
                        typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(inner), kind)!);
            }
    }

    // A code.load'd assembly may reference types it can't fully load; keep the ones that resolve.
    private static IEnumerable<System.Type> SafeTypes(System.Reflection.Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    // A property's declared type down to what it holds: Nullable<T> and Data<T> peel.
    private static System.Type UnwrapHolder(System.Type type)
    {
        var n = Nullable.GetUnderlyingType(type);
        if (n != null) type = n;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(data.@this<>))
            type = type.GetGenericArguments()[0];
        return type;
    }

    // Down to the set itself: a choice<T> holds T.
    private static System.Type Unwrap(System.Type type)
    {
        type = UnwrapHolder(type);
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(global::app.type.item.choice.@this<>)
            ? type.GetGenericArguments()[0] : type;
    }
}
