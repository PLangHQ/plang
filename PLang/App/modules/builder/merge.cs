using App.Variables;
using App.modules.builder.providers;

namespace App.modules.builder;

[System.ComponentModel.Description("Merge an LLM-generated step result onto the existing step, preserving runtime fields")]
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
