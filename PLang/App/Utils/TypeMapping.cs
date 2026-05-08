using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using App;

namespace App.Utils;

/// <summary>
/// Maps between PLang type names and .NET types.
/// Provides a centralized place for type resolution.
/// </summary>
public static class TypeMapping
{
    // Primitive names only. Domain types declare their name via [PlangType] on the
    // class itself — see PlangTypeIndex.ResolveName / ResolveType for the lookup.
    private static readonly Dictionary<string, Type> Primitives = new(StringComparer.OrdinalIgnoreCase)
    {
        // Scalars
        ["string"] = typeof(string),
        ["text"] = typeof(string),
        ["int"] = typeof(int),
        ["integer"] = typeof(int),
        ["long"] = typeof(long),
        ["float"] = typeof(float),
        ["double"] = typeof(double),
        ["decimal"] = typeof(decimal),
        ["bool"] = typeof(bool),
        ["boolean"] = typeof(bool),
        ["datetime"] = typeof(DateTime),
        ["date"] = typeof(DateTime),
        ["time"] = typeof(TimeSpan),
        ["timespan"] = typeof(TimeSpan),
        ["guid"] = typeof(Guid),
        ["byte"] = typeof(byte),
        ["bytes"] = typeof(byte[]),

        // Collections
        ["list"] = typeof(List<object>),
        ["array"] = typeof(object[]),
        ["dictionary"] = typeof(Dictionary<string, object>),
        ["dict"] = typeof(Dictionary<string, object>),
        ["map"] = typeof(Dictionary<string, object>),
        ["object"] = typeof(object),
        ["dynamic"] = typeof(object),
        ["json"] = typeof(JsonNode),

        // Nullable scalars
        ["int?"] = typeof(int?),
        ["long?"] = typeof(long?),
        ["double?"] = typeof(double?),
        ["bool?"] = typeof(bool?),
        ["datetime?"] = typeof(DateTime?),
        ["guid?"] = typeof(Guid?),
    };

    // Reverse map — primitives only. Domain types go through PlangTypeIndex.
    private static readonly Dictionary<Type, string> PrimitiveNames = new()
    {
        [typeof(string)] = "string",
        [typeof(int)] = "int",
        [typeof(long)] = "long",
        [typeof(float)] = "float",
        [typeof(double)] = "double",
        [typeof(decimal)] = "decimal",
        [typeof(bool)] = "bool",
        [typeof(DateTime)] = "datetime",
        [typeof(TimeSpan)] = "timespan",
        [typeof(Guid)] = "guid",
        [typeof(byte)] = "byte",
        [typeof(byte[])] = "bytes",
        [typeof(object)] = "object",
    };

    /// <summary>
    /// Registers a domain type for deserialization and type resolution.
    /// Prefer declaring [PlangType(name)] on the class itself — that's the single
    /// source of truth. This API remains for test harnesses that synthesize types.
    /// </summary>
    public static void Register(string plangName, Type clrType)
    {
        PlangTypeIndex.RegisterRuntime(plangName, clrType);
    }

    private const int MaxGenericDepth = 20;

    /// <summary>
    /// Gets the .NET Type for a PLang type name.
    /// Handles generics (list&lt;string&gt;), dictionaries (dict&lt;K,V&gt;), nullable (int?), and MIME types.
    /// Depth-guarded against unbounded generic nesting.
    /// </summary>
    public static Type? GetType(string typeName) => GetType(typeName, 0);

    private static Type? GetType(string typeName, int depth)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        if (depth > MaxGenericDepth)
            return null;

        // Handle generic list syntax: list<string>
        if (typeName.StartsWith("list<", StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">"))
        {
            var innerTypeName = typeName[5..^1];
            var innerType = GetType(innerTypeName, depth + 1);
            return innerType != null ? typeof(List<>).MakeGenericType(innerType) : null;
        }

