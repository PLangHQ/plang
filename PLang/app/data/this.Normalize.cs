using System.Collections;
using app;
using app.channels.serializers.filters;

namespace app.data;

/// <summary>
/// Data structural normalization — Stage 2 of data-normalize.
///
/// <para>Walks <see cref="@this.Value"/> into a uniform tree whose runtime
/// types are limited to: <c>null</c>, primitives (string, int, long, double,
/// bool, DateTime, decimal), <c>byte[]</c>, <see cref="@this"/>, or
/// <see cref="IList"/> of any of the above. Reflection fires here (one-time,
/// cached by <see cref="Wire"/>) so format encoders never have to introspect
/// arbitrary C# types.</para>
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
        return NormalizeValue(Value, mode, visited, depth: 0);
    }

    /// <summary>
    /// Walk a single value. Recurses on containers + domain objects; returns
    /// already-tree values unchanged.
    /// </summary>
    internal static object? NormalizeValue(object? value, View mode, HashSet<object> visited, int depth)
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
                var innerNormalized = NormalizeValue(nested.Value, mode, visited, depth + 1);
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
                    var child = new @this(name, NormalizeValue(e.Value, mode, visited, depth + 1));
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
                    var normalized = NormalizeValue(item, mode, visited, depth + 1);
                    wrapped.Add(normalized);
                }
                return wrapped;
            }
            finally { visited.Remove(value); }
        }

        // Domain object: reflect into List<Data> children using the wire-view filter.
        return NormalizeObject(value, mode, visited, depth);
    }

    private static List<@this> NormalizeObject(object obj, View mode, HashSet<object> visited, int depth)
    {
        if (!visited.Add(obj))
            throw CycleError(obj);
        try
        {
            var entries = app.channels.serializers.filters.Tagged.PropertiesFor(obj.GetType(), mode);
            var children = new List<@this>(entries.Length);

            foreach (var entry in entries)
            {
                var name = entry.Property.Name.ToLowerInvariant();

                if (entry.Masked)
                {
                    // Per the architect spec: the getter is never invoked for
                    // a [Masked] property — the value never traverses memory
                    // boundaries it shouldn't.
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

                children.Add(new @this(name, NormalizeValue(raw, mode, visited, depth + 1)));
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

    private static bool IsTreeLeafType(System.Type t)
        => t == typeof(string) || t == typeof(int) || t == typeof(long)
        || t == typeof(double) || t == typeof(float) || t == typeof(bool)
        || t == typeof(System.DateTime) || t == typeof(decimal) || t == typeof(byte[]);

    private static System.Exception CycleError(object node)
        => new NormalizeException(
            $"Cycle detected during Normalize at type {node.GetType().Name}.",
            "NormalizeCycleDetected");
}
