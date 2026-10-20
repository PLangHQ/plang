using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.item.kind.reflection;

/// <summary>
/// Tagged-view filter: decides which properties on a CLR type a view writes.
/// Read by an item's tagged <c>Output</c> and the reflection kind's, which
/// write those properties as the value's property bag.
///
/// <para>Three modes:</para>
/// <list type="bullet">
///   <item><b>View.Out</b> — only <c>[Out]</c> ships. <c>[Sensitive]</c>
///         excluded. <c>[Masked]</c> emits <c>"****"</c>. The wire form
///         third parties see.</item>
///   <item><b>View.Store</b> — only <c>[Store]</c> ships, but <c>[Sensitive]</c>
///         and <c>[Masked]</c> are *ignored* in this mode. Round-trips the
///         full local object to disk so it can be restored as-is (Identity
///         with PrivateKey + IsDefault, setting with real value, etc.).
///         No observer to hide from on the local persistence path.</item>
///   <item><b>View.Debug</b> — every public instance property ships. <c>[Sensitive]</c>
///         and <c>[Masked]</c> emit <c>"****"</c>: a developer reading a diagnostic tells
///         "not set" from "set but hidden"; debug never unmasks.</item>
/// </list>
///
/// <para>The per-(type, mode) result is cached on first use. Reflection
/// fires once per type per mode per process.</para>
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

    // True when the type carries any Out/Store/Sensitive/Masked tag — a deliberate host whose
    // reflected shape is a wire contract (cycles are [JsonIgnore]-guarded). A type WITHOUT any
    // tag is opaque: reflecting it whole risks pulling in cyclic/infra graph, so it is written
    // by value, not reflected.
    public static bool IsTagAware(System.Type type)
    {
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (p.IsDefined(typeof(OutAttribute), inherit: false)
                || p.IsDefined(typeof(StoreAttribute), inherit: false)
                || p.IsDefined(typeof(SensitiveAttribute), inherit: false)
                || p.IsDefined(typeof(MaskedAttribute), inherit: false))
                return true;
        return false;
    }

    private static IReadOnlyList<Entry> Compute(System.Type type, global::app.View view)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var entries = new List<Entry>(props.Length);

        // A type with no Out/Store/Sensitive/Masked tag anywhere is transparent: every public property ships (nested
        // helper records round-trip without a tag inventory). PropertyInfo's IsDefined ignores the inherit flag — a
        // subclass that needs an [Out] on an overridden property re-applies it (FilePath/HttpPath's Scheme).
        var tagged = IsTagAware(type);

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
            entries.Add(new Entry(prop, view.Masks(prop), wireName));
        }

        // Materialize as an array — fixed-size, no overhead vs List<T>'s
        // _size/_version book-keeping. The return type IReadOnlyList<Entry>
        // exposes only the read surface.
        return entries.ToArray();
    }

}
