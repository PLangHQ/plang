using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.Goals.PathTypingTests;

/// <summary>
/// Stage 3 — Batch 4. Goal/GoalCall typing flip (D3, C7/C11).
///
/// <c>Goal.Path</c>, <c>Goal.PrPath</c>, <c>Goal.LoadedFromPrPath</c>,
/// <c>Goal.FolderPath</c>, <c>Goal.GetRuntimeDirectory()</c>, and
/// <c>GoalCall.PrPath</c> all become <c>path?</c>. JSON converter ↔ Relative
/// string lives in <c>PLang/app/types/path/this.JsonConverter.cs</c>. The
/// post-deserialize back-reference pass that sets <c>Goal.App</c> /
/// <c>step.Goal</c> extends to wire <c>Path.Context</c>.
/// </summary>
public class GoalPathTypingTests
{
    [Test] public async Task GoalPath_Property_IsPathTyped_NotString()
    {
        // Compile-time-ish guard: Goal.Path's reflected type is app.types.path.@this.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task GoalPrPath_IsDerivedFromPath_ViaInBuildFolder()
    {
        // PrPath getter = Path.Parent.Combine(".build").Combine(name + ".pr"), per D1.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task GoalPrPath_InitSetter_IsNoOp_SwallowsJsonValue()
    {
        // C8: the init {} no-op stays — JSON round-trip must not error on prPath field.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task GoalGetRuntimeDirectory_DerivesFromLoadedFromPrPath()
    {
        // GetRuntimeDirectory = LoadedFromPrPath.Parent.Parent.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Goal_JsonRoundTrip_PreservesPathAsRelativeString()
    {
        // Serialize → on-disk JSON contains relative path string; deserialize → Path.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Goal_JsonRoundTrip_ReconstitutesPath_UnderDifferentAppRoot()
    {
        // Built under root A, loaded under root B — Path resolves correctly against B.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task Goal_JsonRoundTrip_BackReferencePass_WiresPathContext()
    {
        // After load, every Path on the Goal tree has Context != null (wired by App).
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task GoalCallPrPath_RoundTrips_AsRelativeString()
    {
        // GoalCall.PrPath is [Store]'d — must serialize/deserialize the same way.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }

    [Test] public async Task PathJsonConverter_LeavesContextNullAtDeserializeTime()
    {
        // Converter sets _absolutePath + Raw; Context is wired later by back-ref pass.
        await Task.CompletedTask; Assert.Fail("Not implemented");
    }
}
