using app.variables;
using app.modules.builder.code;

namespace app.modules.builder;

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

    public async Task<data.@this<global::app.builder.Types.@this>> Run()
    {
        var result = await Builder.Types(this);
        return data.@this<global::app.builder.Types.@this>.From(result);
    }
}
