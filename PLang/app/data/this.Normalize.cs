using System.Collections;
using app;
using app.channels.serializers.filters;

namespace app.data;

/// <summary>
/// Data structural normalization. Walks <see cref="@this.Value"/> into a
/// uniform tree whose runtime types are limited to: <c>null</c>, primitives
/// (string, int, long, double, bool, DateTime, decimal), <c>byte[]</c>,
/// <see cref="@this"/>, or <see cref="IList"/> of any of the above.
/// Reflection fires here (one-time, cached by the wire-view filter) so
/// format encoders never have to introspect arbitrary C# types.
///
/// <para>Bounded: a visited-set guards against reference cycles, a depth cap
/// prevents stack overflow on deep but acyclic graphs. Both raise typed
/// errors hard at serialize-time — no silent truncation.</para>
/// </summary>
public partial class @this
{
    /// <summary>
    /// Hard cap on Normalize depth. Mirrors <c>MaxRehydrationDepth</c> in
    /// <see cref="this.Transport.cs"/> — past this is almost certainly an
    /// unbounded structure rather than legitimate nesting.
    /// </summary>
    private const int MaxNormalizeDepth = 128;

    /// <summary>
    /// Normalize <see cref="Value"/> into the uniform tree. Idempotent — calling
    /// twice produces the same shape. The default mode is <see cref="View.Out"/>;
    /// pass <see cref="View.Debug"/> to bypass the <c>[Out]</c> whitelist.
    /// </summary>
    public object? Normalize(View mode = View.Out)
    {
        var visited = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        return NormalizeValue(Value, mode, visited, depth: 0, types: _context?.App?.Types);
    }

    /// <summary>
    /// Walk a single value. Recurses on containers + domain objects; returns
    /// already-tree values unchanged.
    /// </summary>
    internal static object? NormalizeValue(object? value, View mode, HashSet<object> visited, int depth)
        => NormalizeValue(value, mode, visited, depth, types: null);

    /// <summary>
    /// Walk-with-types overload — when <paramref name="types"/> is non-null,
    /// values whose CLR type resolves to a registered <see cref="app.Attributes.PlangTypeAttribute"/>
    /// name AND have at least one entry in <see cref="app.types.renderers.@this"/>
    /// are tagged as <see cref="TypedValueNode"/> rather than reflected. Bare
    /// (no-types) callers fall back to today's reflection — safe default for
    /// tests / no-Context paths.
    /// </summary>
    internal static object? NormalizeValue(object? value, View mode, HashSet<object> visited, int depth,
        app.types.@this? types)
    {
        if (depth > MaxNormalizeDepth)
            throw new NormalizeException(
                $"Normalize depth exceeded cap ({MaxNormalizeDepth}). Likely an unbounded structure.",
                "NormalizeMaxDepthExceeded");

        // Tree-native leaves ------------------------------------------------
        if (value is null) return null;
        if (value is string || value is bool
            || value is System.DateTime || value is System.DateTimeOffset
            || value is System.TimeSpan || value is System.Guid
            || value is decimal || value is byte[])
            return value;
        // Enums are leaves — represented by name on the wire. Without this
        // they (and DateTimeOffset/TimeSpan/Guid above) fall through to
        // NormalizeObject which walks their public struct properties into
        // an unusable property bag.
        if (value is System.Enum) return value;
        // Delegates aren't representable as a property bag — Method/Target
        // walks blow up on reflection-only members like
        // RuntimeType.DeclaringMethod. Emit as null leaf.
        if (value is System.Delegate) return null;
        if (value is int || value is long || value is double || value is float)
            return value;

        // Nested Data: normalize its Value into a fresh tree. Do NOT mutate the
        // source Data — outbound serialization must be observation-only or
        // domain values (Identity etc.) get replaced with their property-bag
        // form on the original, breaking subsequent in-memory reads.
        if (value is @this nested)
        {
            if (!visited.Add(nested))
                throw CycleError(nested);
            try
            {
                var innerNormalized = NormalizeValue(nested.Value, mode, visited, depth + 1, types);
                if (ReferenceEquals(innerNormalized, nested.Value)) return nested;
                var copy = new @this(nested.Name, innerNormalized, nested.Type);
                copy.Properties = nested.Properties;
                copy.Signature = nested.Signature;
                return copy;
            }
            finally { visited.Remove(nested); }
        }

        // Dictionaries: become List<Data> with keys as names.
        if (value is IDictionary dict)
        {
            if (!visited.Add(value))
                throw CycleError(value);
            try
            {
                var list = new List<@this>(dict.Count);
                foreach (DictionaryEntry e in dict)
                {
                    var name = e.Key?.ToString() ?? "";
                    var child = new @this(name, NormalizeValue(e.Value, mode, visited, depth + 1, types));
                    list.Add(child);
                }
                return list;
            }
            finally { visited.Remove(value); }
        }

        // Lists / arrays --------------------------------------------------
        if (value is IEnumerable enumerable)
        {
            if (!visited.Add(value))
                throw CycleError(value);
            try
            {
                if (IsHomogeneousPrimitive(enumerable, out _))
                {
                    var bare = new List<object?>();
                    foreach (var item in enumerable) bare.Add(item);
                    return bare;
                }

                var wrapped = new List<object?>();
                foreach (var item in enumerable)
                {
                    var normalized = NormalizeValue(item, mode, visited, depth + 1, types);
                    wrapped.Add(normalized);
                }
                return wrapped;
            }
            finally { visited.Remove(value); }
        }

        // Registered-type tag — when a registry is in scope, a value whose CLR
        // type (or any ancestor in its inheritance chain) resolves to a
        // [PlangType] name AND has a renderer is tagged as a TypedValueNode
        // (writer-resolved). Skips the reflection walk; [Sensitive] discipline
        // becomes the renderer's responsibility.
        //
        // The ancestor walk matches PLang's abstract-base pattern: a concrete
        // FilePath / HttpPath inherits from the abstract path.@this; the
        // renderer is registered under "path" but the runtime value is FilePath.
        // Walking up finds the right registration without combinatorial
        // per-subclass entries.
        if (types != null)
        {
            for (var t = value.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                var typeName = types.ResolveName(t);
                if (typeName != null && types.Renderers.Has(typeName))
                    return new TypedValueNode(value, typeName);
            }
        }

        // Domain object: reflect into List<Data> children using the wire-view filter.
        return NormalizeObject(value, mode, visited, depth, types);
    }

