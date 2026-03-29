using System.Reflection;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;
using Action = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.Action.@this;
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Returns all registered actions with parameter schemas for the LLM prompt.
/// </summary>
[Action("getActions")]
public partial class getActions : IContext
{
    public async Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400));

        var modules = engine.Modules;
        var actions = new Actions();

        foreach (var ns in modules.Names)
        {
            foreach (var className in modules.GetActions(ns))
            {
                var parameterType = modules.GetActionType(ns, className);
                if (parameterType == null) continue;

                var parameters = new List<Data>();
                var nCtx = new NullabilityInfoContext();

                foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;

                    var typeName = TypeMapping.GetTypeName(prop.PropertyType);

                    // Nullable reference types
                    bool isNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null;
                    if (!isNullable && !prop.PropertyType.IsValueType)
                        isNullable = nCtx.Create(prop).WriteState == NullabilityState.Nullable;
                    if (isNullable && !typeName.EndsWith("?"))
                        typeName += "?";

                    // ValidValues
                    var validValues = TypeMapping.GetValidValues(prop.PropertyType);
                    if (validValues != null)
                        typeName += $"({string.Join("|", validValues)})";

                    // @var marker
                    var hasVar = prop.GetCustomAttribute<VariableNameAttribute>() != null;

                    // Default value
                    var defaultAttr = prop.GetCustomAttribute<DefaultAttribute>();

                    var desc = hasVar ? $"@var {typeName}" : typeName;
                    if (defaultAttr != null)
                        desc += $" = {FormatDefault(defaultAttr.Value)}";

                    parameters.Add(new Data(prop.Name, desc));
                }

                // Cacheable flag
                bool cacheable = true;
                var actionAttr = parameterType.GetCustomAttribute<ActionAttribute>();
                if (actionAttr != null)
                    cacheable = actionAttr.Cacheable;

                actions.Add(new Action
                {
                    Module = ns,
                    ActionName = className,
                    ParameterSchema = parameterType,
                    Parameters = parameters,
                    Cacheable = cacheable
                });
            }
        }

        return Data.Ok(actions);
    }

    private static string FormatDefault(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "null"
    };
}
