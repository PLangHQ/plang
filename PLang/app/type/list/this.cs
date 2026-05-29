using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using app.Attributes;
using app.module;

namespace app.type.list;

/// <summary>
/// Owns PLang name ↔ CLR type identity, the [Choices] vocabulary registry, and the
/// type-conversion entry points. The primary partial holds the public surface; the
/// <c>Registry</c> partial (formerly <c>Utils.PlangTypeIndex</c>) absorbs assembly
/// indexing for [PlangType] and the @this convention.
///
/// File-format characteristics (extension → Kind, extension → MIME, Kind →
/// compressibility) live separately on <see cref="app.format.list.@this"/> at
/// <c>app.Format</c>.
/// </summary>
public sealed partial class @this
{
    /// <summary>[Choices] vocabulary registry — reachable as <c>app.type.choices</c>.</summary>
    public choice.list.@this Choices { get; } = new();

    /// <summary>
    /// Per-App scheme registry for <see cref="path.@this"/>. Populated at App
    /// construction with built-in factories (<c>"file"</c>; later <c>"http"</c>
    /// and <c>"https"</c>). External DLLs loaded via <c>code.load</c> add their
    /// own schemes via <see cref="path.scheme.@this.Register"/>.
    /// </summary>
    public path.scheme.@this Scheme { get; } = new();

    /// <summary>
    /// Per-App build-time kind dispatcher — discovers and invokes each
    /// registered type's <c>static string? Build(object?)</c> hook (the
    /// build-time sibling of <c>Resolve</c>). Owns the reflection cache.
    /// Call: <c>App.Types.Kinds.Of(declaredType, value)</c> during build to
    /// stamp <c>Data.Kind</c> alongside <c>Data.Type</c>.
    /// </summary>
    public kind.@this Kinds { get; } = new();

    /// <summary>
    /// Per-(type, format) renderer dispatch — feeds the writer's
    /// <see cref="data.TypedValueNode"/> case. Discovers
    /// <c>app/types/&lt;name&gt;/serializer/&lt;format&gt;.cs</c> classes via
    /// reflection over <see cref="renderer.@this.Assemblies"/> and exposes a
    /// runtime-registration seam for DLLs loaded at runtime.
    /// </summary>
    public renderer.@this Renderers { get; } = new();

    // --- Primitive lookup tables ---
    // Aliases / canonical-name data lives on app.type.primitive.@this — one
    // home for the seeded entries that both the registry (instance lookup) and
    // the static no-context fallback (GetPrimitiveOrMime / GetPrimitiveName)
    // read from.

    private const int MaxGenericDepth = 20;

    /// <summary>
    /// Static-friendly subset of <see cref="Get"/> that handles primitives and MIME without
    /// touching the per-App registry. Used as a fallback when no <c>Context</c> is available
    /// (e.g. <c>Data.Type.ClrType</c> on a Data minted outside an action's context).
    /// </summary>
    public static System.Type? GetPrimitiveOrMime(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        if (app.type.primitive.@this.Aliases.TryGetValue(typeName, out var primitive)) return primitive;
        return ClrFromMime(typeName);
    }

    /// <summary>
    /// Static-friendly subset of <see cref="GetTypeName"/> covering the primitive table only —
    /// returns null for domain/[Choices]/array/generic types. Used as a no-context fallback by
    /// <see cref="data.@this"/> when inferring a Type from a value without an active Context.
    /// </summary>
    public static string? GetPrimitiveName(System.Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return app.type.primitive.@this.Canonical.TryGetValue(underlying, out var name) ? name : null;
    }

