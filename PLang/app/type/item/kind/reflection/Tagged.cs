using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.item.kind.reflection;

/// <summary>
/// The members of a CLR type a view writes, each with its wire name and whether it is masked, read once per
/// (type, view) and kept. Which members show and which mask is the view's answer (<see cref="global::app.View"/>);
/// this only remembers it. Read by item's general write and the reflection kind's read.
/// </summary>
public static class Tagged
{
    // WireName is the property's serialized name (camelCase), computed once — never re-cased at a
    // call site. Matches STJ's PropertyNamingPolicy.CamelCase so an Output-written shape round-trips
    // through an STJ read.
    public readonly record struct Entry(PropertyInfo Property, bool Masked, string WireName);

    private static readonly ConcurrentDictionary<(System.Type Type, global::app.View Mode), IReadOnlyList<Entry>> _cache = new();

    /// <summary>
    /// Returns the property entries that ship on the wire for <paramref name="type"/>
    /// in <paramref name="mode"/>. Cached. The returned list is immutable —
    /// the same reference is handed back to every caller for the same key.
    /// </summary>
    public static IReadOnlyList<Entry> PropertiesFor(System.Type type, global::app.View mode)
        => _cache.GetOrAdd((type, mode), key => Compute(key.Type, key.Mode));

    private static IReadOnlyList<Entry> Compute(System.Type type, global::app.View view)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var entries = new List<Entry>(props.Length);

        // A type with no Out/Store/Sensitive/Masked tag anywhere is transparent: every public property ships (nested
        // helper records round-trip without a tag inventory). PropertyInfo's IsDefined ignores the inherit flag — a
        // subclass that needs an [Out] on an overridden property re-applies it (FilePath/HttpPath's Scheme).
        var tagged = view.Tagged(type);

        foreach (var prop in props)
        {
            // Indexed properties (this[int] etc.) have no name shape on the wire.
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!prop.CanRead) continue;
            // the view says which members show, and which of those hide their value
            if (!view.Shows(prop, tagged)) continue;
            // [JsonPropertyName] wins (STJ honors it), else camelCase — so an Output-written shape
            // matches what an STJ read expects, key for key.
            var wireName = prop.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
                ?? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            // the member as the class that declares it has it: a base's private setter is no setter seen from the
            // subclass, and every read onto a host sets through this entry
            var member = prop.DeclaringType is { } owner && owner != type
                ? owner.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) ?? prop
                : prop;
            entries.Add(new Entry(member, view.Masks(prop), wireName));
        }

        // Materialize as an array — fixed-size, no overhead vs List<T>'s
        // _size/_version book-keeping. The return type IReadOnlyList<Entry>
        // exposes only the read surface.
        return entries.ToArray();
    }

}
