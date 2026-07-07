using app.variable;
using app.module.build.code;

namespace app.module.build;

/// <summary>
/// Build-time only handler. Invoked by os/system/builder/ApplyStep.goal during
/// `plang build`. Not exercised by `--test`; the honest signal for regressions
/// is the next bootstrap cycle (rebuild of system/builder/), where a broken
/// promotion fails the build immediately and visibly. Tester: 0% line coverage
/// here is intentional — do not flag.
/// </summary>
[Action("promoteGroups")]
public partial class promoteGroups : IContext
{
    [IsNotNull]
    public partial data.@this Steps { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public Task<data.@this> Run() => Builder.PromoteGroups(this);
}
