using app.variable;
using app.module.action.build.code;
using Goal = app.goal.@this;

namespace app.module.action.build;

/// <summary>
/// Build-time only handler. Invoked by os/system/builder/ApplyStep.goal during
/// `plang build`. Not exercised by `--test`; the honest signal for regressions
/// is the next bootstrap cycle (rebuild of system/builder/), where missing
/// backfilled actions or wrong source tags surface immediately as build
/// failures. Tester: 0% line coverage here is intentional — do not flag.
/// </summary>
[Action("enrichResponse")]
public partial class enrichResponse : IContext
{
    [IsNotNull]
    public partial data.@this<BuildResponse> StepResults { get; init; }

    [IsNotNull]
    public partial data.@this<global::app.type.clr.@this<Goal>> Goal { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public Task<data.@this> Run() => Builder.EnrichResponse(this);
}
