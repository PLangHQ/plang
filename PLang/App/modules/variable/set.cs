using App.Attributes;
using App.Variables;

namespace App.modules.variable;

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
    public static string? ValidateBuild(List<Data.@this> parameters)
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
                             ?? global::App.Types.@this.GetPrimitiveOrMime(value.Type.Value);
            if (targetType != null && !targetType.IsInstanceOfType(value.Value))
            {
                var (_, error) = global::App.Types.@this.TryConvertTo(value.Value, targetType);
                if (error != null)
                    return $"Parameter 'Value' has type={value.Type.Value} but value cannot be converted: {error.Message}";
            }
        }
        return null;
    }

    public partial Data.@this<Variable> Name { get; init; }
    public partial Data.@this Value { get; init; }
    public partial Data.@this<string>? Type { get; init; }
    [Default(false)]
    public partial Data.@this<bool> AsDefault { get; init; }

    public Task<Data.@this> Run()
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
                return Task.FromResult(global::App.Data.@this.FromError(
                    new Errors.ServiceError($"Unknown type '{Type.Value}'", "UnknownType", 400)));
            }
            object? converted = Value.Value;
            if (converted != null && !targetType.IsInstanceOfType(converted))
            {
                var (c, err) = global::App.Types.@this.TryConvertTo(converted, targetType, Context);
                if (err != null)
                    return Task.FromResult(global::App.Data.@this.FromError(err));
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
        Data.@this minted = MintTyped(Name.Value, raw, Context);
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
    private static void CopyProperties(Data.@this source, Data.@this target)
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
    private static Data.@this MintTyped(string name, object? raw, Actor.Context.@this ctx)
    {
        return raw switch
        {
            null                                 => new Data.@this(name, null) { Context = ctx },
            string s                             => new Data.@this<string>(name, s) { Context = ctx },
            bool b                               => new Data.@this<bool>(name, b) { Context = ctx },
            int i                                => new Data.@this<int>(name, i) { Context = ctx },
            long l                               => new Data.@this<long>(name, l) { Context = ctx },
            double d                             => new Data.@this<double>(name, d) { Context = ctx },
            decimal m                            => new Data.@this<decimal>(name, m) { Context = ctx },
            float f                              => new Data.@this<float>(name, f) { Context = ctx },
            DateTime t                           => new Data.@this<DateTime>(name, t) { Context = ctx },
            DateTimeOffset to                    => new Data.@this<DateTimeOffset>(name, to) { Context = ctx },
            Guid g                               => new Data.@this<Guid>(name, g) { Context = ctx },
            byte[] ba                            => new Data.@this<byte[]>(name, ba) { Context = ctx },
            List<object?> list                   => new Data.@this<List<object?>>(name, (List<object?>?)global::App.Data.@this.SnapshotClone(list) ?? new List<object?>()) { Context = ctx },
            Dictionary<string, object?> dict     => new Data.@this<Dictionary<string, object?>>(name, (Dictionary<string, object?>?)global::App.Data.@this.SnapshotClone(dict) ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)) { Context = ctx },
            _                                    => ConstructDataOfT(name, raw.GetType(), raw, ctx)
        };
    }

    /// <summary>
    /// Reflection construction of Data&lt;T&gt; for a runtime type not in the hot if-chain.
    /// </summary>
    private static Data.@this ConstructDataOfT(string name, System.Type t, object? value, Actor.Context.@this ctx)
    {
        var generic = typeof(Data.@this<>).MakeGenericType(t);
        var instance = (Data.@this)Activator.CreateInstance(generic, name, value, null, null)!;
        instance.Context = ctx;
        return instance;
    }

}
