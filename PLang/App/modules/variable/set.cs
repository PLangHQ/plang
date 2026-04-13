using App.Variables;

namespace App.modules.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext, IBuildValidatable
{
    public static string? ValidateBuild(List<Data.@this> parameters)
    {
        var value = parameters.FirstOrDefault(p =>
            string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase));
        if (value?.Value is string s && s == "this")
            return "Parameter 'Value' is the literal string \"this\" — this is wrong. For \"write to %var%\" patterns, use \"%__data__%\" to capture the previous action's result. \"this\" is a type annotation, not a value.";
        return null;
    }

    [VariableName]
    public partial string Name { get; init; }
    public partial Data.@this Value { get; init; }
    public partial string? Type { get; init; }
    [Default(false)]
    public partial bool AsDefault { get; init; }

    public Task<Data.@this> Run()
    {
        if (AsDefault)
        {
            var existing = Context.Variables.Get(Name);
            if (existing.IsInitialized)
                return Task.FromResult(Data());
        }

        Value.Name = Name;
        Context.Variables.Set(Name, Value,
            Type != null ? App.Data.Type.FromName(Type) : null);

        return Task.FromResult(Data());
    }
}
