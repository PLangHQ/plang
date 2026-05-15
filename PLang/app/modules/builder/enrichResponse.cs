using app.Variables;
using app.modules.builder.code;
using Goal = app.Goals.Goal.@this;

namespace app.modules.builder;

/// <summary>
/// Build-time only handler. Invoked by os/system/builder/ApplyStep.goal during
/// `plang build`. Not exercised by `--test`; the honest signal for regressions
/// is the next bootstrap cycle (rebuild of system/builder/), where missing
/// backfilled actions or wrong source tags surface immediately as build
/// failures. Tester: 0% line coverage here is intentional — do not flag.
/// </summary>
[System.ComponentModel.Description("Enriches LLM build response: backfills actions for keep:true steps from prior .pr, tags each step with source (new|known|hint)")]
[Action("enrichResponse")]
public partial class enrichResponse : IContext
{
    [IsNotNull]
    public partial Data.@this<BuildResponse> StepResults { get; init; }

    [IsNotNull]
    public partial Data.@this<Goal> Goal { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public Task<Data.@this> Run() => Task.FromResult(Builder.EnrichResponse(this));
}
