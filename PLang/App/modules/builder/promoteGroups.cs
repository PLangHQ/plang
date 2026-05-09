using App.Variables;
using App.modules.builder.code;

namespace App.modules.builder;

/// <summary>
/// Build-time only handler. Invoked by os/system/builder/ApplyStep.goal during
/// `plang build`. Not exercised by `--test`; the honest signal for regressions
/// is the next bootstrap cycle (rebuild of system/builder/), where a broken
/// promotion fails the build immediately and visibly. Tester: 0% line coverage
/// here is intentional — do not flag.
/// </summary>
[System.ComponentModel.Description("Promote grouped sub-steps into top-level steps for correct inline step handling")]
[Action("promoteGroups")]
public partial class promoteGroups : IContext
{
    [IsNotNull]
    public partial Data.@this Steps { get; init; }

    [Provider]
    public partial IBuilder Builder { get; }

    public Task<Data.@this> Run() => Builder.PromoteGroups(this);
}
