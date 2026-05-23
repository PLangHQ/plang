using app.variables;
using app.modules.builder.code;

namespace app.modules.builder;

[System.ComponentModel.Description("Return the list of registered PLang type names available to the builder, optionally filtered to the types referenced by a set of module.action entries")]
[Action("types")]
public partial class types : IContext
{
    /// <summary>
    /// Optional filter — when set to a list of <c>"module.action"</c> names,
    /// the returned Schema's <c>Types</c> list is restricted to type entries
    /// that those actions reference (parameter types + return type). The
    /// <c>PrimitiveNames</c> list is unchanged. Null/empty → full catalog.
    /// </summary>
    public partial data.@this<List<string>>? Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public Task<data.@this> Run() => Task.FromResult(Builder.Types(this));
}
