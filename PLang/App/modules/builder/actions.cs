using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[ModuleDescription("Builder internals: load, merge, validate, and save goal and step data during the build pipeline")]
[System.ComponentModel.Description("Retrieve the registered action catalog for use in the builder prompt")]
[Action("actions")]
public partial class GetActions : IContext
{
    [Provider]
    public partial IBuilderProvider Builder { get; }

    public async Task<Data.@this> Run() => await Builder.Actions(this);
}
