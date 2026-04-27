using App.Variables;
using App.modules.builder.providers;
using Goal = App.Goals.Goal.@this;

namespace App.modules.builder;

[System.ComponentModel.Description("Enriches LLM build response: backfills actions for keep:true steps from prior .pr, tags each step with source (new|known|hint)")]
[Action("enrichResponse")]
public partial class enrichResponse : IContext
{
    [IsNotNull]
    public partial Data.@this<BuildResponse> StepResults { get; init; }

    [IsNotNull]
    public partial Data.@this<Goal> Goal { get; init; }

    [Provider]
    public partial IBuilderProvider Builder { get; }

    public Task<Data.@this> Run() => Task.FromResult(Builder.EnrichResponse(this));
}
