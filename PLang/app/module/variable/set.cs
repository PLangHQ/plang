using app.Attributes;
using app.variable;

namespace app.module.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
///
/// variable.set is the binding-mint site — it owns type inference (MintTyped picks
/// the concrete Data&lt;T&gt; for the runtime type). Variables.Set decides whether to
/// update the existing binding in place (same type) or replace it (type changed),
/// and carries Properties + event subscribers across replacement.
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext, IBuildValidatable
{
    public static string? ValidateBuild(List<data.@this> parameters)
    {
        var value = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase));
        if (value?.Value is string s && s == "this")
            return "Parameter 'Value' is the literal string \"this\" — this is wrong. For \"write to %var%\" patterns, use \"%!data%\" to capture the previous action's result. \"this\" is a type annotation, not a value.";

        // Strict kind enforcement at build for literals. Pulls the user-named
        // type entity from the Type parameter (post-Stage-4 — a type, not a
        // string). Mismatches return a build error; %var% values defer to Run.
        var typeParam = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
        if (typeParam?.Value is global::app.type.@this t && t.Strict && t.Kind != null
            && value?.Value != null && !value.HasVariableReference)
        {
            var clr = t.ClrType;
            if (clr != null && typeof(global::app.data.IKindValidatable).IsAssignableFrom(clr))
            {
                var probe = TryInstantiateValidator(clr, value.Value);
                if (probe is global::app.data.IKindValidatable v)
                {
                    var (ok, actual) = v.ValidateKind(value.Value, t.Kind);
                    if (!ok)
                        return $"Strict kind mismatch: declared {t.Name}/{t.Kind}"
                            + (actual != null ? $" but content is {actual}." : ".");
                }
            }
        }

        if (value?.Type?.Name != null && value.Value != null)
        {
            // Skip validation when value contains %variable% references — they resolve at runtime
            if (value.HasVariableReference) return null;

            // ClrType is non-public on `type.@this` — `internal` to the entity
            // so the registry/primitive-fallback chain stays one place. Same
            // assembly, so the read is direct; no external GetPrimitiveOrMime
            // fallback to maintain at the call site.
            var targetType = value.Type.ClrType;
            if (targetType != null && !targetType.IsInstanceOfType(value.Value))
            {
                var (_, error) = global::app.type.list.@this.TryConvertTo(value.Value, targetType);
                if (error != null)
                    return $"Parameter 'Value' has type={value.Type.Name} but value cannot be converted: {error.Message}";
            }
        }
        return null;
    }

    public partial data.@this<app.variable.@this> Name { get; init; }
    public partial data.@this Value { get; init; }
    /// <summary>
    /// Optional <c>as</c> clause. Carries the whole <c>type</c> entity (Name,
    /// Kind, Strict) the LLM constructed — replaces the historical bare string.
    /// <c>Run</c> reads <c>Type.Value.Name</c> to resolve the CLR type via the
    /// registry and stamps the entire entity (kind included) onto the minted
    /// variable.
    /// </summary>
    public partial data.@this<global::app.type.@this>? Type { get; init; }
    [Default(false)]
    public partial data.@this<bool> AsDefault { get; init; }

    public Task<data.@this> Run()
    {
        // Variable.Resolve flagged the slot as syntactically malformed
        // (`%x!!cost%`, `%x!a!b%`, etc.) — fail with a typed error rather
        // than silently writing to Properties[""] or replacing the binding
        // with a junk Name.
        if (Name.Value!.IsMalformed)
            return Task.FromResult(global::app.data.@this.FromError(
                new global::app.error.ServiceError(
                    $"Variable reference '{Name.Value.RawValue}' is not a valid name — only a single '!' separates a variable from its Property key, and the suffix may not appear after '.' or '['.",
                    "InvalidVariableReference", 400)));

        // %x!cost% target — mutate the named variable's Properties[key]
        // instead of replacing the binding. Same action, two stores:
        // bare-name slots hit Value, !-suffixed slots hit Properties.
        // Goes through Variable.Resolve's parsing — see Variable.Property.
        var property = Name.Value.Property;
        if (!string.IsNullOrEmpty(property))
        {
            var target = Context.Variable.Get(Name.Value.Name);
            if (target == null || !target.IsInitialized)
                return Task.FromResult(global::app.data.@this.FromError(
                    new global::app.error.ServiceError($"Variable '{Name.Value.Name}' is not set",
                        "VariableNotFound", 400)));
            try
            {
                target.Properties[property] = Value.Value;
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(global::app.data.@this.FromError(
                    new global::app.error.ServiceError(ex.Message, "InvalidPropertyValue", 400)));
            }
            return Task.FromResult(target);
        }

        if (AsDefault.Value)
        {
            var existing = Context.Variable.Get(Name.Value);
            if (existing.IsInitialized)
                return Task.FromResult(existing);
        }

        // Forced type via [Type]: convert via TryConvertTo and mint Data<T>. Conversion failure
        // surfaces as Data.Error (Success=false) — Variables.Set is not called in that case so
        // the binding stays whatever it was. For primitives this is straight coercion ("42" → 42).
        // For json (TypeMapping maps "json" → typeof(JsonNode)), TryConvertTo parses the string
        // into a JsonObject which IS IDictionary — that's what enables `convert %json% from
        // json` (mapped to variable.set Type=json) followed by foreach over the resulting dict.
        if (Type?.Value != null)
        {
            var typeEntity = Type.Value;
            // Canonicalise kind through the format registry — `markdown` → `md`,
            // `jpeg` → `jpg`. The factory does this when a context is passed;
            // the .pr round-trip loses the context, so we run it again here.
            if (typeEntity.Kind != null)
            {
                var canon = Context.App.Format.CanonicaliseKind(typeEntity.Kind);
                if (canon != null) typeEntity.Kind = canon;
            }
            var typeName = typeEntity.Name;
            var targetType = Context.App.Type.Get(typeName);

            // Stamp kind from the value when the type has a Build(object?) hook
            // and the user didn't supply an explicit kind. Mirrors what
            // NormalizeParameterTypes does at build for known-typed slots;
            // here we run it on the resolved CLR type at runtime so literals
            // like `set %x% = "readme.md" as text` pick up kind=md without
            // the LLM having to spell it out.
            if (typeEntity.Kind == null && targetType != null)
            {
                var derivedKind = Context.App.Type.KindHooks.Of(targetType, Value.Value)
                                  ?? (Context.App.Type[typeName] is { ClrType: { } familyClr }
                                      ? Context.App.Type.KindHooks.Of(familyClr, Value.Value)
                                      : null);
                if (derivedKind != null) typeEntity.Kind = derivedKind;
            }
            if (targetType == null)
            {
                return Task.FromResult(global::app.data.@this.FromError(
                    new global::app.error.ServiceError($"Unknown type '{typeName}'", "UnknownType", 400)));
            }

            // Strict kind enforcement at runtime — for `%var%` paths
            // ValidateBuild deferred to here. When the resolved CLR type
            // implements IKindValidatable and Strict is true, sniff the value.
            // We construct a sample instance using the raw value as the first
            // ctor argument (image's primary ctor takes byte[]); a type without
            // a fitting ctor is treated as "no probe available".
            if (typeEntity.Strict && typeEntity.Kind != null
                && typeof(global::app.data.IKindValidatable).IsAssignableFrom(targetType))
            {
                var probe = TryInstantiateValidator(targetType, Value.Value);
                if (probe is global::app.data.IKindValidatable v)
                {
                    var (ok, actual) = v.ValidateKind(Value.Value!, typeEntity.Kind);
                    if (!ok)
                        return Task.FromResult(global::app.data.@this.FromError(
                            new global::app.error.ServiceError(
                                $"Strict kind mismatch: declared {typeName}/{typeEntity.Kind}"
                                + (actual != null ? $" but content is {actual}." : "."),
                                "StrictKindMismatch", 400)));
                }
            }

            object? converted = Value.Value;
            // CLR target type that can construct from the raw value (string→int,
            // string→DateTime, dict→record) — convert in place. When the target
            // can't be constructed from a literal string (e.g. `as image/gif` on
            // a "real.gif" path string — image is byte-backed, not path-backed),
            // keep the raw value and let the Type entity carry the meaning.
            // Downstream consumers that need the bytes can resolve via the path.
            System.Type? mintType = targetType;
            if (converted != null && !targetType.IsInstanceOfType(converted))
            {
                var (c, err) = global::app.type.list.@this.TryConvertTo(converted, targetType, Context);
                if (err == null)
                    converted = c;
                else
                    // Convert failed: fall back to the value's own CLR type so
                    // ConstructDataOfT mints a Data<actualClr> rather than trying
                    // to wrap a string inside Data<image>. Type entity stays as
                    // the user-declared annotation.
                    mintType = converted.GetType();
            }
            var typedData = ConstructDataOfT(Name.Value, mintType, converted, Context);
            // Pin the whole user-named Type entity onto the Data — kind and
            // strict survive the binding-mint. (The dropped-kind bug fixed
            // by construction: we no longer copy just the name.)
            typedData.Type = typeEntity;
            CopyProperties(Value, typedData);
            return Task.FromResult(Context.Variable.Set(typedData));
        }

        // No forced type — type-infer from Value.Value's runtime type. Hot types (string,
        // int, long, double, bool, decimal, DateTime, Guid, byte[], List, Dict) take the
        // if-chain; cold types fall through to reflection.
        var raw = Value.Value;
        data.@this minted = MintTyped(Name.Value, raw, Context);
        // The source Data may carry an explicit Type that's narrower than the runtime
        // type of its Value (e.g. `data.Compress()` returns a Data<byte[]> tagged
        // type=archived; MintTyped would otherwise produce Data<byte[]> with no
        // type label, losing the transport marker). Preserve the source's Type
        // when it has one AND when it carries real information — skip the
        // "object" catch-all so a literal `5` lazy-derives to {number, int}
        // instead of being pinned to `object` by the parameter schema's
        // polymorphic Value-slot annotation. Properties already get copied
        // below for the same "carry source metadata across the binding-mint"
        // reason.
        if (Value.Type is { } sourceType && !sourceType.IsNull && sourceType.Name != "object")
            minted.Type = sourceType;
        CopyProperties(Value, minted);
        return Task.FromResult(Context.Variable.Set(minted));
    }

    /// <summary>
    /// Properties carry per-Data result metadata (test.report's summaryFail, condition.if's
    /// branchIndex, etc.) that downstream <c>%var.prop%</c> navigation depends on. MintTyped
    /// builds a fresh Data from the source's raw value; without this copy, Properties get
    /// dropped at the binding-mint site. Pre-merge variable.set passed Value directly to
    /// Variables.Set which aliased the Data and kept Properties; the binding-mint refactor
    /// (runtime2-data-share-state) lost that property-survival path.
    /// </summary>
    private static void CopyProperties(data.@this source, data.@this target)
    {
        if (source.Properties.Count == 0 || ReferenceEquals(source, target)) return;
        foreach (var p in source.Properties)
            target.Properties[p.Key] = p.Value;
    }

    /// <summary>
    /// Type-infer + mint a Data&lt;T&gt; for the runtime type of <paramref name="raw"/>.
    /// Mutable refs (List, Dict) snapshot-cloned via JSON roundtrip so later mutation of
    /// the source doesn't bleed through. null produces plain Data (un-typed).
    /// </summary>
    private static data.@this MintTyped(string name, object? raw, actor.context.@this context)
    {
        return raw switch
        {
            null                                 => new data.@this(name, null) { Context = context },
            string s                             => new data.@this<string>(name, s) { Context = context },
            bool b                               => new data.@this<bool>(name, b) { Context = context },
            int i                                => new data.@this<int>(name, i) { Context = context },
            long l                               => new data.@this<long>(name, l) { Context = context },
            double d                             => new data.@this<double>(name, d) { Context = context },
            decimal m                            => new data.@this<decimal>(name, m) { Context = context },
            float f                              => new data.@this<float>(name, f) { Context = context },
            DateTime t                           => new data.@this<DateTime>(name, t) { Context = context },
            DateTimeOffset to                    => new data.@this<DateTimeOffset>(name, to) { Context = context },
            Guid g                               => new data.@this<Guid>(name, g) { Context = context },
            byte[] ba                            => new data.@this<byte[]>(name, ba) { Context = context },
            List<object?> list                   => new data.@this<List<object?>>(name, (List<object?>?)global::app.data.@this.SnapshotClone(list) ?? new List<object?>()) { Context = context },
            Dictionary<string, object?> dict     => new data.@this<Dictionary<string, object?>>(name, (Dictionary<string, object?>?)global::app.data.@this.SnapshotClone(dict) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)) { Context = context },
            _                                    => ConstructDataOfT(name, raw.GetType(), raw, context)
        };
    }

    /// <summary>
    /// Reflection construction of Data&lt;T&gt; for a runtime type not in the hot if-chain.
    /// </summary>
    private static object? TryInstantiateValidator(System.Type targetType, object? rawValue)
    {
        if (rawValue == null) return null;
        foreach (var ctor in targetType.GetConstructors())
        {
            var ps = ctor.GetParameters();
            if (ps.Length == 0) continue;
            if (!ps[0].ParameterType.IsAssignableFrom(rawValue.GetType())) continue;
            var args = new object?[ps.Length];
            args[0] = rawValue;
            for (int i = 1; i < ps.Length; i++)
                args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue
                    : ps[i].ParameterType == typeof(string) ? string.Empty : null;
            try { return ctor.Invoke(args); }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            { continue; }
        }
        return null;
    }

    private static data.@this ConstructDataOfT(string name, System.Type t, object? value, actor.context.@this context)
    {
        var generic = typeof(data.@this<>).MakeGenericType(t);
        var instance = (data.@this)Activator.CreateInstance(generic, name, value, null, null)!;
        instance.Context = context;
        return instance;
    }

}
