using App.Engine.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[Action("steps.merge")]
public partial class merge : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    [IsNotNull]
    public partial Step StepFromLlm { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data> Run() => Task.FromResult(Builder.Merge(this));
}
