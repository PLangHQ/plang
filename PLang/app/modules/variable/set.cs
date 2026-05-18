using app.Attributes;
using app.variables;

namespace app.modules.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
///
/// variable.set is the binding-mint site — it owns type inference (MintTyped picks
/// the concrete Data&lt;T&gt; for the runtime type). Variables.Set decides whether to
/// update the existing binding in place (same type) or replace it (type changed),
/// and carries Properties + event subscribers across replacement.
/// </summary>
[System.ComponentModel.Description("Assign a value to a named variable, optionally coercing to a type or setting only when unset")]
[Action("set", Cacheable = false)]
[Example(
    "set %data% = {\"name\": \"%user%\", \"age\": 30}, type=json",
    "variable.set Name([string] %data%), Value([json] {\"name\":\"%user%\",\"age\":30}), Type([string] json)")]
public partial class Set : IContext, IBuildValidatable
{
    public static string? ValidateBuild(List<data.@this> parameters)
    {
        var value = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase));
        if (value?.Value is string s && s == "this")
            return "Parameter 'Value' is the literal string \"this\" — this is wrong. For \"write to %var%\" patterns, use \"%__data__%\" to capture the previous action's result. \"this\" is a type annotation, not a value.";
        if (value?.Type?.Value != null && value.Value != null)
        {
            // Skip validation when value contains %variable% references — they resolve at runtime
            if (value.HasVariableReference) return null;

            var targetType = value.Context?.App.Types.Get(value.Type.Value)
                             ?? global::app.types.@this.GetPrimitiveOrMime(value.Type.Value);
            if (targetType != null && !targetType.IsInstanceOfType(value.Value))
            {
                var (_, error) = global::app.types.@this.TryConvertTo(value.Value, targetType);
                if (error != null)
                    return $"Parameter 'Value' has type={value.Type.Value} but value cannot be converted: {error.Message}";
            }
        }
        return null;
    }

    public partial data.@this<Variable> Name { get; init; }
    public partial data.@this Value { get; init; }
    public partial data.@this<string>? Type { get; init; }
    [Default(false)]
    public partial data.@this<bool> AsDefault { get; init; }

    public Task<data.@this> Run()
    {
        if (AsDefault.Value)
        {
            var existing = Context.Variables.Get(Name.Value);
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
            var targetType = Context.App.Types.Get(Type.Value);
            if (targetType == null)
            {
                return Task.FromResult(global::app.data.@this.FromError(
                    new errors.ServiceError($"Unknown type '{Type.Value}'", "UnknownType", 400)));
            }
            object? converted = Value.Value;
            if (converted != null && !targetType.IsInstanceOfType(converted))
            {
                var (c, err) = global::app.types.@this.TryConvertTo(converted, targetType, Context);
                if (err != null)
                    return Task.FromResult(global::app.data.@this.FromError(err));
                converted = c;
            }
            var typedData = ConstructDataOfT(Name.Value, targetType, converted, Context);
            CopyProperties(Value, typedData);
            return Task.FromResult(Context.Variables.Set(typedData));
        }

        // No forced type — type-infer from Value.Value's runtime type. Hot types (string,
        // int, long, double, bool, decimal, DateTime, Guid, byte[], List, Dict) take the
        // if-chain; cold types fall through to reflection.
        var raw = Value.Value;
        data.@this minted = MintTyped(Name.Value, raw, Context);
        CopyProperties(Value, minted);
        return Task.FromResult(Context.Variables.Set(minted));
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
            target.Properties.Set(p.Name, p.Value, p.Type);
    }

    /// <summary>
    /// Type-infer + mint a Data&lt;T&gt; for the runtime type of <paramref name="raw"/>.
    /// Mutable refs (List, Dict) snapshot-cloned via JSON roundtrip so later mutation of
    /// the source doesn't bleed through. null produces plain Data (un-typed).
    /// </summary>
    private static data.@this MintTyped(string name, object? raw, actor.context.@this ctx)
    {
        return raw switch
        {
            null                                 => new data.@this(name, null) { Context = ctx },
            string s                             => new data.@this<string>(name, s) { Context = ctx },
            bool b                               => new data.@this<bool>(name, b) { Context = ctx },
            int i                                => new data.@this<int>(name, i) { Context = ctx },
            long l                               => new data.@this<long>(name, l) { Context = ctx },
            double d                             => new data.@this<double>(name, d) { Context = ctx },
            decimal m                            => new data.@this<decimal>(name, m) { Context = ctx },
            float f                              => new data.@this<float>(name, f) { Context = ctx },
            DateTime t                           => new data.@this<DateTime>(name, t) { Context = ctx },
            DateTimeOffset to                    => new data.@this<DateTimeOffset>(name, to) { Context = ctx },
            Guid g                               => new data.@this<Guid>(name, g) { Context = ctx },
            byte[] ba                            => new data.@this<byte[]>(name, ba) { Context = ctx },
            List<object?> list                   => new data.@this<List<object?>>(name, (List<object?>?)global::app.data.@this.SnapshotClone(list) ?? new List<object?>()) { Context = ctx },
            Dictionary<string, object?> dict     => new data.@this<Dictionary<string, object?>>(name, (Dictionary<string, object?>?)global::app.data.@this.SnapshotClone(dict) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)) { Context = ctx },
            _                                    => ConstructDataOfT(name, raw.GetType(), raw, ctx)
        };
    }

    /// <summary>
    /// Reflection construction of Data&lt;T&gt; for a runtime type not in the hot if-chain.
    /// </summary>
    private static data.@this ConstructDataOfT(string name, System.Type t, object? value, actor.context.@this ctx)
    {
        var generic = typeof(data.@this<>).MakeGenericType(t);
        var instance = (data.@this)Activator.CreateInstance(generic, name, value, null, null)!;
        instance.Context = ctx;
        return instance;
    }

}