    private static List<@this> NormalizeObject(object obj, View mode, HashSet<object> visited, int depth,
        app.types.@this? types)
    {
        if (!visited.Add(obj))
            throw CycleError(obj);
        try
        {
            var entries = app.channels.serializers.filters.Tagged.PropertiesFor(obj.GetType(), mode);
            var children = new List<@this>(entries.Count);

            foreach (var entry in entries)
            {
                var name = entry.Property.Name.ToLowerInvariant();

                if (entry.Masked)
                {
                    // [Masked] never invokes the getter — the value must not
                    // traverse memory boundaries it shouldn't cross.
                    children.Add(new @this(name, "****"));
                    continue;
                }

                object? raw;
                try
                {
                    raw = entry.Property.GetValue(obj);
                }
                catch (System.Exception ex)
                {
                    throw new NormalizeException(
                        $"Normalize failed reading {obj.GetType().Name}.{entry.Property.Name}: {ex.Message}",
                        "NormalizeGetterThrew", ex);
                }

                children.Add(new @this(name, NormalizeValue(raw, mode, visited, depth + 1, types: types)));
            }

            return children;
        }
        finally { visited.Remove(obj); }
    }

    private static bool IsHomogeneousPrimitive(IEnumerable enumerable, out System.Type? elementType)
    {
        elementType = null;
        foreach (var item in enumerable)
        {
            if (item == null) return false;
            var t = item.GetType();
            if (!IsTreeLeafType(t)) return false;
            if (elementType == null) elementType = t;
            else if (elementType != t) return false;
        }
        return elementType != null;
    }

    // Keep in lockstep with the leaf cases in NormalizeValue (top of file).
    private static bool IsTreeLeafType(System.Type t)
        => t == typeof(string) || t == typeof(int) || t == typeof(long)
        || t == typeof(double) || t == typeof(float) || t == typeof(bool)
        || t == typeof(System.DateTime) || t == typeof(System.DateTimeOffset)
        || t == typeof(System.TimeSpan) || t == typeof(System.Guid)
        || t == typeof(decimal) || t == typeof(byte[])
        || t.IsEnum;

    private static System.Exception CycleError(object node)
        => new NormalizeException(
            $"Cycle detected during Normalize at type {node.GetType().Name}.",
            "NormalizeCycleDetected");
}
