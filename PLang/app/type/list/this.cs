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
///
/// This IS the collection of all system types (<c>app.Type</c>) — the current and future home,
/// distinct from <c>app.type.item.list</c> (the plang list VALUE). Its internals are still the
/// legacy registry blob; Stage-3-core cleans them in place (untangle the Registry index, reparent
/// the sub-registries to <c>app.type.*</c>) — the class is not going anywhere.
/// </summary>
public sealed partial class @this
{
    /// <summary>
    /// The context this catalog births values from. The type catalog is a
    /// system-owned collection, born with the App's system context.
    /// </summary>
    internal actor.context.@this Context { get; }

    public @this(actor.context.@this context) : this()
    {
        Context = context;
        Kind = new kind.list.@this(context);   // per-App, born with context → its kinds are stamped
    }

    /// <summary>The choice registry — the closed-set vocabulary. Owns discovering closed sets
    /// and registering each set's name + reader. Reachable as <c>app.type.choice</c>.</summary>
    public global::app.type.item.choice.list.@this Choice { get; }

    /// <summary>
    /// Per-App scheme registry for <see cref="path.@this"/>. Populated at App
    /// construction with built-in factories (<c>"file"</c>; later <c>"http"</c>
    /// and <c>"https"</c>). External DLLs loaded via <c>code.load</c> add their
    /// own schemes via <see cref="global::app.type.item.path.scheme.@this.Register"/>.
    /// </summary>
    public global::app.type.item.path.scheme.@this Scheme { get; } = new();

    /// <summary>
    /// The singleton store of kind behaviors (navigate / enumerate / load / convert), one
    /// <see cref="kind.behavior.@this"/> per format. INTERNAL plumbing — reached only
    /// through the kind token (<c>value.Kind.Navigate(…)</c>), never a flat
    /// <c>App.Type.&lt;plural&gt;</c>. Distinct from <c>type.Kinds</c> (advertised vocabulary).
    /// </summary>
    internal kind.list.@this Kind { get; private set; } = new(null);

    /// <summary>
    /// Per-(type, format) renderer table. Vestigial now that a value renders
    /// itself via <c>item.Write</c> — only its membership check feeds Normalize's
    /// "renders-itself, don't reflect" signal. Discovers
    /// <c>app/types/&lt;name&gt;/serializer/&lt;format&gt;.cs</c> classes via
    /// reflection over <see cref="renderer.@this.Assemblies"/> and exposes a
    /// runtime-registration seam for DLLs loaded at runtime.
    /// </summary>
    public renderer.@this Renderer { get; } = new();

    /// <summary>
    /// Per-(type, kind) reader dispatch — the read-side mirror of
    /// <see cref="Renderers"/>. Discovers <c>app/type/&lt;name&gt;/serializer/&lt;kind&gt;.cs</c>
    /// classes exposing a static <c>Read(object, string?, ReadContext)</c> and
    /// exposes the same runtime-registration seam. The single json
    /// <c>Converter</c> routes mid-graph typed fields through here.
    /// </summary>
    public reader.@this Reader { get; } = new();

    /// <summary>The primitives' spelled names — this registry's own data. <c>app.Type[name]</c> is
    /// the one door that turns a spelled name into its canonical type.</summary>
    internal app.type.primitive.@this Primitive { get; } = new();

    private const int MaxGenericDepth = 20;


    // --- PLang name → CLR type ---

    /// <summary>
    /// PLang type name → CLR type. Handles generics (list&lt;string&gt;), dictionaries,
    /// nullable (int?), and MIME types. Depth-guarded against unbounded generic nesting.
    /// </summary>
    public System.Type? Get(string typeName) => Get(typeName, 0);

    /// <summary>Alias for <see cref="Get(string)"/> — preserves existing <c>app.type.Clr</c> caller habit.</summary>
    public System.Type? Clr(string plangName) => Get(plangName);

    /// <summary>True when <paramref name="typeName"/> names a plang type — the presence question
    /// beside the indexer, which selects and throws on a miss.</summary>
    public bool Contains(string typeName)
        // A spelled {name/kind} ("text/md") is known when its name is — the same split the name door makes.
        => Get(typeName.IndexOf('/') is > 0 and var slash && !typeName.Contains('<') ? typeName[..slash] : typeName) != null;

