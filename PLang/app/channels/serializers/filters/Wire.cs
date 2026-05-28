using System.Collections.Concurrent;
using System.Reflection;

namespace app.channels.serializers.filters;

/// <summary>
/// Wire-view filter: decides which properties on a CLR type ship on the wire,
/// per the data-normalize Stage 2 contract. Consumed by <c>Data.Normalize</c>
/// when walking a non-primitive value into its property-bag child list.
///
/// <para>Two modes:</para>
/// <list type="bullet">
///   <item><b>View.Out</b> — only properties tagged <c>[Out]</c> ship.</item>
///   <item><b>View.Debug</b> — every public instance property ships, except
///         <c>[Sensitive]</c>. Debug never unmasks; <c>[Masked]</c> still
///         applies in both modes.</item>
/// </list>
///
/// <para><c>[Sensitive]</c> is hard-excluded in either mode. <c>[Masked]</c>
/// is preserved on the returned entry so Normalize can emit <c>"****"</c>
/// without invoking the getter.</para>
///
/// <para>The per-(type, mode) result is cached on first use. Reflection
/// fires once per type per mode per process.</para>
/// </summary>
public static class Wire
{
    public readonly record struct Entry(PropertyInfo Property, bool Masked);

    private static readonly ConcurrentDictionary<(System.Type Type, global::app.View Mode), Entry[]> _cache = new();

    /// <summary>
    /// Returns the property entries that ship on the wire for <paramref name="type"/>
    /// in <paramref name="mode"/>. Cached.
    /// </summary>
    public static Entry[] PropertiesFor(System.Type type, global::app.View mode)
        => _cache.GetOrAdd((type, mode), key => Compute(key.Type, key.Mode));

    private static Entry[] Compute(System.Type type, global::app.View mode)
    {
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var entries = new List<Entry>(props.Length);

        foreach (var prop in props)
        {
            // Indexed properties (this[int] etc.) have no name shape on the wire.
            if (prop.GetIndexParameters().Length > 0) continue;
            if (!prop.CanRead) continue;
            if (prop.IsDefined(typeof(SensitiveAttribute), inherit: true)) continue;

            bool include = mode == global::app.View.Debug
                || prop.IsDefined(typeof(OutAttribute), inherit: true);

            if (!include) continue;

            bool masked = prop.IsDefined(typeof(MaskedAttribute), inherit: true);
            entries.Add(new Entry(prop, masked));
        }

        return entries.ToArray();
    }

    /// <summary>Test-only — clears the per-type cache.</summary>
    internal static void ClearCacheForTests() => _cache.Clear();

    /// <summary>Test-only — current cache size.</summary>
    internal static int CacheSize => _cache.Count;
}
