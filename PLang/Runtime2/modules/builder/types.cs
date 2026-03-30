using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Utility;

namespace PLang.Runtime2.modules.builder;

/// <summary>
/// Returns PLang type names and complex type JSON schemas for the LLM prompt.
/// </summary>
[Action("types")]
public partial class types : IContext
{
    public Task<Data> Run()
    {
        var engine = Context.Engine;
        if (!engine.Building.IsEnabled)
            return Task.FromResult(Data.FromError(new Engine.Errors.ActionError("Building is not enabled", "BuildingDisabled", 400)));

        var names = TypeMapping.GetBuilderTypeNames();
        var schemas = TypeMapping.GetComplexTypeSchemas();
        var schemaLines = schemas.Select(kvp => $"  {kvp.Key}: {kvp.Value}");

        var result = new
        {
            TypeNames = string.Join(", ", names),
            TypeSchemas = string.Join("\n", schemaLines)
        };

        return Task.FromResult(Data.Ok(result));
    }
}