    // --- Stage 3 accessor surface ---

    // Catalog cache keyed by PLang type name.  The no-module catalog walk is
    // App-global (only the KnownTypes() seed varies, and that seed is identity
    // to this registry instance), so a single Lazy is enough: BuildTypeEntries
    // runs once per registry instance and every fold-property read of
    // app.Type[name] then comes from the cache.
    //
    // The (modules)-overload of BuildTypeEntries stays uncached — its input is
    // the App's module set which can change at runtime via code.load.
    private readonly Lazy<Dictionary<string, app.type.@this>> _catalogByName;

    private Dictionary<string, app.type.@this> CatalogByName => _catalogByName.Value;

    public @this()
    {
        Choice = new global::app.type.item.choice.list.@this(this);
        _catalogByName = new Lazy<Dictionary<string, app.type.@this>>(() =>
        {
            var dict = new Dictionary<string, app.type.@this>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in BuildTypeEntries(null))
            {
                // Collision resolution: when two CLR types map to the same PLang
                // name (e.g. `app.goal.@this` the goal entity and
                // `app.channel.type.goal.@this` the goal-channel both lowercase to
                // "goal" via the @this convention), prefer the catalog-richer
                // entry.  First-wins TryAdd over reflection-ordered types is
                // non-deterministic — a Scalar entry could shadow a Record with
                // populated Fields depending on assembly load order.
                // Richness rank: Record (has Fields) > Enum (has Values) > Scalar.
                // (codeanalyzer v2 finding #1.)
                if (!dict.TryGetValue(entry.Name, out var existing))
                {
                    dict[entry.Name] = entry;
                    continue;
                }
                if (entry.Richness > existing.Richness)
                    dict[entry.Name] = entry;
            }
            return dict;
        });
    }

    /// <summary>
    /// Index by PLang type name.  Returns the catalog-built entity — fully
    /// populated with Fields / Values / Shape / Example / Description / Kinds
    /// / ClrType.  Throws on miss; index-miss is a hard error.
    /// </summary>
    /// <remarks>
    /// Both doors (<c>app.Type[name]</c> and <c>data.Type</c>) hand back
    /// equivalent entities — same catalog fold data, same ClrType.  Per
    /// codeanalyzer v1 finding #1: returning <c>new app.type.@this(name)</c>
    /// here would ship a contextless half-entity whose fold properties
    /// silently null; cached catalog lookup closes that gap.
    /// </remarks>
    public app.type.@this this[string typeName]
    {
        get
        {
            if (CatalogByName.TryGetValue(typeName, out var built)) return built;
            // A spelled form — "text/markdown" is {text, markdown}: the name before the first
            // slash, the rest the kind, which the identity door canonicalises.
            var slash = typeName.IndexOf('/');
            if (slash > 0 && !typeName.Contains('<'))
                return this[new app.type.@this(typeName[..slash], typeName[(slash + 1)..])];
            // The container spelling a type prints — "list<path>" is {list, path}: the element is the kind.
            var open = typeName.IndexOf('<');
            if (open > 0 && typeName.EndsWith('>'))
                return this[new app.type.@this(typeName[..open], typeName[(open + 1)..^1])];
            if (Get(typeName) is not { } clr)
                throw new KeyNotFoundException($"No PLang type registered under name '{typeName}'.");
            // THE canonicalising door: an alias lands on the name of the item that owns it —
            // "string" → the "text" entry — and a precision name is a kind of number: "int" → {number, int}.
            if (_typeToName.TryGetValue(clr, out var canonicalName))
            {
                if (Precision(typeName, canonicalName) is { } precision)
                    return this[new app.type.@this(canonicalName, precision)];
                if (!string.Equals(canonicalName, typeName, StringComparison.OrdinalIgnoreCase))
                    return this[canonicalName];
                if (CatalogByName.TryGetValue(canonicalName, out var canonical)) return canonical;
            }
            // Not in the catalog (a generic shape, a primitive item the catalog doesn't list) but the
            // name resolves — a type born knowing its class, no facts to carry.
            return new app.type.@this(typeName.ToLowerInvariant(), clr);
        }
    }

    // A number spelled by its precision ("int", "integer", "long?") — the precision is number's kind.
    private static string? Precision(string spelled, string canonicalName)
    {
        if (canonicalName != "number") return null;
        var lower = spelled.ToLowerInvariant().TrimEnd('?');
        if (lower == "integer") return "int";
        return lower != "number" && app.type.item.number.@this.Kinds.ContainsKey(lower) ? lower : null;
    }

    // The full types built per identity {name, kind, strict, template} — each built once.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Name, string? Kind, bool Strict, string? Template), app.type.@this> _full = new();

    /// <summary>
    /// The full type for a value's type — its identity (name, kind, strict, template) with the
    /// entry's facts. A choice's kind names its set, so a <c>{choice, operator}</c> carries that
    /// set's options. The kind is canonicalised (<c>markdown</c> → <c>md</c>). A name the registry
    /// doesn't know answers as its bare identity. Holds no context; cached per identity.
    /// </summary>
    public app.type.@this this[app.type.@this type]
    {
        get
        {
            var kind = type.Kind?.Name is { } k ? Context?.App.Format.CanonicaliseKind(k) ?? k : null;
            return _full.GetOrAdd((type.Name.ToLowerInvariant(), kind?.ToLowerInvariant(), type.Strict, type.Template),
                id => Full(type.Name, kind, id.Strict, id.Template));
        }
    }

    private app.type.@this Full(string name, string? kind, bool strict, string? template)
    {
        if (!Contains(name)) return new app.type.@this(name, kind, strict, template);
        var entry = this[name];
        if (kind == null && !strict && template == null) return entry;
        // A number's precision kind carries its own C# mate ({number, int} → Int32), stamped at birth.
        var clr = entry.Name == "number" && kind != null
            ? (Primitive.Aliases.TryGetValue(kind, out var mate) ? mate : null)
            : entry.ClrType;
        return new app.type.@this(entry.Name, clr, kind, strict, template)
        {
            Fields = entry.Fields,
            Values = entry.Name == "choice" && kind != null && Choice.Contains(kind) ? Choice[kind].Values : entry.Values,
            Properties = entry.Properties,
            Shape = entry.Shape,
            ConstructorSignature = entry.ConstructorSignature,
            Example = entry.Example,
            Description = entry.Description,
            Kinds = entry.Kinds,
        };
    }

    /// <summary>
    /// Index by CLR type — the type entity for a live CLR type's plang identity, or null when
    /// the CLR type names no plang type (a raw POCO). The navigable mirror of
    /// <see cref="this[string]"/>; replaces the old <c>ResolveName</c> verb-lookup. Null on miss
    /// (a CLR type MAY not be plang vocabulary), unlike the name door's throw-on-miss.
    /// </summary>
    public app.type.@this this[System.Type clrType]
    {
        get
        {
            // The name comes from PlangName — the same step the catalog names through, so the
            // entity's face and the catalog can never disagree. A kinded family is born here with
            // its kind; a choice carries its set's options; every other name is the named entity.
            var (name, kind) = PlangName(clrType);
            return kind == null ? this[name] : this[new app.type.@this(name, kind)];
        }
    }

    // The container families whose element rides as the KIND (list<path> = {list, kind:path}).
    // The one generic recognition (PlangName names through it) — the element is the kind; dict's kind is the VALUE
    // (key defaults text; surface a keyed axis only if a real param needs one). Returns null for
    // non-containers (they resolve on the item/clr rungs).
    private (string Family, System.Type Element)? ContainerFamily(System.Type type)
    {
        if (type.IsArray)
        {
            var arr = type.GetElementType()!;
            return arr == typeof(byte) ? null : ("list", arr);   // byte[] is "bytes", not a list
        }
        if (type.IsGenericType)
        {
            var g = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (g == typeof(app.type.item.list.@this<>)) return ("list", args[0]);
            if (g == typeof(List<>) || g == typeof(IList<>) || g == typeof(IEnumerable<>) || g == typeof(ICollection<>)
                || g == typeof(IReadOnlyCollection<>) || g == typeof(IReadOnlyList<>) || g == typeof(HashSet<>)
                || (g.FullName?.StartsWith("System.Collections.Immutable.ImmutableList`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.Generic.ISet`", StringComparison.Ordinal) ?? false))
                return ("list", args[0]);
            if (g == typeof(Dictionary<,>) || g == typeof(IDictionary<,>)
                || (g.FullName?.StartsWith("System.Collections.Concurrent.ConcurrentDictionary`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.ObjectModel.ReadOnlyDictionary`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.Generic.SortedDictionary`", StringComparison.Ordinal) ?? false)
                || (g.FullName?.StartsWith("System.Collections.Immutable.ImmutableDictionary`", StringComparison.Ordinal) ?? false))
                return ("dict", args[^1]);
        }
        // A plang list<T> NODE (action.list : list<action>, step.list : list<step>) is a non-generic
        // subclass of list.@this<T> — walk to that base and take its element as the kind, so the
        // reader dispatches the element's own reader.
        for (var t = type.BaseType; t != null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(app.type.item.list.@this<>))
                return ("list", t.GetGenericArguments()[0]);
        // Any other concrete CLR collection (a third-party IList<T>/IReadOnlyList<T>) IS a list<element>.
        var listIface = type.GetInterfaces().FirstOrDefault(i =>
            i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IList<>)
                             || i.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)));
        return listIface != null ? ("list", listIface.GetGenericArguments()[0]) : null;
    }

    // The born-native lift moved to its rightful owner — the produced type. "Build whatever this raw
    // is" is item.@this.Create(raw, ctx) (item's own ICreate face); the registry keeps only SELECTION
    // (the identity indexer this[System.Type], both string doors), never construction.

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

        // The registry is the single source of truth — an item, or an alias resolving to one.
        return ResolveType(typeName);
    }

    // --- CLR type → PLang name ---

    // The plang name of a CLR type — {name, kind} — read off the registry's own indexes. The one
    // naming step: the entity door (this[System.Type]) builds its entity from it, and the catalog fold
    // names through it directly (the door reads the catalog, so the fold cannot ask the door).
    // Nullability is the slot's fact, never part of a name; a Data<T> slot names T; plain Data is the
    // open item slot; a family names its content as the kind; a raw CLR type no plang type owns is clr.
    private (string Name, string? Kind) PlangName(System.Type type)
    {
        EnsureInitialized();
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(data.@this<>))
            type = type.GetGenericArguments()[0];
        if (type == typeof(data.@this)) return ("item", null);
        // A plang list<T> NODE (action.list : list<action>) is {list, kind: element} — named before
        // the item index, which would answer a plain "list" with no element.
        if (typeof(app.type.item.list.@this).IsAssignableFrom(type) && ContainerFamily(type) is { } node)
            return (node.Family, Face(PlangName(node.Element)));
        if (_clr.TryGetValue(type, out var owner)) return (owner, null);   // conversion owner: int → number
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(app.type.item.choice.@this<>))
            return ("choice", Choice[type].Name);
        // Only an item answers by its indexed name — _typeToName also holds the concept names of
        // non-item @this classes (callstack, the serializers registry), and those are not plang types.
        if (typeof(app.type.item.@this).IsAssignableFrom(type) && _typeToName.TryGetValue(type, out var declared))
            return (declared, null);
        if (Primitive.Canonical.TryGetValue(type, out var primitive)) return (primitive, null);
        if (ContainerFamily(type) is { } fam) return (fam.Family, Face(PlangName(fam.Element)));
        return ("clr", null);
    }

    // A {name, kind} as the entity's face prints it — the kind rides in the name for a family.
    private string Face((string Name, string? Kind) named)
        => named.Kind == null ? named.Name : $"{named.Name}<{named.Kind}>";

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


    // --- Type-kind queries ---

    // A CLR scalar the catalog fold never walks into (it has no fields to list).
    private bool IsPrimitive(System.Type type)
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
    /// Walks action parameter types and returns structured catalog entries.
    /// Discovery is transitive: every type referenced in a schema is itself surfaced.
    ///   - Enum (or ValidValues) → TypeEntry with Values populated.
    ///   - Record                → TypeEntry with Fields built from [LlmBuilder] props.
    ///   - Opaque (no markers)   → not surfaced.
    /// </summary>
    [System.Obsolete("Type/module discovery moves to list<type>/list<module> + a Fluid render — do not add new callers.")]
    public List<app.type.@this> BuildTypeEntries(app.module.list.@this? modules)
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
                        var unwrapped = UnwrapType(prop.PropertyType);
                        Enqueue(unwrapped);
                        // A native typed list<T> carries its element type intrinsically;
                        // walk it so the builder gets T's schema (e.g. list<LlmMessage>).
                        if (unwrapped is { IsGenericType: true } u
                            && u.GetGenericTypeDefinition() == typeof(app.type.item.list.@this<>))
                            Enqueue(u.GetGenericArguments()[0]);
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

            var typeName = Face(PlangName(type));
            // A CLR type no plang type owns is not a catalog entry.
            if (typeName == "clr") continue;

            // Skip the type entity itself — its wire shape ({name, kind?, strict?})
            // and kind vocabulary are taught explicitly in the compile prompt's
            // "Type reference" block. Rendering it as a catalog scalar
            // ("type: string") confuses the LLM.
            if (type == typeof(app.type.@this)) continue;
            // Skip `data.@this` — actions with polymorphic Value slots (variable.set
            // etc.) declare it as `object`; surfacing it again as a scalar
            // ("object: string") in the catalog is redundant and confusing.
            if (type == typeof(data.@this)) continue;

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

            var values = Choice.Contains(type) ? Choice[type].Values : null;
            if (values != null)
            {
                entries.Add(new app.type.@this(typeName, ResolveType(typeName) is { IsAbstract: true } baseClr && baseClr.IsAssignableFrom(type) ? baseClr : type)
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
                    derivedShape = Face(PlangName(first.ParameterType));
                    constructorSignature = $"{first.Name}: {derivedShape}";
                }
            }

            var llmProps = new List<app.type.Field>();
            // A member that needs the asker's context is a one-context method; it is the same
            // field to the catalog, listed where it is declared among the properties.
            var llmMethods = new Queue<MethodInfo>(type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => Attribute.IsDefined(m, typeof(LlmBuilderAttribute)) && m.ReturnType != typeof(void)
                    && m.GetParameters() is [{ ParameterType: var p }] && p == typeof(actor.context.@this))
                .OrderBy(m => m.MetadataToken));
            void AddField(string name, System.Type fieldType)
            {
                llmProps.Add(new app.type.Field
                {
                    Name = char.ToLower(name[0]) + name[1..],
                    TypeName = Face(PlangName(fieldType)),
                });
                Enqueue(UnwrapType(fieldType));
            }
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || prop.Name == "EqualityContract") continue;
                if (!Attribute.IsDefined(prop, typeof(LlmBuilderAttribute))) continue;
                while (llmMethods.TryPeek(out var m) && m.DeclaringType == prop.DeclaringType
                    && m.MetadataToken < prop.GetMethod!.MetadataToken)
                    AddField(llmMethods.Dequeue().Name, m.ReturnType);
                // [LlmBuilder] is the explicit opt-in for catalog visibility;
                // [JsonIgnore] only governs STJ wire shape. A property can be
                // both (e.g. type.Kind: not on the entity's own wire — the
                // wire emits `kind` from data.Type.Kind via Wire.cs — but
                // discoverable as a builder field).
                AddField(prop.Name, prop.PropertyType);
            }
            while (llmMethods.TryDequeue(out var m)) AddField(m.Name, m.ReturnType);

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
                entries.Add(new app.type.@this(typeName, ResolveType(typeName) is { IsAbstract: true } baseClr && baseClr.IsAssignableFrom(type) ? baseClr : type)
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
                entries.Add(new app.type.@this(typeName, ResolveType(typeName) is { IsAbstract: true } baseClr && baseClr.IsAssignableFrom(type) ? baseClr : type)
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
    /// Reads a public-static string property by name from <paramref name="type"/>.
    /// Used to source catalog metadata (Example, Description, Shape) from a
    /// convention rather than from a per-parameter attribute — see
    /// <see cref="BuildTypeEntries"/>. Returns null when the property is absent,
    /// non-string, or throws.
    /// </summary>
    private string? ReadStaticString(System.Type type, string propertyName)
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
    private IReadOnlyList<string>? ReadStaticStringList(System.Type type, string propertyName)
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
    private System.Type? UnwrapType(System.Type type)
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
