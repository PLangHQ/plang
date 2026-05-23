using app.variables;
using app.modules.builder.code;

namespace app.modules.builder;

[ModuleDescription("Builder internals: load, merge, validate, and save goal and step data during the build pipeline")]
[System.ComponentModel.Description("Retrieve the registered action catalog for use in the builder prompt, optionally filtered to a set of module.action names")]
[Action("actions")]
public partial class GetActions : IContext
{
    /// <summary>
    /// Optional filter — the <c>module.action</c> names to restrict the catalog
    /// to. Null or empty returns the full catalog. The builder's Compile step
    /// passes the planner's action set here so the prompt carries only the
    /// relevant rows.
    /// </summary>
    public partial data.@this<List<string>>? Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public async Task<data.@this> Run() => await Builder.Actions(this);
}
