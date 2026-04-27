using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[System.ComponentModel.Description("Promote grouped sub-steps into top-level steps for correct inline step handling")]
[Action("promoteGroups")]
public partial class promoteGroups : IContext
{
    [IsNotNull]
    public partial Data.@this Steps { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data.@this> Run() => Task.FromResult(Builder.PromoteGroups(this));
}
