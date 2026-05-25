using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using FilePath = global::app.types.path.file.@this;

namespace PLang.Tests.App.Types.PathTests;

/// <summary>
/// Stage 2 — Batch 3. <c>.goal</c> MIME → Goal deserialization (D2).
///
/// Mirrors the existing <c>.pr</c> pattern. <c>FilePath.ReadText</c> converts
/// via the MIME map, then stamps <c>goal.Path = this</c> as a post-conversion
/// back-reference (same shape as GoalCall.LoadFromFile's existing stamp of
/// LoadedFromPrPath/App/step.Goal).
/// </summary>
public class GoalMimeDeserializationTests
{
    [Test] public async Task ReadText_OnDotGoalFile_ReturnsParsedGoal()
    {
        // FilePath("/Tests/Start.goal").ReadText() → Data with Value is Goal.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadText_OnDotGoalFile_StampsGoalPathToSelf()
    {
        // After conversion, goal.Path equals the FilePath that read it.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadText_OnDotTestGoalFile_FlowsSameWay()
    {
        // .test.goal is part of the same MIME flow — parses to Goal, Path stamped.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadText_OnDotPrFile_StillReturnsGoal_RegressionGuard()
    {
        // The existing .pr → Goal path must keep working after the .goal addition.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task ReadText_OnMalformedDotGoalFile_ReturnsFailureWithError()
    {
        // Parser error surfaces as Data.Fail with a Goal.Parse error, not a throw.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task GoalMimeRegistration_AppearsInTypeMapping()
    {
        // TypeMapping registers .goal → CLR Goal so the converter can dispatch.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
