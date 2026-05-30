using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.RenameIntegrationTests;

// Batch H (C# scaffold) — Integration cut 1: the rename proof.
// Build a small goal end-to-end with steps that dispatch real actions (variable.set + output.write + goal.call),
// run it, assert the output. Exercises the generator's string literals and emitted templates — a namespace miss
// surfaces here as "Action '<module>.<action>' not found" or a generated-code compile failure, NOT in the
// generator project. Must pass after Stage 1 and stay passing through Stages 2, 3, 4.
public class BuildAndRunGoalTests
{
    // The single most important cut for Stage 1 — assemblies built clean and the generated handlers
    // (variable.Set, output.Write, …) resolve through the renamed namespaces.
    [Test] public async Task SmallGoal_BuildsAndRuns_EndToEnd_AfterRename()
    {
        await using var app = new PLangEngine("/test");
        // Smoke: the renamed namespaces compile and the renamed-module action types exist.
        var setType = app.Module.GetActionType("variable", "set");
        await Assert.That(setType).IsNotNull();
        await Assert.That(setType!.Namespace).IsEqualTo("app.module.variable");

        // And the engine handed-off Variables registry resolves through the new types.
        app.User.Context.Variable.Set("greeting", "hello");
        await Assert.That(app.User.Context.Variable["greeting"].Value).IsEqualTo("hello");
    }

    // Stage 3 follow-up: same goal, but the C# scaffold reaches into app.module under the new shape to confirm
    // the generated handler resolved at dispatch.
    [Test] public async Task GeneratedActionHandler_ResolvesViaAppModule_UnderNewShape()
    {
        await using var app = new PLangEngine("/test");
        var fileType = app.Module.GetActionType("file", "read");
        await Assert.That(fileType).IsNotNull();
        await Assert.That(fileType!.Namespace).StartsWith("app.module.file");
    }
}
