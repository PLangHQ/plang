using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("merge")]
public partial class merge : IContext
{
    [IsNotNull]
    public partial Data.@this<Step> Step { get; init; }

    [IsNotNull]
    public partial Data.@this<Step> StepFromLlm { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data.@this> Run() => Task.FromResult(Builder.Merge(this));
}
