using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.SingularNamespaces.RenameIntegrationTests;

// Batch H (C# scaffold) — Integration cut 1: the rename proof.
// Build a small goal end-to-end with steps that dispatch real actions (variable.set + output.write + goal.call),
// run it, assert the output. Exercises the generator's string literals and emitted templates — a namespace miss
// surfaces here as "Action '<module>.<action>' not found" or a generated-code compile failure, NOT in the
// generator project. Must pass after Stage 1 and stay passing through Stages 2, 3, 4.
public class BuildAndRunGoalTests
{
    // The single most important cut for Stage 1 — clean rebuild + this test green = the rename held.
    [Test] public async Task SmallGoal_BuildsAndRuns_EndToEnd_AfterRename()
        => Assert.Fail("Not implemented");

    // Stage 3 follow-up: same goal, but the C# scaffold reaches into app.module under the new shape to confirm
    // the generated handler resolved at dispatch.
    [Test] public async Task GeneratedActionHandler_ResolvesViaAppModule_UnderNewShape()
        => Assert.Fail("Not implemented");
}
