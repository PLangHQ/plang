using TUnit.Core;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 5 (C# half): `Data.Snapshot.Resume(ctx)` recursive cross-
/// goal continuation; `callback.run` is the resume entry and requires Snapshot.
public class SnapshotResumeTests
{
    [Test] public Task CallbackRun_NullSnapshot_ReturnsNoSnapshotError()        { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task CallbackRun_WithSnapshot_DelegatesToSnapshotResume()     { Assert.Fail("Not implemented"); return Task.CompletedTask; }

    [Test] public Task SnapshotResume_EmptyChainAfterRestore_ReturnsNoPositionError() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task SnapshotResume_SingleGoal_ReentersAtSuspendedPosition()  { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task SnapshotResume_NestedChain_UnwindsToParentAfterSubGoalCompletes() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
    [Test] public Task ResumeChain_MultiActionStep_ContinuesAtActionIndexPlusOne() { Assert.Fail("Not implemented"); return Task.CompletedTask; }
}
