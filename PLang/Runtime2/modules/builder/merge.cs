using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.builder.providers;

namespace PLang.Runtime2.modules.builder;

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
