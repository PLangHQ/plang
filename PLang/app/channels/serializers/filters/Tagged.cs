using System.Collections.Concurrent;
using System.Reflection;

namespace app.channels.serializers.filters;

/// <summary>
/// Tagged-view filter: decides which properties on a CLR type ship on the
/// wire. Consumed by <c>Data.Normalize</c> when walking a non-primitive
/// value into its property-bag child list.
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
///   <item><b>View.Debug</b> — every public instance property ships, except
///         <c>[Sensitive]</c>. <c>[Masked]</c> still emits <c>"****"</c>;
///         debug never unmasks.</item>
/// </list>
///
/// <para>The per-(type, mode) result is cached on first use. Reflection
/// fires once per type per mode per process.</para>
/// </summary>
public static class Tagged
{
    public readonly record struct Entry(PropertyInfo Property, bool Masked);

    private static readonly ConcurrentDictionary<(System.Type Type, global::app.View Mode), IReadOnlyList<Entry>> _cache = new();

    /// <summary>
    /// Returns the property entries that ship on the wire for <paramref name="type"/>
    /// in <paramref name="mode"/>. Cached. The returned list is immutable —
    /// the same reference is handed back to every caller for the same key.
    /// </summary>
    public static IReadOnlyList<Entry> PropertiesFor(System.Type type, global::app.View mode)
        => _cache.GetOrAdd((type, mode), key => Compute(key.Type, key.Mode));

    private static IReadOnlyList<Entry> Compute(System.Type type, global::app.View mode)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var entries = new List<Entry>(props.Length);

        // Tagged-awareness: types that have no Out/Store/Sensitive/Masked tag
        // anywhere are treated as transparent — every public property ships.
        // Lets nested helper types (verb.@this and its Read/Write/Delete/Execute
        // sub-records) round-trip naturally without each sub-property carrying
        // a tag inventory. Mirrors the existing View filter's "isViewAware"
        // discipline.
        bool typeIsTagAware = false;
        foreach (var p in props)
        {
            if (p.IsDefined(typeof(OutAttribute), inherit: true)
                || p.IsDefined(typeof(StoreAttribute), inherit: true)
                || p.IsDefined(typeof(SensitiveAttribute), inherit: true)
                || p.IsDefined(typeof(MaskedAttribute), inherit: true))
            {
                typeIsTagAware = true;
                break;
            }
        }

        foreach (var prop in props)
        {
            // Indexed properties (this[int] etc.) have no name shape on the wire.
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!prop.CanRead) continue;

            // [Sensitive] is wire-layer-only. Store-mode persists the full
            // object (Identity.PrivateKey needs to survive sqlite round-trip).
            if (mode != global::app.View.Store
                && prop.IsDefined(typeof(SensitiveAttribute), inherit: true))
                continue;

            // [JsonIgnore] in Debug/Store mode still excludes — those tags
            // exist for cycle-protection on runtime-graph properties
            // (path.GoalCall, path.Context, GoalCall.Event, etc.). An explicit
            // [Out] or [Store] tag opts the property back in.
            if (mode != global::app.View.Out
                && prop.IsDefined(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), inherit: true)
                && !prop.IsDefined(typeof(OutAttribute), inherit: true)
                && !prop.IsDefined(typeof(StoreAttribute), inherit: true))
                continue;

            bool include = mode switch
            {
                global::app.View.Debug => true,
                // Transparent types (no tag inventory) ship every public
                // property regardless of mode — the type opted out of the
                // tagged-property filter discipline.
                global::app.View.Store when !typeIsTagAware => true,
                global::app.View.Out when !typeIsTagAware => true,
                global::app.View.Store => prop.IsDefined(typeof(StoreAttribute), inherit: true),
                _ => prop.IsDefined(typeof(OutAttribute), inherit: true),
            };

            if (!include) continue;

            // [Masked] is a wire-shape concern (hide the value from the
            // receiver). On the local persistence path, the real value
            // travels — no observer to hide from.
            bool masked = mode != global::app.View.Store
                && prop.IsDefined(typeof(MaskedAttribute), inherit: true);
            entries.Add(new Entry(prop, masked));
        }

        // Materialize as an array — fixed-size, no overhead vs List<T>'s
        // _size/_version book-keeping. The return type IReadOnlyList<Entry>
        // exposes only the read surface.
        return entries.ToArray();
    }

    /// <summary>Test-only — clears the per-type cache.</summary>
    internal static void ClearCacheForTests() => _cache.Clear();

    /// <summary>Test-only — current cache size.</summary>
    internal static int CacheSize => _cache.Count;
}
