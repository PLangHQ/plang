using System.Collections.Concurrent;
using System.Reflection;

namespace app.channel.serializer.filter;

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
        //
        // PropertyInfo's IsDefined ignores the inherit flag — base-declaration
        // attributes don't propagate to override properties regardless of the
        // flag. Subclasses that need an [Out] on an overridden property re-apply
        // the tag explicitly (see FilePath/HttpPath's Scheme override).
        // inherit:false documents the actual semantics.
        bool typeIsTagAware = false;
        foreach (var p in props)
        {
            if (p.IsDefined(typeof(OutAttribute), inherit: false)
                || p.IsDefined(typeof(StoreAttribute), inherit: false)
                || p.IsDefined(typeof(SensitiveAttribute), inherit: false)
                || p.IsDefined(typeof(MaskedAttribute), inherit: false))
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
                && prop.IsDefined(typeof(SensitiveAttribute), inherit: false))
                continue;

            // [JsonIgnore] in Debug/Store mode still excludes — those tags
            // exist for cycle-protection on runtime-graph properties
            // (path.GoalCall, path.Context, GoalCall.Event, etc.). An explicit
            // [Out] or [Store] tag opts the property back in.
            if (mode != global::app.View.Out
                && prop.IsDefined(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), inherit: false)
                && !prop.IsDefined(typeof(OutAttribute), inherit: false)
                && !prop.IsDefined(typeof(StoreAttribute), inherit: false))
                continue;

            bool include = mode switch
            {
                global::app.View.Debug => true,
                // Transparent types (no tag inventory) ship every public
                // property regardless of mode — the type opted out of the
                // tagged-property filter discipline.
                global::app.View.Store when !typeIsTagAware => true,
                global::app.View.Out when !typeIsTagAware => true,
                global::app.View.Store => prop.IsDefined(typeof(StoreAttribute), inherit: false),
                _ => prop.IsDefined(typeof(OutAttribute), inherit: false),
            };

            if (!include) continue;

            // [Masked] is a wire-shape concern (hide the value from the
            // receiver). On the local persistence path, the real value
            // travels — no observer to hide from.
            bool masked = mode != global::app.View.Store
                && prop.IsDefined(typeof(MaskedAttribute), inherit: false);
            // [JsonPropertyName] wins (STJ honors it), else camelCase — so an Output-written shape
            // matches what an STJ read expects, key for key.
            var wireName = prop.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name
                ?? System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            entries.Add(new Entry(prop, masked, wireName));
        }

        // Materialize as an array — fixed-size, no overhead vs List<T>'s
        // _size/_version book-keeping. The return type IReadOnlyList<Entry>
        // exposes only the read surface.
        return entries.ToArray();
    }

}
