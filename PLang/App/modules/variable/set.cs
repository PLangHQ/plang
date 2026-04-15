using App.Attributes;
using App.Variables;

namespace App.modules.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
/// </summary>
[Action("set", Cacheable = false)]
[Example(
    "set %data% = {\"name\": \"%user%\", \"age\": 30}, type=json",
    "{\"module\":\"variable\",\"action\":\"set\",\"parameters\":[{\"name\":\"Name\",\"value\":\"%data%\",\"type\":\"string\"},{\"name\":\"Value\",\"value\":{\"name\":\"%user%\",\"age\":30},\"type\":\"json\"}]}")]
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
            var targetType = Utils.TypeMapping.GetType(value.Type.Value);
            if (targetType != null && !targetType.IsInstanceOfType(value.Value))
            {
                var (_, error) = Utils.TypeMapping.TryConvertTo(value.Value, targetType);
                if (error != null)
                    return $"Parameter 'Value' has type={value.Type.Value} but value cannot be converted: {error.Message}";
            }
        }
        return null;
    }

    [VariableName]
    public partial string Name { get; init; }
    public partial Data.@this Value { get; init; }
    public partial Data.@this<string>? Type { get; init; }
    [Default(false)]
    public partial Data.@this<bool> AsDefault { get; init; }

    public Task<Data.@this> Run()
    {
        if (AsDefault.Value)
        {
            var existing = Context.Variables.Get(Name);
            if (existing.IsInitialized)
                return Task.FromResult(Data());
        }

        Value.Name = Name;
        Context.Variables.Set(Name, Value,
            Type?.Value != null ? App.Data.Type.FromName(Type.Value) : null);

        return Task.FromResult(Data());
    }
}