    /// <summary>
    /// Static-friendly variant of <see cref="GetTypeName"/> — handles primitives, generics,
    /// nullable, arrays, Data&lt;T&gt; unwrap, and reads [PlangType] / @this convention names
    /// directly off the type via reflection (no per-App registry). For callers that don't
    /// have an App in scope (e.g. <see cref="app.module.@this"/> instances constructed
    /// without an App backing in test fixtures).
    /// </summary>
    public static string GetTypeNameStatic(System.Type type)
    {
        if (type == null) return "object";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return GetTypeNameStatic(underlying) + "?";

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(data.@this<>))
                return GetTypeNameStatic(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>)
                || generic == typeof(IEnumerable<>) || generic == typeof(ICollection<>)
                || generic == typeof(IReadOnlyCollection<>) || generic == typeof(IReadOnlyList<>)
                || generic == typeof(HashSet<>))
                return $"list<{GetTypeNameStatic(type.GetGenericArguments()[0])}>";
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
            {
                var args = type.GetGenericArguments();
                return $"dict<{GetTypeNameStatic(args[0])},{GetTypeNameStatic(args[1])}>";
            }
        }

        if (type == typeof(data.@this)) return "object";

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            if (elementType == typeof(byte)) return "bytes";
            return $"list<{GetTypeNameStatic(elementType)}>";
        }

        if (app.type.primitive.@this.Canonical.TryGetValue(type, out var name)) return name;

        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (listIface != null)
            return $"list<{GetTypeNameStatic(listIface.GetGenericArguments()[0])}>";

        // [PlangType("name")] direct attribute read — first non-null Name wins.
        var plangAttr = type.GetCustomAttributes<PlangTypeAttribute>(inherit: false)
            .FirstOrDefault(a => a.Name != null);
        if (plangAttr?.Name != null) return plangAttr.Name;

        // @this convention: last-namespace-segment lowercased.
        if (string.Equals(type.Name, "this", StringComparison.Ordinal) && !string.IsNullOrEmpty(type.Namespace))
        {
            var ns = type.Namespace!;
            var lastDot = ns.LastIndexOf('.');
            return (lastDot >= 0 ? ns[(lastDot + 1)..] : ns).ToLowerInvariant();
        }

        return StripGenericArity(type.Name).ToLowerInvariant();
    }

    // --- PLang name → CLR type ---

    /// <summary>
    /// PLang type name → CLR type. Handles generics (list&lt;string&gt;), dictionaries,
    /// nullable (int?), and MIME types. Depth-guarded against unbounded generic nesting.
    /// </summary>
    public System.Type? Get(string typeName) => Get(typeName, 0);

    /// <summary>Alias for <see cref="Get(string)"/> — preserves existing <c>app.type.Clr</c> caller habit.</summary>
    public System.Type? Clr(string plangName) => Get(plangName);

    // --- Stage 3 accessor surface ---

    /// <summary>
    /// Index by PLang type name. Returns the type entity (<see cref="app.type.@this"/>);
    /// throws on miss — index-miss is a hard error.
    /// </summary>
    public app.type.@this this[string typeName]
    {
        get
        {
            if (Get(typeName) == null)
                throw new KeyNotFoundException($"No PLang type registered under name '{typeName}'.");
            return new app.type.@this(typeName);
        }
    }

    /// <summary>Compile-time generic lookup — returns the entity for T.</summary>
    public app.type.@this of<T>() => new app.type.@this(GetTypeName(typeof(T)));

    private System.Type? Get(string typeName, int depth)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        if (depth > MaxGenericDepth) return null;

        if (typeName.StartsWith("list<", StringComparison.OrdinalIgnoreCase) && typeName.EndsWith(">"))
        {
            var innerTypeName = typeName[5..^1];
            var innerType = Get(innerTypeName, depth + 1);
            return innerType != null ? typeof(List<>).MakeGenericType(innerType) : null;
        }

        if ((typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ||
             typeName.StartsWith("dictionary<", StringComparison.OrdinalIgnoreCase)) && typeName.EndsWith(">"))
        {
            var prefix = typeName.StartsWith("dict<", StringComparison.OrdinalIgnoreCase) ? 5 : 11;
            var inner = typeName[prefix..^1];
            var parts = inner.Split(',');
            if (parts.Length == 2)
            {
                var keyType = Get(parts[0].Trim(), depth + 1);
                var valueType = Get(parts[1].Trim(), depth + 1);
                if (keyType == null || valueType == null) return null;
                return typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }
        }

        // Registry is the single source of truth — primitives are seeded into
        // it at init via SeedClrPrimitives. The static Primitives dict still
        // backs the no-context fallback (GetPrimitiveOrMime / GetPrimitiveName).
        var domainType = ResolveType(typeName);
        if (domainType != null) return domainType;

        var mimeType = ClrFromMime(typeName);
        if (mimeType != null) return mimeType;

        return null;
    }

    /// <summary>
    /// MIME content-type → CLR type for deserialization. Returns <c>null</c>
    /// when the input isn't a MIME string (no slash) or isn't a recognised family.
    /// Pure logic with no instance state — exposed as static so it's reachable from
    /// any caller, and from <see cref="Get"/>'s internal path.
    /// </summary>
    public static System.Type? ClrFromMime(string mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.Contains('/')) return null;

        if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return typeof(string);
        if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return typeof(byte[]);
        if (mimeType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
            return typeof(object);
        if (mimeType.Equals("application/plang-goal", StringComparison.OrdinalIgnoreCase))
            return typeof(app.goal.@this);
        if (mimeType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return typeof(byte[]);

        return null;
    }

    // --- CLR type → PLang name ---

    /// <summary>
    /// CLR type → PLang type name.
    /// </summary>
    public string GetTypeName(System.Type type)
    {
        if (type == null) return "object";

        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return GetTypeName(underlying) + "?";

        if (type.IsGenericType)
        {
            var generic = type.GetGenericTypeDefinition();
            if (generic == typeof(data.@this<>))
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

        if (type == typeof(data.@this))
            return "object";

        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            if (elementType == typeof(byte))
                return "bytes";
            return $"list<{GetTypeName(elementType)}>";
        }

        if (app.type.primitive.@this.Canonical.TryGetValue(type, out var name))
            return name;

        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
        if (listIface != null)
            return $"list<{GetTypeName(listIface.GetGenericArguments()[0])}>";

        var declared = ResolveName(type);
        if (declared != null) return declared;

        if (Choices.Has(type))
            return StripGenericArity(type.Name).ToLowerInvariant();

        return StripGenericArity(type.Name).ToLowerInvariant();
    }

    /// <summary>Alias for <see cref="GetTypeName"/> — preserves existing <c>app.type.Name</c> caller habit.</summary>
    public string Name(System.Type clrType) => GetTypeName(clrType);

    private static string StripGenericArity(string name)
    {
        var idx = name.IndexOf('`');
        return idx >= 0 ? name[..idx] : name;
    }

    // --- Registration ---

    /// <summary>
    /// Registers a domain type for deserialization and type resolution.
    /// Prefer declaring [PlangType(name)] on the class itself — that's the single
    /// source of truth. This API remains for test harnesses that synthesize types.
    /// </summary>
    public void Register(string plangName, System.Type clrType)
    {
        RegisterRuntime(plangName, clrType);
    }

    /// <summary>
    /// Registers domain types needed for settings store rehydration.
    /// Called by App constructor. Today this is a no-op — Identity carries
    /// <c>[PlangType("identity")]</c> on the class itself, which the assembly
    /// scan picks up. The hook stays so future domain types that need
    /// runtime registration (test harness shims, dynamically loaded plugins)
    /// have an obvious entry point.
    /// </summary>
    public void RegisterDomainTypes()
    {
    }

    // --- Constrained-value catalog ---

    /// <summary>
    /// Gets the valid values for a constrained type — enum names for real enums,
    /// or the [Choices] vocabulary for types that declare one. Returns null when
    /// the type is neither an enum nor a [Choices]-bearing type.
    /// </summary>
    public string[]? GetValidValues(System.Type type, actor.context.@this? context = null)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) type = underlying;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(data.@this<>))
            type = type.GetGenericArguments()[0];

        if (type.IsEnum)
            return Enum.GetNames(type);

        return Choices.Get(type, context);
    }

    /// <summary>Alias for <see cref="GetValidValues"/> — preserves existing <c>app.type.ValidValues</c> caller habit.</summary>
    public string[]? ValidValues(System.Type type, actor.context.@this? context = null) => GetValidValues(type, context);

    // --- Type-kind queries ---

    /// <summary>
    /// True for [PlangType] domain types whose wire form is a primitive (typically string).
    /// Pure reflection — kept static so it's reachable from <see cref="Utils.TypeConverter"/>'s
    /// static path without needing an App instance.
    /// </summary>
    public static bool IsScalarPlangType(System.Type type)
    {
        // A type is catalog-visible when it's named via [PlangType] override OR is
        // an @this class (last-namespace-segment convention).
        var hasPlangName = type
            .GetCustomAttributes(typeof(PlangTypeAttribute), inherit: false)
            .Length > 0;
        var isThisClass = string.Equals(type.Name, "this", System.StringComparison.Ordinal);
        if (!hasPlangName && !isThisClass) return false;

        // Resolve(input, context) factory → catalog derives the wire shape from the
        // first parameter. Marks the type as scalar without further checks.
        if (type.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static) != null)
            return true;

        // Static Shape property → caller asserts the wire form explicitly.
        if (ReadStaticString(type, "Shape") != null)
            return true;

        // Final fallback: a catalog-named type with no [LlmBuilder] properties is, by
        // convention, a wrapped primitive (TString). The catalog renders it as a string.
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (System.Attribute.IsDefined(prop, typeof(LlmBuilderAttribute)))
                return false;
        }
        return true;
    }

    /// <summary>
    /// True when <paramref name="type"/> is a primitive PLang type. Pure logic — static.
    /// </summary>
    public static bool IsPrimitive(System.Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid);
    }

    // --- Conversion methods now live in Types/Conversion.cs (the partial). ---

    // --- Catalog support ---

    /// <summary>
    /// Returns the primitive type names exposed to the builder (excludes aliases like
    /// "text"→"string" and all nullable variants). Domain types are surfaced through
    /// the schemas block via [PlangType] declarations, not listed here.
    /// </summary>
    public List<string> GetBuilderTypeNames() => app.type.primitive.@this.BuilderNames.ToList();

    /// <summary>Alias for <see cref="GetBuilderTypeNames"/> — preserves existing <c>app.type.BuilderNames</c> caller habit.</summary>
    public List<string> BuilderNames() => GetBuilderTypeNames();

    /// <summary>
    /// Walks action parameter types and returns structured catalog entries.
    /// Discovery is transitive: every type referenced in a schema is itself surfaced.
    ///   - Enum (or ValidValues) → TypeEntry with Values populated.
    ///   - Record                → TypeEntry with Fields built from [LlmBuilder] props.
    ///   - Opaque (no markers)   → not surfaced.
    /// </summary>
    public List<app.type.@this> BuildTypeEntries(app.module.@this? modules)
    {
        var entries = new List<app.type.@this>();
        var seen = new HashSet<System.Type>();
        var queue = new Queue<System.Type>();

        void Enqueue(System.Type? t)
        {
            if (t == null || seen.Contains(t)) return;
            if (IsPrimitive(t) || t == typeof(object)) return;
            if (t.IsArray || t.IsGenericType) return;
            queue.Enqueue(t);
        }

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
            foreach (var t in KnownTypes())
                Enqueue(t);
        }

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type)) continue;

            var typeName = GetTypeName(type);

            // Catalog metadata sourced from static-property convention on the type:
            //   public static string Example => "...";
            //   public static string Description => "...";
            //   public static string Shape => "string";
            // Missing properties → null. Replaces the former [PlangType(Example=, ...)]
            // parameters; the attribute now only carries Name overrides for divergent
            // cases (goal.call, catalog).
            string? staticExample = ReadStaticString(type, "Example");
            string? staticDescription = ReadStaticString(type, "Description");
            string? staticShape = ReadStaticString(type, "Shape");
            IReadOnlyList<string>? staticKinds = ReadStaticStringList(type, "Kinds");

            var values = GetValidValues(type);
            if (values != null)
            {
                entries.Add(new app.type.@this(typeName, type)
                {
                    Values = values,
                    Description = staticDescription,
                    Example = staticExample,
                });
                continue;
            }

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

            var llmProps = new List<app.type.Field>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.Name == "EqualityContract") continue;
                if (!Attribute.IsDefined(prop, typeof(LlmBuilderAttribute))) continue;
                if (Attribute.IsDefined(prop, typeof(JsonIgnoreAttribute))) continue;

                llmProps.Add(new app.type.Field
                {
                    Name = char.ToLower(prop.Name[0]) + prop.Name[1..],
                    TypeName = GetTypeName(prop.PropertyType),
                });
                Enqueue(UnwrapType(prop.PropertyType));
            }

            // Scalar discriminant: either has a Resolve(input, context) factory (so the
            // wire shape is derivable), declares a static Shape property, or is
            // catalog-named but has no LLM-builder properties (a domain wrapper around
            // a primitive). Records have llmProps; scalars don't.
            var hasPlangName = type.GetCustomAttributes<PlangTypeAttribute>().Any();
            var isThisClass = string.Equals(type.Name, "this", System.StringComparison.Ordinal);
            bool isScalar = constructorSignature != null
                || staticShape != null
                || ((hasPlangName || isThisClass) && llmProps.Count == 0);

            if (isScalar)
            {
                entries.Add(new app.type.@this(typeName, type)
                {
                    Shape = derivedShape ?? staticShape ?? "string",
                    ConstructorSignature = constructorSignature,
                    Properties = llmProps.Count > 0 ? llmProps : null,
                    Description = staticDescription,
                    Example = staticExample,
                    Kinds = staticKinds,
                });
                continue;
            }

            if (llmProps.Count > 0)
            {
                entries.Add(new app.type.@this(typeName, type)
                {
                    Fields = llmProps,
                    Description = staticDescription,
                    Example = staticExample,
                    Kinds = staticKinds,
                });
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns the catalog's record/enum entries, keyed by name. When two distinct
    /// CLR types resolve to the same PLang name (e.g. two <c>@this</c> classes
    /// sharing a last-namespace-segment), the first one wins — the registry's
    /// <c>ResolveName</c> follows the same first-wins rule so consumers stay
    /// consistent across the catalog and the type lookup.
    /// </summary>
    public Dictionary<string, app.type.@this> ComplexSchemas()
    {
        var dict = new Dictionary<string, app.type.@this>();
        foreach (var entry in BuildTypeEntries(null))
            dict.TryAdd(entry.Value, entry);
        return dict;
    }

    /// <summary>
    /// Reads a public-static string property by name from <paramref name="type"/>.
    /// Used to source catalog metadata (Example, Description, Shape) from a
    /// convention rather than from a per-parameter attribute — see
    /// <see cref="BuildTypeEntries"/>. Returns null when the property is absent,
    /// non-string, or throws.
    /// </summary>
    private static string? ReadStaticString(System.Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (prop == null || prop.PropertyType != typeof(string)) return null;
        try
        {
            return prop.GetValue(null) as string;
        }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a public-static <c>IReadOnlyList&lt;string&gt;</c> (or <c>IEnumerable&lt;string&gt;</c>)
    /// property — the catalog's opt-in <c>Kinds</c> vocabulary convention. Returns null when
    /// the property is absent, the wrong shape, or throws.
    /// </summary>
    private static IReadOnlyList<string>? ReadStaticStringList(System.Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        if (prop == null) return null;
        try
        {
            var raw = prop.GetValue(null);
            if (raw is IReadOnlyList<string> list) return list;
            if (raw is IEnumerable<string> seq) return seq.ToList();
            return null;
        }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        {
            return null;
        }
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
            if (generic == typeof(data.@this<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(List<>) || generic == typeof(IList<>))
                return UnwrapType(type.GetGenericArguments()[0]);
            if (generic == typeof(Dictionary<,>) || generic == typeof(IDictionary<,>))
                return null;
        }

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