        // Handle generic dictionary syntax: dict<string,int>
        if ((typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ||
             typeName.StartsWith("dictionary<", StringComparison.OrdinalIgnoreCase)) && typeName.EndsWith(">"))
        {
            var prefix = typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ? 5 : 11;
            var inner = typeName[prefix..^1];
            var parts = inner.Split(',');
            if (parts.Length == 2)
            {
                var keyType = GetType(parts[0].Trim(), depth + 1);
                var valueType = GetType(parts[1].Trim(), depth + 1);
                if (keyType == null || valueType == null) return null;
                return typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }
        }

        if (Primitives.TryGetValue(typeName, out var type))
            return type;

        // Domain types: declared via [PlangType] on the class, or discovered via
        // the @this convention (last namespace segment). PlangTypeIndex owns the
        // bidirectional map.
        var domainType = PlangTypeIndex.ResolveType(typeName);
        if (domainType != null) return domainType;

        // MIME families (text/..., image/..., etc.) live in MimeTypes.
        var mimeType = MimeTypes.TryGetClrType(typeName);
        if (mimeType != null) return mimeType;

        return null;
    }

    /// <summary>Forwards to <see cref="MimeTypes.GetMimeType"/>. Prefer MimeTypes directly.</summary>
    public static string GetMimeType(string extension) => MimeTypes.GetMimeType(extension);

    /// <summary>
    /// Gets the PLang type name for a .NET Type.
    /// </summary>
    public static string GetTypeName(Type type)
    {
        if (type == null)
            return "object";

        // Handle nullable types
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return GetTypeName(underlying) + "?";
        }

        // Handle generic types
        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(Data.@this<>))
                return GetTypeName(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>)
                || generic == typeof(IEnumerable<>) || generic == typeof(ICollection<>)
                || generic == typeof(IReadOnlyCollection<>) || generic == typeof(IReadOnlyList<>)
                || generic == typeof(HashSet<>)
                || (generic.FullName != null && (
                    generic.FullName.StartsWith("System.Collections.Immutable.ImmutableList`", StringComparison.Ordinal)
                    || generic.FullName.StartsWith("System.Collections.Generic.ISet`", StringComparison.Ordinal))))
            {
                return $"list<{GetTypeName(type.GetGenericArguments()[0])}>";
            }
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>)
                || (generic.FullName != null && (
                    generic.FullName.StartsWith("System.Collections.Concurrent.ConcurrentDictionary`", StringComparison.Ordinal)
                    || generic.FullName.StartsWith("System.Collections.ObjectModel.ReadOnlyDictionary`", StringComparison.Ordinal)
                    || generic.FullName.StartsWith("System.Collections.Generic.SortedDictionary`", StringComparison.Ordinal)
                    || generic.FullName.StartsWith("System.Collections.Immutable.ImmutableDictionary`", StringComparison.Ordinal))))
            {
                var args = type.GetGenericArguments();
                return $"dict<{GetTypeName(args[0])},{GetTypeName(args[1])}>";
            }
        }

        // Plain Data.@this (non-generic) — universal wrapper, maps to object
        if (type == typeof(Data.@this))
            return "object";

        // Handle arrays
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            if (elementType == typeof(byte))
                return "bytes";
            return $"list<{GetTypeName(elementType)}>";
        }

        if (PrimitiveNames.TryGetValue(type, out var name))
            return name;

        // Custom classes that IMPLEMENT IList<T> (e.g. Actions : IList<Action>) render
        // as list<T>, mirroring the treatment of List<T>/IList<T>. Keeps the catalog
        // honest: Actions isn't a separate opaque type, it's a list of actions.
        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (listIface != null)
            return $"list<{GetTypeName(listIface.GetGenericArguments()[0])}>";

        // Domain types: [PlangType] on the class, or the @this convention. Both are
        // resolved by PlangTypeIndex — no separate reflection walk here.
        var declared = PlangTypeIndex.ResolveName(type);
        if (declared != null) return declared;

        // [Choices]-bearing types: lowercased type name (e.g. Actor → "actor"). The
        // catalog renders the choices list separately via Catalog/Type Information.
        if (App.Choices.@this.Has(type))
            return StripGenericArity(type.Name).ToLowerInvariant();

        return StripGenericArity(type.Name).ToLowerInvariant();
    }

    private static string StripGenericArity(string name)
    {
        var idx = name.IndexOf('`');
        return idx >= 0 ? name[..idx] : name;
    }

    // Previously: a private _obpThisCache + ResolveObpThisType helper duplicated
    // the forward/backward @this walk. That moved into PlangTypeIndex which owns
    // the full [PlangType] + @this resolution for domain types.

    /// <summary>
    /// Gets the valid values for a constrained type — enum names for real enums,
    /// or the [Choices] vocabulary for types that declare one. Returns null when
    /// the type is neither an enum nor a [Choices]-bearing type.
    /// </summary>
    public static string[]? GetValidValues(Type type, Actor.Context.@this? context = null)
    {
        // Unwrap nullable
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        // Unwrap Data<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Data.@this<>))
            type = type.GetGenericArguments()[0];

        // Enums: return all enum names
        if (type.IsEnum)
            return Enum.GetNames(type);

        // [Choices] convention
        return App.Choices.@this.Get(type, context);
    }

    /// <summary>
    /// Determines if a type is considered a primitive type in PLang.
    /// </summary>
    /// <summary>
    /// True for [PlangType] domain types whose wire form is a primitive (typically string).
    /// Recognizes any of:
    ///   - public static <c>Resolve(input, Context)</c> constructor (the source-generator convention — Path)
    ///   - <c>[PlangType(Shape = "...")]</c> declaration
    ///   - <c>[PlangType]</c> with no <c>[LlmBuilder]</c> properties (a wrapped primitive — TString)
    /// Used by NormalizeParameterTypes (skip TryConvertTo so the .pr keeps the primitive)
    /// and validateResponse (reject record-shape values for these params).
    /// </summary>
    public static bool IsScalarPlangType(Type type)
    {
        var hasPlangType = type
            .GetCustomAttributes(typeof(App.Attributes.PlangTypeAttribute), inherit: false)
            .Length > 0;
        if (!hasPlangType) return false;

        if (type.GetMethod("Resolve",
                BindingFlags.Public | BindingFlags.Static) != null)
            return true;

        if (type.GetCustomAttributes(typeof(App.Attributes.PlangTypeAttribute), inherit: false)
            .Cast<App.Attributes.PlangTypeAttribute>()
            .Any(a => a.Shape != null))
            return true;

        // Final fallback: a [PlangType] type with no [LlmBuilder] properties is, by
        // convention, a wrapped primitive (TString). The catalog renders it as a string.
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (System.Attribute.IsDefined(prop, typeof(LlmBuilderAttribute)))
                return false;
        }
        return true;
    }

    public static bool IsPrimitive(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid);
    }

    // --- Conversion — moved to App.Utils.TypeConverter ---
    // Thin forwards below let existing callers keep working; prefer TypeConverter directly.

    /// <summary>Forwards to <see cref="TypeConverter.ConvertTo{T}"/>.</summary>
    public static T? ConvertTo<T>(object? value) => TypeConverter.ConvertTo<T>(value);

    /// <summary>Forwards to <see cref="TypeConverter.ConvertTo"/>.</summary>
    public static object? ConvertTo(object? value, Type targetType) => TypeConverter.ConvertTo(value, targetType);

    /// <summary>Forwards to <see cref="TypeConverter.Populate"/>.</summary>
    public static void Populate(object target, IDictionary<string, object?> values) => TypeConverter.Populate(target, values);

    /// <summary>Forwards to <see cref="TypeConverter.TryConvertTo"/>.</summary>
    public static (object? Value, Errors.Error? Error) TryConvertTo(object? value, Type targetType, Actor.Context.@this? context = null)
        => TypeConverter.TryConvertTo(value, targetType, context);



    /// <summary>
    /// Returns the primitive type names exposed to the builder (excludes aliases like
    /// "text"→"string" and all nullable variants). Domain types are surfaced through
    /// the schemas block via [PlangType] declarations, not listed here.
    /// </summary>
    public static List<string> GetBuilderTypeNames()
    {
        var seen = new HashSet<Type>();
        var names = new List<string>();
        foreach (var kvp in Primitives)
        {
            if (kvp.Key.EndsWith("?")) continue;
            if (seen.Contains(kvp.Value)) continue;
            seen.Add(kvp.Value);
            names.Add(kvp.Key);
        }
        return names;
    }

    /// <summary>
    /// Walks action parameter types and returns structured catalog entries.
    /// Discovery is transitive: every type referenced in a schema is itself surfaced.
    ///   - Enum (or ValidValues) → TypeEntry with Values populated.
    ///   - Record                → TypeEntry with Fields built from [LlmBuilder] props.
    ///   - Opaque (no markers)   → not surfaced.
    /// </summary>
    public static List<App.Modules.Schema.Entry> BuildTypeEntries(App.Modules.@this? modules)
    {
        var entries = new List<App.Modules.Schema.Entry>();
        var seen = new HashSet<System.Type>();
        var queue = new Queue<System.Type>();

        void Enqueue(System.Type? t)
        {
            if (t == null || seen.Contains(t)) return;
            if (IsPrimitive(t) || t == typeof(object)) return;
            if (t.IsArray || t.IsGenericType) return;
            queue.Enqueue(t);
        }

        // Seed from action parameter types. Domain types declare themselves via
        // [PlangType] — reached transitively from the seed set below.
        if (modules != null)
        {
            foreach (var ns in modules.Names)
            {
                foreach (var actionName in modules.GetActions(ns))
                {
                    var actionType = modules.GetActionType(ns, actionName);
                    if (actionType == null) continue;

                    foreach (var prop in actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;
                        Enqueue(UnwrapType(prop.PropertyType));
                    }
                }
            }
        }
        else
        {
            // No modules — seed from every type declared via [PlangType] / @this so the
            // result still represents the full catalog vocabulary (used by standalone
            // schema dumps / docs).
            foreach (var t in PlangTypeIndex.KnownTypes())
                Enqueue(t);
        }

        // Process transitively — every property type in a record we emit also enters
        // the queue, so schemas form a closed world (goal.visibility → visibility enum).
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type)) continue;

            var typeName = GetTypeName(type);

            // Pull self-declared LLM teaching from [PlangType] (Shape/Example/Description).
            // The first attribute carrying any of these wins — multiple [PlangType] aliases
            // are allowed but only one carries the teaching.
            var plangAttr = type.GetCustomAttributes<App.Attributes.PlangTypeAttribute>()
                .FirstOrDefault(a => a.Shape != null || a.Description != null || a.Example != null);

            // Enum / constrained-value type
            var values = GetValidValues(type);
            if (values != null)
            {
                entries.Add(new App.Modules.Schema.Entry
                {
                    Name = typeName,
                    Kind = App.Modules.Schema.EntryKind.Enum,
                    Values = values,
                    Description = plangAttr?.Description,
                    Example = plangAttr?.Example,
                    ClrType = type,
                });
                continue;
            }

            // Resolve-method convention: a static `Resolve(input, Context)` method declares
            // the construction signature for a domain type. Source generator already uses
            // it to auto-wrap string parameters; the catalog uses the same method to teach
            // the LLM what input to emit (e.g. `path: constructor(rawPath: string)`). When
            // present, the type is a Scalar and the LlmBuilder-tagged read-only props
            // become its navigation properties (what `%var.Property%` can reach).
            var resolveMethod = type.GetMethod("Resolve",
                BindingFlags.Public | BindingFlags.Static);
            string? constructorSignature = null;
            string? derivedShape = null;
            if (resolveMethod != null)
            {
                var resolveParams = resolveMethod.GetParameters();
                if (resolveParams.Length >= 1)
                {
                    var first = resolveParams[0];
                    derivedShape = GetTypeName(first.ParameterType);
                    constructorSignature = $"{first.Name}: {derivedShape}";
                }
            }

            // Read-only navigation properties — surfaced for both Scalar and Record types
            // that opt-in via [LlmBuilder]. Used by the LLM to write %var.Property% paths.
            var llmProps = new List<App.Modules.Schema.Field>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.Name == "EqualityContract") continue;
                if (!Attribute.IsDefined(prop, typeof(LlmBuilderAttribute))) continue;
                if (Attribute.IsDefined(prop, typeof(JsonIgnoreAttribute))) continue;

                llmProps.Add(new App.Modules.Schema.Field
                {
                    Name = char.ToLower(prop.Name[0]) + prop.Name[1..],
                    TypeName = GetTypeName(prop.PropertyType),
                });
                Enqueue(UnwrapType(prop.PropertyType));
            }

            // Default rule: any [PlangType]-declared class is a Scalar/string at the wire
            // level unless it explicitly opts into being a Record by tagging properties
            // with [LlmBuilder]. This kills the "must add Shape to every domain type"
            // whack-a-mole — opaque-looking types like TString stop hallucinating into
            // {value, key} records because the catalog now teaches them as plain strings.
            // Resolve-derived constructorSignature still wins when present (gives a
            // typed input name), and explicit [PlangType(Shape=...)] overrides the default.
            var hasPlangType = type.GetCustomAttributes<App.Attributes.PlangTypeAttribute>().Any();
            bool isScalar = constructorSignature != null
                || plangAttr?.Shape != null
                || (hasPlangType && llmProps.Count == 0);

            if (isScalar)
            {
                entries.Add(new App.Modules.Schema.Entry
                {
                    Name = typeName,
                    Kind = App.Modules.Schema.EntryKind.Scalar,
                    Shape = derivedShape ?? plangAttr?.Shape ?? "string",
                    ConstructorSignature = constructorSignature,
                    Properties = llmProps.Count > 0 ? llmProps : null,
                    Description = plangAttr?.Description,
                    Example = plangAttr?.Example,
                    ClrType = type,
                });
                continue;
            }

            // Record — [LlmBuilder] props on a non-[PlangType] type, or [PlangType] type
            // that didn't qualify as Scalar (shouldn't happen given the rule above, but
            // we keep the fallback for safety).
            if (llmProps.Count > 0)
            {
                entries.Add(new App.Modules.Schema.Entry
                {
                    Name = typeName,
                    Kind = App.Modules.Schema.EntryKind.Record,
                    Fields = llmProps,
                    Description = plangAttr?.Description,
                    Example = plangAttr?.Example,
                    ClrType = type,
                });
            }
            // Types with no [LlmBuilder] props, no Resolve, and no [PlangType] stay opaque —
            // their name is still valid (resolver knows it via @this convention) but they
            // carry no schema in the catalog.
        }

        return entries;
    }

    /// <summary>
    /// Unwraps generic wrappers (List&lt;T&gt;, Nullable&lt;T&gt;) to get the inner type.
    /// </summary>
    private static System.Type? UnwrapType(System.Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return UnwrapType(underlying);

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(Data.@this<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
                return null; // dict values are too generic
        }

        // Custom classes that IMPLEMENT IList<T> (e.g. Actions : IList<Action>) unwrap
        // to their element type — same as List<T>. Without this, the catalog hides
        // the element type behind an opaque collection name.
        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (listIface != null)
            return UnwrapType(listIface.GetGenericArguments()[0]);

        if (type.IsArray)
            return UnwrapType(type.GetElementType()!);

        if (IsPrimitive(type)) return null;
        return type;
    }

}
