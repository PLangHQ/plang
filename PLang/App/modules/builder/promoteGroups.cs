using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("steps.promoteGroups")]
public partial class promoteGroups : IContext
{
    [IsNotNull]
    public partial object Steps { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data.@this> Run() => Task.FromResult(Builder.PromoteGroups(this));
}
